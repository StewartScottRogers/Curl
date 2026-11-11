using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.AzureStorage.Client;

/// <summary>
/// Refuses the request methods an <c>az://</c>, <c>abfs://</c> or <c>abfss://</c> transfer cannot send (ADR-1249): <c>-I</c>/<c>--head</c>, and a
/// <c>-X</c> other than <c>GET</c> without <c>-T</c> or <c>PUT</c> with it, so a <c>-X DELETE</c> never downloads instead.
/// </summary>
internal static class AzureStorageRequestMethod
{
    /// <summary>
    /// Gives why <paramref name="context"/> asks for a method the transfer cannot send, or <see langword="null"/> when
    /// it asks for none or for the one it sends.
    /// </summary>
    /// <param name="context">The transfer, whose <c>-I</c>, <c>-X</c> and <c>-T</c> are read.</param>
    /// <returns>The refusal's message, or <see langword="null"/>.</returns>
    internal static string? RefusalOf(ITransferContext context)
    {
        if (context.NoBody)
        {
            return $"{context.Url.Scheme}:// does not support -I/--head; it sends only GET, or PUT with -T";
        }

        string sent = context.Upload is null ? "GET" : "PUT";
        return context.Http?.CustomMethod is { } method && method != sent
            ? $"{context.Url.Scheme}:// does not support -X {method}; it sends only GET, or PUT with -T"
            : null;
    }
}
