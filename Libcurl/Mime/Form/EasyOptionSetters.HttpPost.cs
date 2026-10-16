using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Mime;

namespace Libcurl.Options;

// CURLOPT_HTTPPOST takes the deprecated CurlForm, as upstream's option is deprecated with it.
#pragma warning disable CS0618

/// <summary>The <c>CURLOPT_HTTPPOST</c> part of <see cref="EasyOptionSetters"/> (ADR-0586).</summary>
internal static partial class EasyOptionSetters
{
    /// <summary>Adds the <c>CURLOPT_HTTPPOST</c> row.</summary>
    /// <param name="setters">The rows to add to.</param>
    static partial void AddFormPostSetters(Dictionary<CurlOption, EasyOptionSetter> setters) => setters[CurlOption.Httppost] = new(SetHttpPost);

    /// <summary>
    /// <c>CURLOPT_HTTPPOST</c>: a <see cref="CurlForm"/>'s body as its fields are now, or <see langword="null"/>
    /// to send none; any other value is <see cref="CurlCode.BadFunctionArgument"/>.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The form has been freed.</exception>
    private static CurlCode SetHttpPost(ref EasyOptions options, object? value)
    {
        if (value is not (null or CurlForm))
        {
            return CurlCode.BadFunctionArgument;
        }

        var body = (value as CurlForm)?.Build();
        options = options with { Http = options.Http with { HttpPost = body } };
        return CurlCode.Ok;
    }
}
