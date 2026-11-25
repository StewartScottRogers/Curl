using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

[TestClass]
public sealed class ExtendedProtocolHandlersTests
{
    private static readonly ProtocolHandlerServices Services = new(null!, null!, null!, null!);

    [TestMethod]
    public void For_WithTheSwitchSetToZero_GivesNoFactory()
    {
        Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>>? factory =
            ExtendedProtocolHandlers.For(name => name == "CURL_EXTENSIONS" ? "0" : null);

        Assert.IsNull(factory);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("1")]
    [DataRow("")]
    public void For_WithTheSwitchUnsetOrNotZero_GivesTheCatalogueFactory(string? value)
    {
        Func<ProtocolHandlerServices, IEnumerable<IProtocolHandler>>? factory =
            ExtendedProtocolHandlers.For(_ => value);

        Assert.IsNotNull(factory);
        Assert.HasCount(ExtensionCatalogue.Entries.Count, factory(Services));
    }

    [TestMethod]
    public void For_GivesTheExtensionsTheRunsEnvironmentReader()
    {
        Func<string, string?> reader = _ => null;
        ProtocolHandlerServices? seen = null;

        _ = ExtendedProtocolHandlers.For(reader, [services => { seen = services; return new SchemeOnlyHandler("x"); }])!(Services).ToList();

        Assert.AreSame(reader, seen!.ReadEnvironmentVariable);
        Assert.IsNull(Services.ReadEnvironmentVariable);
    }

    [TestMethod]
    public void For_WithTheCatalogue_GivesOneHandlerPerEntry() =>
        Assert.HasCount(ExtensionCatalogue.Entries.Count, ExtendedProtocolHandlers.For(_ => null)!(Services));

    [TestMethod]
    public void For_WithNoReader_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtendedProtocolHandlers.For(null!));

    [TestMethod]
    public void Create_GivesOneHandlerPerCatalogueEntry() =>
        Assert.HasCount(ExtensionCatalogue.Entries.Count, ExtendedProtocolHandlers.Create(Services));

    [TestMethod]
    public void Create_WithNoServices_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => ExtendedProtocolHandlers.Create(null!));

    [TestMethod]
    public void CreateFrom_BuildsEachHandlerOverTheServicesInOrder()
    {
        List<ProtocolHandlerServices> seen = [];
        SchemeOnlyHandler first = new("first");
        SchemeOnlyHandler second = new("second");

        IProtocolHandler[] handlers = [.. ExtendedProtocolHandlers.CreateFrom(
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
