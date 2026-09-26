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
}
