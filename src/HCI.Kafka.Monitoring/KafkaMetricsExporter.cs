using System.Text.Json;
using HCI.Kafka.Monitoring.Models;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace HCI.Kafka.Monitoring;

/// <summary>
/// Exports librdkafka statistics to Prometheus metrics.
///
/// Wire this up by passing HandleStatistics to the Kafka client builder:
///
///   .SetStatisticsHandler((_, json) => KafkaMetricsExporter.HandleStatistics(json, logger))
///
/// Then expose /metrics endpoint:
///   app.MapMetrics("/metrics");
/// </summary>
public static class KafkaMetricsExporter
{
    // ── Producer metrics ─────────────────────────────────────────────────────

    private static readonly Gauge ProducerQueueDepth = Metrics.CreateGauge(
        "kafka_producer_queue_messages",
        "Number of messages waiting in the local producer queue",
        new GaugeConfiguration { LabelNames = ["client_id"] });

    private static readonly Gauge ProducerQueueBytes = Metrics.CreateGauge(
        "kafka_producer_queue_bytes",
        "Bytes waiting in the local producer queue",
        new GaugeConfiguration { LabelNames = ["client_id"] });

    private static readonly Gauge ProducerBrokerRttAvgMs = Metrics.CreateGauge(
        "kafka_producer_broker_rtt_avg_ms",
        "Average round-trip time to broker in milliseconds",
        new GaugeConfiguration { LabelNames = ["client_id", "broker"] });

    private static readonly Gauge ProducerBrokerRttP99Ms = Metrics.CreateGauge(
        "kafka_producer_broker_rtt_p99_ms",
        "P99 round-trip time to broker in milliseconds",
        new GaugeConfiguration { LabelNames = ["client_id", "broker"] });

    private static readonly Counter ProducerTotalMessages = Metrics.CreateCounter(
        "kafka_producer_messages_total",
        "Total messages produced successfully",
        new CounterConfiguration { LabelNames = ["client_id"] });

    private static readonly Gauge ProducerBrokerOutbufCount = Metrics.CreateGauge(
        "kafka_producer_broker_outbuf_messages",
        "Messages waiting to be sent to broker",
        new GaugeConfiguration { LabelNames = ["client_id", "broker"] });

    private static readonly Counter ProducerBrokerTxErrors = Metrics.CreateCounter(
        "kafka_producer_broker_tx_errors_total",
        "Total broker transmit errors",
        new CounterConfiguration { LabelNames = ["client_id", "broker"] });

    // ── Consumer metrics ─────────────────────────────────────────────────────

    private static readonly Gauge ConsumerPartitionLag = Metrics.CreateGauge(
        "kafka_consumer_partition_lag",
        "Consumer lag per topic partition (messages behind latest offset)",
        new GaugeConfiguration { LabelNames = ["client_id", "topic", "partition"] });

    private static readonly Gauge ConsumerFetchQueueCount = Metrics.CreateGauge(
        "kafka_consumer_fetch_queue_messages",
        "Messages in the consumer fetch queue awaiting processing",
        new GaugeConfiguration { LabelNames = ["client_id", "topic", "partition"] });

    private static readonly Counter ConsumerTotalMessages = Metrics.CreateCounter(
        "kafka_consumer_messages_total",
        "Total messages consumed",
        new CounterConfiguration { LabelNames = ["client_id"] });

    // ── Application-level metrics (not from librdkafka) ──────────────────────

    public static readonly Counter MessagesProducedTotal = Metrics.CreateCounter(
        "hci_kafka_produced_total",
        "HCI application-level produced message count",
        new CounterConfiguration { LabelNames = ["topic"] });

    public static readonly Counter ProduceErrorsTotal = Metrics.CreateCounter(
        "hci_kafka_produce_errors_total",
        "HCI application-level produce error count",
        new CounterConfiguration { LabelNames = ["topic", "error_code"] });

    public static readonly Counter MessagesConsumedTotal = Metrics.CreateCounter(
        "hci_kafka_consumed_total",
        "HCI application-level consumed message count",
        new CounterConfiguration { LabelNames = ["topic", "consumer_group"] });

    public static readonly Histogram ProcessingDurationSeconds = Metrics.CreateHistogram(
        "hci_kafka_processing_duration_seconds",
        "Time to process a single Kafka message",
        new HistogramConfiguration
        {
            LabelNames = ["topic"],
            Buckets = Histogram.ExponentialBuckets(0.001, 2, 14) // 1ms to ~8s
        });

    public static readonly Counter DlqMessagesTotal = Metrics.CreateCounter(
        "hci_kafka_dlq_messages_total",
        "Messages routed to Dead Letter Queue",
        new CounterConfiguration { LabelNames = ["source_topic", "error_type"] });

    public static readonly Counter ValidationErrorsTotal = Metrics.CreateCounter(
        "hci_kafka_validation_errors_total",
        "Data validation errors by rule name",
        new CounterConfiguration { LabelNames = ["rule_name"] });

    // ── Statistics handler ────────────────────────────────────────────────────

    /// <summary>
    /// Parses librdkafka JSON statistics and exports to Prometheus.
    /// Pass this to SetStatisticsHandler on the Kafka client builder.
    /// </summary>
    public static void HandleStatistics(string statisticsJson, ILogger? logger = null)
    {
        try
        {
            var stats = JsonSerializer.Deserialize<KafkaStatistics>(statisticsJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (stats is null) return;

            // Producer queue depth
            ProducerQueueDepth.WithLabels(stats.ClientId).Set(stats.MessageQueueCount);
            ProducerQueueBytes.WithLabels(stats.ClientId).Set(stats.MessageQueueBytes);

            // Total messages counter (use IncTo to handle stats resets)
            ProducerTotalMessages.WithLabels(stats.ClientId).IncTo(stats.TotalMessagesProduced);
            ConsumerTotalMessages.WithLabels(stats.ClientId).IncTo(stats.TotalMessagesConsumed);

            // Per-broker metrics
            foreach (var (_, broker) in stats.Brokers)
            {
                if (broker.Name.Contains("bootstrap", StringComparison.OrdinalIgnoreCase))
                    continue; // Skip internal bootstrap broker

                var rttAvgMs = broker.Rtt.Avg / 1000.0; // µs → ms
                var rttP99Ms = broker.Rtt.P99 / 1000.0;

                ProducerBrokerRttAvgMs.WithLabels(stats.ClientId, broker.Name).Set(rttAvgMs);
                ProducerBrokerRttP99Ms.WithLabels(stats.ClientId, broker.Name).Set(rttP99Ms);
                ProducerBrokerOutbufCount.WithLabels(stats.ClientId, broker.Name).Set(broker.OutboundBufferCount);
                ProducerBrokerTxErrors.WithLabels(stats.ClientId, broker.Name).IncTo(broker.TransmitErrors);
            }

            // Per-partition consumer lag
            foreach (var (_, topic) in stats.Topics)
            {
                foreach (var (_, partition) in topic.Partitions)
                {
                    if (partition.Partition < 0) continue; // Partition -1 = internal aggregate

                    ConsumerPartitionLag
                        .WithLabels(stats.ClientId, topic.Topic, partition.Partition.ToString())
                        .Set(Math.Max(0, partition.ConsumerLag));

                    ConsumerFetchQueueCount
                        .WithLabels(stats.ClientId, topic.Topic, partition.Partition.ToString())
                        .Set(partition.FetchQueueCount);
                }
            }
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Failed to parse Kafka statistics JSON");
        }
    }
}
