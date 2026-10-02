using Curl.Protocol.Abstractions;

namespace Curl.Core;

/// <summary>
/// Hands a transfer to the <see cref="IProtocolHandler" /> whose
/// <see cref="IProtocolHandler.SupportedSchemes" /> contains the URL's scheme, matched
/// without regard to case.
/// </summary>
/// <remarks>
/// When no handler serves the scheme, the transfer fails as curl 8.21.0 fails it: exit 1
/// (<see cref="CurlExitCode.UnsupportedProtocol" />) with the message
/// <c>Protocol "xyz" not supported</c>, the scheme lowercased. The <c>curl: (1) </c>
/// prefix is added by the console layer, not here. A scheme a handler serves but
/// <c>--proto</c> excludes fails with exit 1 and <c>Protocol "http" is disabled</c>.
/// </remarks>
public sealed class ProtocolDispatcher
{
    private readonly Dictionary<string, IProtocolHandler> handlersByScheme =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="ProtocolDispatcher" /> class.
    /// </summary>
    /// <param name="handlers">Every registered handler; may be empty.</param>
    /// <exception cref="ArgumentException">
    /// Two handlers claim the same scheme. That is a composition bug, not a curl
    /// behaviour, and the message names the scheme.
    /// </exception>
    public ProtocolDispatcher(IEnumerable<IProtocolHandler> handlers)
    {
        foreach (IProtocolHandler handler in handlers)
        {
            foreach (string scheme in handler.SupportedSchemes)
            {
                if (!handlersByScheme.TryAdd(scheme, handler))
                {
                    throw new ArgumentException(
                        $"More than one protocol handler claims the scheme \"{scheme}\".",
                        nameof(handlers));
                }
            }
        }
    }

    /// <summary>
    /// Performs the transfer with the handler registered for its URL's scheme.
    /// </summary>
    /// <param name="context">The transfer to perform, passed to the handler unchanged.</param>
    /// <param name="allowedSchemes">
    /// The lowercase schemes <c>--proto</c> allows, or <see langword="null" /> to allow every scheme.
    /// </param>
    /// <returns>
    /// The handler's result unchanged; exit 1
    /// (<see cref="CurlExitCode.UnsupportedProtocol" />) with
    /// <c>Protocol "&lt;scheme&gt;" not supported</c> when no handler serves the scheme; or exit 1 with
    /// <c>Protocol "&lt;scheme&gt;" is disabled</c>, before the handler is called, when one does but
    /// <paramref name="allowedSchemes" /> does not allow it, as curl 8.21.0 refuses it (measured, BL-523 Notes).
    /// </returns>
    public ValueTask<TransferResult> DispatchAsync(ITransferContext context, IReadOnlySet<string>? allowedSchemes = null)
    {
        string scheme = context.Url.Scheme.ToLowerInvariant();
        if (!handlersByScheme.TryGetValue(scheme, out IProtocolHandler? handler))
        {
            return Refused(context, $"Protocol \"{scheme}\" not supported");
        }

        return allowedSchemes is null || allowedSchemes.Contains(scheme)
            ? handler.ExecuteAsync(context)
            : Refused(context, $"Protocol \"{scheme}\" is disabled");
    }

    /// <summary>
    /// Fails the transfer with exit 1 and <paramref name="message" />, which is also reported to
    /// the transfer's events as the info line curl 8.21.0's <c>-v</c> writes before its
    /// <c>curl: (1)</c> line (measured, BL-805 Notes).
    /// </summary>
    /// <param name="context">The refused transfer.</param>
    /// <param name="message">The refusal's message.</param>
    /// <returns>The exit 1 failure carrying <paramref name="message" />.</returns>
    private static ValueTask<TransferResult> Refused(ITransferContext context, string message)
    {
        context.Events.ReportInfo(message);
        return ValueTask.FromResult(TransferResult.Failure(CurlExitCode.UnsupportedProtocol, message));
    }
}
