using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options.Http;

namespace Libcurl.Mime;

/// <summary>
/// A MIME handle (<c>curl_mime</c>), created by <see cref="Global.CurlLibrary.MimeInit"/>
/// (<c>curl_mime_init</c>) over the engine's <see cref="IMimeBuilder"/> (ADR-0583): a multipart body
/// to set with <c>CURLOPT_MIMEPOST</c> or to nest in a part with <see cref="CurlMimePart.SetSubParts"/>.
/// <see cref="Dispose"/> is <c>curl_mime_free</c>; this handle and its parts used afterwards throw
/// <see cref="ObjectDisposedException"/>.
/// </summary>
public sealed class CurlMime : IDisposable
{
    private readonly IMimeBuilder builder;
    private bool freed;

    /// <summary>Wraps <paramref name="builder"/>.</summary>
    /// <param name="builder">The engine's MIME builder.</param>
    internal CurlMime(IMimeBuilder builder) => this.builder = builder;

    /// <summary>Whether <see cref="Dispose"/> (<c>curl_mime_free</c>) has run.</summary>
    public bool IsFreed => freed;

    /// <summary>The engine's builder, for <see cref="CurlMimePart.SetSubParts"/>.</summary>
    /// <exception cref="ObjectDisposedException">The handle has been freed.</exception>
    internal IMimeBuilder Builder
    {
        get
        {
            ThrowIfFreed();
            return builder;
        }
    }

    /// <summary><c>curl_mime_addpart</c>: appends a new, empty part.</summary>
    /// <returns>The new part.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been freed.</exception>
    public CurlMimePart AddPart() => new(this, Builder.AddPart());

    /// <summary><c>curl_mime_free</c>: frees the handle and its parts. A second call does nothing.</summary>
    public void Dispose() => freed = true;

    /// <summary>The body as the parts' setters have left it, which <c>CURLOPT_MIMEPOST</c> sends.</summary>
    /// <returns>The body.</returns>
    /// <exception cref="ObjectDisposedException">The handle has been freed.</exception>
    internal EasyMimeBody Build() => Builder.Build();

    /// <summary>Throws when the handle has been freed.</summary>
    /// <exception cref="ObjectDisposedException">The handle has been freed.</exception>
    internal void ThrowIfFreed() => ObjectDisposedException.ThrowIf(freed, this);
}
