# C# Kafka Client App Monitoring — Confluent Cloud Kafka Training

## Architecture

```
 ┌──────────────────────────────────────────────────────────────────┐
 │                     Confluent Cloud                              │
 │                                                                  │
 │   hci.medicinal-products.v1  (6 partitions, 3d retention)       │
 │   hci.drug-alerts.v1         (6 partitions, 90d retention)      │
 │   hci.medicinal-products.v1.dlq  (6 partitions, 30d retention)  │
 │                                                                  │
 └──────────────────────────────────────────────────────────────────┘
          ▲                              │
          │ produce                      │ consume
          │                              ▼
 ┌─────────────────┐           ┌─────────────────────┐
 │  HCI.Kafka.     │           │  HCI.Kafka.         │
 │  Producer       │           │  Consumer           │
 │                 │           │                     │
 │  - Acks=All     │           │  - Manual commit    │
 │  - Idempotent   │           │  - Rebalance aware  │
 │  - LZ4 compress │           │  - Graceful shutdown│
 │  - /metrics:9092│           │  - /metrics:9091    │
 └─────────────────┘           └─────────────────────┘
          │                              │
          └──────────┬───────────────────┘
                     ▼
          ┌─────────────────────┐
          │  Prometheus         │  :9090 (monitoring container)
          │  + AlertManager     │  :9093
          └──────────┬──────────┘
                     ▼
          ┌─────────────────────┐
          │  Grafana            │  :3000
          │  Dashboard          │
          └─────────────────────┘
```

---

## Project Structure

```
dotnet-kafka-client-with-monitoring/
├── HCI.Kafka.sln
│
├── src/
│   ├── HCI.Kafka.Domain/                    # Pure domain models — no Kafka dependencies
│   │   ├── Models/
│   │   │   ├── MedicinalProduct.cs          # Core pharmaceutical product record
│   │   │   ├── MarketingStatus.cs           # Swissmedic marketing status enum
│   │   │   └── DrugAlert.cs                 # Safety alert model
│   │   └── Factories/
│   │       └── SampleDataFactory.cs         # Realistic Swiss pharma test data generator
│   │
│   ├── HCI.Kafka.Infrastructure/            # Kafka client configuration
│   │   ├── Configuration/
│   │   │   └── KafkaOptions.cs              # Confluent Cloud options (binds to appsettings)
│   │   ├── Kafka/
│   │   │   └── KafkaClientFactory.cs        # Centralized, hardened ProducerConfig/ConsumerConfig
│   │   └── Serialization/
│   │       └── JsonSerializer.cs            # JSON serializer (Module 3 upgrades to Avro)
│   │
│   ├── HCI.Kafka.Monitoring/                # Prometheus metrics
│   │   ├── Models/
│   │   │   └── KafkaStatistics.cs           # librdkafka statistics JSON model
│   │   └── KafkaMetricsExporter.cs          # Exports librdkafka stats → Prometheus gauges/counters
│   │
│   ├── HCI.Kafka.Producer/                  # Producer application (Worker Service)
│   │   ├── Services/
│   │   │   └── MedicinalProductProducer.cs  # Producer with error handling
│   │   ├── ProducerWorker.cs                # BackgroundService driving the producer
│   │   ├── Program.cs                       # DI setup, metrics server startup
│   │   └── appsettings.json                 # Configuration template
│   │
│   └── HCI.Kafka.Consumer/                  # Consumer application (Worker Service)
│       ├── Services/
│       │   └── MedicinalProductConsumer.cs  # Consumer with manual offset commit
│       ├── Program.cs                       # DI setup, metrics server startup
│       └── appsettings.json                 # Configuration template
│
├── tests/
│   └── HCI.Kafka.Tests/
│       ├── Domain/
│       │   └── SampleDataFactoryTests.cs    # Validates generated data meets schema constraints
│       ├── Infrastructure/
│       │   ├── SerializerTests.cs           # Round-trip serialization tests
│       │   └── KafkaClientFactoryTests.cs   # Validates production config values
│       └── Monitoring/
│           └── KafkaMetricsExporterTests.cs # Prometheus metric integration tests
│
├── schemas/
│   ├── hci.medicinal-product.v1.avsc        # Avro schema (used in Module 3)
│   └── hci.drug-alert.v1.avsc               # Avro schema (used in Module 3)
│
├── monitoring/
│   ├── prometheus/
│   │   ├── prometheus.yml                   # Scrape config for both services
│   │   └── alerts/
│   │       └── kafka-hci.yml                # Alert rules (lag, DLQ, errors, RTT)
│   └── grafana/
│       ├── hci-kafka-dashboard.json         # Pre-built dashboard (import manually)
│       └── provisioning/                    # Auto-provisioned for docker-compose
│
├── scripts/
│   └── setup-confluent-cloud.sh             # Creates topics and registers schemas
└── docker-compose.yml                       # Prometheus + Grafana + AlertManager
```

---

## Prerequisites

| Tool | Version | Purpose |
|------|---------|---------|
| .NET SDK | 10.0+ | Build and run C# projects |
| Confluent CLI | v3.x | Topic/schema management |
| Docker Desktop | any | Local monitoring stack |
| JetBrains Rider or Visual Studio | 2022+ | IDE |

### Verify prerequisites

```bash
dotnet --version           # >= 10.0
confluent version           # >= 3.0
docker --version
```

---

## Quick Start

### Step 1 — Clone and build

```bash
git clone git@github.com:darefamuy/dotnet-kafka-client-with-monitoring.git
cd dotnet-kafka-client-with-monitoring
dotnet build
```

### Step 2 — Configure Confluent Cloud credentials

Copy the appsettings template for each project and fill in your Confluent Cloud credentials:

```bash
cp src/HCI.Kafka.Producer/appsettings.json src/HCI.Kafka.Producer/appsettings.Development.json
cp src/HCI.Kafka.Consumer/appsettings.json src/HCI.Kafka.Consumer/appsettings.Development.json
```

Edit both `appsettings.Development.json` files:

```json
{
  "Kafka": {
    "BootstrapServers": "pkc-XXXXX.westeurope.azure.confluent.cloud:9092",
    "ApiKey":           "YOUR_CONFLUENT_CLOUD_API_KEY",
    "ApiSecret":        "YOUR_CONFLUENT_CLOUD_API_SECRET",
    "SchemaRegistryUrl": "https://psrc-XXXXX.westeurope.azure.confluent.cloud",
    "SchemaRegistryApiKey":    "YOUR_SR_API_KEY",
    "SchemaRegistryApiSecret": "YOUR_SR_API_SECRET"
  }
}
```

> **Security**: `appsettings.Development.json` is in `.gitignore`. Never commit credentials.

### Step 3 — Create Confluent Cloud topics

```bash
# Authenticate with Confluent CLI
confluent login

# Select your environment and cluster
confluent environment use env-XXXXX
confluent kafka cluster use lkc-XXXXX

# Run the setup script
chmod +x scripts/setup-confluent-cloud.sh
./scripts/setup-confluent-cloud.sh
```

### Step 4 — Start the monitoring stack

```bash
docker-compose up -d
```

| Service | URL | Credentials |
|---------|-----|-------------|
| Grafana | http://localhost:3000 | admin / admin |
| Prometheus | http://localhost:9090 | — |
| AlertManager | http://localhost:9093 | — |

Import the dashboard: **Grafana → + → Import → Upload JSON** → select `monitoring/grafana/hci-kafka-dashboard.json`

### Step 5 — Run the producer

```bash
cd src/HCI.Kafka.Producer
DOTNET_ENVIRONMENT=Development dotnet run
```

Expected output:
```
[10:24:31.002 INF] HCI.Kafka.Producer.ProducerWorker: Generated 1000 sample MedicinalProduct records. Producing...
[10:24:31.245 DBG] Produced 7612345987654321 (Paracetamol Novartis) → [hci.medicinal-products.v1] partition=3 offset=1042
[10:24:33.891 INF] HCI.Kafka.Producer.ProducerWorker: Load test complete. Produced 1000 messages in 2.64s (378 msg/s)
```

### Step 6 — Run the consumer (new terminal)

```bash
cd src/HCI.Kafka.Consumer
DOTNET_ENVIRONMENT=Development dotnet run
```

Expected output:
```
[10:24:35.001 INF] MedicinalProductConsumer: Partition assignment: 6 partitions assigned
[10:24:35.012 INF] Processing [hci.medicinal-products.v1][0]@0 | GTIN=7612345987654321 | Product=Paracetamol Novartis | Status=Active | Price=CHF 34.50
```

### Step 7 — Run tests

```bash
dotnet test --logger "console;verbosity=detailed"
```


### Monitoring: librdkafka Statistics

The Confluent .NET client emits rich JSON statistics every `StatisticsIntervalMs` milliseconds via the `SetStatisticsHandler` callback. This includes:

- Per-broker round-trip time histograms (P50/P95/P99)
- Producer queue depth and byte count
- Per-partition consumer lag
- Connection state and reconnect counts

`KafkaMetricsExporter.cs` parses this JSON and exports the values as Prometheus gauges and counters, enabling the Grafana dashboard.

---

## Monitoring & Alerting

### Key Metrics

| Metric | Description | Alert Threshold |
|--------|-------------|----------------|
| `kafka_consumer_partition_lag` | Messages behind latest offset | Warning: >10,000 / Critical: >50,000 |
| `hci_kafka_produced_total` | Producer throughput rate | — |
| `kafka_producer_broker_rtt_p99_ms` | Broker round-trip P99 | Warning: >500ms |
| `hci_kafka_dlq_messages_total` | Dead Letter Queue routing | Any non-zero |
| `kafka_producer_queue_messages` | Local producer queue depth | Warning: >10,000 |
| `hci_kafka_processing_duration_seconds` | Message processing latency | P99 >1s |

