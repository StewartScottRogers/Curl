using Curl.Extension.Abstractions.Arguments;

namespace Curl.Extension.Composition;

[TestClass]
public sealed class ExtensionArgumentExpanderCatalogueTests
{
    [TestMethod]
    public void Entries_EachExpandersOption_IsRegisteredOnceInTheOptionCatalogue()
    {
        foreach (IArgumentExpander expander in ExtensionArgumentExpanderCatalogue.Entries)
        {
            Assert.Contains(expander.Option, ExtensionOptionCatalogue.Entries);
        }

        string[] names = [.. ExtensionArgumentExpanderCatalogue.Entries.Select(expander => expander.Option.Name)];
        Assert.HasCount(names.Distinct(StringComparer.Ordinal).Count(), names);
    }
}
