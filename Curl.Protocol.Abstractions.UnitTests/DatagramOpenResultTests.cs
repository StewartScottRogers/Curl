using System.Net;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the two ways a <see cref="DatagramOpenResult" /> may be built, and the invariant
/// that a channel is present exactly when the exit code is <see cref="CurlExitCode.Ok" />.
/// </summary>
[TestClass]
public sealed class DatagramOpenResultTests
{
    [TestMethod]
    public void Opened_WithNullChannel_ThrowsArgumentNullException()
    {
        IDatagramChannel? channel = null;

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => DatagramOpenResult.Opened(channel!));

        Assert.AreEqual("channel", exception.ParamName);
    }

    [TestMethod]
    public void Opened_WithChannel_ExposesItWithOkAndNoMessage()
    {
        var channel = new UnusedDatagramChannel();

        var result = DatagramOpenResult.Opened(channel);

        Assert.AreSame(channel, result.Channel);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Failed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => DatagramOpenResult.Failed(CurlExitCode.Ok, "unused"));

        Assert.AreEqual("exitCode", exception.ParamName);
    }

    [TestMethod]
    public void Failed_WithCouldntResolveHost_ExposesCodeAndMessageWithNoChannel()
    {
        var result = DatagramOpenResult.Failed(
            CurlExitCode.CouldntResolveHost,
            "Could not resolve host: nonexistent.invalid");

        Assert.IsNull(result.Channel);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: nonexistent.invalid", result.ErrorMessage);
    }

    [TestMethod]
    public void Opened_WithoutNumber_HasConnectionNumberZero()
    {
        var result = DatagramOpenResult.Opened(new UnusedDatagramChannel());

        Assert.AreEqual(0L, result.ConnectionNumber);
    }

    [TestMethod]
    public void WithConnectionNumber_OnOpened_KeepsChannelAndCarriesNumber()
    {
        var channel = new UnusedDatagramChannel();

        var result = DatagramOpenResult.Opened(channel).WithConnectionNumber(3);

        Assert.AreSame(channel, result.Channel);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
        Assert.AreEqual(3L, result.ConnectionNumber);
    }

    [TestMethod]
    public void WithConnectionNumber_OnFailed_KeepsCodeAndMessageAndCarriesNumber()
    {
        var result = DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, "refused").WithConnectionNumber(2);

        Assert.IsNull(result.Channel);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("refused", result.ErrorMessage);
        Assert.AreEqual(2L, result.ConnectionNumber);
    }

    private sealed class UnusedDatagramChannel : IDatagramChannel
    {
        public EndPoint ServerEndPoint { get; } = new IPEndPoint(IPAddress.Loopback, 69);

        public ValueTask SendAsync(
            ReadOnlyMemory<byte> datagram,
            EndPoint destination,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask<DatagramReceived> ReceiveAsync(Memory<byte> buffer, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
