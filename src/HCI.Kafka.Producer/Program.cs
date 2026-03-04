using HCI.Kafka.Infrastructure.Configuration;
using HCI.Kafka.Monitoring;
using HCI.Kafka.Producer;
using HCI.Kafka.Producer.Services;
using Prometheus;
using Serilog;
using Serilog.Events;

// ── Logging bootstrap ────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss.fff} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("Starting HCI Kafka Producer (Module 1)");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            Log.Information("Environment: {Environment}", context.HostingEnvironment.EnvironmentName);

            // ── Configuration ────────────────────────────────────────────
            services.Configure<KafkaOptions>(
                context.Configuration.GetSection(KafkaOptions.SectionName));

            var kafkaOptions = context.Configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>();
            Log.Information("BootstrapServers: {BootstrapServers}", kafkaOptions?.BootstrapServers);

            // ── Kafka producer ───────────────────────────────────────────
            services.AddSingleton<MedicinalProductProducer>();

            // ── Background worker ────────────────────────────────────────
            services.AddHostedService<ProducerWorker>();
        })
        .Build();

    // ── Prometheus metrics server (runs on port 9092) ────────────────────────
    var metricsServer = new MetricServer(hostname: "*", port: 9092);
    metricsServer.Start();
    Log.Information("Prometheus metrics available at port 9092 (listening on all interfaces)");

    await host.RunAsync();
    metricsServer.Stop();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Producer host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
