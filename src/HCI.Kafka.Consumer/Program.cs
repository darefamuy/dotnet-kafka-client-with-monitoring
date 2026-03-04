using HCI.Kafka.Consumer.Services;
using HCI.Kafka.Infrastructure.Configuration;
using Prometheus;
using Serilog;
using Serilog.Events;

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
    Log.Information("Starting HCI Kafka Consumer (Module 1)");

    var host = Host.CreateDefaultBuilder(args)
        .UseSerilog()
        .ConfigureServices((context, services) =>
        {
            services.Configure<KafkaOptions>(
                context.Configuration.GetSection(KafkaOptions.SectionName));

            services.AddHostedService<MedicinalProductConsumer>();
        })
        .Build();

    // Prometheus scrape endpoint on port 9091 (9090 used by producer)
    var metricsServer = new MetricServer(hostname: "*", port: 9091);
    metricsServer.Start();
    Log.Information("Prometheus metrics available at port 9091 (listening on all interfaces)");

    await host.RunAsync();
    metricsServer.Stop();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Consumer host terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}
