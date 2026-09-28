namespace Curl.Networking;

/// <summary>
/// <see cref="SystemClientCertificateStore" /> against the machine's own stores. Only
/// whether a store opens is asserted, never what is in it.
/// </summary>
[TestClass]
public sealed class SystemClientCertificateStoreTests
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(nameof(ClientCertificateStoreLocation.CurrentUser))]
    [DataRow(nameof(ClientCertificateStoreLocation.LocalMachine))]
    public void OpenCertificates_WithTheMyStoreOnWindows_ReturnsItsCertificates(string location)
    {
        Assert.IsNotNull(new SystemClientCertificateStore().OpenCertificates(Enum.Parse<ClientCertificateStoreLocation>(location), "MY"));
    }

    [TestMethod]
    public void OpenCertificates_WithAStoreThatDoesNotExist_ReturnsNull()
    {
        var storeName = "NoSuchStore" + Guid.NewGuid().ToString("N");

        Assert.IsNull(new SystemClientCertificateStore().OpenCertificates(ClientCertificateStoreLocation.CurrentUser, storeName));
    }

    [TestMethod]
    [DataRow(nameof(ClientCertificateStoreLocation.CurrentService))]
    [DataRow(nameof(ClientCertificateStoreLocation.Services))]
    [DataRow(nameof(ClientCertificateStoreLocation.Users))]
    [DataRow(nameof(ClientCertificateStoreLocation.CurrentUserGroupPolicy))]
    [DataRow(nameof(ClientCertificateStoreLocation.LocalMachineGroupPolicy))]
    [DataRow(nameof(ClientCertificateStoreLocation.LocalMachineEnterprise))]
    public void OpenCertificates_WithALocationX509StoreCannotReach_ReturnsNull(string location)
    {
        Assert.IsNull(new SystemClientCertificateStore().OpenCertificates(Enum.Parse<ClientCertificateStoreLocation>(location), "MY"));
    }
}
