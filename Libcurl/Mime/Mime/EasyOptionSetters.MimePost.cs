using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Mime;

namespace Libcurl.Options;

/// <summary>The <c>CURLOPT_MIMEPOST</c> part of <see cref="EasyOptionSetters"/> (ADR-0583).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the <c>CURLOPT_MIMEPOST</c> row.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddMimePostSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => setters[CurlOption.Mimepost] = new(SetMimePost);

    /// <summary>
    /// <c>CURLOPT_MIMEPOST</c>: a <see cref="CurlMime"/>'s body as its parts are now, or <see langword="null"/>
    /// to send none; any other value is <see cref="CurlCode.BadFunctionArgument"/>.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The handle has been freed.</exception>
    private static CurlCode SetMimePost(ref EasyOptions options, object? value)
    {
        if (value is not (null or CurlMime))
        {
            return CurlCode.BadFunctionArgument;
        }

        var body = (value as CurlMime)?.Build();
        options = options with { Http = options.Http with { MimePost = body } };
        return CurlCode.Ok;
    }
}
