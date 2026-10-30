using System.Xml.Linq;

namespace Curl.Extension.GoogleCloudStorage.Xml;

/// <summary>
/// Reads the <c>Error</c> body of the Cloud Storage "XML API error responses": its <c>Code</c>,
/// <c>Message</c> and <c>Details</c>.
/// </summary>
public static class GcsErrorResponseParser
{
    /// <summary>Reads <paramref name="xml"/> as the error Cloud Storage returned.</summary>
    /// <param name="xml">The response body.</param>
    /// <returns>The error, or why the body could not be read.</returns>
    public static GcsXmlParseResult<GcsErrorResponse> Parse(string xml) => GcsXmlBody.Parse(xml, "Error", ReadError);

    private static GcsErrorResponse ReadError(XElement root) => new(
        GcsXmlBody.RequiredText(root, "Code"),
        GcsXmlBody.OptionalText(root, "Message") ?? string.Empty,
        GcsXmlBody.OptionalText(root, "Details"));
}
