using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// <see cref="ClientCertificateStorePath" />: each case is a <c>--cert</c> value measured
/// 2026-09-27 against curl 8.21.0's Schannel build on Windows, which reads a value that is
/// not a store path as a file.
/// </summary>
[TestClass]
public sealed class ClientCertificateStorePathTests
{
    private const string Thumbprint = "1AE11B120B6AA2E1EAB4A3B13C9B47F7DC578FB0";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(@"CurrentUser\MY\" + Thumbprint, nameof(ClientCertificateStoreLocation.CurrentUser), "MY")]
    [DataRow(@"CurrentUser\my\" + Thumbprint, nameof(ClientCertificateStoreLocation.CurrentUser), "my")]
    [DataRow(@"Current\MY\" + Thumbprint, nameof(ClientCertificateStoreLocation.CurrentUser), "MY")]
    [DataRow(@"\MY\" + Thumbprint, nameof(ClientCertificateStoreLocation.CurrentUser), "MY")]
    [DataRow(@"LocalMachine\Root\" + Thumbprint, nameof(ClientCertificateStoreLocation.LocalMachine), "Root")]
    [DataRow(@"CurrentService\MY\" + Thumbprint, nameof(ClientCertificateStoreLocation.CurrentService), "MY")]
    [DataRow(@"Services\MY\" + Thumbprint, nameof(ClientCertificateStoreLocation.Services), "MY")]
    [DataRow(@"Users\MY\" + Thumbprint, nameof(ClientCertificateStoreLocation.Users), "MY")]
    [DataRow(@"CurrentUserGroupPolicy\MY\" + Thumbprint, nameof(ClientCertificateStoreLocation.CurrentUserGroupPolicy), "MY")]
    [DataRow(@"LocalMachineGroupPolicy\MY\" + Thumbprint, nameof(ClientCertificateStoreLocation.LocalMachineGroupPolicy), "MY")]
    [DataRow(@"LocalMachineEnterprise\MY\" + Thumbprint, nameof(ClientCertificateStoreLocation.LocalMachineEnterprise), "MY")]
    [DataRow(@"CurrentUser\\" + Thumbprint, nameof(ClientCertificateStoreLocation.CurrentUser), "")]
    public void Parse_WithAStorePath_ReturnsItsLocationStoreNameAndThumbprint(
        string path,
        string expectedLocation,
        string expectedStoreName)
    {
        Diagnostics.Arrange("path", path);
        Diagnostics.Arrange("expected location", expectedLocation);
        Diagnostics.Arrange("expected store name", expectedStoreName);
        var storePath = ClientCertificateStorePath.Parse(path);
        Diagnostics.Act("store path", storePath);
        var expected = new ClientCertificateStorePath(Enum.Parse<ClientCertificateStoreLocation>(expectedLocation), expectedStoreName, Thumbprint);
        Diagnostics.Assert("store path", expected, storePath);

        Assert.AreEqual(new ClientCertificateStorePath(Enum.Parse<ClientCertificateStoreLocation>(expectedLocation), expectedStoreName, Thumbprint), storePath);
    }

    [TestMethod]
    public void Parse_WithAThumbprintThatIsNotHex_KeepsItForTheLoaderToRefuse()
    {
        var thumbprint = new string('z', 40);
        Diagnostics.Arrange("thumbprint", thumbprint);

        var storePath = ClientCertificateStorePath.Parse(@"CurrentUser\MY\" + thumbprint);
        Diagnostics.Act("parsed thumbprint", storePath?.Thumbprint);
        Diagnostics.Assert("parsed thumbprint", thumbprint, storePath?.Thumbprint);

        Assert.AreEqual(thumbprint, storePath?.Thumbprint);
    }

    [TestMethod]
    [DataRow(@"currentuser\MY\" + Thumbprint)]
    [DataRow(@"Bogus\MY\" + Thumbprint)]
    [DataRow(@"CurrentUserX\MY\" + Thumbprint)]
    [DataRow(@"CurrentUser\MY\abc")]
    [DataRow(@"CurrentUser\MY\" + Thumbprint + "0")]
    [DataRow(@"CurrentUser\" + Thumbprint)]
    [DataRow("client.p12")]
    [DataRow("")]
    public void Parse_WithAPathThatIsNotAStorePath_ReturnsNull(string path)
    {
        Diagnostics.Arrange("path", path);
        var storePath = ClientCertificateStorePath.Parse(path);
        Diagnostics.Act("store path is null", storePath is null);
        Diagnostics.Assert("store path is null", true, storePath is null);
        Assert.IsNull(ClientCertificateStorePath.Parse(path));
    }
}
