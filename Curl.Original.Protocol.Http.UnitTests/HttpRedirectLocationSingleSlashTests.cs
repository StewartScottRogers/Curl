using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins BL-3828: an absolute <c>Location</c> with one slash after the scheme and a port in its
/// authority is reported as curl 8.22.0 reports it, with two slashes.
/// </summary>
[TestClass]
public sealed class HttpRedirectLocationSingleSlashTests
{
    private static readonly CurlUrl RequestUrl = CurlUrl.Parse("http://127.0.0.1:18731/a");

    [TestMethod]
    [DataRow("http:/127.0.0.1:18731/y", "http://127.0.0.1:18731/y", DisplayName = "ipv4 with port")]
    [DataRow("http:/localhost:18731/y", "http://localhost:18731/y", DisplayName = "name with port")]
    [DataRow("http:/[::1]:18731/y", "http://[::1]:18731/y", DisplayName = "ipv6 with port")]
    public void Resolve_OneSlashThenHostAndPort_HasTwoSlashes(string location, string expected)
    {
        string resolved = HttpRedirectLocation.Resolve(RequestUrl, location, driveLetters: false);

        Assert.AreEqual(expected, resolved);
    }
}
