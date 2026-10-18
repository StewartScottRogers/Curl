using Curl.Api.Callbacks;
using Curl.Api.Codes;

namespace Curl.Api.UnitTests.Callbacks;

[TestClass]
public sealed class EasyCallbackOptionsTests
{
    private static readonly EasyCallbackOptions Defaults = new();

    [TestMethod]
    public void New_DataCallbacks_DefaultToNull()
    {
        Assert.IsNull(Defaults.Write);
        Assert.IsNull(Defaults.Read);
        Assert.IsNull(Defaults.Header);
        Assert.IsNull(Defaults.Interleave);
        Assert.IsNull(Defaults.Trailer);
        Assert.IsNull(Defaults.Seek);
        Assert.IsNull(Defaults.Ioctl);
    }

    [TestMethod]
    public void New_ProgressAndDebugCallbacks_DefaultToNull()
    {
        Assert.IsNull(Defaults.Progress);
        Assert.IsNull(Defaults.TransferInfo);
        Assert.IsNull(Defaults.Debug);
    }

    [TestMethod]
    public void New_ConnectionCallbacks_DefaultToNull()
    {
        Assert.IsNull(Defaults.SocketOption);
        Assert.IsNull(Defaults.OpenSocket);
        Assert.IsNull(Defaults.CloseSocket);
        Assert.IsNull(Defaults.Prerequest);
        Assert.IsNull(Defaults.ResolverStart);
    }

    [TestMethod]
    public void New_WildcardCallbacks_DefaultToNull()
    {
        Assert.IsNull(Defaults.ChunkBegin);
        Assert.IsNull(Defaults.ChunkEnd);
        Assert.IsNull(Defaults.FileNameMatch);
    }

    [TestMethod]
    public void New_SecurityCallbacks_DefaultToNull()
    {
        Assert.IsNull(Defaults.HstsRead);
        Assert.IsNull(Defaults.HstsWrite);
        Assert.IsNull(Defaults.SshKey);
        Assert.IsNull(Defaults.SshHostKey);
        Assert.IsNull(Defaults.SslContext);
    }

    [TestMethod]
    public void Init_WriteCallback_IsCalledWithTheSpan()
    {
        var options = Defaults with { Write = data => data.Length };

        Assert.AreEqual(3L, options.Write!([1, 2, 3]));
    }

    [TestMethod]
    public void Init_HstsReadCallback_HandsOverItsEntry()
    {
        var entry = new HstsEntry("example.com", IncludeSubDomains: true, Expire: null);
        var options = Defaults with
        {
            HstsRead = (out HstsEntry? handed) =>
            {
                handed = entry;
                return HstsResult.Ok;
            },
        };

        Assert.AreEqual(HstsResult.Ok, options.HstsRead!(out var received));
        Assert.AreSame(entry, received);
    }

    [TestMethod]
    public void Init_SslContextCallback_ReturnsItsCode()
    {
        var options = Defaults with { SslContext = _ => CurlCode.Ok };

        Assert.AreEqual(CurlCode.Ok, options.SslContext!(0));
    }
}
