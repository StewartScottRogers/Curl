using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ws;

/// <summary>
/// Pins <see cref="WsAuthUsingLines" />: the <c>Server auth using</c> line curl 8.21.0 writes
/// before the upgrade request for each picked scheme, and none when it picks none (BL-953 Notes).
/// </summary>
[TestClass]
public sealed class WsAuthUsingLinesTests
{
    [TestMethod]
    [DataRow("Basic dTpw", HttpAuthSchemes.Basic, false, "Server auth using Basic with user 'u'")]
    [DataRow("Bearer tok", HttpAuthSchemes.Bearer, false, "Server auth using Bearer with user 'u'")]
    [DataRow("NTLM TlRM", HttpAuthSchemes.Ntlm, false, "Server auth using NTLM with user 'u'")]
    [DataRow("NTLM TlRM", HttpAuthSchemes.Ntlm, true, "Server auth using NTLM with user 'u'")]
    [DataRow("Digest x", HttpAuthSchemes.Digest, false, "Server auth using Digest with user 'u'")]
    [DataRow("Negotiate YII", HttpAuthSchemes.Negotiate, false, "Server auth using Negotiate with user 'u'")]
    [DataRow(null, HttpAuthSchemes.Negotiate, false, "Server auth using Negotiate with user 'u'")]
    [DataRow(null, HttpAuthSchemes.Digest, false, "Server auth using Digest with user 'u'")]
    [DataRow(null, HttpAuthSchemes.Any, false, null)]
    [DataRow(null, HttpAuthSchemes.Basic, false, null)]
    [DataRow("Basic dTpw", HttpAuthSchemes.Basic, true, null)]
    [DataRow("Bearer tok", HttpAuthSchemes.Bearer, true, null)]
    [DataRow("AWS4-HMAC-SHA256 x", HttpAuthSchemes.Basic, false, null)]
    [DataRow("AWS4-HMAC-SHA256 x", HttpAuthSchemes.None, false, null)]
    [DataRow("Basic dTpw", HttpAuthSchemes.Basic | HttpAuthSchemes.Negotiate, false, null)]
    [DataRow("NTLM TlRM", HttpAuthSchemes.Any, false, null)]
    public void ServerAuthUsing_PickedScheme_WritesCurlsLine(string? authorization, HttpAuthSchemes schemes, bool headerNamesAuthorization, string? expected) =>
        Assert.AreEqual(expected, WsAuthUsingLines.ServerAuthUsing(Request(schemes, new NetworkCredential("u", "p")), authorization, headerNamesAuthorization));

    [TestMethod]
    public void ServerAuthUsing_DigestWithoutUser_WritesNothing() =>
        Assert.IsNull(WsAuthUsingLines.ServerAuthUsing(Request(HttpAuthSchemes.Digest, null), null, headerNamesAuthorization: false));

    [TestMethod]
    public void ServerAuthUsing_NoUser_NamesNobody() =>
        Assert.AreEqual("Server auth using Bearer with user ''", WsAuthUsingLines.ServerAuthUsing(Request(HttpAuthSchemes.Bearer, null), "Bearer tok", headerNamesAuthorization: false));

    private static HttpAuthRequest Request(HttpAuthSchemes schemes, NetworkCredential? credential) =>
        new("GET", CurlUrl.Parse("ws://h/"), "/", credential, null, schemes, IsProxy: false);
}
