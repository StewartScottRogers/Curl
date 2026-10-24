namespace Curl.Conformance.Differential.UnitTests.Protocols.Proxy;

[TestClass]
public sealed class ProxyCaseFilesTests
{
    private const string CaseFolder = "CliProtocols/Proxy/";

    private const string ResourcePrefix = "Curl.Conformance.Differential.Cases.CliProtocols.Proxy.";

    [TestMethod]
    public void ReadEmbedded_EveryProxyCaseFile_HoldsOneCaseNamedAfterItsFile()
    {
        var fileNames = ProxyCaseFileNames();

        Assert.HasCount(35, fileNames);
        foreach (var fileName in fileNames)
        {
            var cases = DifferentialCaseFileReader.ReadEmbedded(CaseFolder + fileName);

            Assert.HasCount(1, cases, fileName);
            Assert.AreEqual(fileName[..^".json".Length], cases[0].Name);
            Assert.IsTrue(cases[0].RunsAgainstReferenceBuild, fileName);
        }
    }

    [TestMethod]
    public void ReadEmbedded_ProxyTunnelHttpsCase_ExpandsTheProxyAndCaArgumentsAsTheSourceDid()
    {
        var differentialCase = DifferentialCaseFileReader.ReadEmbedded(CaseFolder + "px-proxy-tunnel-https.json").Single();

        CollectionAssert.AreEqual(
            new[] { "-sS", "-x", "http://127.0.0.1:18300", "--cacert", "{PKI}/ca.pem", "--ssl-no-revoke", "https://127.0.0.1:18100/echo" },
            differentialCase.Arguments.ToArray());
    }

    [TestMethod]
    public void ReadEmbedded_ProxyHeaderAndEmptyProxyCases_SplitQuotedArgumentsAsTheSourceDid()
    {
        var header = DifferentialCaseFileReader.ReadEmbedded(CaseFolder + "px-proxy-header.json").Single();
        var empty = DifferentialCaseFileReader.ReadEmbedded(CaseFolder + "px-proxy-empty.json").Single();

        CollectionAssert.AreEqual(
            new[] { "-s", "-x", "http://127.0.0.1:18300", "--proxy-header", "X-P: 1", "-H", "X-U: 2", "http://example.test/x" },
            header.Arguments.ToArray());
        CollectionAssert.AreEqual(new[] { "-s", "-x", string.Empty, "{U}/echo" }, empty.Arguments.ToArray());
    }

    private static string[] ProxyCaseFileNames() =>
        [.. typeof(DifferentialCaseFileReader).Assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            .Select(name => name[ResourcePrefix.Length..])
            .Order(StringComparer.Ordinal)];
}
