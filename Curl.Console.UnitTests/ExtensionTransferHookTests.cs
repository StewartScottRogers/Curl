using System.Text;
using Curl.Cli;
using Curl.Engine;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Transfer;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins that the Console passes the transfer hooks an option group's extension options activate down to the
/// engine (ADR-1007): a transfer with the option given runs the hook, and one without it gets no hook.
/// </summary>
[TestClass]
public sealed class ExtensionTransferHookTests
{
    private const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok";
    private static readonly ExtensionOption HookOption = new("test-hook", ExtensionOptionArgument.Value, "Test hook");

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly ScriptedConnector server = new([Encoding.Latin1.GetBytes(Response)]);
    private readonly HeaderAddingHook hook = new();

    private string RequestText => Encoding.Latin1.GetString(server.Written);

    [TestMethod]
    public async Task RunAsync_ExtensionOptionGiven_RunsItsTransferHook()
    {
        int exitCode = await RunAsync("-sS", "--test-hook", "on", "http://127.0.0.1:20601/");

        Assert.AreEqual(0, exitCode, Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.Contains("X-Test-Hook: on\r\n", RequestText);
        Assert.AreEqual(1, hook.FinishedTransfers);
        Assert.AreEqual("ok", Encoding.UTF8.GetString(standardOutput.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_ExtensionOptionNotGiven_RunsNoTransferHook()
    {
        int exitCode = await RunAsync("-sS", "http://127.0.0.1:20601/");

        Assert.AreEqual(0, exitCode, Encoding.UTF8.GetString(standardError.ToArray()));
        Assert.DoesNotContain("X-Test-Hook", RequestText);
        Assert.AreEqual(0, hook.FinishedTransfers);
    }

    [TestMethod]
    public void ExtensionTransferHooksOf_NoExtensionOptionGiven_PassesNoHookToTheEngine()
    {
        Assert.IsNull(CurlComposition.ExtensionTransferHooksOf(new CommandLineOptions(), [ActivatedByTheTestOption]));
        Assert.IsNull(CurlComposition.ExtensionTransferHooksOf(new CommandLineOptions()));
    }

    private ITransferHook? ActivatedByTheTestOption(ExtensionOptionValues values) =>
        values.ValueOf(HookOption) is { } value ? hook.WithValue(value) : null;

    private Task<int> RunAsync(params string[] arguments) =>
        CurlComposition.CreateRunner(
                standardOutput,
                standardError,
                new MemoryStream(),
                server,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "unused"),
                extensionOptions: [HookOption],
                extensionTransferHookActivators: [ActivatedByTheTestOption])
            .RunAsync(arguments);

    private sealed class HeaderAddingHook : ITransferHook
    {
        private string value = string.Empty;

        public int FinishedTransfers { get; private set; }

        public HeaderAddingHook WithValue(string given)
        {
            value = given;
            return this;
        }

        public IEnumerable<string> RequestHeadersFor(ITransferContext context) => [$"X-Test-Hook: {value}"];

        public void TransferFinished(TransferResult result) => FinishedTransfers++;
    }
}
