namespace Curl.Conformance.SshServer;

[TestClass]
public sealed class SshServerDuplexConnectionTests
{
    [TestMethod]
    public async Task ReadAsync_BufferSmallerThanTheWrite_ReturnsTheRestOnTheNextRead()
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();
        await client.WriteAsync(new byte[] { 1, 2, 3 }, CancellationToken.None);
        await client.FlushAsync(CancellationToken.None);
        byte[] first = new byte[2];
        byte[] second = new byte[2];

        int firstCount = await server.ReadAsync(first, CancellationToken.None);
        int secondCount = await server.ReadAsync(second, CancellationToken.None);

        Assert.AreEqual(2, firstCount);
        Assert.AreEqual(1, secondCount);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, new[] { first[0], first[1], second[0] });
    }

    [TestMethod]
    public async Task ReadAsync_OtherEndDisposed_ReturnsZero()
    {
        (SshServerDuplexConnection client, SshServerDuplexConnection server) = SshServerDuplexConnection.CreatePair();

        await server.DisposeAsync();

        Assert.AreEqual(0, await client.ReadAsync(new byte[1], CancellationToken.None));
    }

    [TestMethod]
    public async Task WriteAsync_ThisEndDisposed_ThrowsIOException()
    {
        (SshServerDuplexConnection client, _) = SshServerDuplexConnection.CreatePair();

        await client.DisposeAsync();

        await Assert.ThrowsExactlyAsync<IOException>(async () => await client.WriteAsync(new byte[] { 1 }, CancellationToken.None));
    }

    [TestMethod]
    public void CreatePair_Ends_AreNotSecureAndHaveNoRemoteEndPoint()
    {
        (SshServerDuplexConnection client, _) = SshServerDuplexConnection.CreatePair();

        Assert.IsFalse(client.IsSecure);
        Assert.IsNull(client.RemoteEndPoint);
    }
}
