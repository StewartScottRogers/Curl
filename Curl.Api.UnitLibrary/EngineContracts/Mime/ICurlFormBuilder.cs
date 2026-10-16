using Curl.Api.Codes;
using Curl.Api.Options.Http;

namespace Curl.Api.EngineContracts.Mime;

/// <summary>
/// The deprecated form API (<c>curl_formadd</c>, ADR-0506): builds the same <see cref="EasyMimeBody"/>
/// as <see cref="IMimeBuilder"/>, field by field, for <c>CURLOPT_HTTPPOST</c>. Upstream's
/// <c>httppost</c> and <c>last_post</c> pointers are this builder's state; <c>curl_formfree</c>
/// has no member: dropping the builder frees it.
/// </summary>
public interface ICurlFormBuilder
{
    /// <summary><c>curl_formadd</c>: add one field, described by its options; <see cref="CurlFormOption.End"/> is implied.</summary>
    /// <param name="arguments">The field's options in order.</param>
    /// <returns>The call's result; nothing is added when it fails.</returns>
    CurlFormAddCode Add(IReadOnlyList<CurlFormArgument> arguments);

    /// <summary><c>curl_formget</c>: serialise the form as it would be sent, piece by piece.</summary>
    /// <param name="append"><c>append</c>: receives each piece.</param>
    /// <returns>0 when the whole form was serialised, else a nonzero <c>CURLcode</c> number.</returns>
    int Serialize(FormGetCallback append);

    /// <summary>The body the added fields make, in the order they were added.</summary>
    /// <returns>The body.</returns>
    EasyMimeBody Build();
}
