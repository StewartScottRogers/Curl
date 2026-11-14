namespace Curl.Protocol.Abstractions;

/// <summary>
/// Implements one protocol family. Each protocol library contributes exactly one.
/// </summary>
/// <remarks>
/// Handlers are registered with dependency injection and resolved as a set, so
/// <c>Curl.Core</c> dispatches on scheme without referencing any protocol library.
/// That is what keeps the protocol libraries independent of one another.
/// </remarks>
public interface IProtocolHandler
{
    /// <summary>
    /// Gets the URL schemes this handler serves, lowercased, such as
    /// <c>http</c> and <c>https</c>.
    /// </summary>
    IReadOnlyCollection<string> SupportedSchemes { get; }

    /// <summary>
    /// Performs one transfer.
    /// </summary>
    /// <param name="context">The transfer to perform.</param>
    /// <returns>The outcome, carrying the exit code the process should report.</returns>
    ValueTask<TransferResult> ExecuteAsync(ITransferContext context);

    /// <summary>
    /// Tells the handler that a transfer it served the first request of has ended, its whole redirect chain
    /// (or its single request) with the final merged result (BL-5164). Called once per transfer, after the
    /// last hop, also when a hop was refused. Does nothing by default.
    /// </summary>
    /// <param name="first">The transfer as the follower was given it, before any redirect.</param>
    /// <param name="result">The final result, with the chain's merged report.</param>
    /// <returns>A task that completes when the handler has been told.</returns>
    ValueTask TransferChainFinishedAsync(ITransferContext first, TransferResult result) => ValueTask.CompletedTask;
}
