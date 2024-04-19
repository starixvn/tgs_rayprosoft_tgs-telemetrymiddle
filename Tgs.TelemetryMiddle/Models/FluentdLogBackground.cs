using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Polly;
using Polly.Retry;
using Valhalla.Messages;

namespace Tgs.TelemetryMiddle.Models;

public class FluentdLogBackground(
	FluentdClient fluentdClient,
	IEventBus eventBus,
	ILogger<FluentdLogBackground> logger)
	: BackgroundService
{
	private static readonly ResiliencePropertyKey<ILogger<FluentdLogBackground>> _LoggerKey = new("logger");

	private static readonly ResiliencePipeline _Pipeline = new ResiliencePipelineBuilder()
		.AddRetry(new RetryStrategyOptions
		{
			ShouldHandle = new PredicateBuilder().Handle<Exception>(),
			Delay = TimeSpan.FromSeconds(1),
			UseJitter = true,
			MaxDelay = TimeSpan.FromSeconds(5),
			MaxRetryAttempts = int.MaxValue,
			OnRetry = arg =>
			{
				_ = arg.Context.Properties.TryGetValue(_LoggerKey, out var sysLog);

				sysLog?.LogWarning("Connect retry {Count}, RetryDelay: {Delay}", arg.AttemptNumber, arg.RetryDelay);

				return ValueTask.CompletedTask;
			}
		})
		.Build();

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var context = ResilienceContextPool.Shared.Get(stoppingToken);
		context.Properties.Set(_LoggerKey, logger);

		await _Pipeline.ExecuteAsync(
			async ctx => await fluentdClient.ConnectAsync(
				ctx.CancellationToken).ConfigureAwait(false),
			context).ConfigureAwait(false);

		var observable = eventBus.GetEventObserable<FluentdLogsBatch>("LogStream");

		await observable!.SelectMany(
			batch => Observable.FromAsync(async () => await fluentdClient.SendAsync(
				batch.ServiceName,
				new OpenTelemetry.Batch<FluentdLog>(batch.Logs, batch.Logs.Length),
				stoppingToken).ConfigureAwait(false)))
			.ToTask(stoppingToken)
			.ConfigureAwait(false);
	}
}
