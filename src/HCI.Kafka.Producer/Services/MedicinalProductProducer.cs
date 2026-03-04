using System.Text;
using Confluent.Kafka;
using HCI.Kafka.Domain.Factories;
using HCI.Kafka.Domain.Models;
using HCI.Kafka.Infrastructure.Configuration;
using HCI.Kafka.Infrastructure.Kafka;
using HCI.Kafka.Infrastructure.Serialization;
using HCI.Kafka.Monitoring;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HCI.Kafka.Producer.Services;

/// <summary>
/// Kafka producer for MedicinalProduct events.
///
/// Demonstrates:
/// - Confluent Cloud connectivity with SASL/SSL
/// - Idempotent exactly-once producer semantics
/// - Acks=All for maximum durability
/// - Structured message headers (source, schema-version, event-time)
/// - Prometheus metrics integration via StatisticsHandler
/// - Proper error handling distinguishing fatal vs transient errors
/// - Graceful shutdown with Flush()
/// </summary>
public sealed class MedicinalProductProducer : IDisposable
{
    private readonly IProducer<string, MedicinalProduct> _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<MedicinalProductProducer> _logger;

    public MedicinalProductProducer(
        IOptions<KafkaOptions> options,
        ILogger<MedicinalProductProducer> logger)
    {
        _options = options.Value;
        _logger = logger;

        var config = KafkaClientFactory.CreateProducerConfig(_options, "hci-medicinal-product-producer");

        _producer = new ProducerBuilder<string, MedicinalProduct>(config)
            .SetValueSerializer(new JsonSerializer<MedicinalProduct>())
            // Error handler: log all broker/network errors
            .SetErrorHandler((_, error) =>
            {
                if (error.IsFatal)
                    _logger.LogCritical("FATAL Kafka producer error [{Code}]: {Reason}", error.Code, error.Reason);
                else
                    _logger.LogError("Kafka producer error [{Code}]: {Reason}", error.Code, error.Reason);
            })
            // Log handler: surface librdkafka internal logs
            .SetLogHandler((_, logMessage) =>
            {
                var level = logMessage.Level switch
                {
                    SyslogLevel.Emergency or SyslogLevel.Alert or SyslogLevel.Critical => LogLevel.Critical,
                    SyslogLevel.Error => LogLevel.Error,
                    SyslogLevel.Warning => LogLevel.Warning,
                    SyslogLevel.Notice or SyslogLevel.Info => LogLevel.Information,
                    _ => LogLevel.Debug
                };
                _logger.Log(level, "[librdkafka] {Facility}: {Message}", logMessage.Facility, logMessage.Message);
            })
            // Statistics handler: export librdkafka metrics to Prometheus
            .SetStatisticsHandler((_, json) => KafkaMetricsExporter.HandleStatistics(json, _logger))
            .Build();

        _logger.LogInformation(
            "MedicinalProductProducer initialized → {BootstrapServers} / topic: {Topic}",
            _options.BootstrapServers, _options.MedicinalProductsTopic);
    }

    /// <summary>
    /// Produces a single MedicinalProduct event asynchronously.
    /// The GTIN is used as the message key to ensure ordering per product.
    /// </summary>
    public async Task<DeliveryResult<string, MedicinalProduct>> ProduceAsync(
        MedicinalProduct product,
        CancellationToken ct = default)
    {
        var message = new Message<string, MedicinalProduct>
        {
            Key = product.Gtin,
            Value = product,
            Headers = BuildHeaders(product)
        };

        try
        {
            var deliveryResult = await _producer.ProduceAsync(
                _options.MedicinalProductsTopic, message, ct);

            KafkaMetricsExporter.MessagesProducedTotal
                .WithLabels(_options.MedicinalProductsTopic)
                .Inc();

            _logger.LogDebug(
                "Produced {Gtin} ({ProductName}) → [{Topic}] partition={Partition} offset={Offset} latency={Latency}ms",
                product.Gtin,
                product.ProductName,
                deliveryResult.Topic,
                deliveryResult.Partition.Value,
                deliveryResult.Offset.Value,
                deliveryResult.Timestamp.UtcDateTime != DateTime.MinValue
                    ? (DateTime.UtcNow - deliveryResult.Timestamp.UtcDateTime).TotalMilliseconds.ToString("F0")
                    : "N/A");

            return deliveryResult;
        }
        catch (ProduceException<string, MedicinalProduct> ex)
        {
            KafkaMetricsExporter.ProduceErrorsTotal
                .WithLabels(_options.MedicinalProductsTopic, ex.Error.Code.ToString())
                .Inc();

            _logger.LogError(ex,
                "Failed to produce {Gtin}: code={Code} reason={Reason} fatal={IsFatal}",
                product.Gtin, ex.Error.Code, ex.Error.Reason, ex.Error.IsFatal);

            if (ex.Error.IsFatal)
                throw; // Fatal errors (e.g. broker auth failure) must surface immediately

            throw; // Non-fatal errors should also surface — let caller decide on retry
        }
    }

    /// <summary>
    /// Produces a batch of MedicinalProduct events using fire-and-forget Produce()
    /// for maximum throughput, then flushes at the end.
    ///
    /// Use this for bulk ingestion (e.g. initial data load from upstream sources).
    /// Use ProduceAsync for individual events where you need delivery confirmation.
    /// </summary>
    public async Task ProduceBatchAsync(
        IEnumerable<MedicinalProduct> products,
        CancellationToken ct = default)
    {
        var count = 0;
        var errors = 0;

        foreach (var product in products)
        {
            ct.ThrowIfCancellationRequested();

            _producer.Produce(
                _options.MedicinalProductsTopic,
                new Message<string, MedicinalProduct>
                {
                    Key = product.Gtin,
                    Value = product,
                    Headers = BuildHeaders(product)
                },
                deliveryReport =>
                {
                    if (deliveryReport.Error.IsError)
                    {
                        errors++;
                        KafkaMetricsExporter.ProduceErrorsTotal
                            .WithLabels(_options.MedicinalProductsTopic, deliveryReport.Error.Code.ToString())
                            .Inc();
                        _logger.LogWarning(
                            "Delivery failed for {Key}: {Error}",
                            deliveryReport.Message.Key, deliveryReport.Error);
                    }
                    else
                    {
                        KafkaMetricsExporter.MessagesProducedTotal
                            .WithLabels(_options.MedicinalProductsTopic)
                            .Inc();
                    }
                });

            count++;

            // Poll to trigger delivery callbacks periodically during large batches
            if (count % 1000 == 0)
            {
                _logger.LogInformation("Queued {Count} messages (errors so far: {Errors})", count, errors);
                await Task.Yield(); // Allow cancellation token to be checked
            }
        }

        // Wait for all messages to be acknowledged or timeout
        _logger.LogInformation("Flushing {Count} messages...", count);
        _producer.Flush(TimeSpan.FromSeconds(60));
        _logger.LogInformation("Batch complete. Produced: {Count}, Errors: {Errors}", count, errors);
    }

    /// <summary>
    /// Flush outstanding messages and dispose the producer.
    /// Always call this on shutdown to avoid message loss.
    /// </summary>
    public void Dispose()
    {
        _logger.LogInformation("Flushing producer before shutdown...");
        _producer.Flush(TimeSpan.FromSeconds(30));
        _producer.Dispose();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private static Headers BuildHeaders(MedicinalProduct product)
    {
        return new Headers
        {
            { "source", Encoding.UTF8.GetBytes(product.DataSource) },
            { "schema-version", Encoding.UTF8.GetBytes("1") },
            { "event-time", Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")) },
            { "content-type", Encoding.UTF8.GetBytes("application/json") },
            { "correlation-id", Encoding.UTF8.GetBytes(Guid.NewGuid().ToString()) }
        };
    }
}
