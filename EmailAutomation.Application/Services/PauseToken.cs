using System.Threading;
using System.Threading.Tasks;

namespace EmailAutomation.Application.Services;

/// <summary>
/// A cooperative pause mechanism analogous to CancellationTokenSource/CancellationToken, but for
/// pausing instead of cancelling. Backed by a TaskCompletionSource "gate" rather than a blocking
/// wait handle, so a paused batch run doesn't tie up a thread while waiting to be resumed.
/// </summary>
public sealed class PauseTokenSource
{
    private volatile TaskCompletionSource<bool>? _pauseCompletionSource;

    public PauseToken Token => new(this);

    public bool IsPaused => _pauseCompletionSource != null;

    public void Pause()
    {
        // CompareExchange makes repeated Pause() calls idempotent - only the first installs a gate.
        var newGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.CompareExchange(ref _pauseCompletionSource, newGate, null);
    }

    public void Resume()
    {
        var gate = Interlocked.Exchange(ref _pauseCompletionSource, null);
        gate?.TrySetResult(true);
    }

    internal Task WaitWhilePausedAsync(CancellationToken cancellationToken)
    {
        var gate = _pauseCompletionSource;
        return gate is null ? Task.CompletedTask : gate.Task.WaitAsync(cancellationToken);
    }
}

public readonly struct PauseToken
{
    private readonly PauseTokenSource? _source;

    internal PauseToken(PauseTokenSource source)
    {
        _source = source;
    }

    /// <summary>Equivalent of CancellationToken.None - never pauses.</summary>
    public static PauseToken None => default;

    public bool IsPaused => _source?.IsPaused ?? false;

    public Task WaitWhilePausedAsync(CancellationToken cancellationToken = default)
        => _source?.WaitWhilePausedAsync(cancellationToken) ?? Task.CompletedTask;
}
