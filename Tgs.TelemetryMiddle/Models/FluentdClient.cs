using System.Net.Sockets;
using FluentdForward.OpenTelemetry.Exporter.Logs;
using FluentdForward.OpenTelemetry.MessagePack;
using MessagePack;
using MessagePack.Resolvers;
using OpenTelemetry;

namespace Tgs.TelemetryMiddle.Models;

public class FluentdClient : IDisposable
{
	private static readonly MessagePackSerializerOptions _PackSerializerOptions = MessagePackSerializerOptions.Standard
		.WithResolver(CompositeResolver.Create(
			CompositeResolver.Create(new ActivityFormatter(), new KeyValuePairFormatter()),
			StandardResolver.Instance));

	private readonly TcpClient _tcp;
	private bool _disposed;
	private NetworkStream _stream = default!;

	/// <summary>
	/// Create a new <see cref="FluentdClient"/> instance.
	/// </summary>
	/// <param name="setting">The setting for connecting to fluentd server.</param>
	public FluentdClient()
	{
		_tcp = new TcpClient
		{
			NoDelay = true
		};
	}

	/// <summary>
	/// Destructor for not calling <see cref="Dispose()"/> method.
	/// </summary>
	~FluentdClient()
	{
		Dispose(false);
	}

	/// <inheritdoc cref="IFluentdClient.ConnectAsync" />
	public async Task ConnectAsync(CancellationToken cancellationToken)
	{
		await _tcp.ConnectAsync(
			"localhost",
			24224,
			cancellationToken).ConfigureAwait(false);

		_ = cancellationToken.Register(_tcp.Close);
	}

	public async Task SendAsync(string tag, Batch<FluentdLog> batch, CancellationToken cancellationToken)
	{
		var value = Serialize(tag, batch);

		await InternalSendAsync(value, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
	/// </summary>
	public void Dispose()
	{
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Free, release, or reset managed or unmanaged resources.
	/// </summary>
	/// <param name="disposing">Wether to free, release, or resetting unmanaged resources or not.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
			return;

		if (disposing)
		{
			_stream?.Dispose();
			_tcp?.Dispose();
		}

		_disposed = true;
	}

	private static byte[] Serialize(string tag, Batch<FluentdLog> batch)
	{
		var payload = new ArrayPayload<FluentdLog>
		{
			Tag = tag,
			Body = ReadAsPayloadBody(batch)
		};

		return MessagePackSerializer.Serialize(payload, _PackSerializerOptions);
	}

	private static IEnumerable<ArrayPayloadBody<FluentdLog>> ReadAsPayloadBody(Batch<FluentdLog> batch)
	{
		foreach (var record in batch)
			yield return new ArrayPayloadBody<FluentdLog>
			{
				Metadata = new ArrayPayloadMetadata
				{
					Timestamp = record.ObservedTimestamp
				},
				Message = record
			};
	}

	private async Task InternalSendAsync(byte[] message, CancellationToken cancellationToken)
	{
		if (!_tcp.Connected)
			await ConnectAsync(cancellationToken).ConfigureAwait(false);

		_stream = _tcp.GetStream();

		await _stream.WriteAsync(message, cancellationToken).ConfigureAwait(false);

		await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
	}
}
