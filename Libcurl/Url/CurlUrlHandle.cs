using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;
using Curl.Protocol.Abstractions;
using Libcurl.Misc.ErrorText;

namespace Libcurl.Url;

/// <summary>
/// A URL handle (<c>CURLU</c>, ADR-0506): <c>curl_url</c>'s result, created by
/// <see cref="Global.CurlLibrary.UrlInit"/> over the engine's <see cref="ICurlUrlHandle"/>.
/// <see cref="Get"/> is <c>curl_url_get</c>, <see cref="Set"/> is <c>curl_url_set</c>,
/// <see cref="Duplicate"/> is <c>curl_url_dup</c>, <see cref="ErrorText"/> is <c>curl_url_strerror</c>,
/// and <see cref="Dispose"/> is <c>curl_url_cleanup</c> (ADR-0587).
/// </summary>
public sealed class CurlUrlHandle : IDisposable
{
    /// <summary>The engine's URL handle every call goes through.</summary>
    private readonly ICurlUrlHandle handle;
    /// <summary>1 once <see cref="Dispose"/> has run, set atomically so the engine handle is disposed once; 0 until then.</summary>
    private int cleanedUp;

    /// <summary>Wraps the engine's <paramref name="handle"/>.</summary>
    /// <param name="handle">The engine's URL handle; this handle disposes it on cleanup.</param>
    internal CurlUrlHandle(ICurlUrlHandle handle) => this.handle = handle;

    /// <summary>Whether <see cref="Dispose"/> (<c>curl_url_cleanup</c>) has run.</summary>
    public bool IsCleanedUp => Volatile.Read(ref cleanedUp) != 0;

    /// <summary>The engine's URL handle, which <c>CURLOPT_CURLU</c> hands to an easy handle's options.</summary>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    internal ICurlUrlHandle EngineHandle => LiveHandle();

    /// <summary>
    /// <c>curl_url_strerror</c>: upstream's text for <paramref name="code"/>, or
    /// <c>"CURLUcode unknown"</c> for a code upstream does not define.
    /// </summary>
    /// <param name="code">The URL API result code to describe.</param>
    /// <returns>Upstream's text for the code.</returns>
    public static string ErrorText(CurlUrlCode code) => CurlErrorText.Url(code);

    /// <summary><c>curl_url_get</c>: one part of the URL, as the engine's handle answers it.</summary>
    /// <param name="part"><c>what</c>: the part to get.</param>
    /// <param name="flags"><c>flags</c>: how to return it.</param>
    /// <param name="value"><c>part</c>: the part's text, or <see langword="null"/> when the call fails.</param>
    /// <returns>The call's result; a <c>CURLUE_NO_*</c> code such as <see cref="CurlUrlCode.NoQuery"/> when the part is not set.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlUrlCode Get(CurlUrlPart part, CurlUrlFlags flags, out string? value) =>
        LiveHandle().Get(part, flags, out value);

    /// <summary>
    /// <c>curl_url_get</c> as the C ABI returns it: one part of the URL as the bytes of its
    /// <c>char *</c>, so a byte <c>CURLU_URLDECODE</c> decodes that is not UTF-8 (<c>%FF</c>) is that
    /// byte, as libcurl 8.22.0 gives it, not U+FFFD (ADR-0877).
    /// </summary>
    /// <param name="part"><c>what</c>: the part to get.</param>
    /// <param name="flags"><c>flags</c>: how to return it.</param>
    /// <param name="value"><c>part</c>: the part's bytes, or <see langword="null"/> when the call fails.</param>
    /// <returns>The call's result; a <c>CURLUE_NO_*</c> code such as <see cref="CurlUrlCode.NoQuery"/> when the part is not set.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlUrlCode GetBytes(CurlUrlPart part, CurlUrlFlags flags, out byte[]? value)
    {
        CurlUrlCode code = Get(part, flags, out string? text);
        value = text is null ? null : UrlByteText.ToBytes(text);

        return code;
    }

    /// <summary><c>curl_url_set</c>: sets or clears one part of the URL, as the engine's handle does.</summary>
    /// <param name="part"><c>what</c>: the part to set; <see cref="CurlUrlPart.Url"/> parses a whole URL, relative ones against the current one.</param>
    /// <param name="value"><c>part</c>: the part's new text, or <see langword="null"/> to clear it.</param>
    /// <param name="flags"><c>flags</c>: how to parse it.</param>
    /// <returns>The call's result; the handle is unchanged when it fails.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlUrlCode Set(CurlUrlPart part, string? value, CurlUrlFlags flags) =>
        LiveHandle().Set(part, value, flags);

    /// <summary><c>curl_url_dup</c>: a new handle over the engine's copy of this one's parts.</summary>
    /// <returns>The copy, independent of this handle; the caller disposes it.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    public CurlUrlHandle Duplicate() => new(LiveHandle().Duplicate());

    /// <summary><c>curl_url_cleanup</c>: disposes the engine's handle once; a second call does nothing.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref cleanedUp, 1) == 0)
        {
            handle.Dispose();
        }
    }

    /// <summary>The engine handle for a call to use.</summary>
    /// <exception cref="ObjectDisposedException">The handle has been cleaned up.</exception>
    private ICurlUrlHandle LiveHandle()
    {
        ObjectDisposedException.ThrowIf(IsCleanedUp, this);
        return handle;
    }
}
