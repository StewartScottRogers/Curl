using Curl.Api.Callbacks;

namespace Curl.Engine;

/// <summary>Pins when an ioctl-rewound upload reports it was read, which decides whether the ioctl callback is called (AF-0813).</summary>
[TestClass]
public sealed class IoctlRewoundCallbackReadStreamTests
{
    [TestMethod]
    public void HasBeenRead_NothingReadYet_IsFalse()
    {
        using IoctlRewoundCallbackReadStream stream = new(_ => 0, _ => IoctlResult.Ok);

        Assert.IsFalse(stream.HasBeenRead);
    }

    [TestMethod]
    public void HasBeenRead_AfterBytesRead_IsTrue()
    {
        using IoctlRewoundCallbackReadStream stream = new(buffer => { buffer[0] = 1; return 1; }, _ => IoctlResult.Ok);

        _ = stream.Read(new byte[4], 0, 4);

        Assert.IsTrue(stream.HasBeenRead);
    }

    [TestMethod]
    public void RewindToStart_AfterARead_IsOkAndHasBeenReadIsFalseAgain()
    {
        using IoctlRewoundCallbackReadStream stream = new(buffer => { buffer[0] = 1; return 1; }, _ => IoctlResult.Ok);
        _ = stream.Read(new byte[4], 0, 4);

        string? failure = stream.RewindToStart();

        Assert.IsNull(failure);
        Assert.IsFalse(stream.HasBeenRead);
    }
}
