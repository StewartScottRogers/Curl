using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Libcurl.Mime;

namespace Libcurl.Options;

/// <summary>The <c>CURLOPT_MIMEPOST</c> part of <see cref="EasyOptionSetters"/> (ADR-0583).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the <c>CURLOPT_MIMEPOST</c> row.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddMimePostSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => setters[CurlOption.Mimepost] = new(SetMimePost);

    /// <summary>
    /// <c>CURLOPT_MIMEPOST</c>: a <see cref="CurlMime"/>'s body as its parts are now, or for <see langword="null"/>
    /// an empty <see cref="EasyHttpOptions.HttpPost"/> form, which the engine still posts as a zero-byte body
    /// (upstream lib589); either clears <c>CURLOPT_NOBODY</c>, as curl 8.22.0's <c>setopt.c</c> sets the MIME POST
    /// method whatever the pointer is. Any other value is <see cref="CurlCode.BadFunctionArgument"/>.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The handle has been freed.</exception>
    private static CurlCode SetMimePost(ref EasyOptions options, object? value)
    {
        if (value is not (null or CurlMime))
        {
            return CurlCode.BadFunctionArgument;
        }

        var http = value is CurlMime mime
            ? options.Http with { MimePost = mime.Build() }
            : options.Http with { MimePost = null, HttpPost = new EasyMimeBody() };
        options = options with { Http = http, Transfer = options.Transfer with { NoBody = false } };
        return CurlCode.Ok;
    }
}
