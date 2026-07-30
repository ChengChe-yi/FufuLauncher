/*
Copyright (c) FufuLauncher Dev Team. All rights reserved.
Licensed under the MIT License.
ky3-backpack
*/
using System.IO.Pipes;
using System.Text;

namespace FufuLauncher.Services.Backpack;

sealed class PipeListenerService
{
    private const string PipeName = "ky3-backpack";

    public event EventHandler<(string Event, string Json)>? PacketReceived;

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(
                PipeName,
                PipeDirection.In,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            try
            {
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                server.Dispose();
                break;
            }
            catch
            {
                server.Dispose();
                continue;
            }
            _ = ProcessAsync(server, cancellationToken);
        }
    }

    private async Task ProcessAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        using (server)
        {
            try
            {
                byte[] hdr = new byte[16];
                await ReadExactAsync(server, hdr, cancellationToken).ConfigureAwait(false);

                uint bodyLen = BitConverter.ToUInt32(hdr, 0);
                if (bodyLen is 0 or > 64 * 1024 * 1024) return;

                string eventName = Encoding.ASCII.GetString(hdr, 4, 12).TrimEnd('\0');

                byte[] body = new byte[bodyLen];
                await ReadExactAsync(server, body, cancellationToken).ConfigureAwait(false);

                PacketReceived?.Invoke(this, (eventName, Encoding.UTF8.GetString(body)));
            }
            catch { }
        }
    }

    private static async Task ReadExactAsync(PipeStream stream, byte[] buffer, CancellationToken ct)
    {
        int offset = 0;
        while (offset < buffer.Length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(offset), ct).ConfigureAwait(false);
            if (read == 0) throw new EndOfStreamException();
            offset += read;
        }
    }
}
