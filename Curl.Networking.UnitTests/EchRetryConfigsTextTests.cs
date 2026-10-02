using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// The lines curl 8.21.0's OpenSSL ECH build writes for a server's <c>retry_configs</c>,
/// measured with OpenSSL 4.0.0 (BL-1171).
/// </summary>
[TestClass]
public sealed class EchRetryConfigsTextTests
{
    [TestMethod]
    public void Rejected_WithoutRetryConfigs_IsNoRetryConfigs()
    {
        CollectionAssert.AreEqual(new[] { "ECH: no retry_configs (rv = 1)" }, EchRetryConfigsText.Rejected(null, "curl.test", "public.test").ToArray());
    }

    [TestMethod]
    public void Rejected_WithRetryConfigs_IsTheListThenTheNamesReasonAndStatus()
    {
        var retryConfigs = Configs("other.test");

        CollectionAssert.AreEqual(
            new[]
            {
                "ECH: retry_configs " + Convert.ToBase64String(retryConfigs.Encoded),
                "ECH: retry_configs for curl.test from public.test, 424 -106",
            },
            EchRetryConfigsText.Rejected(retryConfigs, "curl.test", "public.test").ToArray());
    }

    [TestMethod]
    public void Grease_WithoutRetryConfigs_IsNothing()
    {
        Assert.IsEmpty(EchRetryConfigsText.Grease(null));
    }

    [TestMethod]
    public void Grease_WithRetryConfigs_IsTheListThenNullNames()
    {
        var retryConfigs = Configs("other.test");

        CollectionAssert.AreEqual(
            new[]
            {
                "ECH: retry_configs " + Convert.ToBase64String(retryConfigs.Encoded),
                "ECH: retry_configs for NULL from NULL, 0 3",
            },
            EchRetryConfigsText.Grease(retryConfigs).ToArray());
    }

    private static EchConfigList Configs(string publicName) =>
        EchConfigList.Decode(HandBuiltTlsProviderTests.EchConfigListBytes(publicName)).Value!;
}
