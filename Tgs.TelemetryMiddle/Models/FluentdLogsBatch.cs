namespace Tgs.TelemetryMiddle.Models;

public record FluentdLogsBatch(
	string ServiceName,
	FluentdLog[] Logs);
