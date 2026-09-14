using System.IO.Pipes;

namespace WinLLMUsage.Windows;

public sealed class SingleInstanceGuard : IDisposable
{
    public const string ActivateMessage = "ACTIVATE";

    private readonly Mutex _mutex;
    private CancellationTokenSource? _listener;

    public SingleInstanceGuard(string name)
    {
        _mutex = new Mutex(true, name, out var created);
        CreatedNew = created;
        PipeName = name.Replace('\\', '.') + ".pipe";
    }

    public bool CreatedNew { get; }
    public string PipeName { get; }
    public event Action? Activated;

    public void StartListening()
    {
        if (!CreatedNew)
        {
            return;
        }

        _listener = new CancellationTokenSource();
        _ = Task.Run(() => ListenAsync(_listener.Token));
    }

    public void SignalExisting()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(500);
            using var writer = new StreamWriter(client);
            writer.WriteLine(ActivateMessage);
        }
        catch (Exception)
        {
        }
    }

    public void Dispose()
    {
        _listener?.Cancel();
        _mutex.Dispose();
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(PipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);
                using var reader = new StreamReader(server);
                var line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (line == ActivateMessage)
                {
                    Activated?.Invoke();
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception)
            {
                await Task.Delay(200, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
