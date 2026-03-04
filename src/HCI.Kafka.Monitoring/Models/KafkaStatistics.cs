using System.Text.Json.Serialization;

namespace HCI.Kafka.Monitoring.Models;

/// <summary>
/// Represents the librdkafka JSON statistics payload emitted via StatisticsHandler.
/// Only the fields relevant to HCI monitoring are mapped here.
/// Full schema: https://github.com/confluentinc/librdkafka/blob/master/STATISTICS.md
/// </summary>
public sealed class KafkaStatistics
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("client_id")]
    public string ClientId { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty; // "producer" or "consumer"

    [JsonPropertyName("ts")]
    public long TimestampMicros { get; set; }

    [JsonPropertyName("time")]
    public long WallClockSeconds { get; set; }

    [JsonPropertyName("msg_cnt")]
    public long MessageQueueCount { get; set; }

    [JsonPropertyName("msg_size")]
    public long MessageQueueBytes { get; set; }

    [JsonPropertyName("tx")]
    public long TotalTransmits { get; set; }

    [JsonPropertyName("tx_bytes")]
    public long TotalTransmitBytes { get; set; }

    [JsonPropertyName("rx")]
    public long TotalReceives { get; set; }

    [JsonPropertyName("rx_bytes")]
    public long TotalReceiveBytes { get; set; }

    [JsonPropertyName("txmsgs")]
    public long TotalMessagesProduced { get; set; }

    [JsonPropertyName("rxmsgs")]
    public long TotalMessagesConsumed { get; set; }

    [JsonPropertyName("brokers")]
    public Dictionary<string, BrokerStatistics> Brokers { get; set; } = new();

    [JsonPropertyName("topics")]
    public Dictionary<string, TopicStatistics> Topics { get; set; } = new();
}

public sealed class BrokerStatistics
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("rtt")]
    public WindowStatistics Rtt { get; set; } = new();

    [JsonPropertyName("outbuf_cnt")]
    public long OutboundBufferCount { get; set; }

    [JsonPropertyName("waitresp_cnt")]
    public long WaitingResponseCount { get; set; }

    [JsonPropertyName("tx")]
    public long Transmits { get; set; }

    [JsonPropertyName("txerrs")]
    public long TransmitErrors { get; set; }

    [JsonPropertyName("rxerrs")]
    public long ReceiveErrors { get; set; }

    [JsonPropertyName("connects")]
    public long ConnectionAttempts { get; set; }

    [JsonPropertyName("disconnects")]
    public long Disconnections { get; set; }
}

public sealed class WindowStatistics
{
    [JsonPropertyName("min")]
    public long Min { get; set; }

    [JsonPropertyName("max")]
    public long Max { get; set; }

    [JsonPropertyName("avg")]
    public long Avg { get; set; }

    [JsonPropertyName("p99")]
    public long P99 { get; set; }
}

public sealed class TopicStatistics
{
    [JsonPropertyName("topic")]
    public string Topic { get; set; } = string.Empty;

    [JsonPropertyName("partitions")]
    public Dictionary<string, PartitionStatistics> Partitions { get; set; } = new();
}

public sealed class PartitionStatistics
{
    [JsonPropertyName("partition")]
    public int Partition { get; set; }

    [JsonPropertyName("txmsgs")]
    public long MessagesProduced { get; set; }

    [JsonPropertyName("rxmsgs")]
    public long MessagesConsumed { get; set; }

    [JsonPropertyName("consumer_lag")]
    public long ConsumerLag { get; set; }

    [JsonPropertyName("fetchq_cnt")]
    public long FetchQueueCount { get; set; }
}
