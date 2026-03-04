using Confluent.Kafka;
using HCI.Kafka.Infrastructure.Configuration;

namespace HCI.Kafka.Infrastructure.Kafka;

/// <summary>
/// Centralized factory for creating Confluent Kafka client configuration objects.
/// All essential settings are defined here — do not scatter config
/// across services.
/// </summary>
public static class KafkaClientFactory
{
    /// <summary>
    /// Creates a ProducerConfig for Confluent Cloud.
    /// 
    /// Key settings:
    /// - Acks.All: wait for all in-sync replicas before acknowledging
    /// - EnableIdempotence: exactly-once producer semantics per partition
    /// - LingerMs + BatchSize: micro-batching for throughput without sacrificing much latency
    /// - CompressionType.Lz4: ~60% size reduction for JSON-heavy payloads
    /// </summary>
    public static ProducerConfig CreateProducerConfig(KafkaOptions opts, string clientId)
    {
        return new ProducerConfig
        {
            // ── Connectivity ─────────────────────────────────────────────────
            BootstrapServers = opts.BootstrapServers,
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = opts.ApiKey,
            SaslPassword = opts.ApiSecret,
            ClientId = clientId,

            // ── Reliability ──────────────────────────────────────────────────
            Acks = Acks.All,                 // Strongest durability guarantee
            EnableIdempotence = true,         // Prevents duplicate messages on retry
            MaxInFlight = 5,                  // Max unacknowledged requests (idempotent allows up to 5)
            MessageSendMaxRetries = 10,       // Retry transient broker errors
            RetryBackoffMs = 500,             // 500ms between retries
            MessageTimeoutMs = 30000,         // 30s overall message delivery timeout

            // ── Performance ──────────────────────────────────────────────────
            LingerMs = 5,                     // Collect messages for 5ms before sending batch
            BatchSize = 65536,                // 64KB batch size
            CompressionType = CompressionType.Lz4,  // Fast compression for pharma JSON payloads
            QueueBufferingMaxMessages = 100000,
            QueueBufferingMaxKbytes = 1048576,       // 1GB producer queue

            // ── Observability ────────────────────────────────────────────────
            StatisticsIntervalMs = 10000,     // Emit stats every 10 seconds (for Prometheus)

            // ── Connection resilience ────────────────────────────────────────
            SocketKeepaliveEnable = true,
            ReconnectBackoffMs = 100,
            ReconnectBackoffMaxMs = 10000,
        };
    }

    /// <summary>
    /// Creates a ConsumerConfig for Confluent Cloud.
    ///
    /// Key settings:
    /// - EnableAutoCommit = false: manual offset commit after successful processing
    /// - EnableAutoOffsetStore = false: explicit store-then-commit for safety
    /// - MaxPollIntervalMs: allow up to 5 min for slow processing before broker considers consumer dead
    /// </summary>
    public static ConsumerConfig CreateConsumerConfig(KafkaOptions opts, string clientId)
    {
        return new ConsumerConfig
        {
            // ── Connectivity ─────────────────────────────────────────────────
            BootstrapServers = opts.BootstrapServers,
            SecurityProtocol = SecurityProtocol.SaslSsl,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = opts.ApiKey,
            SaslPassword = opts.ApiSecret,
            ClientId = clientId,
            GroupId = opts.ConsumerGroupId,

            // ── Offset management ────────────────────────────────────────────
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,         // Manual commit — we commit AFTER processing
            EnableAutoOffsetStore = false,    // We store offsets explicitly (StoreOffset before Commit)

            // ── Session & rebalance ──────────────────────────────────────────
            MaxPollIntervalMs = 300000,       // 5 min — allow for slow DB writes
            SessionTimeoutMs = 45000,         // 45s heartbeat timeout
            HeartbeatIntervalMs = 3000,       // Heartbeat every 3s (must be < SessionTimeout/3)

            // ── Fetch tuning ─────────────────────────────────────────────────
            FetchMaxBytes = 52428800,         // 50MB max fetch
            MaxPartitionFetchBytes = 10485760, // 10MB per partition
            FetchMinBytes = 1,
            FetchWaitMaxMs = 500,

            // ── Observability ────────────────────────────────────────────────
            StatisticsIntervalMs = 10000,

            // ── Connection resilience ────────────────────────────────────────
            SocketKeepaliveEnable = true,
            ReconnectBackoffMs = 100,
            ReconnectBackoffMaxMs = 10000,
        };
    }
}
