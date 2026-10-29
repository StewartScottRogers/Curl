namespace Curl.Extension.Abstractions.Transfer;

[TestClass]
public sealed class TransferHookServicesTests
{
    [TestMethod]
    public void Constructor_GivenAConnectorAndAListener_HoldsBoth()
    {
        TransferHookServices given = UnusedTransferHookServices.Create();

        TransferHookServices services = new(given.Connector, given.Listener);

        Assert.AreSame(given.Connector, services.Connector);
        Assert.AreSame(given.Listener, services.Listener);
    }

    [TestMethod]
    public void Constructor_WithoutAConnectorOrAListener_Throws()
    {
        TransferHookServices given = UnusedTransferHookServices.Create();

        Assert.ThrowsExactly<ArgumentNullException>(() => new TransferHookServices(null!, given.Listener));
        Assert.ThrowsExactly<ArgumentNullException>(() => new TransferHookServices(given.Connector, null!));
    }
}
