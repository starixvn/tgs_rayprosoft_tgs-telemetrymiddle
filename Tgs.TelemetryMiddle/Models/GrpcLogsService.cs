using System.Diagnostics;
using System.Text.Json;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Grpc.Core;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using Valhalla.Messages;

namespace Tgs.TelemetryMiddle.Models;

public class GrpcLogsService(
	IEventPublisher<FluentdLogsBatch> eventPublisher,
	ILogger<GrpcLogsService> logger)
	: LogsService.LogsServiceBase
{
	public override async Task<ExportLogsServiceResponse> Export(ExportLogsServiceRequest request, ServerCallContext context)
	{
		logger.LogInformation("{Request}", JsonFormatter.Default.Format(request));

		foreach (var resourceLog in request.ResourceLogs)
		{
			var resourceAttributes = new List<KeyValuePair<string, object?>>();
			var serviceName = default(string);

			foreach (var attribute in resourceLog.Resource.Attributes)
			{
				if (attribute.Key == "service.name")
					serviceName = attribute.Value.StringValue;

				resourceAttributes.Add(new KeyValuePair<string, object?>(attribute.Key, ReadAnyValue(attribute.Value)));
			}

			foreach (var scopeLog in resourceLog.ScopeLogs)
			{
				var scopeAttributes = scopeLog.Scope.Attributes
					.Select(attr => new KeyValuePair<string, object?>(attr.Key, ReadAnyValue(attr.Value)))
					.ToArray();

				var logs = TransformLog(
					serviceName,
					scopeLog.Scope.Name,
					resourceAttributes.Concat(scopeAttributes),
					scopeLog.LogRecords).ToArray();

				await eventPublisher.PublishAsync(
					new FluentdLogsBatch(serviceName ?? "Unknown", logs),
					context.CancellationToken).ConfigureAwait(false);
			}
		}

		return new ExportLogsServiceResponse();
	}

	private static IEnumerable<FluentdLog> TransformLog(
		string? serviceName,
		string scopeName,
		IEnumerable<KeyValuePair<string, object?>> baseAttributes,
		RepeatedField<OpenTelemetry.Proto.Logs.V1.LogRecord> logRecords)
	{
		foreach (var log in logRecords)
		{
			var eventId = default(int?);
			var eventName = default(string);
			var attributes = new List<KeyValuePair<string, object?>>(baseAttributes);

			foreach (var attr in log.Attributes)
			{
				if (attr.Key == "logrecord.event.id" && attr.Value.ValueCase == AnyValue.ValueOneofCase.IntValue)
				{
					eventId = (int)attr.Value.IntValue;
				}

				if (attr.Key == "logrecord.event.name" && attr.Value.ValueCase == AnyValue.ValueOneofCase.StringValue)
				{
					eventName = attr.Value.StringValue;
				}

				attributes.Add(new KeyValuePair<string, object?>(attr.Key, ReadAnyValue(attr.Value)));
			}

			var traceIdBytes = log.TraceId.ToByteArray();
			var spanIdBytes = log.SpanId.ToByteArray();

			var logRecord = new FluentdLog(
				DateTime.UnixEpoch.AddTicks((long)(log.TimeUnixNano / 100)),
				scopeName,
				log.SeverityText,
				serviceName,
				eventId is null ? null : new EventId(eventId.Value, eventName),
				log.Body.ValueCase == AnyValue.ValueOneofCase.StringValue ? log.Body.StringValue : null,
				attributes,
				traceIdBytes.Length == 16
					? ActivityTraceId.CreateFromBytes(log.TraceId.ToByteArray())
					: null,
				spanIdBytes.Length == 8
					? ActivitySpanId.CreateFromBytes(log.SpanId.ToByteArray())
					: null);

			yield return logRecord;
		}
	}

	private static object? ReadAnyValue(AnyValue value)
		=> value.ValueCase switch
		{
			AnyValue.ValueOneofCase.None => null,
			AnyValue.ValueOneofCase.StringValue => value.StringValue,
			AnyValue.ValueOneofCase.BoolValue => value.BoolValue,
			AnyValue.ValueOneofCase.IntValue => value.IntValue,
			AnyValue.ValueOneofCase.DoubleValue => value.DoubleValue,
			AnyValue.ValueOneofCase.ArrayValue => JsonSerializer.Serialize(value.ArrayValue.Values.Select(ReadAnyValue)),
			AnyValue.ValueOneofCase.KvlistValue => JsonSerializer.Serialize(value.KvlistValue.Values
				.Select(kv => new KeyValuePair<string, object?>(kv.Key, ReadAnyValue(kv.Value)))),
			AnyValue.ValueOneofCase.BytesValue => value.BytesValue.ToString(),
			_ => null
		};
}
