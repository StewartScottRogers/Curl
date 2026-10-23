using System.Security.Authentication;
using Curl.Conformance.Differential.Tls;

namespace Curl.Conformance.Differential.UnitTests.Tls;

[TestClass]
public sealed class DifferentialTlsEndpointsTests
{
    [TestMethod]
    public void All_RunnerPortsTable_ServesEachPlaceholderOnItsPort()
    {
        var ports = DifferentialTlsEndpoints.All.ToDictionary(endpoint => endpoint.Placeholder, endpoint => endpoint.Port);

        CollectionAssert.AreEquivalent(
            new Dictionary<string, int> { ["T"] = 18100, ["T12"] = 18110, ["T13"] = 18120, ["MTLS"] = 18130, ["WRONG"] = 18140, ["EXP"] = 18150, ["RSA"] = 18160 },
            ports);
    }

    [TestMethod]
    public void All_TlsServerSettings_MatchTheRunnerEnvironment()
    {
        var byPlaceholder = DifferentialTlsEndpoints.All.ToDictionary(endpoint => endpoint.Placeholder);

        Assert.AreEqual(SslProtocols.Tls12, byPlaceholder["T12"].EnabledProtocols);
        Assert.AreEqual(SslProtocols.Tls13, byPlaceholder["T13"].EnabledProtocols);
        Assert.AreEqual(SslProtocols.Tls12 | SslProtocols.Tls13, byPlaceholder["T"].EnabledProtocols);
        Assert.IsTrue(byPlaceholder["MTLS"].RequiresClientCertificate);
        Assert.IsFalse(byPlaceholder["T"].RequiresClientCertificate);
        Assert.AreEqual("wrong", byPlaceholder["WRONG"].CertificateName);
        Assert.AreEqual("expired", byPlaceholder["EXP"].CertificateName);
        Assert.AreEqual("rsa", byPlaceholder["RSA"].CertificateName);
        Assert.AreEqual("127.0.0.1:18130", byPlaceholder["MTLS"].Authority);
    }

    [TestMethod]
    public void Substitute_PlaceholdersAndPki_WritesAuthoritiesAndSlashedPki()
    {
        var differentialCase = new DifferentialCase(
            "tls", ["--cacert", "{PKI}/ca.pem", "https://{T}/echo", "https://{T12}/x", "{U}/echo"], null,
            new Dictionary<string, byte[]>(), TimeSpan.FromSeconds(1), ComparesStandardError: true, ComparesWire: false, []);

        var substituted = DifferentialTlsEndpoints.Substitute(differentialCase, @"dir\pki");

        CollectionAssert.AreEqual(
            new[] { "--cacert", "dir/pki/ca.pem", "https://127.0.0.1:18100/echo", "https://127.0.0.1:18110/x", "{U}/echo" },
            substituted.Arguments.ToArray());
        Assert.AreEqual("tls", substituted.Name);
    }

    [TestMethod]
    public void Substitute_NullArguments_Throw()
    {
        var differentialCase = new DifferentialCase("a", [], null, new Dictionary<string, byte[]>(), TimeSpan.Zero, true, true, []);

        Assert.ThrowsExactly<ArgumentNullException>(() => DifferentialTlsEndpoints.Substitute(null!, "pki"));
        Assert.ThrowsExactly<ArgumentNullException>(() => DifferentialTlsEndpoints.Substitute(differentialCase, null!));
    }

    [TestMethod]
    public void ReadEmbedded_CliTlsCases_ReadsEveryTesterCase()
    {
        var cases = DifferentialCaseFileReader.ReadEmbedded("CliTls/CliTlsCases.json");

        Assert.HasCount(70, cases);
        Assert.IsTrue(cases.All(differentialCase => !differentialCase.ComparesWire));
        Assert.AreEqual(3_000_000, cases.Single(differentialCase => differentialCase.Name == "tls-bigpost").Files["big.bin"].Length);
    }
}
