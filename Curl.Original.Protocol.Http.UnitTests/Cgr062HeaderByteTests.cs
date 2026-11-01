using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-062 part 2 (BL-3867): curl 8.22.0 Schannel writes a raw byte 0x80 to 0xFF of an
/// absolute <c>Location</c> back as that byte in <c>%{redirect_url}</c>, so
/// <see cref="HttpRedirectLocation" /> marks those bytes (<see cref="HeaderByteText" />,
/// ADR-0923); a relative <c>Location</c> keeps the request URL's own authority, which is
/// command-line text and is not marked (upstream tests 2046 and 2047).
/// </summary>
[TestClass]
public sealed class Cgr062HeaderByteTests
{
    [TestMethod]
    [DataRow("http://é.invalid/b", DisplayName = "host")]
    [DataRow("http://x.test/é?ÿ#\u0080", DisplayName = "path, query and fragment")]
    public void Resolve_AbsoluteLocationHoldingAHighByte_MarksTheByte(string location)
    {
        string resolved = HttpRedirectLocation.Resolve(CurlUrl.Parse("http://127.0.0.1:18867/a/"), location);

        Assert.AreEqual(HeaderByteText.Mark(location), resolved);
        Assert.AreNotEqual(location, resolved);
    }

    [TestMethod]
    public void Resolve_RelativeLocationAfterAnIdnHost_KeepsTheHostAsText()
    {
        string resolved = HttpRedirectLocation.Resolve(CurlUrl.Parse("http://åäö.se/a/"), "/b");

        Assert.AreEqual("http://åäö.se/b", resolved);
    }

    [TestMethod]
    public void Resolve_AbsoluteAsciiLocation_IsUnchanged()
    {
        string resolved = HttpRedirectLocation.Resolve(CurlUrl.Parse("http://127.0.0.1:18867/a/"), "http://x.test/b");

        Assert.AreEqual("http://x.test/b", resolved);
    }
}
