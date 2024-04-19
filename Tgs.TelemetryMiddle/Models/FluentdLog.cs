using System.Diagnostics;
using MessagePack;

namespace Tgs.TelemetryMiddle.Models;

[MessagePackObject]
public record FluentdLog(
	[property: Key("Timestamp"), MessagePackFormatter(typeof(StringDateTimeFormatter))]
	DateTime ObservedTimestamp,
	[property: Key("CategoryName")] string CategoryName,
	[property: Key("LogLevel")] string LogLevel,
	[property: Key("ServiceName")] string? ServiceName,
	[property: Key("EventId")] EventId? EventId,
	[property: Key("Body")] string? Body,
	[property: Key("Attributes")] IReadOnlyList<KeyValuePair<string, object?>> Attributes,
	[property: Key("TraceId")] ActivityTraceId? TraceId,
	[property: Key("SpanId")] ActivitySpanId? SpanId);
