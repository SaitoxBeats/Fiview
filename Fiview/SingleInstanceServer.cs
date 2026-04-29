using System.IO.Pipes;

namespace Fiview;

internal sealed class SingleInstanceServer : IDisposable
{
    public const string MutexName = "Fiview-SingleInstance-Mutex";
    public const string PipeName = "Fiview-SingleInstance-Pipe";

    private readonly Action<string> _onImageReceived;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly Task _listenTask;

    public SingleInstanceServer(Action<string> onImageReceived)
    {
        _onImageReceived = onImageReceived;
        _listenTask = Task.Run(ListenAsync);
    }

    public static bool SendImagePath(string imagePath)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(300);

            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(imagePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _shutdown.Cancel();

        try
        {
            _listenTask.Wait(500);
        }
        catch
        {
            // The app is closing; there is nothing useful to recover here.
        }

        _shutdown.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(_shutdown.Token);
                using var reader = new StreamReader(server);
                var path = await reader.ReadLineAsync();

                if (path is not null)
                {
                    _onImageReceived(path);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(100, _shutdown.Token).ContinueWith(_ => { });
            }
        }
    }
}
