using Curl.Conformance.Extended.Harness;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

/// <summary>curl's URL errors (manual, "EXIT CODES" 3, 6 and 7) for <c>socketio</c>, <c>socketios</c>.</summary>
[TestClass]
public sealed class SocketIoUrlErrorCases
{
    internal static UrlErrorScheme For(string scheme) => scheme switch
    {
        "socketio" => new(scheme),
        _ => new(scheme),
    };

    [TestMethod]
    [SpecRequirement("CurlSurface:url-bad-port-socketio-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-socketio-1")]
    [SpecRequirement("CurlSurface:url-space-socketio-1")]
    [SpecRequirement("CurlSurface:url-bad-port-socketios-1")]
    [SpecRequirement("CurlSurface:url-bad-percent-encoding-socketios-1")]
    [SpecRequirement("CurlSurface:url-space-socketios-1")]
    [DataRow("socketio", UrlErrorChecks.BadPort)]
    [DataRow("socketio", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("socketio", UrlErrorChecks.Space)]
    [DataRow("socketios", UrlErrorChecks.BadPort)]
    [DataRow("socketios", UrlErrorChecks.BadPercentEncoding)]
    [DataRow("socketios", UrlErrorChecks.Space)]
    public Task MalformedUrl_EndsWithExit3AndCurlsReason(string scheme, string check) => UrlErrorChecks.MalformedAsync(For(scheme), check);

    [TestMethod]
    [SpecRequirement("CurlSurface:unresolvable-host-socketio-1")]
    [SpecRequirement("CurlSurface:unresolvable-host-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task UnresolvableHost_EndsWithExit6(string scheme) => UrlErrorChecks.UnresolvableHostAsync(For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:connection-refused-socketio-1")]
    [SpecRequirement("CurlSurface:connection-refused-socketios-1")]
    [DataRow("socketio")]
    [DataRow("socketios")]
    public Task ConnectionRefused_EndsWithExit7(string scheme) => UrlErrorChecks.ConnectionRefusedAsync(For(scheme));
}
