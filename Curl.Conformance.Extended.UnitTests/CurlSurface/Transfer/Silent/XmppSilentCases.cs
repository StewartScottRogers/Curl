using Curl.Conformance.Extended.Harness;
using Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.UrlErrors;

namespace Curl.Conformance.Extended.UnitTests.CurlSurface.Transfer.Silent;

/// <summary>curl's <c>-s</c> and <c>-S</c> (manual, "-s" and "-S") for <c>xmpp</c>, <c>xmpps</c>.</summary>
[TestClass]
public sealed class XmppSilentCases
{
    [TestMethod]
    [SpecRequirement("CurlSurface:silent-xmpp-1")]
    [SpecRequirement("CurlSurface:silent-xmpps-1")]
    [DataRow("xmpp")]
    [DataRow("xmpps")]
    public Task Silent_MalformedUrl_WritesNothingAndEndsWithExit3(string scheme) => SilentChecks.SilentMalformedAsync(XmppUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:show-error-xmpp-1")]
    [SpecRequirement("CurlSurface:show-error-xmpps-1")]
    [DataRow("xmpp")]
    [DataRow("xmpps")]
    public Task ShowError_MalformedUrl_WritesOnlyTheErrorMessageAndEndsWithExit3(string scheme) => SilentChecks.ShowErrorMalformedAsync(XmppUrlErrorCases.For(scheme));

    [TestMethod]
    [SpecRequirement("CurlSurface:silent-refused-xmpp-1")]
    [SpecRequirement("CurlSurface:silent-refused-xmpps-1")]
    [DataRow("xmpp")]
    [DataRow("xmpps")]
    public Task Silent_ConnectionRefused_WritesNothingAndEndsWithExit7(string scheme) => SilentChecks.SilentRefusedAsync(XmppUrlErrorCases.For(scheme));
}
