using System;
using System.Threading;
using System.Threading.Tasks;

namespace billpg.SpartanHttpClient.Internal;

/// <summary>
/// netstandard2.0 predates Task.WaitAsync, and none of the async socket/TLS/DNS calls
/// this library drives accept a CancellationToken directly on that target, so every
/// await against the network is raced against the token here instead.
/// </summary>
internal static class TaskCancellationExtensions
{
    internal static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
            return await task.ConfigureAwait(false);

        var cancelSignal = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), cancelSignal))
        {
            var completed = await Task.WhenAny(task, cancelSignal.Task).ConfigureAwait(false);
            if (completed == cancelSignal.Task)
                throw new OperationCanceledException(cancellationToken);
            return await task.ConfigureAwait(false);
        }
    }

    internal static async Task WithCancellation(this Task task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled)
        {
            await task.ConfigureAwait(false);
            return;
        }

        var cancelSignal = new TaskCompletionSource<bool>();
        using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s!).TrySetResult(true), cancelSignal))
        {
            var completed = await Task.WhenAny(task, cancelSignal.Task).ConfigureAwait(false);
            if (completed == cancelSignal.Task)
                throw new OperationCanceledException(cancellationToken);
            await task.ConfigureAwait(false);
        }
    }
}
