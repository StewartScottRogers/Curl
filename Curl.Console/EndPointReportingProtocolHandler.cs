using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// Performs each transfer with <paramref name="handler" /> and puts on its report the end
/// points of the first connection it opened through the recording connectors, so every
/// scheme's handler reports them without copying them itself (ADR-0119).
/// </summary>
/// <param name="handler">The handler that performs the transfer.</param>
/// <param name="recorder">The recorder the handler's connectors record into.</param>
internal sealed class EndPointReportingProtocolHandler(IProtocolHandler handler, ConnectionEndPointRecorder recorder) : IProtocolHandler
{
    /// <summary>Gets the schemes <see cref="Handler" /> serves.</summary>
    public IReadOnlyCollection<string> SupportedSchemes => handler.SupportedSchemes;

    /// <summary>
    /// Gets the handler that performs the transfer: a <see cref="LazyProtocolHandler" />'s
    /// built handler, building it if it has not been yet.
    /// </summary>
    internal IProtocolHandler Handler => handler is LazyProtocolHandler lazy ? lazy.Handler : handler;

    /// <summary>Gets the handler as given, a <see cref="LazyProtocolHandler" /> left unbuilt.</summary>
    internal IProtocolHandler GivenHandler => handler;

    /// <summary>
    /// Clears the recorder, performs the transfer with <see cref="Handler" />, and puts the
    /// recorded end points on its report (<see cref="ConnectionEndPointRecorder.ReportOn" />).
    /// </summary>
    /// <param name="context">The transfer.</param>
    /// <returns>The handler's result, with the end points on its report.</returns>
    public async ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        recorder.Clear();
        TransferResult result = await handler.ExecuteAsync(context).ConfigureAwait(false);

        return recorder.ReportOn(result);
    }
}
