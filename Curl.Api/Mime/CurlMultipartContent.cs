using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.Options;
using Curl.Api.Options.Http;

namespace Curl.Api;

/// <summary>
/// A multipart body (<c>CURLOPT_MIMEPOST</c>, ADR-0637): the <see cref="CurlMimePart"/>s, in order,
/// built through the engine's <see cref="IMimeBuilder"/> each time the request is sent. The builder,
/// and the builder of every nested multipart part, is disposed when the request ends. Immutable;
/// a body may be sent more than once and nested in more than one part.
/// </summary>
public sealed class CurlMultipartContent : CurlContent
{
    /// <summary>A multipart body of <paramref name="parts"/>, in this order.</summary>
    /// <param name="parts">The parts.</param>
    /// <exception cref="ArgumentNullException"><paramref name="parts"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">A part is <see langword="null"/>.</exception>
    public CurlMultipartContent(params IEnumerable<CurlMimePart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        Parts = [.. parts];
        if (Parts.Contains(null!))
        {
            throw new ArgumentException("A part is null.", nameof(parts));
        }
    }

    /// <summary>The parts, in the order they are sent.</summary>
    public IReadOnlyList<CurlMimePart> Parts { get; }

    internal override EasyOptions ApplyTo(EasyOptions options, ICurlEngine engine, ICollection<IDisposable> owned)
    {
        EasyMimeBody body = BuildOn(engine, owned).Build();
        return options with { Http = options.Http with { Post = false, PostFields = null, PostFieldSize = -1, MimePost = body } };
    }

    /// <summary>
    /// A new MIME handle of <paramref name="engine"/> filled with the parts; the handle goes in
    /// <paramref name="owned"/> when it is <see cref="IDisposable"/>.
    /// </summary>
    /// <param name="engine">The engine performing the request.</param>
    /// <param name="owned">Takes every handle created, to dispose when the request ends.</param>
    /// <returns>The filled MIME handle.</returns>
    /// <exception cref="Errors.CurlException">A <c>curl_mime_*</c> setter failed, such as a file that cannot be read.</exception>
    internal IMimeBuilder BuildOn(ICurlEngine engine, ICollection<IDisposable> owned)
    {
        IMimeBuilder mime = engine.CreateMime();
        if (mime is IDisposable disposable)
        {
            owned.Add(disposable);
        }

        foreach (CurlMimePart part in Parts)
        {
            part.SetOn(mime.AddPart(), engine, owned);
        }

        return mime;
    }
}
