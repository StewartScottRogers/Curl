using Curl.Testing;

namespace Curl.Networking;

[TestClass]
public sealed class Socks5AuthenticationOptionsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Default_AllowsBothMethodsAndAsksForRcmd()
    {
        Diagnostics.Arrange("options", "Default");

        var options = Socks5AuthenticationOptions.Default;

        var usesSspiTextsMatchesPlatform = options.UsesSspiTexts == OperatingSystem.IsWindows();
        var credentialCacheNameHasExpectedPrefix = options.CredentialCacheName.StartsWith(
            Environment.GetEnvironmentVariable("KRB5CCNAME") is { Length: > 0 } ? string.Empty : "FILE:/tmp/krb5cc_",
            StringComparison.Ordinal);
        Diagnostics.Act(
            "user name and password, GSS-API, service, NEC, security contexts",
            (options.AllowUserNameAndPassword, options.AllowGssapi, options.GssapiServiceName, options.GssapiNec, options.SecurityContexts is null));
        Diagnostics.Assert(
            "SSPI texts match the platform, credential cache name has the expected prefix",
            "True, True",
            $"{usesSspiTextsMatchesPlatform}, {credentialCacheNameHasExpectedPrefix}");

        Assert.AreEqual((true, true, "rcmd", false), (options.AllowUserNameAndPassword, options.AllowGssapi, options.GssapiServiceName, options.GssapiNec));
        Assert.IsNull(options.SecurityContexts);
        Assert.AreEqual(OperatingSystem.IsWindows(), options.UsesSspiTexts);
        Assert.StartsWith(Environment.GetEnvironmentVariable("KRB5CCNAME") is { Length: > 0 } ? string.Empty : "FILE:/tmp/krb5cc_", options.CredentialCacheName);
    }

    [TestMethod]
    public void DefaultCredentialCacheName_WithKrb5ccname_IsItAsItIs()
    {
        Diagnostics.Arrange("KRB5CCNAME", "DIR:/x");

        var name = Socks5AuthenticationOptions.DefaultCredentialCacheName(_ => "DIR:/x", _ => throw new AssertFailedException("not read"));

        Diagnostics.Act("credential cache name", name);
        Diagnostics.Assert("credential cache name", "DIR:/x", name);

        Assert.AreEqual("DIR:/x", name);
    }

    [TestMethod]
    [DataRow(null, "FILE:/tmp/krb5cc_0", DisplayName = "No /proc/self/status")]
    [DataRow("Name:\tcurl\nUid:\t1000\t1000\t1000\t1000\nGid:\t1000\n", "FILE:/tmp/krb5cc_1000", DisplayName = "Uid line")]
    [DataRow("Name:\tcurl\n", "FILE:/tmp/krb5cc_0", DisplayName = "No Uid line")]
    [DataRow("Uid:\n", "FILE:/tmp/krb5cc_0", DisplayName = "Empty Uid line")]
    public void DefaultCredentialCacheName_WithoutKrb5ccname_IsTheUsersTmpFile(string? status, string expected)
    {
        Diagnostics.Arrange("/proc/self/status", status?.ReplaceLineEndings("\\n").Replace("\t", "\\t", StringComparison.Ordinal) ?? "null");

        var name = Socks5AuthenticationOptions.DefaultCredentialCacheName(_ => "", path => path == "/proc/self/status" ? status : null);

        Diagnostics.Act("credential cache name", name);
        Diagnostics.Assert("credential cache name", expected, name);

        Assert.AreEqual(expected, name);
    }

    [TestMethod]
    public void DefaultCredentialCacheName_WithNullArguments_Throws()
    {
        Diagnostics.Arrange("null argument", "getEnvironmentVariable, then readFile");

        var first = Assert.ThrowsExactly<ArgumentNullException>(() => Socks5AuthenticationOptions.DefaultCredentialCacheName(null!, _ => null));
        var second = Assert.ThrowsExactly<ArgumentNullException>(() => Socks5AuthenticationOptions.DefaultCredentialCacheName(_ => null, null!));

        Diagnostics.Act("exceptions", $"{first.GetType().Name}, {second.GetType().Name}");
        Diagnostics.Assert("exceptions", "ArgumentNullException, ArgumentNullException", $"{first.GetType().Name}, {second.GetType().Name}");
    }
}
