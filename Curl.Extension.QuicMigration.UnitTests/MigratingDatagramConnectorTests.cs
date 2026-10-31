using Curl.Protocol.Abstractions;

namespace Curl.Extension.QuicMigration;

/// <summary>Checks that <see cref="MigratingDatagramConnector" /> opens through curl's connector and wraps each channel it opens.</summary>
[TestClass]
public sealed class MigratingDatagramConnectorTests
{
    [TestMethod]
    public void Constructor_NullConnector_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new MigratingDatagramConnector(null!));

    [TestMethod]
    public async Task OpenAsync_CurlsConnectorOpens_GivesAMigratingChannelWithItsConnectionNumber()
    {
        FakeDatagramConnector curl = new();
        FakeDatagramChannel first = curl.Add(5000);
        MigratingDatagramConnector connector = new(curl);

        DatagramOpenResult result = await connector.OpenAsync("example.com", 443, CancellationToken.None);
        await using IDatagramChannel channel = result.Channel!;

        Assert.IsInstanceOfType<MigratingDatagramChannel>(channel);
        Assert.AreSame(first.LocalEndPoint, channel.LocalEndPoint);
        Assert.AreEqual(4, result.ConnectionNumber);
        Assert.AreSame(curl, connector.CurlDatagramConnector);
        CollectionAssert.AreEqual(new[] { ("example.com", 443) }, curl.Opens);
    }

    [TestMethod]
    public async Task OpenAsync_CurlsConnectorFails_GivesItsFailureUnchanged()
    {
        MigratingDatagramConnector connector = new(new FakeDatagramConnector());

        DatagramOpenResult result = await connector.OpenAsync("example.com", 443, CancellationToken.None);

        Assert.IsNull(result.Channel);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("no network", result.ErrorMessage);
    }
}
