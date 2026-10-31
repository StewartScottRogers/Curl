using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Curl.Extension.WebDavTools.Multistatus;

/// <summary>
/// Reads a <c>207 Multistatus</c> body (RFC 4918 sections 13, 14.16, 14.22 and 14.24) into one
/// <see cref="WebDavResourceEntry"/> per <c>response</c>, matching elements in the <c>DAV:</c> namespace whatever
/// prefix the body binds it to, and taking properties only from <c>propstat</c> elements with a 2xx status.
/// A document type declaration is refused, never processed, so no external entity is resolved.
/// </summary>
public static class WebDavMultistatusReader
{
    private static readonly XNamespace Dav = "DAV:";

    /// <summary>Reads <paramref name="xml"/>, returning its entries or, when it cannot be read, why.</summary>
    /// <param name="xml">The response body.</param>
    /// <returns>The entries, or the typed reason the body was refused.</returns>
    public static WebDavMultistatusResult Read(string xml)
    {
        ArgumentNullException.ThrowIfNull(xml);
        XElement root;
        try
        {
            root = Load(xml);
        }
        catch (XmlException exception)
        {
            var kind = xml.Contains("<!DOCTYPE", StringComparison.Ordinal)
                ? WebDavMultistatusFailureKind.DocumentTypeDeclaration
                : WebDavMultistatusFailureKind.NotWellFormed;
            return WebDavMultistatusResult.Refused(kind, exception.Message);
        }

        if (root.Name != Dav + "multistatus")
        {
            return WebDavMultistatusResult.Refused(
                WebDavMultistatusFailureKind.NotMultistatus,
                $"Expected the root element multistatus in the DAV: namespace, found {root.Name}.");
        }

        try
        {
            return WebDavMultistatusResult.Success(root.Elements(Dav + "response").Select(ReadResponse).ToList());
        }
        catch (MultistatusBodyException exception)
        {
            return WebDavMultistatusResult.Refused(exception.Kind, exception.Message);
        }
    }

    private static XElement Load(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, settings);
        return XDocument.Load(reader).Root!;
    }

    private static WebDavResourceEntry ReadResponse(XElement response)
    {
        var href = Uri.UnescapeDataString(Required(response, "href").Value.Trim());
        var propstats = response.Elements(Dav + "propstat").Select(ReadPropstat).ToList();
        var successful = propstats.Where(propstat => propstat.Status / 100 == 2).ToList();
        var responseStatus = response.Element(Dav + "status");
        var status = responseStatus is null ? PropstatStatus(propstats, successful) : ParseStatus(responseStatus.Value);
        var properties = successful.SelectMany(propstat => propstat.Prop.Elements()).ToList();
        return new WebDavResourceEntry(
            href,
            IsCollection(Property(properties, "resourcetype")),
            ParseLength(Property(properties, "getcontentlength")),
            ParseHttpDate(Property(properties, "getlastmodified")),
            TrimmedText(Property(properties, "getetag")),
            status);
    }

    private static Propstat ReadPropstat(XElement propstat) =>
        new(Required(propstat, "prop"), ParseStatus(Required(propstat, "status").Value));

    private static int PropstatStatus(List<Propstat> propstats, List<Propstat> successful)
    {
        if (propstats.Count == 0)
        {
            throw new MultistatusBodyException(WebDavMultistatusFailureKind.MissingElement, "A response has neither a status nor a propstat.");
        }

        return successful.Count > 0 ? successful[0].Status : propstats[0].Status;
    }

    private static XElement Required(XElement parent, string localName) =>
        parent.Element(Dav + localName)
        ?? throw new MultistatusBodyException(WebDavMultistatusFailureKind.MissingElement, $"A {parent.Name.LocalName} has no {localName}.");

    private static XElement? Property(List<XElement> properties, string localName) =>
        properties.FirstOrDefault(property => property.Name == Dav + localName);

    private static int ParseStatus(string statusLine)
    {
        var parts = statusLine.Trim().Split(' ', 3);
        if (parts.Length < 2
            || !parts[0].StartsWith("HTTP/", StringComparison.Ordinal)
            || parts[1].Length != 3
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var code))
        {
            throw new MultistatusBodyException(WebDavMultistatusFailureKind.InvalidStatus, $"The status '{statusLine}' is not an HTTP status line.");
        }

        return code;
    }

    private static bool IsCollection(XElement? resourceType) => resourceType?.Element(Dav + "collection") is not null;

    private static string? TrimmedText(XElement? property) => property?.Value.Trim();

    private static long? ParseLength(XElement? property) =>
        property is not null && long.TryParse(property.Value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var length) ? length : null;

    private static DateTimeOffset? ParseHttpDate(XElement? property) =>
        property is not null && DateTimeOffset.TryParseExact(property.Value.Trim(), "r", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var time)
            ? time
            : null;

    /// <summary>One <c>propstat</c>: its <c>prop</c> and the status code that applies to it.</summary>
    private sealed record Propstat(XElement Prop, int Status);

    /// <summary>Reports a well-formed body that breaks RFC 4918's multistatus structure.</summary>
    private sealed class MultistatusBodyException(WebDavMultistatusFailureKind kind, string message) : Exception(message)
    {
        public WebDavMultistatusFailureKind Kind { get; } = kind;
    }
}
