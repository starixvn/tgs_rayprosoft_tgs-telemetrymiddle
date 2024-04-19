using System.Text;
using MessagePack;
using MessagePack.Formatters;

namespace Tgs.TelemetryMiddle.Models;

public class StringDateTimeFormatter : IMessagePackFormatter<DateTime>
{
	public void Serialize(ref MessagePackWriter writer, DateTime value, MessagePackSerializerOptions options)
		=> writer.WriteString(Encoding.UTF8.GetBytes(value.ToString("O")));

	public DateTime Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options) => throw new NotImplementedException();
}
