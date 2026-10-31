using System.Xml.Linq;

namespace Curl.Extension.Protocol.S3.Xml;

/// <summary>
/// Reads the <c>Error</c> body of the Amazon S3 API reference "Error responses": its <c>Code</c> and
/// <c>Message</c>.
/// </summary>
public static class S3ErrorResponseParser
{
    /// <summary>Reads <paramref name="xml"/> as the error S3 returned.</summary>
    /// <param name="xml">The response body.</param>
    /// <returns>The error, or why the body could not be read.</returns>
    public static S3XmlParseResult<S3ErrorResponse> Parse(string xml) => S3XmlBody.Parse(xml, "Error", ReadError);

    private static S3ErrorResponse ReadError(XElement root) => new(
        S3XmlBody.RequiredText(root, "Code"),
        S3XmlBody.OptionalText(root, "Message") ?? string.Empty);
}
