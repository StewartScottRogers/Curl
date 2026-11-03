using System.Globalization;
using System.Text;
using System.Xml.Linq;

namespace Curl.Conformance.Extended.GoogleCloudStorage;

/// <summary>One response of the Cloud Storage XML API reference endpoint.</summary>
/// <param name="StatusCode">The HTTP status code.</param>
/// <param name="ReasonPhrase">The status line's reason phrase.</param>
/// <param name="ContentType">The <c>Content-Type</c> header's value.</param>
/// <param name="Body">The body, framed by <c>Content-Length</c>.</param>
public sealed record GcsReferenceResponse(int StatusCode, string ReasonPhrase, string ContentType, byte[] Body)
{
    /// <summary>A <c>200 OK</c> response.</summary>
    /// <param name="contentType">The <c>Content-Type</c> header's value.</param>
    /// <param name="body">The body.</param>
    /// <returns>The response.</returns>
    public static GcsReferenceResponse Ok(string contentType, byte[] body) => new(200, "OK", contentType, body);

    /// <summary>An error response with the XML API's <c>Error</c> body ("XML API error responses"): <c>Code</c>, <c>Message</c> and <c>Details</c>.</summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="reasonPhrase">The status line's reason phrase.</param>
    /// <param name="code">The <c>Code</c> element.</param>
    /// <param name="message">The <c>Message</c> element.</param>
    /// <param name="details">The <c>Details</c> element.</param>
    /// <returns>The response.</returns>
    public static GcsReferenceResponse Error(int statusCode, string reasonPhrase, string code, string message, string details) =>
        new(statusCode, reasonPhrase, "application/xml; charset=UTF-8", XmlBody(new XElement("Error", new XElement("Code", code), new XElement("Message", message), new XElement("Details", details))));

    /// <summary>Gives an XML document's bytes, UTF-8 behind the XML declaration Cloud Storage sends.</summary>
    /// <param name="root">The document's root element.</param>
    /// <returns>The bytes.</returns>
    public static byte[] XmlBody(XElement root)
    {
        ArgumentNullException.ThrowIfNull(root);
        return Encoding.UTF8.GetBytes("<?xml version='1.0' encoding='UTF-8'?>" + root.ToString(SaveOptions.DisableFormatting));
    }

    /// <summary>Gives the bytes sent on the wire: status line, <c>Content-Type</c>, <c>Content-Length</c>, the blank line and the body.</summary>
    /// <returns>The bytes.</returns>
    public byte[] ToBytes() =>
        [.. Encoding.Latin1.GetBytes(string.Create(CultureInfo.InvariantCulture, $"HTTP/1.1 {StatusCode} {ReasonPhrase}\r\nContent-Type: {ContentType}\r\nContent-Length: {Body.Length}\r\n\r\n")), .. Body];
}
