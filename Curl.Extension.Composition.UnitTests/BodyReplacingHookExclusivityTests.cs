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
    public async Task HookFor_GraphQlIntrospectWithJsonRpc_Refuses()
    {
        var hook = BodyReplacingHookExclusivity.HookFor(Given("graphql-introspect", "jsonrpc"))!;

        var result = await hook.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null }, _ => throw new AssertFailedException("A request was sent."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual("--graphql-introspect and --jsonrpc cannot be used together", result.ErrorMessage);
    }

    [TestMethod]
    public async Task HookFor_GraphQlDocumentWithIntrospect_IsOneBodyAndNotRefusedAsTwo()
    {
        var hook = BodyReplacingHookExclusivity.HookFor(Given("graphql", "graphql-introspect", "graphql-variables"))!;
        var expected = TransferResult.Failure(CurlExitCode.Ok, "next");

        var result = await hook.ExecuteAsync(ContextWith(null, false), _ => ValueTask.FromResult(expected));

        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void HookFor_NoBodyOptionOrHarAlone_GivesNoHook()
    {
        Assert.IsNull(BodyReplacingHookExclusivity.HookFor(Given("har")));

        Assert.IsNull(BodyReplacingHookExclusivity.HookFor(ExtensionOptionValues.None));
        Assert.ThrowsExactly<ArgumentNullException>(() => BodyReplacingHookExclusivity.HookFor(null!));
    }

    [TestMethod]
    public void Catalogue_EachBodyOptionAloneActivatesItsOwnHookAndTheUploadAndHeadRefusal()
    {
        foreach (string name in Names)
        {
            var hooks = TransferHooks.ActivatedBy(Given(name), ExtensionTransferHookCatalogue.Entries).Hooks;
            Assert.AreEqual(2, hooks.Count, name);
        }
    }

    [TestMethod]
    public async Task Catalogue_TwoBodyOptions_RefusalComesFirst()
    {
        var hooks = TransferHooks.ActivatedBy(Given("graphql", "jsonrpc"), ExtensionTransferHookCatalogue.Entries).Hooks;

        var result = await hooks[0].ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null }, _ => throw new AssertFailedException("A request was sent."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
    }

    private static TransferContext ContextWith(Stream? upload, bool noBody) =>
        new() { Url = CurlUrl.Parse("http://example.com/"), Output = Stream.Null, Upload = upload, NoBody = noBody };

    [TestMethod]
    [DataRow("graphql")]
    [DataRow("jsonrpc")]
    [DataRow("grpc-web")]
    [DataRow("connect-rpc")]
    public async Task HookFor_OneBodyOptionWithUpload_RefusesNamingOptionAndT(string name)
    {
        var hook = BodyReplacingHookExclusivity.HookFor(Given(name))!;

        var result = await hook.ExecuteAsync(ContextWith(new MemoryStream(), false), _ => throw new AssertFailedException("A request was sent."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual($"--{name} cannot be used with -T", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("graphql")]
    [DataRow("jsonrpc")]
    [DataRow("grpc-web")]
    [DataRow("connect-rpc")]
    public async Task HookFor_OneBodyOptionWithNoBody_RefusesNamingOptionAndI(string name)
    {
        var hook = BodyReplacingHookExclusivity.HookFor(Given(name))!;

        var result = await hook.ExecuteAsync(ContextWith(null, true), _ => throw new AssertFailedException("A request was sent."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual($"--{name} cannot be used with -I", result.ErrorMessage);
    }

    [TestMethod]
    public async Task HookFor_OneBodyOptionWithoutUploadOrNoBody_CallsNextUnchanged()
    {
        var hook = BodyReplacingHookExclusivity.HookFor(Given("graphql"))!;
        var expected = TransferResult.Failure(CurlExitCode.Ok, "next");

        var result = await hook.ExecuteAsync(ContextWith(null, false), _ => ValueTask.FromResult(expected));

        Assert.AreEqual(expected, result);
    }
}
