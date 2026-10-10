namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamResolveCheck"/>: a <c>%RESOLVE</c> line, as the expander leaves it,
/// passes silently when its name resolves in the asked family and otherwise prints resolve.c's
/// reason and exits 1, with no lookup; any other line is refused.
/// </summary>
[TestClass]
public sealed class UpstreamResolveCheckTests
{
    [TestMethod]
    [DataRow("resolve --ipv6 ::1")]
    [DataRow("resolve --ipv6 ip6-localhost")]
    [DataRow("resolve --ipv6 LOCALHOST")]
    [DataRow("resolve --ipv4 127.0.0.1")]
    [DataRow("resolve localhost ")]
    public void RunLine_NameThatResolves_ExitsZeroPrintingNothing(string line)
    {
        Assert.IsTrue(UpstreamResolveCheck.Interprets(line));
        Assert.AreEqual(new UpstreamPerlOneLinerResult(0, ""), UpstreamResolveCheck.RunLine(line));
    }

    [TestMethod]
    [DataRow("resolve --ipv6 127.0.0.1", "IPv6", "127.0.0.1")]
    [DataRow("resolve --ipv6 [::1]", "IPv6", "[::1]")]
    [DataRow("resolve --ipv6 non-existing-host.haxx.se.", "IPv6", "non-existing-host.haxx.se.")]
    [DataRow("resolve ::1", "IPv4", "::1")]
    [DataRow("resolve --ipv4 ip6-localhost", "IPv4", "ip6-localhost")]
    public void RunLine_NameThatDoesNotResolve_PrintsTheReasonAndExitsOne(string line, string family, string host)
    {
        Assert.AreEqual(new UpstreamPerlOneLinerResult(1, $"Resolving {family} '{host}' didn't work\n"), UpstreamResolveCheck.RunLine(line));
    }

    [TestMethod]
    [DataRow("resolve")]
    [DataRow("resolve --ipv5 ::1")]
    [DataRow("resolve --ipv6 a b")]
    [DataRow("perl -e 'exit(0)'")]
    public void RunLine_OtherLine_IsNotInterpreted(string line)
    {
        Assert.IsFalse(UpstreamResolveCheck.Interprets(line));
        Assert.IsNull(UpstreamResolveCheck.RunLine(line));
    }
}
