namespace Curl.Extension.Abstractions.Scheme;

/// <summary>
/// Serves an extension's schemes with its handler in place of curl's, and every other scheme of curl's handler with
/// curl's (ADR-1048).
/// </summary>
/// <param name="curlHandler">Curl's handler.</param>
/// <param name="hookSchemes">The schemes the extension serves, in lower case.</param>
/// <param name="hookHandler">The extension's handler.</param>
internal sealed class SchemeServingProtocolHandler(IProtocolHandler curlHandler, IReadOnlyCollection<string> hookSchemes, IProtocolHandler hookHandler) : IProtocolHandler
{
    /// <inheritdoc />
    public IReadOnlyCollection<string> SupportedSchemes => curlHandler.SupportedSchemes;

    /// <inheritdoc />
    public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) =>
        hookSchemes.Contains(context.Url.Scheme.ToLowerInvariant())
            ? hookHandler.ExecuteAsync(context)
            : curlHandler.ExecuteAsync(context);
}
