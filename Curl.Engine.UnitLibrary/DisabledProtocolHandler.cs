using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Refuses every transfer for <paramref name="schemes" />, schemes curl knows by name but its reference
/// builds leave out, with exit 1 (<see cref="CurlExitCode.UnsupportedProtocol" />) and
/// <c>Protocol "&lt;scheme&gt;" is disabled</c>, before any connection, as curl 8.22.0 refuses
/// <c>smb://</c> and <c>smbs://</c> (measured, BL-3887 Notes; ADR-0829).
/// </summary>
/// <param name="schemes">The disabled schemes, lowercase.</param>
internal sealed class DisabledProtocolHandler(IReadOnlyCollection<string> schemes) : IProtocolHandler
{
    /// <summary>Gets the disabled schemes.</summary>
    public IReadOnlyCollection<string> SupportedSchemes => schemes;

    /// <summary>
    /// Fails the transfer with exit 1 and <c>Protocol "&lt;scheme&gt;" is disabled</c>, also reported to the
    /// transfer's events as the info line curl's <c>-v</c> writes.
    /// </summary>
    /// <param name="context">The refused transfer.</param>
    /// <returns>The exit 1 failure.</returns>
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context)
    {
        string message = $"Protocol \"{context.Url.Scheme.ToLowerInvariant()}\" is disabled";
        context.Events.ReportInfo(message);
        return ValueTask.FromResult(TransferResult.Failure(CurlExitCode.UnsupportedProtocol, message));
    }
}
