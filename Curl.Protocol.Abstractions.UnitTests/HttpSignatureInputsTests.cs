using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins that <see cref="HttpSignatureSettings" /> and <see cref="HttpSignatureInputs" /> carry what
/// the <c>--httpsig-*</c> options sign, and that neither an <see cref="HttpRequestOptions" /> nor an
/// <see cref="HttpAuthRequest" /> carries any unless given (BL-2182).
/// </summary>
[TestClass]
public sealed class HttpSignatureInputsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Constructor_RoundTripsTheSettingsAuthorityAndHeaders()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        string[] headers = ["Content-Type: text/plain"];
        HttpSignatureSettings settings = new("hmac-sha256", "0011", "k1", "@method");
        diagnostics.Arrange("settings", settings);

        HttpSignatureInputs inputs = new(settings, "example.com:8000", headers);

        diagnostics.Act("inputs", inputs);
        diagnostics.Assert("authority", "example.com:8000", inputs.Authority);
        Assert.AreSame(settings, inputs.Settings);
        Assert.AreEqual("example.com:8000", inputs.Authority);
        Assert.AreSame(headers, inputs.CustomHeaders);
        Assert.AreEqual("hmac-sha256", settings.Algorithm);
        Assert.AreEqual("0011", settings.Key);
        Assert.AreEqual("k1", settings.KeyId);
        Assert.AreEqual("@method", settings.Components);
    }

    [TestMethod]
    public void Defaults_CarryNoSignature()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        HttpAuthRequest request = new("GET", CurlUrl.Parse("http://example.com/"), "/", null, null, HttpAuthSchemes.Basic, IsProxy: false);
        diagnostics.Arrange("request", request);

        HttpRequestOptions options = new();

        diagnostics.Act("options signature", options.HttpSignature);
        diagnostics.Assert("request signature", "(null)", request.HttpSignature?.ToString() ?? "(null)");
        Assert.IsNull(options.HttpSignature);
        Assert.IsNull(request.HttpSignature);
    }
}
