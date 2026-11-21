using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Imap;

/// <summary>
/// Pins that a kept Imap connection serves a transfer only for the same bearer token and SASL
/// authorization identity as well as the same user, as curl 8.22.0's <c>ConnectionExists</c>
/// compares <c>oauth_bearer</c> and <c>sasl_authzid</c> for protocols that log in per
/// connection (BL-5641).
/// </summary>
[TestClass]
public sealed class ImapKeptConnectionLoginKeyTests
{
    [TestMethod]
    [DataRow("tok", null, "tok", null, true, DisplayName = "same token")]
    [DataRow("tok", null, "other", null, false, DisplayName = "another token")]
    [DataRow("tok", null, null, null, false, DisplayName = "token dropped")]
    [DataRow(null, "id", null, "id", true, DisplayName = "same authorization identity")]
    [DataRow(null, "id", null, "other", false, DisplayName = "another authorization identity")]
    public void LoginKeyOf_BearerTokenAndAuthorizationIdentity_MatchOnlyWhenBothAreEqual(string? firstToken, string? firstIdentity, string? secondToken, string? secondIdentity, bool expected)
    {
        TransferContext first = ContextWith(firstToken, firstIdentity);
        TransferContext second = ContextWith(secondToken, secondIdentity);

        bool same = ImapKeptConnection.LoginKeyOf(first) == ImapKeptConnection.LoginKeyOf(second);

        Assert.AreEqual(expected, same);
    }

    [TestMethod]
    public void LoginKeyOf_NoMailOptionsAtAll_IsTheKeyOfDefaultMailOptions()
    {
        var without = new TransferContext { Url = CurlUrl.Parse("imap://127.0.0.1/1"), Output = Stream.Null, Credentials = new NetworkCredential("u", "p"), Mail = null! };

        Assert.AreEqual(ImapKeptConnection.LoginKeyOf(ContextWith(null, null)), ImapKeptConnection.LoginKeyOf(without));
    }

    private static TransferContext ContextWith(string? token, string? identity) => new()
    {
        Url = CurlUrl.Parse("imap://127.0.0.1/1"),
        Output = Stream.Null,
        Credentials = new NetworkCredential("u", "p"),
        Mail = new MailRequestOptions { BearerToken = token, SaslAuthorizationIdentity = identity },
    };
}
