using FluentdForward.OpenTelemetry.Exporter.Logs;
using FluentdForward.OpenTelemetry.MessagePack;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Tgs.TelemetryMiddle.Models;
using Valhalla.Messages;

var builder = WebApplication.CreateBuilder(args);
{
	builder.Logging
		.AddOpenTelemetry(options =>
		{
			options.IncludeFormattedMessage = true;
			options.ParseStateValues = true;
			_ = options.AddFluentdForwardExporter(fluentOptions =>
			{
				var nodeIp = builder.Configuration.GetValue<string>("NODE_IP")!;
				fluentOptions.Host = nodeIp;
				fluentOptions.Tag = builder.Configuration.GetValue<string>("OTEL_SERVICE_NAME")!;
				fluentOptions.UseMessagePack(LogRecordFormatterResolver.GetResolverInstanceWithExtendInfo(
					"MachineName",
					() => Environment.MachineName));
			});
		});

	_ = builder.Services
		.AddTransient(sp => ActivatorUtilities.CreateInstance<GrpcLogsService>(
			sp,
			sp.GetRequiredKeyedService<IEventPublisher<FluentdLogsBatch>>("LogStream")))
		.AddGrpc()
		.Services
		.AddEventBus(config => config.RegisterEventSource<FluentdLogsBatch>("LogStream"))
		.AddKeyedSingleton(
			"LogStream",
			(sp, _) => sp.GetRequiredService<IEventBus>().GetEventPublisher<FluentdLogsBatch>("LogStream")!)
		.AddSingleton(sp => new FluentdClient(new FluentdOptions
		{
			Tag = "Unknown"
		}))
		.AddHostedService<FluentdLogBackground>();

	var excludeExtensions = new[] { "html", "ico", "png", "jpg" };
	_ = builder.Services
		.AddHealthChecks()
		.Services
		.AddOpenTelemetry()
		.WithTracing(traceBuilder => traceBuilder
			.SetResourceBuilder(ResourceBuilder.CreateDefault()
				.AddEnvironmentVariableDetector())
			.AddSource("Tgs.*")
			.AddSource("Valhalla.*")
			.AddHttpClientInstrumentation()
			.AddOtlpExporter())
		.WithMetrics(meterBuiler => meterBuiler
			.AddRuntimeInstrumentation()
			.AddOtlpExporter());
}

var app = builder.Build();
{
	app.MapGrpcService<GrpcLogsService>();
	app.MapHealthChecks("/healthz");
}

app.MapGet("/", () => "Hello World!");

app.Run();
