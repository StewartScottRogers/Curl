using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-061 of the 2026-10-25 gap report: curl 8.22.0 percent-encodes a control byte
/// (TAB, 0x01-0x1F) in a relative <c>Location</c>, each as itself, and follows it, where
/// <see cref="HttpRedirectLocation" /> used to keep the raw byte; an absolute one keeps its
/// bytes, for the follower to refuse (measured, BL-3590 Notes).
/// </summary>
[TestClass]
public sealed class Cgr061Tests
{
    private const string RequestUrl = "http://127.0.0.1:18590/a/b";

    [TestMethod]
    [DataRow("/b\tc", "http://127.0.0.1:18590/b%09c", DisplayName = "TAB in an absolute path")]
    [DataRow("/b\u0001c", "http://127.0.0.1:18590/b%01c", DisplayName = "0x01")]
    [DataRow("/b\u001Fc", "http://127.0.0.1:18590/b%1Fc", DisplayName = "0x1F")]
    [DataRow("/b\u007Fc", "http://127.0.0.1:18590/b%7Fc", DisplayName = "DEL")]
    [DataRow("/b?x\ty", "http://127.0.0.1:18590/b?x%09y", DisplayName = "TAB in the query")]
    [DataRow("/b#x\ty", "http://127.0.0.1:18590/b#x%09y", DisplayName = "TAB in the fragment")]
    [DataRow("b\u0001c", "http://127.0.0.1:18590/a/b%01c", DisplayName = "relative path")]
    [DataRow("//127.0.0.1:18590/b\u0001c", "http://127.0.0.1:18590/b%01c", DisplayName = "scheme-relative")]
    public void Resolve_RelativeLocationHoldingAControlByte_EscapesIt(string location, string expected) =>
        Assert.AreEqual(expected, HttpRedirectLocation.Resolve(CurlUrl.Parse(RequestUrl), location));

    [TestMethod]
    [DataRow("http://127.0.0.1:18590/b\u0001c")]
    [DataRow("http://127.0.0.1:18590/b\u007Fc")]
    public void Resolve_AbsoluteLocationHoldingAControlByte_KeepsItsBytes(string location) =>
        Assert.AreEqual(location, HttpRedirectLocation.Resolve(CurlUrl.Parse(RequestUrl), location));
}
