using Confluent.Kafka;
using HCI.Kafka.Domain.Models;
using HCI.Kafka.Infrastructure.Configuration;
using HCI.Kafka.Infrastructure.Kafka;
using HCI.Kafka.Infrastructure.Serialization;
using HCI.Kafka.Monitoring;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Prometheus;

namespace HCI.Kafka.Consumer.Services;

/// <summary>
/// Kafka consumer for MedicinalProduct events.
///
/// Demonstrates:
/// - Manual offset commit (EnableAutoCommit = false) for at-least-once semantics
/// - Explicit StoreOffset then Commit pattern for safety
/// - Partition assignment/revocation callbacks for rebalance handling
/// - Graceful shutdown via Consumer.Close() (triggers offset commit + group leave)
/// - Processing latency measurement via Prometheus Histogram
/// - Consumer lag tracking via StatisticsHandler
/// - Distinguishing transient errors (retry) from fatal errors (surface)
/// </summary>
public sealed class MedicinalProductConsumer : BackgroundService
{
    private readonly IConsumer<string, MedicinalProduct> _consumer;
    private readonly KafkaOptions _options;
    private readonly ILogger<MedicinalProductConsumer> _logger;

    // ── Processing stats ──────────────────────────────────────────────────────
    private long _totalProcessed;
    private long _totalErrors;

    public MedicinalProductConsumer(
        IOptions<KafkaOptions> options,
        ILogger<MedicinalProductConsumer> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = KafkaClientFactory.CreateConsumerConfig(_options, "hci-compendium-consumer");

        _consumer = new ConsumerBuilder<string, MedicinalProduct>(config)
            .SetValueDeserializer(new JsonSerializer<MedicinalProduct>())
            .SetErrorHandler((_, error) =>
            {
                if (error.IsFatal)
                    _logger.LogCritical("FATAL consumer error [{Code}]: {Reason}", error.Code, error.Reason);
                else
                    _logger.LogError("Consumer error [{Code}]: {Reason}", error.Code, error.Reason);
            })
            .SetLogHandler((_, msg) =>
                _logger.LogDebug("[librdkafka] {Facility}: {Message}", msg.Facility, msg.Message))
            .SetStatisticsHandler((_, json) =>
                KafkaMetricsExporter.HandleStatistics(json, _logger))
            .SetPartitionsAssignedHandler((c, partitions) =>
            {
                _logger.LogInformation(
                    "Partition assignment: {Count} partitions assigned → [{Partitions}]",
                    partitions.Count,
                    string.Join(", ", partitions.Select(p => $"{p.Topic}[{p.Partition}]")));
            })
            .SetPartitionsRevokedHandler((c, offsets) =>
            {
                // CRITICAL: commit current positions before partitions are revoked
                // This prevents reprocessing messages already handled during a rebalance
                _logger.LogWarning(
                    "Partition revocation: committing {Count} offsets before rebalance",
                    offsets.Count);

                var validOffsets = offsets
                    .Where(o => o.Offset != Offset.Unset)
                    .ToList();

                if (validOffsets.Any())
                {
                    try { c.Commit(validOffsets); }
                    catch (KafkaException ex)
                    {
                        _logger.LogError(ex, "Failed to commit offsets during partition revocation");
                    }
                }
            })
            .SetPartitionsLostHandler((c, partitions) =>
            {
                // Partitions lost (e.g. consumer session timeout) — cannot commit
                _logger.LogWarning(
                    "Partitions LOST (session timeout?): {Count} partitions. Some messages may be reprocessed.",
                    partitions.Count);
            })
            .Build();

        _consumer.Subscribe(_options.MedicinalProductsTopic);

        _logger.LogInformation(
            "MedicinalProductConsumer initialized → group={GroupId} topic={Topic}",
            _options.ConsumerGroupId, _options.MedicinalProductsTopic);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Consumer starting. Waiting for messages...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Poll with a 1-second timeout — returns null if no messages available
                var result = _consumer.Consume(TimeSpan.FromSeconds(1));

                if (result is null || result.IsPartitionEOF)
                {
                    // No message or end of partition — loop back
                    if (result?.IsPartitionEOF == true)
                    {
                        _logger.LogDebug(
                            "Reached end of partition {Topic}[{Partition}]",
                            result.Topic, result.Partition.Value);
                    }
                    continue;
                }

                // ── Process the message ────────────────────────────────────────
                using var timer = KafkaMetricsExporter.ProcessingDurationSeconds
                    .WithLabels(result.Topic)
                    .NewTimer();

                await ProcessMessageAsync(result, stoppingToken);

                // ── Commit offset ──────────────────────────────────────────────
                // StoreOffset marks the offset for the next automatic commit tick.
                // Commit() forces an immediate synchronous commit.
                // This pattern ensures we ONLY commit after successful processing.
                _consumer.StoreOffset(result);
                _consumer.Commit(result);

                Interlocked.Increment(ref _totalProcessed);

                KafkaMetricsExporter.MessagesConsumedTotal
                    .WithLabels(result.Topic, _options.ConsumerGroupId)
                    .Inc();
            }
            catch (ConsumeException ex) when (!ex.Error.IsFatal)
            {
                // Transient consume error (e.g. broker temporarily unavailable)
                // Log and continue — librdkafka will handle reconnection
                Interlocked.Increment(ref _totalErrors);
                _logger.LogWarning(ex,
                    "Transient consume error (will retry): {Code} - {Reason}",
                    ex.Error.Code, ex.Error.Reason);
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            catch (ConsumeException ex) when (ex.Error.IsFatal)
            {
                _logger.LogCritical(ex,
                    "Fatal consume error — consumer cannot continue: {Code}", ex.Error.Code);
                throw;
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer stopping (cancellation requested).");
                break;
            }
            catch (Exception ex)
            {
                // Unexpected processing error — log and continue to avoid stopping the consumer
                Interlocked.Increment(ref _totalErrors);
                _logger.LogError(ex, "Unexpected error processing Kafka message");
            }
        }

        // ── Graceful shutdown ─────────────────────────────────────────────────
        // Consumer.Close() performs:
        //  1. Commits current offsets
        //  2. Leaves the consumer group (triggers immediate rebalance)
        _logger.LogInformation(
            "Consumer shutting down gracefully. Processed: {Processed}, Errors: {Errors}",
            _totalProcessed, _totalErrors);

        _consumer.Close();
    }

    private async Task ProcessMessageAsync(
        ConsumeResult<string, MedicinalProduct> result,
        CancellationToken ct)
    {
        var product = result.Message.Value;

        // Validate message headers
        var source = result.Message.Headers.TryGetLastBytes("source", out var sourceBytes)
            ? System.Text.Encoding.UTF8.GetString(sourceBytes)
            : "unknown";

        _logger.LogInformation(
            "Processing [{Topic}][{Partition}]@{Offset} | GTIN={Gtin} | Product={Name} | " +
            "Status={Status} | Price=CHF {Price:F2} | Source={Source}",
            result.Topic,
            result.Partition.Value,
            result.Offset.Value,
            product.Gtin,
            product.ProductName,
            product.MarketingStatus,
            product.PublicPrice,
            source);

        if (product.MarketingStatus == MarketingStatus.Withdrawn ||
            product.MarketingStatus == MarketingStatus.Suspended)
        {
            _logger.LogWarning(
                "MARKET STATUS ALERT: {Gtin} ({Name}) is {Status} — downstream systems must be notified",
                product.Gtin, product.ProductName, product.MarketingStatus);
        }

        // Simulate async database write
        await Task.Delay(TimeSpan.FromMilliseconds(1), ct);
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
