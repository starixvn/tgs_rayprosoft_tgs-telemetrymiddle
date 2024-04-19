using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using Valhalla.Messages;

namespace Tgs.TelemetryMiddle.Models;

public class FluentdLogBackground(FluentdClient fluentdClient, IEventBus eventBus) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await fluentdClient.ConnectAsync(
			stoppingToken).ConfigureAwait(false);

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
