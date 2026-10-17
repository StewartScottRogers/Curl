using System.Collections;
using Curl.Api.EngineContracts;

namespace Curl.Engine;

/// <summary>
/// <c>CURLINFO_COOKIELIST</c> of a handle attached to a share handle that shares cookies: the share's cookies as
/// Netscape cookie-file lines (<see cref="ManagedShareHandle.ListCookies" />), read under one cookie lock pair the
/// first time the list is read, as curl 8.22.0's <c>curl_easy_getinfo</c> reads them, and kept from then on.
/// </summary>
/// <param name="share">The share handle whose cookies are listed.</param>
/// <param name="transfer">The transfer the lock callbacks are told of.</param>
internal sealed class ShareCookieListLines(ManagedShareHandle share, IEasyTransfer transfer) : IReadOnlyList<string>
{
    private IReadOnlyList<string>? lines;

    /// <inheritdoc />
    public int Count => Lines.Count;

    /// <inheritdoc />
    public string this[int index] => Lines[index];

    /// <summary>Gets the lines, read from the share handle on first use.</summary>
    private IReadOnlyList<string> Lines => lines ??= share.ListCookies(transfer);

    /// <inheritdoc />
    public IEnumerator<string> GetEnumerator() => Lines.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
