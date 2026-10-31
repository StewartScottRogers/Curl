using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Curl.Extended.Protocol.S3.Xml;

/// <summary>
/// Reads an S3 XML body into a value, matching elements by local name so a body in the S3 namespace
/// <c>http://s3.amazonaws.com/doc/2006-03-01/</c> (Amazon S3, MinIO, R2) and one without a namespace
/// read alike. Turns every reason a body cannot be read into a failed <see cref="S3XmlParseResult{T}"/>.
/// </summary>
internal static class S3XmlBody
{
    /// <summary>The namespace S3 puts its bodies in.</summary>
    public const string Namespace = "http://s3.amazonaws.com/doc/2006-03-01/";

    /// <summary>Parses <paramref name="xml"/>, checks its root is <paramref name="rootName"/> and reads it with <paramref name="readRoot"/>.</summary>
    public static S3XmlParseResult<T> Parse<T>(string xml, string rootName, Func<XElement, T> readRoot)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(xml);
        try
        {
            var root = XDocument.Parse(xml).Root!;
            if (root.Name.LocalName != rootName)
            {
                return S3XmlParseResult<T>.Malformed($"Expected the root element {rootName}, found {root.Name.LocalName}.");
            }

            return S3XmlParseResult<T>.Success(readRoot(root));
        }
        catch (Exception exception) when (exception is XmlException or FormatException or OverflowException or S3XmlBodyException)
        {
            return S3XmlParseResult<T>.Malformed(exception.Message);
        }
    }

    /// <summary>Returns the children of <paramref name="parent"/> named <paramref name="localName"/>.</summary>
    public static IEnumerable<XElement> Children(XElement parent, string localName) =>
        parent.Elements().Where(element => element.Name.LocalName == localName);

    /// <summary>Returns the text of the first child named <paramref name="localName"/>, or <see langword="null"/> when there is none.</summary>
    public static string? OptionalText(XElement parent, string localName) => Children(parent, localName).FirstOrDefault()?.Value;

    /// <summary>Returns the text of the first child named <paramref name="localName"/>, failing when there is none.</summary>
    public static string RequiredText(XElement parent, string localName) =>
        OptionalText(parent, localName) ?? throw new S3XmlBodyException($"The element {parent.Name.LocalName} has no {localName}.");

    /// <summary>Returns the first child named <paramref name="localName"/> read as a whole number.</summary>
    public static long RequiredInteger(XElement parent, string localName) =>
        long.Parse(RequiredText(parent, localName), NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>Returns the first child named <paramref name="localName"/> read as an <c>xs:boolean</c>.</summary>
    public static bool RequiredBoolean(XElement parent, string localName) => XmlConvert.ToBoolean(RequiredText(parent, localName));

    /// <summary>Returns the first child named <paramref name="localName"/> read as an ISO 8601 time, UTC when it names no offset.</summary>
    public static DateTimeOffset RequiredTime(XElement parent, string localName) =>
        DateTimeOffset.Parse(RequiredText(parent, localName), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    /// <summary>Reports a body that is well-formed XML but lacks an element it must have.</summary>
    private sealed class S3XmlBodyException(string message) : Exception(message);
}
