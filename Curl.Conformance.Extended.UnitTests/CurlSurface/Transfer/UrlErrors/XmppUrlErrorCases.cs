using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>xmpp</c>, <c>xmpps</c>.</summary>
[TestClass]
public sealed class XmppUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "xmpp" => new(scheme),
        _ => new(scheme),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-xmpp-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-xmpp-1")]
    [SpecRequirement("CurlSurface:url-space-xmpp-1")]
    [SpecRequirement("CurlSurface:url-bad-port-xmpps-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-xmpps-1")]
    [SpecRequirement("CurlSurface:url-space-xmpps-1")]
    [DataRow("xmpp", UrlErrorChecks.BadPort)]
    [DataRow("xmpp", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("xmpp", UrlErrorChecks.Space)]
    [DataRow("xmpps", UrlErrorChecks.BadPort)]
    [DataRow("xmpps", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("xmpps", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-xmpp-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-xmpps-1")]
    [DataRow("xmpp")]
    [DataRow("xmpps")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-xmpp-1")]
    [SpecRequirement("CurlSurface:connection-refused-xmpps-1")]
    [DataRow("xmpp")]
    [DataRow("xmpps")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
