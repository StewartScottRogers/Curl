using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// A handler for one scheme that returns its scripted results, one per transfer or
/// <c>--retry</c> attempt, in order, and the last again once they run out; it sends and
/// writes nothing.
/// </summary>
/// <param name="scheme">The scheme it serves.</param>
/// <param name="results">The results, in the order the attempts get them.</param>
internal sealed class ScriptedResultHandler(string scheme, params TransferResult[] results) : IProtocolHandler
{
    private int attempts;

    public IReadOnlyCollection<string> SupportedSchemes { get; } = [scheme];

    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) =>
        ValueTask.FromResult(results[Math.Min(attempts++, results.Length - 1)]);
}
