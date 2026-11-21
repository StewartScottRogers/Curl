using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.AzureStorage.Client;

/// <summary>
/// Refuses the request methods an <c>az://</c>, <c>abfs://</c> or <c>abfss://</c> transfer cannot send (ADR-1249, ADR-1323):
/// <c>-I</c>/<c>--head</c>, and a <c>-X</c> other than <c>GET</c> without <c>-T</c> (or <c>DELETE</c> on <c>az://</c>) or
/// <c>PUT</c> with it, so a <c>-X DELETE</c> on <c>abfs://</c> never downloads instead.
/// </summary>
internal static class AzureStorageRequestMethod
{
    /// <summary>
    /// Gives why <paramref name="context"/> asks for a method the transfer cannot send, or <see langword="null"/> when
    /// it asks for none or for one it sends.
    /// </summary>
    /// <param name="context">The transfer, whose <c>-I</c>, <c>-X</c> and <c>-T</c> are read.</param>
    /// <returns>The refusal's message, or <see langword="null"/>.</returns>
    internal static string? RefusalOf(ITransferContext context)
    {
        bool deletes = context.Url.Scheme == "az";
        string only = deletes ? "GET, DELETE with -X DELETE, or PUT with -T" : "GET, or PUT with -T";
        if (context.NoBody)
        {
            return $"{context.Url.Scheme}:// does not support -I/--head; it sends only {only}";
        }

        return context.Http?.CustomMethod is { } method && !Sends(context.Upload is not null, deletes).Contains(method)
            ? $"{context.Url.Scheme}:// does not support -X {method}; it sends only {only}"
            : null;
    }

    private static string[] Sends(bool uploads, bool deletes) => uploads ? ["PUT"] : deletes ? ["GET", "DELETE"] : ["GET"];
}
