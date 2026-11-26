using System.Text;
using System.Xml.Linq;
using Curl.Extended.Protocol.Xmpp.Wire;
using Curl.Extension.Abstractions;
using Curl.Protocol.Abstractions;

namespace Curl.Extended.Protocol.Xmpp.Client;

/// <summary>
/// Turns an <c>xmpp:</c> URI's query (RFC 5122, 2.2 "iquery"; the XMPP URI query registry's <c>message</c> and
/// <c>disco</c> types) into the stanza the client sends (ADR-1068). <c>?message;subject=..;body=..;type=..</c> gives a
/// <c>&lt;message/&gt;</c> (RFC 6121, 5.2), its body from <c>-d</c> when given; <c>?disco;request=info|items;node=..</c>
/// gives a service discovery <c>&lt;iq type='get'/&gt;</c> (XEP-0030, 3.1 and 4.1). With no query type, a body makes
/// it a message and no body a disco#info query.
/// </summary>
internal static class XmppRequests
{
    /// <summary>The <c>id</c> of the disco <c>&lt;iq/&gt;</c>, by which the flow knows its answer.</summary>
    internal const string DiscoIdentifier = "disco";

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Gives the stanza for <paramref name="target"/>, the URL's query (without <c>?</c>) and the body.</summary>
    /// <exception cref="XmppClientException">The query names a type or request the client does not provide, or a part of
    /// it other than a body is not valid UTF-8 (exit 3); or a body, from <c>-d</c>, <c>-T</c> or the query, is not valid
    /// UTF-8 (exit 43, ADR-1350, ADR-1429).</exception>
    internal static XElement For(string target, string? query, ReadOnlyMemory<byte>? body)
    {
        string[] parts = (query ?? string.Empty).Split(';');
        Dictionary<string, string> pairs = PairsOf(parts.AsSpan(1));
        string queryType = parts[0].Length > 0 ? DecodedQueryPart(parts[0]) : body is null ? "disco" : "message";
        return queryType switch
        {
            "message" => Message(target, pairs, body),
            "disco" => Disco(target, pairs),
            _ => throw new XmppClientException(CurlExitCode.UrlMalformat, XmppClientMessages.UnsupportedQuery(queryType)),
        };
    }

    private static Dictionary<string, string> PairsOf(ReadOnlySpan<string> pairs)
    {
        Dictionary<string, string> keys = new(StringComparer.Ordinal);
        foreach (string pair in pairs)
        {
            string[] keyAndValue = pair.Split('=', 2);
            string key = DecodedQueryPart(keyAndValue[0]);
            keys[key] = keyAndValue.Length > 1 ? DecodedValue(key, keyAndValue[1]) : string.Empty;
        }

        return keys;
    }

    /// <summary>Gives the JID the URL's path (without its leading slash) names.</summary>
    /// <exception cref="XmppClientException">It is not valid UTF-8 (exit 3, ADR-1429).</exception>
    internal static string DecodedAddress(string escapedPath) =>
        DecodedText(escapedPath, CurlExitCode.UrlMalformat, XmppClientMessages.AddressNotUtf8);

    private static string DecodedQueryPart(string escaped) =>
        DecodedText(escaped, CurlExitCode.UrlMalformat, XmppClientMessages.QueryNotUtf8);

    private static string DecodedValue(string key, string escaped) =>
        key == "body"
            ? DecodedText(escaped, CurlExitCode.BadFunctionArgument, XmppClientMessages.BodyNotUtf8)
            : DecodedQueryPart(escaped);

    private static string DecodedText(string escaped, CurlExitCode exitCode, string notUtf8Message) =>
        UrlPercentDecoding.Utf8TextOf(escaped) ?? throw new XmppClientException(exitCode, notUtf8Message);

    private static XElement Message(string target, Dictionary<string, string> pairs, ReadOnlyMemory<byte>? body)
    {
        string? text = body is { } data ? TextOf(data.Span) : pairs.GetValueOrDefault("body");
        return new XElement(
            XmppNamespaces.Client + "message",
            new XAttribute("to", target),
            Optional(pairs, "type", key => new XAttribute(key, pairs[key])),
            Optional(pairs, "subject", key => new XElement(XmppNamespaces.Client + key, pairs[key])),
            text is null ? null : new XElement(XmppNamespaces.Client + "body", text));
    }

    private static string TextOf(ReadOnlySpan<byte> body)
    {
        try
        {
            return StrictUtf8.GetString(body);
        }
        catch (DecoderFallbackException)
        {
            throw new XmppClientException(CurlExitCode.BadFunctionArgument, XmppClientMessages.BodyNotUtf8);
        }
    }

    private static XElement Disco(string target, Dictionary<string, string> pairs)
    {
        string request = pairs.GetValueOrDefault("request", "info");
        XNamespace space = request switch
        {
            "info" => XmppNamespaces.DiscoInfo,
            "items" => XmppNamespaces.DiscoItems,
            _ => throw new XmppClientException(CurlExitCode.UrlMalformat, XmppClientMessages.UnsupportedDiscoRequest(request)),
        };
        return new XElement(
            XmppNamespaces.Client + "iq",
            new XAttribute("type", "get"),
            new XAttribute("id", DiscoIdentifier),
            new XAttribute("to", target),
            new XElement(space + "query", Optional(pairs, "node", key => new XAttribute(key, pairs[key]))));
    }

    private static XObject? Optional(Dictionary<string, string> pairs, string key, Func<string, XObject> create) =>
        pairs.ContainsKey(key) ? create(key) : null;
}
