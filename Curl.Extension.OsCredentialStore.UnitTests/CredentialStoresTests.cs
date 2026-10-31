namespace Curl.Extension.OsCredentialStore.UnitTests;

[TestClass]
public sealed class CredentialStoresTests
{
    [TestMethod]
    public void PlatformOf_EachOs_NamesItsStore()
    {
        Assert.AreEqual(CredentialStorePlatform.Windows, CredentialStores.PlatformOf(isWindows: true, isMacOs: false));
        Assert.AreEqual(CredentialStorePlatform.MacOs, CredentialStores.PlatformOf(isWindows: false, isMacOs: true));
        Assert.AreEqual(CredentialStorePlatform.Libsecret, CredentialStores.PlatformOf(isWindows: false, isMacOs: false));
    }

    [TestMethod]
    public void For_EachPlatform_GivesItsStore()
    {
        Assert.IsInstanceOfType<WindowsCredentialManagerStore>(CredentialStores.For(CredentialStorePlatform.Windows));
        Assert.IsInstanceOfType<MacKeychainStore>(CredentialStores.For(CredentialStorePlatform.MacOs));
        Assert.IsInstanceOfType<LibsecretStore>(CredentialStores.For(CredentialStorePlatform.Libsecret));
    }

    [TestMethod]
    public void ForThisPlatform_GivesTheRunningOsStore() =>
        Assert.AreEqual(
            CredentialStores.For(CredentialStores.PlatformOf(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS())).GetType(),
            CredentialStores.ForThisPlatform().GetType());
}
