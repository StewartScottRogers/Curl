using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Curl.Extended.Protocol.AzureStorage.Bodies;

/// <summary>
/// Reads an Azure Storage XML body into a value, matching elements by local name. Turns every reason a
/// body cannot be read into a failed <see cref="AzureStorageBodyParseResult{T}"/>.
/// </summary>
internal static class AzureStorageXmlBody
{
    /// <summary>Parses <paramref name="xml"/>, checks its root is <paramref name="rootName"/> and reads it with <paramref name="readRoot"/>.</summary>
    public static AzureStorageBodyParseResult<T> Parse<T>(string xml, string rootName, Func<XElement, T> readRoot)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(xml);
        try
        {
            var root = XDocument.Parse(xml).Root!;
            if (root.Name.LocalName != rootName)
            {
                return AzureStorageBodyParseResult<T>.Malformed($"Expected the root element {rootName}, found {root.Name.LocalName}.");
            }

            return AzureStorageBodyParseResult<T>.Success(readRoot(root));
        }
        catch (Exception exception) when (exception is XmlException or FormatException or OverflowException or AzureStorageBodyException)
        {
            return AzureStorageBodyParseResult<T>.Malformed(exception.Message);
        }
    }

    /// <summary>Returns the children of <paramref name="parent"/> named <paramref name="localName"/>.</summary>
    public static IEnumerable<XElement> Children(XElement parent, string localName) =>
        parent.Elements().Where(element => element.Name.LocalName == localName);

    /// <summary>Returns the first child named <paramref name="localName"/>, failing when there is none.</summary>
    public static XElement RequiredChild(XElement parent, string localName) =>
        Children(parent, localName).FirstOrDefault()
            ?? throw new AzureStorageBodyException($"The element {parent.Name.LocalName} has no {localName}.");

    /// <summary>Returns the text of the first child named <paramref name="localName"/>, or <see langword="null"/> when there is none or it is empty.</summary>
    public static string? OptionalText(XElement parent, string localName)
    {
        var text = Children(parent, localName).FirstOrDefault()?.Value;
        return string.IsNullOrEmpty(text) ? null : text;
    }

    /// <summary>Returns the text of the first child named <paramref name="localName"/>, failing when there is none.</summary>
    public static string RequiredText(XElement parent, string localName) => RequiredChild(parent, localName).Value;

    /// <summary>Returns the first child named <paramref name="localName"/> read as a whole number.</summary>
    public static long RequiredInteger(XElement parent, string localName) =>
        long.Parse(RequiredText(parent, localName), NumberStyles.None, CultureInfo.InvariantCulture);

    /// <summary>Returns the first child named <paramref name="localName"/> read as an RFC 1123 time, the form Azure Storage sends.</summary>
    public static DateTimeOffset RequiredHttpTime(XElement parent, string localName) =>
        AzureStorageHttpTime.Parse(RequiredText(parent, localName));
}
