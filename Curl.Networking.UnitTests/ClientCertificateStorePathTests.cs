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
        var storePath = ClientCertificateStorePath.Parse(path);

        Assert.AreEqual(new ClientCertificateStorePath(Enum.Parse<ClientCertificateStoreLocation>(expectedLocation), expectedStoreName, Thumbprint), storePath);
    }

    [TestMethod]
    public void Parse_WithAThumbprintThatIsNotHex_KeepsItForTheLoaderToRefuse()
    {
        var thumbprint = new string('z', 40);

        var storePath = ClientCertificateStorePath.Parse(@"CurrentUser\MY\" + thumbprint);

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
        Assert.IsNull(ClientCertificateStorePath.Parse(path));
    }
}
