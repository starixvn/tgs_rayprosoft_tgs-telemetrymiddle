using System.Text;
using MessagePack;
using MessagePack.Formatters;

namespace Tgs.TelemetryMiddle.Models;

public class KeyValuePairFormatter : IMessagePackFormatter<KeyValuePair<string, object?>>
{
	public void Serialize(ref MessagePackWriter writer, KeyValuePair<string, object?> value, MessagePackSerializerOptions options)
	{
		if (value.Value is null)
			return;

		writer.WriteMapHeader(1);
		var propertyName = Encoding.UTF8.GetBytes(value.Key);
		writer.WriteStringHeader(propertyName.Length);
		writer.WriteRaw(propertyName);
		MessagePackSerializer.Serialize(value.Value.GetType(), ref writer, value.Value, options);
	}

	public KeyValuePair<string, object?> Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new NotImplementedException();
}
