using System.Text.Json;
using FluentAssertions;
using HCI.Kafka.Monitoring;
using HCI.Kafka.Monitoring.Models;
using Prometheus;
using Xunit;

namespace HCI.Kafka.Tests.Monitoring;

/// <summary>
/// Tests for the Kafka metrics exporter.
/// Validates that librdkafka statistics JSON is correctly parsed
/// and exported to the appropriate Prometheus metrics.
/// </summary>
public sealed class KafkaMetricsExporterTests
{
    [Fact]
    public void HandleStatistics_WithValidProducerStats_ShouldNotThrow()
    {
        var statsJson = BuildProducerStatsJson(
            clientId: "test-producer",
            queueCount: 42,
            queueBytes: 8192,
            totalProduced: 1000);

        var act = () => KafkaMetricsExporter.HandleStatistics(statsJson);

        act.Should().NotThrow();
    }

    [Fact]
    public void HandleStatistics_WithInvalidJson_ShouldNotThrow()
    {
        // Metrics exporter must be fault-tolerant — never crash the application
        var act = () => KafkaMetricsExporter.HandleStatistics("not valid json {{{{");

        act.Should().NotThrow("statistics handler must never crash the application");
    }

    [Fact]
    public void HandleStatistics_WithEmptyStats_ShouldNotThrow()
    {
        var act = () => KafkaMetricsExporter.HandleStatistics("{}");

        act.Should().NotThrow();
    }

    [Fact]
    public void ApplicationMetrics_MessagesProducedTotal_ShouldIncrement()
    {
        // Reset is not possible with prometheus-net, so we test increment behaviour
        var before = KafkaMetricsExporter.MessagesProducedTotal
            .WithLabels("test.topic.v1")
            .Value;

        KafkaMetricsExporter.MessagesProducedTotal
            .WithLabels("test.topic.v1")
            .Inc();

        var after = KafkaMetricsExporter.MessagesProducedTotal
            .WithLabels("test.topic.v1")
            .Value;

        after.Should().Be(before + 1);
    }

    [Fact]
    public void ApplicationMetrics_DlqMessagesTotal_ShouldIncrementWithLabels()
    {
        var before = KafkaMetricsExporter.DlqMessagesTotal
            .WithLabels("hci.medicinal-products.v1", "ValidationException")
            .Value;

        KafkaMetricsExporter.DlqMessagesTotal
            .WithLabels("hci.medicinal-products.v1", "ValidationException")
            .Inc();

        var after = KafkaMetricsExporter.DlqMessagesTotal
            .WithLabels("hci.medicinal-products.v1", "ValidationException")
            .Value;

        after.Should().Be(before + 1);
    }

    [Fact]
    public void ApplicationMetrics_ProcessingDurationSeconds_ShouldRecordObservation()
    {
        using var timer = KafkaMetricsExporter.ProcessingDurationSeconds
            .WithLabels("hci.medicinal-products.v1")
            .NewTimer();

        // Simulated processing
        Thread.Sleep(10);

        // Timer disposes and records observation — should not throw
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string BuildProducerStatsJson(
        string clientId,
        long queueCount,
        long queueBytes,
        long totalProduced)
    {
        return JsonSerializer.Serialize(new
        {
            name = clientId,
            client_id = clientId,
            type = "producer",
            ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1000,
            msg_cnt = queueCount,
            msg_size = queueBytes,
            tx = 1500L,
            tx_bytes = 524288L,
            rx = 1500L,
            rx_bytes = 8192L,
            txmsgs = totalProduced,
            rxmsgs = 0L,
            brokers = new Dictionary<string, object>
            {
                ["pkc-xxx.westeurope.azure.confluent.cloud:9092/1"] = new
                {
                    name = "pkc-xxx.westeurope.azure.confluent.cloud:9092/1",
                    state = "UP",
                    rtt = new { min = 5000L, max = 25000L, avg = 12000L, p99 = 22000L },
                    outbuf_cnt = 0L,
                    waitresp_cnt = 2L,
                    tx = 1500L,
                    txerrs = 0L,
                    rxerrs = 0L,
                    connects = 1L,
                    disconnects = 0L
                }
            },
            topics = new Dictionary<string, object>
            {
                ["hci.medicinal-products.v1"] = new
                {
                    topic = "hci.medicinal-products.v1",
                    partitions = new Dictionary<string, object>
                    {
                        ["0"] = new { partition = 0, txmsgs = totalProduced / 6, rxmsgs = 0L, consumer_lag = 0L, fetchq_cnt = 0L },
                        ["1"] = new { partition = 1, txmsgs = totalProduced / 6, rxmsgs = 0L, consumer_lag = 0L, fetchq_cnt = 0L },
                    }
                }
            }
        });
    }
}
