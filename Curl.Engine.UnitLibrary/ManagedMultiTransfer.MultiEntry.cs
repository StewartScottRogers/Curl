using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.Options.Http;
using Curl.Networking;

namespace Curl.Engine;

internal sealed partial class ManagedMultiTransfer
{
    /// <summary>
    /// One added transfer: its perform once started and when, whether its message was queued, its
    /// engine-numbered socket once one was given, and the socket the socket callback was told of with
    /// the value <see cref="Assign" /> gave it.
    /// </summary>
    /// <param name="transfer">The transfer.</param>
    private sealed class MultiEntry(IEasyTransfer transfer)
    {
        public IEasyTransfer Transfer { get; } = transfer;

        public CancellationTokenSource Cancellation { get; } = new();

        public Task<CurlCode>? Perform { get; set; }

        public long StartTimestamp { get; set; }

        public bool Done { get; set; }

        public bool SocketReadyUntilNextPerform { get; set; }

        public CurlSocket? Socket { get; set; }

        public object? SocketState { get; set; }

        public string Origin { get; set; } = string.Empty;

        public bool Multiplexed { get; set; }

        public CurlCode? ForcedResult { get; set; }

        public TransferSocketWait? SocketWait { get; set; }

        // The wait the transfer's dial records its socket on, gating or not (SocketWait or the no-callback one).
        public TransferSocketWait? SocketRecord { get; set; }

        public CurlPoll ReportedPoll { get; set; }

        public Task NextChange => Volatile.Read(ref change).Task;

        private TaskCompletionSource change = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void MarkChanged() =>
            Interlocked.Exchange(ref change, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();
    }
}
