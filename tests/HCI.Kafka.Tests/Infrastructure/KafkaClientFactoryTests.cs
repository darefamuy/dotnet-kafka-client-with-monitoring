using Confluent.Kafka;
using FluentAssertions;
using HCI.Kafka.Infrastructure.Configuration;
using HCI.Kafka.Infrastructure.Kafka;
using Xunit;

namespace HCI.Kafka.Tests.Infrastructure;

/// <summary>
/// Tests for KafkaClientFactory — validates that configuration
/// values are correctly applied to the Kafka client config objects.
/// </summary>
public sealed class KafkaClientFactoryTests
{
    private static readonly KafkaOptions TestOptions = new()
    {
        BootstrapServers = "pkc-test.westeurope.azure.confluent.cloud:9092",
        ApiKey = "test-api-key",
        ApiSecret = "test-api-secret",
        SchemaRegistryUrl = "https://psrc-test.confluent.cloud",
        SchemaRegistryApiKey = "sr-key",
        SchemaRegistryApiSecret = "sr-secret",
        ConsumerGroupId = "hci-test-consumer"
    };

    // ── Producer config tests ─────────────────────────────────────────────────

    [Fact]
    public void CreateProducerConfig_ShouldSetAcksAll()
    {
        var config = KafkaClientFactory.CreateProducerConfig(TestOptions, "test-producer");

        config.Acks.Should().Be(Acks.All,
            "all in-sync replicas must acknowledge for maximum durability");
    }

    [Fact]
    public void CreateProducerConfig_ShouldEnableIdempotence()
    {
        var config = KafkaClientFactory.CreateProducerConfig(TestOptions, "test-producer");

        config.EnableIdempotence.Should().BeTrue(
            "idempotence prevents duplicate messages on retry");
    }

    [Fact]
    public void CreateProducerConfig_ShouldUseSaslSsl()
    {
        var config = KafkaClientFactory.CreateProducerConfig(TestOptions, "test-producer");

        config.SecurityProtocol.Should().Be(SecurityProtocol.SaslSsl,
            "Confluent Cloud requires SASL/SSL");
        config.SaslMechanism.Should().Be(SaslMechanism.Plain);
    }

    [Fact]
    public void CreateProducerConfig_ShouldSetCorrectCredentials()
    {
        var config = KafkaClientFactory.CreateProducerConfig(TestOptions, "test-producer");

        config.SaslUsername.Should().Be(TestOptions.ApiKey);
        config.SaslPassword.Should().Be(TestOptions.ApiSecret);
        config.BootstrapServers.Should().Be(TestOptions.BootstrapServers);
    }

    [Fact]
    public void CreateProducerConfig_ShouldUseLz4Compression()
    {
        var config = KafkaClientFactory.CreateProducerConfig(TestOptions, "test-producer");

        config.CompressionType.Should().Be(CompressionType.Lz4,
            "LZ4 provides best compression/speed tradeoff for pharma JSON payloads");
    }

    [Fact]
    public void CreateProducerConfig_ShouldEnableStatistics()
    {
        var config = KafkaClientFactory.CreateProducerConfig(TestOptions, "test-producer");

        config.StatisticsIntervalMs.Should().BeGreaterThan(0,
            "statistics must be enabled for Prometheus monitoring");
    }

    [Fact]
    public void CreateProducerConfig_ShouldSetClientId()
    {
        const string clientId = "hci-test-producer";
        var config = KafkaClientFactory.CreateProducerConfig(TestOptions, clientId);

        config.ClientId.Should().Be(clientId);
    }

    // ── Consumer config tests ─────────────────────────────────────────────────

    [Fact]
    public void CreateConsumerConfig_ShouldDisableAutoCommit()
    {
        var config = KafkaClientFactory.CreateConsumerConfig(TestOptions, "test-consumer");

        config.EnableAutoCommit.Should().BeFalse(
            "auto-commit must be disabled — we commit manually AFTER successful processing");
    }

    [Fact]
    public void CreateConsumerConfig_ShouldDisableAutoOffsetStore()
    {
        var config = KafkaClientFactory.CreateConsumerConfig(TestOptions, "test-consumer");

        config.EnableAutoOffsetStore.Should().BeFalse(
            "auto offset store must be disabled — we call StoreOffset explicitly");
    }

    [Fact]
    public void CreateConsumerConfig_ShouldSetEarliestAutoOffsetReset()
    {
        var config = KafkaClientFactory.CreateConsumerConfig(TestOptions, "test-consumer");

        config.AutoOffsetReset.Should().Be(AutoOffsetReset.Earliest,
            "new consumer groups should start from the beginning to avoid missing messages");
    }

    [Fact]
    public void CreateConsumerConfig_ShouldSetGroupId()
    {
        var config = KafkaClientFactory.CreateConsumerConfig(TestOptions, "test-consumer");

        config.GroupId.Should().Be(TestOptions.ConsumerGroupId);
    }

    [Fact]
    public void CreateConsumerConfig_ShouldHaveReasonableSessionTimeout()
    {
        var config = KafkaClientFactory.CreateConsumerConfig(TestOptions, "test-consumer");

        // Heartbeat interval must be < session timeout / 3 (Kafka requirement)
        config.SessionTimeoutMs.Should().HaveValue();
        config.HeartbeatIntervalMs.Should().HaveValue();
        config.HeartbeatIntervalMs.Value.Should().BeLessThan(config.SessionTimeoutMs.Value / 3,
            "heartbeat interval must be less than session timeout / 3");
    }

    [Fact]
    public void CreateConsumerConfig_ShouldEnableStatistics()
    {
        var config = KafkaClientFactory.CreateConsumerConfig(TestOptions, "test-consumer");

        config.StatisticsIntervalMs.Should().BeGreaterThan(0,
            "statistics must be enabled for consumer lag monitoring");
    }

    [Fact]
    public void CreateConsumerConfig_ShouldUseSaslSsl()
    {
        var config = KafkaClientFactory.CreateConsumerConfig(TestOptions, "test-consumer");

        config.SecurityProtocol.Should().Be(SecurityProtocol.SaslSsl);
        config.SaslMechanism.Should().Be(SaslMechanism.Plain);
    }
}
