using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// One transport per connection: cancellation never lets an old room feed a new session.
public sealed class NetworkRelayClient : IDisposable
{
    private readonly ClientWebSocket socket = new ClientWebSocket();
    private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
    private readonly ConcurrentQueue<string> outgoing = new ConcurrentQueue<string>();
    private readonly Action<string> receive;
    private readonly Action<string> failed;
    private int stopped;

    public NetworkRelayClient(Uri endpoint, string request, Action<string> receive, Action<string> failed)
    {
        this.receive = receive;
        this.failed = failed;
        socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
        Task.Run(() => Run(endpoint, request));
    }

    public void Send(string message)
    {
        if (Volatile.Read(ref stopped) == 0)
            outgoing.Enqueue(message);
    }

    private async Task Run(Uri endpoint, string request)
    {
        Task reader = null;
        Task sender = null;
        try
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                await socket.ConnectAsync(endpoint, timeout.Token).ConfigureAwait(false);
            }
            await SendText(request).ConfigureAwait(false);
            reader = ReadMessages();
            sender = SendMessages();
            Task completed = await Task.WhenAny(reader, sender).ConfigureAwait(false);
            await completed.ConfigureAwait(false);
            throw new IOException("中转服务连接已断开，请重新加入房间。");
        }
        catch (Exception ex)
        {
            if (Volatile.Read(ref stopped) == 0)
                failed(ex is OperationCanceledException
                    ? "连接中转服务超时，请检查互联网连接，或联系房主确认测试服务仍在运行。"
                    : "无法连接中转服务：" + ex.Message);
        }
        finally
        {
            Dispose();
            if (reader != null && sender != null)
            {
                try { await Task.WhenAll(reader, sender).ConfigureAwait(false); } catch { }
            }
            lifetime.Dispose();
        }
    }

    private async Task SendMessages()
    {
        while (!lifetime.IsCancellationRequested)
        {
            string message;
            while (outgoing.TryDequeue(out message))
                await SendText(message).ConfigureAwait(false);
            await Task.Delay(10, lifetime.Token).ConfigureAwait(false);
        }
    }

    private Task SendText(string message)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        return socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, lifetime.Token);
    }

    private async Task ReadMessages()
    {
        byte[] buffer = new byte[4096];
        while (!lifetime.IsCancellationRequested)
        {
            using (var message = new MemoryStream())
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                // The server sends a heartbeat at least every 15 seconds, even in an empty room.
                timeout.CancelAfter(TimeSpan.FromSeconds(50));
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), timeout.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                        return;
                    if (result.MessageType != WebSocketMessageType.Text || message.Length + result.Count > 16384)
                        throw new InvalidDataException("中转服务消息格式错误。");
                    message.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);
                if (Volatile.Read(ref stopped) == 0)
                    receive(Encoding.UTF8.GetString(message.ToArray()));
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref stopped, 1) != 0)
            return;
        lifetime.Cancel();
        socket.Abort();
        socket.Dispose();
        // Run's async loops still own the token source until their cancelled awaits complete.
    }
}
