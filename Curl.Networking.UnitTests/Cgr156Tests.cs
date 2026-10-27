using System.Text;

using Curl.Networking.Fakes;

namespace Curl.Networking;

/// <summary>
/// CGR-156 (BL-3685, ADR-0849): a user name or password in the <c>--doh-url</c> sends
/// <c>Authorization: Basic</c> on every DoH POST, right after Host, as curl 8.21.0 did when
/// measured with <c>Record-CurlExchange.ps1</c>.
/// </summary>
[TestClass]
public sealed class Cgr156Tests
{
    [TestMethod]
    public async Task ResolveAsync_DohUrlWithUserAndPassword_EachPostCarriesBasicAuthorizationAfterHost()
    {
        var connector = new FakeConnector();
        var resolver = new DohDnsResolver(connector, new Uri("http://u%41:p%3a@127.0.0.1:21800/dns-query"));

        await resolver.ResolveAsync("foo.test", CancellationToken.None);

        Assert.HasCount(2, connector.Opened);
        foreach (var connection in connector.Opened)
        {
            var head = Encoding.Latin1.GetString(connection.Written.ToArray());
            Assert.StartsWith("POST /dns-query HTTP/1.1\r\nHost: 127.0.0.1:21800\r\nAuthorization: Basic dUE6cDo=\r\nAccept: */*\r\n", head);
        }
    }

    [TestMethod]
    [DataRow("u", "Authorization: Basic dTo=\r\n")]
    [DataRow(":p", "Authorization: Basic OnA=\r\n")]
    [DataRow(":", "")]
    [DataRow("", "")]
    public void BasicAuthorizationLine_UserInfo_IsTheLineCurlSent(string userInfo, string expected)
    {
        var line = DohDnsResolver.BasicAuthorizationLine(userInfo);

        Assert.AreEqual(expected, line);
    }
}
