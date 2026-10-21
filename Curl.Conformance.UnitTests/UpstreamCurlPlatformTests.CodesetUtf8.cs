using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamCurlPlatformTests
{
    // Curl reads URLs, mail addresses and -w text as UTF-8 whatever the locale, so the harness stands in
    // for the UTF-8 locale runtests.pl derives codeset-utf8 from, on every platform (BL-2861).
    [TestMethod]
    public void EveryPlatform_ReportsCodesetUtf8()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platforms", UpstreamCurlPlatform.All.Count);

        string[] lacking = [.. UpstreamCurlPlatform.All.Where(platform => !platform.Features.Contains("codeset-utf8")).Select(platform => platform.OperatingSystemName)];

        diagnostics.Act("platforms lacking codeset-utf8", string.Join(", ", lacking));
        diagnostics.Assert("platforms lacking codeset-utf8", string.Empty, string.Join(", ", lacking));
        Assert.IsEmpty(lacking);
    }
}
