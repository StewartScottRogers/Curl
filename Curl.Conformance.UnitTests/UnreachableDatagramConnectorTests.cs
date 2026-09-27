using Curl.Protocol.Abstractions;

namespace Curl.Conformance;

/// <summary>Pins that <see cref="UnreachableDatagramConnector"/> fails every open without a socket.</summary>
[TestClass]
public sealed class UnreachableDatagramConnectorTests
{
    [TestMethod]
    public async Task OpenAsync_FailsWithCouldntConnect()
    {
        DatagramOpenResult result = await new UnreachableDatagramConnector().OpenAsync("127.0.0.1", 69, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(UnreachableDatagramConnector.Message, result.ErrorMessage);
    }
}
