using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.Composition;

[TestClass]
public sealed class WebSocketReplCombinationTests
{
    private static ExtensionOptionValues Given(params string[] names) =>
        new(names.ToDictionary(name => name, _ => "x"));

    [TestMethod]
    [DataRow("ws-subprotocol")]
    [DataRow("ws-extended-connect")]
    public async Task HookFor_ReplWithOtherOption_RefusesWithExit2NamingBothAndConnectsToNothing(string other)
    {
        var hook = WebSocketReplExclusivity.HookFor(Given("ws-repl", other))!;

        var result = await hook.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://example.com/"), Output = Stream.Null }, _ => throw new AssertFailedException("A connection was opened."));

        Assert.AreEqual(CurlExitCode.FailedInit, result.ExitCode);
        Assert.AreEqual($"--ws-repl and --{other} cannot be used together", result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("ws-repl")]
    [DataRow("ws-subprotocol")]
    [DataRow("ws-extended-connect")]
    public void HookFor_OneOptionAlone_IsNotRefused(string only)
    {
        Assert.IsNull(WebSocketReplExclusivity.HookFor(Given(only)));
    }

    [TestMethod]
    public void HookFor_ValuesNull_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => WebSocketReplExclusivity.HookFor(null!));
    }

    [TestMethod]
    public void Catalogue_RunsTheReplExclusivityBeforeTheReplHook()
    {
        var entries = ExtensionTransferHookCatalogue.Entries;
        Assert.IsTrue(entries.ToList().IndexOf(WebSocketReplExclusivity.HookFor) < entries.ToList().IndexOf(Curl.Extension.WebSocketRepl.WebSocketReplExtension.HookFor));
    }
}
