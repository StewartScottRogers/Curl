using System.Net;
using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the two ways a <see cref="DatagramOpenResult" /> may be built, and the invariant
/// that a channel is present exactly when the exit code is <see cref="CurlExitCode.Ok" />.
/// </summary>
[TestClass]
public sealed class DatagramOpenResultTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Opened_WithNullChannel_ThrowsArgumentNullException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        IDatagramChannel? channel = null;
        diagnostics.Arrange("channel", "null");

        var exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => DatagramOpenResult.Opened(channel!));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "channel", exception.ParamName);
        Assert.AreEqual("channel", exception.ParamName);
    }

    [TestMethod]
    public void Opened_WithChannel_ExposesItWithOkAndNoMessage()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var channel = new UnusedDatagramChannel();
        diagnostics.Arrange("channel", nameof(UnusedDatagramChannel));

        var result = DatagramOpenResult.Opened(channel);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage ?? "null");
        diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(channel, result.Channel);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
    }

    [TestMethod]
    public void Failed_WithOk_ThrowsArgumentOutOfRangeException()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.Ok);

        var exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => DatagramOpenResult.Failed(CurlExitCode.Ok, "unused"));

        diagnostics.Act("exception parameter", exception.ParamName);
        diagnostics.Assert("parameter name", "exitCode", exception.ParamName);
        Assert.AreEqual("exitCode", exception.ParamName);
    }

    [TestMethod]
    public void Failed_WithCouldntResolveHost_ExposesCodeAndMessageWithNoChannel()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.CouldntResolveHost);
        diagnostics.Arrange("host", "nonexistent.invalid");

        var result = DatagramOpenResult.Failed(
            CurlExitCode.CouldntResolveHost,
            "Could not resolve host: nonexistent.invalid");

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Assert("error message", "Could not resolve host: nonexistent.invalid", result.ErrorMessage);
        Assert.IsNull(result.Channel);
        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: nonexistent.invalid", result.ErrorMessage);
    }

    [TestMethod]
    public void Opened_WithoutNumber_HasConnectionNumberZero()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("channel", nameof(UnusedDatagramChannel));

        var result = DatagramOpenResult.Opened(new UnusedDatagramChannel());

        diagnostics.Act("connection number", result.ConnectionNumber);
        diagnostics.Assert("connection number", 0L, result.ConnectionNumber);
        Assert.AreEqual(0L, result.ConnectionNumber);
    }

    [TestMethod]
    public void WithConnectionNumber_OnOpened_KeepsChannelAndCarriesNumber()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        var channel = new UnusedDatagramChannel();
        diagnostics.Arrange("connection number", 3);

        var result = DatagramOpenResult.Opened(channel).WithConnectionNumber(3);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("connection number", result.ConnectionNumber);
        diagnostics.Assert("connection number", 3L, result.ConnectionNumber);
        Assert.AreSame(channel, result.Channel);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsNull(result.ErrorMessage);
        Assert.AreEqual(3L, result.ConnectionNumber);
    }

    [TestMethod]
    public void WithConnectionNumber_OnFailed_KeepsCodeAndMessageAndCarriesNumber()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("exit code", CurlExitCode.CouldntConnect);
        diagnostics.Arrange("connection number", 2);

        var result = DatagramOpenResult.Failed(CurlExitCode.CouldntConnect, "refused").WithConnectionNumber(2);

        diagnostics.Act("exit code", result.ExitCode);
        diagnostics.Act("error message", result.ErrorMessage);
        diagnostics.Act("connection number", result.ConnectionNumber);
        diagnostics.Assert("connection number", 2L, result.ConnectionNumber);
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
