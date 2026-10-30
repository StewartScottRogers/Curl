using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

[TestClass]
public sealed class ExtensionProtocolHandlersTests
{
    private static readonly ProtocolHandlerServices Services = new(null!, null!, null!, null!);

    [TestMethod]
    public void For_WithTheSwitchSetToZero_GivesNoFactory()
    {
        Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>>? factory =
            ExtensionProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null);

        Assert.IsNull(factory);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("1")]
    [DataRow("")]
    public void For_WithTheSwitchUnsetOrNotZero_GivesTheCatalogueFactory(string? value)
    {
        Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>>? factory =
            ExtensionProtocolHandlers.For(_ => value);

        Assert.IsNotNull(factory);
        Assert.HasCount(ExtensionCatalogue.Entries.Count, factory(Services));
    }

    [TestMethod]
    public void For_WithNoReader_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtensionProtocolHandlers.For(null!));

    [TestMethod]
    public void Create_GivesOneHandlerPerCatalogueEntry() =>
        Assert.HasCount(ExtensionCatalogue.Entries.Count, ExtensionProtocolHandlers.Create(Services));

    [TestMethod]
    public void Create_WithNoServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtensionProtocolHandlers.Create(null!));

    [TestMethod]
    public void CreateFrom_BuildsEachHandlerOverTheServicesInOrder()
    {
        List<ProtocolHandlerServices> seen = [];
        SchemeOnlyHandler first = new("first");
        SchemeOnlyHandler second = new("second");

        IProtocolHandler[] handlers = [.. ExtensionProtocolHandlers.CreateFrom(
            [
                services => { seen.Add(services); return first; },
                services => { seen.Add(services); return second; },
            ],
            Services)];

        CollectionAssert.AreEqual(new IProtocolHandler[] { first, second }, handlers);
        CollectionAssert.AreEqual(new[] { Services, Services }, seen);
    }

    private sealed class SchemeOnlyHandler(string scheme) : IProtocolHandler
    {
        public IReadOnlyCollection<string> SupportedSchemes { get; } = [scheme];

        public ValueTask<TransferResult> ExecuteAsync(ITransferContext context) => throw new NotSupportedException();
    }
}
