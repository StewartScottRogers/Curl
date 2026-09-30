namespace Curl.Networking;

[TestClass]
public sealed class Socks5AuthenticationOptionsTests
{
    [TestMethod]
    public void Default_AllowsBothMethodsAndAsksForRcmd()
    {
        var options = Socks5AuthenticationOptions.Default;

        Assert.AreEqual((true, true, "rcmd", false), (options.AllowUserNameAndPassword, options.AllowGssapi, options.GssapiServiceName, options.GssapiNec));
        Assert.IsNull(options.SecurityContexts);
        Assert.AreEqual(OperatingSystem.IsWindows(), options.UsesSspiTexts);
        Assert.StartsWith(Environment.GetEnvironmentVariable("KRB5CCNAME") is { Length: > 0 } ? string.Empty : "FILE:/tmp/krb5cc_", options.CredentialCacheName);
    }

    [TestMethod]
    public void DefaultCredentialCacheName_WithKrb5ccname_IsItAsItIs()
    {
        var name = Socks5AuthenticationOptions.DefaultCredentialCacheName(_ => "DIR:/x", _ => throw new AssertFailedException("not read"));

        Assert.AreEqual("DIR:/x", name);
    }

    [TestMethod]
    [DataRow(null, "FILE:/tmp/krb5cc_0", DisplayName = "No /proc/self/status")]
    [DataRow("Name:\tcurl\nUid:\t1000\t1000\t1000\t1000\nGid:\t1000\n", "FILE:/tmp/krb5cc_1000", DisplayName = "Uid line")]
    [DataRow("Name:\tcurl\n", "FILE:/tmp/krb5cc_0", DisplayName = "No Uid line")]
    [DataRow("Uid:\n", "FILE:/tmp/krb5cc_0", DisplayName = "Empty Uid line")]
    public void DefaultCredentialCacheName_WithoutKrb5ccname_IsTheUsersTmpFile(string? status, string expected)
    {
        var name = Socks5AuthenticationOptions.DefaultCredentialCacheName(_ => "", path => path == "/proc/self/status" ? status : null);

        Assert.AreEqual(expected, name);
    }

    [TestMethod]
    public void DefaultCredentialCacheName_WithNullArguments_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Socks5AuthenticationOptions.DefaultCredentialCacheName(null!, _ => null));
        Assert.ThrowsExactly<ArgumentNullException>(() => Socks5AuthenticationOptions.DefaultCredentialCacheName(_ => null, null!));
    }
}
