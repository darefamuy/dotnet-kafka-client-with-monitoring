using HCI.Kafka.Domain.Factories;
using HCI.Kafka.Infrastructure.Configuration;
using HCI.Kafka.Producer.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HCI.Kafka.Producer;

/// <summary>
/// Background service that drives the MedicinalProductProducer.
///
/// Modes (controlled via appsettings):
/// - LoadTest: produce N messages as fast as possible (benchmarking)
/// - Continuous: produce messages at a steady rate simulating real HCI data ingestion
/// </summary>
public sealed class ProducerWorker : BackgroundService
{
    private readonly MedicinalProductProducer _producer;
    private readonly KafkaOptions _options;
    private readonly ILogger<ProducerWorker> _logger;

    public ProducerWorker(
        MedicinalProductProducer producer,
        IOptions<KafkaOptions> options,
        ILogger<ProducerWorker> logger)
    {
        _producer = producer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "ProducerWorker starting. Mode: LoadTest ({Count} messages)",
            _options.LoadTestMessageCount);

        try
        {
            var products = SampleDataFactory
                .GenerateBatch(_options.LoadTestMessageCount)
                .ToList();

            _logger.LogInformation(
                "Generated {Count} sample MedicinalProduct records. Producing...",
                products.Count);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await _producer.ProduceBatchAsync(products, stoppingToken);
            sw.Stop();

            var rate = products.Count / sw.Elapsed.TotalSeconds;
            _logger.LogInformation(
                "Load test complete. Produced {Count} messages in {Elapsed:F2}s ({Rate:F0} msg/s)",
                products.Count, sw.Elapsed.TotalSeconds, rate);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("ProducerWorker cancelled.");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "ProducerWorker encountered a fatal error.");
            throw;
        }
    }
}
