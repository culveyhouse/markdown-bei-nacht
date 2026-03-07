namespace MarkdownBeiNacht.Core.Services;

public sealed class AsyncDebouncer(TimeSpan delay) : IDisposable
{
    private readonly object _syncRoot = new();
    private CancellationTokenSource? _cancellationTokenSource;

    public void Schedule(Func<CancellationToken, Task> action)
    {
        CancellationTokenSource localCts;
        lock (_syncRoot)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();
            localCts = _cancellationTokenSource;
        }

        _ = RunAsync(localCts, action);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }
    }

    private async Task RunAsync(CancellationTokenSource localCts, Func<CancellationToken, Task> action)
    {
        try
        {
            await Task.Delay(delay, localCts.Token);
            await action(localCts.Token);
        }
        catch (OperationCanceledException) when (localCts.IsCancellationRequested)
        {
        }
        finally
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_cancellationTokenSource, localCts))
                {
                    localCts.Dispose();
                    _cancellationTokenSource = null;
                }
            }
        }
    }
}

