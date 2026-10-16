using Curl.Api.Options.Http;

namespace Curl.Api.EngineContracts.Mime;

/// <summary>
/// One MIME handle of an <see cref="ICurlEngine"/> (<c>curl_mime</c>, ADR-0506): builds the
/// <see cref="EasyMimeBody"/> that <c>CURLOPT_MIMEPOST</c> sends, or that a part nests with
/// <see cref="IMimePartBuilder.SetSubParts"/>. <c>curl_mime_free</c> has no member: dropping the
/// builder frees it.
/// </summary>
public interface IMimeBuilder
{
    /// <summary><c>curl_mime_addpart</c>: append a new, empty part.</summary>
    /// <returns>The new part, to fill with its setters.</returns>
    IMimePartBuilder AddPart();

    /// <summary>The body as the parts' setters have left it, parts in the order they were added.</summary>
    /// <returns>The body.</returns>
    EasyMimeBody Build();
}
