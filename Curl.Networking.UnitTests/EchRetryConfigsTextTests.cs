using Curl.Testing;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The lines curl 8.21.0's OpenSSL ECH build writes for a server's <c>retry_configs</c>,
/// measured with OpenSSL 4.0.0 (BL-1171).
/// </summary>
[TestClass]
public sealed class EchRetryConfigsTextTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Rejected_WithoutRetryConfigs_IsNoRetryConfigs()
    {
        Diagnostics.Arrange("retry configs", "(none)");
        Diagnostics.Arrange("inner, outer", "curl.test, public.test");

        var lines = EchRetryConfigsText.Rejected(null, "curl.test", "public.test").ToArray();

        WriteLines(new[] { "ECH: no retry_configs (rv = 1)" }, lines);
        CollectionAssert.AreEqual(new[] { "ECH: no retry_configs (rv = 1)" }, lines);
    }

    [TestMethod]
    public void Rejected_WithRetryConfigs_IsTheListThenTheNamesReasonAndStatus()
    {
        var retryConfigs = Configs("other.test");
        Diagnostics.Bytes("retry configs", retryConfigs.Encoded);
        Diagnostics.Arrange("inner, outer", "curl.test, public.test");

        var lines = EchRetryConfigsText.Rejected(retryConfigs, "curl.test", "public.test").ToArray();

        var expected = new[]
        {
            "ECH: retry_configs " + Convert.ToBase64String(retryConfigs.Encoded),
            "ECH: retry_configs for curl.test from public.test, 424 -106",
        };
        WriteLines(expected, lines);
        CollectionAssert.AreEqual(expected, lines);
    }

    [TestMethod]
    public void Grease_WithoutRetryConfigs_IsNothing()
    {
        Diagnostics.Arrange("retry configs", "(none)");

        var lines = EchRetryConfigsText.Grease(null).ToArray();

        WriteLines([], lines);
        Assert.IsEmpty(lines);
    }

    [TestMethod]
    public void Grease_WithRetryConfigs_IsTheListThenNullNames()
    {
        var retryConfigs = Configs("other.test");
        Diagnostics.Arrange("retry configs public name", "other.test");
        Diagnostics.Bytes("retry configs", retryConfigs.Encoded);

        var lines = EchRetryConfigsText.Grease(retryConfigs).ToArray();

        var expected = new[]
        {
            "ECH: retry_configs " + Convert.ToBase64String(retryConfigs.Encoded),
            "ECH: retry_configs for NULL from NULL, 0 3",
        };
        WriteLines(expected, lines);
        CollectionAssert.AreEqual(expected, lines);
    }

    private void WriteLines(string[] expected, string[] lines)
    {
        Diagnostics.Act("lines", string.Join(" | ", lines));
        Diagnostics.Diff("lines", string.Join("\n", expected), string.Join("\n", lines));
    }

    private static EchConfigList Configs(string publicName) =>
        EchConfigList.Decode(HandBuiltTlsProviderTests.EchConfigListBytes(publicName)).Value!;
}
