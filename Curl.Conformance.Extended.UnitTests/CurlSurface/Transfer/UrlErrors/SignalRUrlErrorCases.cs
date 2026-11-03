using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>signalr</c>, <c>signalrs</c>.</summary>
[TestClass]
public sealed class SignalRUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "signalr" => new(scheme),
        _ => new(scheme),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-signalr-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-signalr-1")]
    [SpecRequirement("CurlSurface:url-space-signalr-1")]
    [SpecRequirement("CurlSurface:url-bad-port-signalrs-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-signalrs-1")]
    [SpecRequirement("CurlSurface:url-space-signalrs-1")]
    [DataRow("signalr", UrlErrorChecks.BadPort)]
    [DataRow("signalr", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("signalr", UrlErrorChecks.Space)]
    [DataRow("signalrs", UrlErrorChecks.BadPort)]
    [DataRow("signalrs", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("signalrs", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-signalr-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-signalr-1")]
    [SpecRequirement("CurlSurface:connection-refused-signalrs-1")]
    [DataRow("signalr")]
    [DataRow("signalrs")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
