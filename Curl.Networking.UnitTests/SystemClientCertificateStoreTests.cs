using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// <see cref="SystemClientCertificateStore" /> against the machine's own stores. Only
/// whether a store opens is asserted, never what is in it.
/// </summary>
[TestClass]
public sealed class SystemClientCertificateStoreTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(nameof(ClientCertificateStoreLocation.CurrentUser))]
    [DataRow(nameof(ClientCertificateStoreLocation.LocalMachine))]
    public void OpenCertificates_WithTheMyStoreOnWindows_ReturnsItsCertificates(string location)
    {
        Diagnostics.Arrange("location, store", $"{location}, MY");

        var certificates = new SystemClientCertificateStore().OpenCertificates(Enum.Parse<ClientCertificateStoreLocation>(location), "MY");

        Diagnostics.Act("store opened", certificates is not null);
        Diagnostics.Assert("store opened", true, certificates is not null);

        Assert.IsNotNull(certificates);
    }

    [TestMethod]
    public void OpenCertificates_WithAStoreThatDoesNotExist_ReturnsNull()
    {
        var storeName = "NoSuchStore" + Guid.NewGuid().ToString("N");

        Diagnostics.Arrange("location, store", "CurrentUser, NoSuchStore<random>");

        var certificates = new SystemClientCertificateStore().OpenCertificates(ClientCertificateStoreLocation.CurrentUser, storeName);

        Diagnostics.Act("store opened", certificates is not null);
        Diagnostics.Assert("store opened", false, certificates is not null);

        Assert.IsNull(certificates);
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
        Diagnostics.Arrange("location, store", $"{location}, MY");

        var certificates = new SystemClientCertificateStore().OpenCertificates(Enum.Parse<ClientCertificateStoreLocation>(location), "MY");

        Diagnostics.Act("store opened", certificates is not null);
        Diagnostics.Assert("store opened", false, certificates is not null);

        Assert.IsNull(certificates);
    }
}
