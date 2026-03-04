namespace HCI.Kafka.Infrastructure.Configuration;

/// <summary>
/// Confluent Cloud connection and Kafka client configuration.
/// Bind from appsettings.json section "Kafka".
/// </summary>
public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>Confluent Cloud bootstrap servers (e.g. pkc-xxx.westeurope.azure.confluent.cloud:9092)</summary>
    public required string BootstrapServers { get; init; }

    /// <summary>Confluent Cloud API Key (SASL username)</summary>
    public required string ApiKey { get; init; }

    /// <summary>Confluent Cloud API Secret (SASL password)</summary>
    public required string ApiSecret { get; init; }

    /// <summary>Schema Registry URL</summary>
    public required string SchemaRegistryUrl { get; init; }

    /// <summary>Schema Registry API Key</summary>
    public required string SchemaRegistryApiKey { get; init; }

    /// <summary>Schema Registry API Secret</summary>
    public required string SchemaRegistryApiSecret { get; init; }

    /// <summary>Consumer group ID for the Compendium updater service</summary>
    public string ConsumerGroupId { get; init; } = "hci-compendium-updater";

    /// <summary>Main topic for medicinal product events</summary>
    public string MedicinalProductsTopic { get; init; } = "hci.medicinal-products.v1";

    /// <summary>Dead Letter Queue topic for failed messages</summary>
    public string DlqTopic { get; init; } = "hci.medicinal-products.v1.dlq";

    /// <summary>Drug alerts topic</summary>
    public string DrugAlertsTopic { get; init; } = "hci.drug-alerts.v1";

    /// <summary>
    /// Number of messages to produce in load-test mode.
    /// Controlled via appsettings or environment variable.
    /// </summary>
    public int LoadTestMessageCount { get; init; } = 1000;
}
