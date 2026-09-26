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
/// prefix is added by the console layer, not here.
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
    /// <returns>
    /// The handler's result unchanged, or exit 1
    /// (<see cref="CurlExitCode.UnsupportedProtocol" />) with
    /// <c>Protocol "&lt;scheme&gt;" not supported</c> when no handler serves the scheme.
    /// </returns>
    public ValueTask<TransferResult> DispatchAsync(ITransferContext context)
    {
        string scheme = context.Url.Scheme.ToLowerInvariant();
        return handlersByScheme.TryGetValue(scheme, out IProtocolHandler? handler)
            ? handler.ExecuteAsync(context)
            : ValueTask.FromResult(TransferResult.Failure(
                CurlExitCode.UnsupportedProtocol,
                $"Protocol \"{scheme}\" not supported"));
    }
}
