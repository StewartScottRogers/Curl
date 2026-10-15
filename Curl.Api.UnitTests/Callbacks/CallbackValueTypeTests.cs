using Curl.Api.Callbacks;

namespace Curl.Api.UnitTests.Callbacks;

[TestClass]
public sealed class CallbackValueTypeTests
{
    [TestMethod]
    public void CurlSocket_Bad_IsMinusOne()
    {
        Assert.AreEqual((nint)(-1), CurlSocket.Bad.Handle);
    }

    [TestMethod]
    public void CurlSocket_SameHandle_IsEqual()
    {
        Assert.AreEqual(new CurlSocket(7), new CurlSocket(7));
        Assert.AreNotEqual(CurlSocket.Bad, new CurlSocket(7));
    }

    [TestMethod]
    public void CurlSocketAddress_KeepsItsFields()
    {
        byte[] bytes = [2, 0, 0, 80];
        var address = new CurlSocketAddress(Family: 2, SocketType: 1, Protocol: 6, Address: bytes);

        Assert.AreEqual(2, address.Family);
        Assert.AreEqual(1, address.SocketType);
        Assert.AreEqual(6, address.Protocol);
        CollectionAssert.AreEqual(bytes, address.Address.ToArray());
    }

    [TestMethod]
    public void HstsEntry_KeepsItsFields()
    {
        var entry = new HstsEntry("example.com", IncludeSubDomains: true, Expire: "20301231 23:59:59");

        Assert.AreEqual("example.com", entry.Name);
        Assert.IsTrue(entry.IncludeSubDomains);
        Assert.AreEqual("20301231 23:59:59", entry.Expire);
    }

    [TestMethod]
    public void SshKey_KeepsItsFields()
    {
        byte[] bytes = [1, 2, 3];
        var key = new SshKey(bytes, SshKeyType.Ed25519);

        CollectionAssert.AreEqual(bytes, key.Key.ToArray());
        Assert.AreEqual(SshKeyType.Ed25519, key.KeyType);
    }

    [TestMethod]
    public void CurlFileInfo_New_UnlistedFieldsAreNullAndTypeUnknown()
    {
        var file = new CurlFileInfo { FileName = "a.txt" };

        Assert.AreEqual("a.txt", file.FileName);
        Assert.AreEqual(CurlFileType.Unknown, file.FileType);
        Assert.IsNull(file.Time);
        Assert.IsNull(file.Permissions);
        Assert.IsNull(file.UserId);
        Assert.IsNull(file.GroupId);
        Assert.IsNull(file.Size);
        Assert.IsNull(file.HardLinks);
        Assert.IsNull(file.TimeText);
        Assert.IsNull(file.PermissionsText);
        Assert.IsNull(file.User);
        Assert.IsNull(file.Group);
        Assert.IsNull(file.Target);
    }
}
