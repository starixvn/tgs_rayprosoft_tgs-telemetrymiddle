using System.Diagnostics;
using System.Text;
using MessagePack;
using MessagePack.Formatters;

namespace Tgs.TelemetryMiddle.Models;

public class ActivityFormatter
	: IMessagePackFormatter<ActivityTraceId>
	, IMessagePackFormatter<ActivitySpanId>
{
	public void Serialize(ref MessagePackWriter writer, ActivityTraceId value, MessagePackSerializerOptions options)
		=> writer.WriteString(Encoding.UTF8.GetBytes(value.ToString()));

	public ActivityTraceId Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new NotImplementedException();

	public void Serialize(ref MessagePackWriter writer, ActivitySpanId value, MessagePackSerializerOptions options)
		=> writer.WriteString(Encoding.UTF8.GetBytes(value.ToString()));

	ActivitySpanId IMessagePackFormatter<ActivitySpanId>.Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new NotImplementedException();
}
