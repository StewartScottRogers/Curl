using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

[TestClass]
public sealed class BodyReplacingHookExclusivityTests
{
    private static readonly string[] Names = ["graphql", "jsonrpc", "connect-rpc", "grpc-web"];

    private static ExtensionOptionValues Given(params string[] names) =>
        new(names.ToDictionary(name => name, _ => "x"));

    public static IEnumerable<object[]> Pairs() =>
        from i in Enumerable.Range(0, Names.Length)
        from j in Enumerable.Range(i + 1, Names.Length - i - 1)
        select new object[] { Names[i], Names[j] };

    [TestMethod]
    [DynamicData(nameof(Pairs))]
    public async Task HookFor_TwoBodyReplacingOptions_RefusesWithExit2NamingBothAndSendsNothing(string first, string second)
    {
        var hook = BodyReplacingHookExclusivity.HookFor(Given(second, first))!;

        var result = await hook.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null }, _ => throw new AssertFailedException("A request was sent."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual($"--{first} and --{second} cannot be used together", result.ErrorMessage);
    }

    [TestMethod]
    public void HookFor_GraphQlIntrospectWithJsonRpc_Refuses()
    {
        Assert.IsNotNull(BodyReplacingHookExclusivity.HookFor(Given("graphql-introspect", "jsonrpc")));
    }

    [TestMethod]
    public void HookFor_GraphQlDocumentWithIntrospect_IsOneBodyAndNotRefused()
    {
        Assert.IsNull(BodyReplacingHookExclusivity.HookFor(Given("graphql", "graphql-introspect", "graphql-variables")));
    }

    [TestMethod]
    public void HookFor_OneBodyOptionOrABodyOptionWithHar_IsNotRefused()
    {
        foreach (string name in Names)
        {
            Assert.IsNull(BodyReplacingHookExclusivity.HookFor(Given(name)));
            Assert.IsNull(BodyReplacingHookExclusivity.HookFor(Given(name, "har")));
        }

        Assert.IsNull(BodyReplacingHookExclusivity.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => BodyReplacingHookExclusivity.HookFor(null!));
    }

    [TestMethod]
    public void Catalogue_EachBodyOptionAloneActivatesItsOwnHookAndNoRefusal()
    {
        foreach (string name in Names)
        {
            var hooks = TransferHooks.ActivatedBy(Given(name), ExtensionTransferHookCatalogue.Entries).Hooks;
            Assert.AreEqual(1, hooks.Count, name);
        }
    }

    [TestMethod]
    public async Task Catalogue_TwoBodyOptions_RefusalComesFirst()
    {
        var hooks = TransferHooks.ActivatedBy(Given("graphql", "jsonrpc"), ExtensionTransferHookCatalogue.Entries).Hooks;

        var result = await hooks[0].ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null }, _ => throw new AssertFailedException("A request was sent."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
    }
}
