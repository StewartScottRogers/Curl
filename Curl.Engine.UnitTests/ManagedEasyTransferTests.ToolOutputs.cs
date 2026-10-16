using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Core;
using Curl.Output;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="ManagedEasyTransfer.ToolOutputs" /> (BL-2396, ADR-0558): a perform from the options with the
/// curl tool's outputs set writes the same bytes, in the same order, as the same attempt performed directly on
/// the runner's own context (<see cref="TransferPerformer.FollowWatchingSpeedAsync" />), for a body, <c>-i</c>, <c>-D</c>, <c>-J</c> and
/// <c>--styled-output</c>, and a failing write ends both with the same exit 23 text.
/// </summary>
public sealed partial class ManagedEasyTransferTests
{
    private const string HeadedResponse =
        "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Disposition: attachment; filename=\"f.txt\"\r\n\r\nhello";

    [TestMethod]
    public async Task PerformAsync_ToolOutputsForABody_WritesWhatTheRunnersContextWrites()
    {
        (IReadOnlyList<string> fromOptions, IReadOnlyList<string> fromAttempt, _) = await WritesBothWaysAsync(new EasyBehaviourOptions(), dumpHeaders: false, watchHeaders: false, styled: false);

        CollectionAssert.AreEqual(new[] { "body:hello" }, fromOptions.ToArray());
        CollectionAssert.AreEqual(fromAttempt.ToArray(), fromOptions.ToArray());
    }

    [TestMethod]
    public async Task PerformAsync_ToolOutputsUnderInclude_WritesTheHeadLinesBeforeTheBodyAsTheRunnersContextDoes()
    {
        (IReadOnlyList<string> fromOptions, IReadOnlyList<string> fromAttempt, _) = await WritesBothWaysAsync(new EasyBehaviourOptions { IncludeHeader = true }, dumpHeaders: false, watchHeaders: false, styled: false);

        Assert.AreEqual("body:HTTP/1.1 200 OK\r\n", fromOptions[0]);
        Assert.AreEqual("body:hello", fromOptions[^1]);
        CollectionAssert.AreEqual(fromAttempt.ToArray(), fromOptions.ToArray());
    }

    [TestMethod]
    public async Task PerformAsync_ToolOutputsWithDumpHeaderUnderInclude_TeesEachLineDumpFirstAsTheRunnersContextDoes()
    {
        (IReadOnlyList<string> fromOptions, IReadOnlyList<string> fromAttempt, _) = await WritesBothWaysAsync(new EasyBehaviourOptions { IncludeHeader = true }, dumpHeaders: true, watchHeaders: false, styled: false);

        Assert.AreEqual("dump:HTTP/1.1 200 OK\r\n", fromOptions[0]);
        Assert.AreEqual("body:HTTP/1.1 200 OK\r\n", fromOptions[1]);
        CollectionAssert.AreEqual(fromAttempt.ToArray(), fromOptions.ToArray());
    }

    [TestMethod]
    public async Task PerformAsync_ToolOutputsWithARemoteHeaderNameWatcher_ShowsItEachLineBeforeTheDumpAsTheRunnersContextDoes()
    {
        (IReadOnlyList<string> fromOptions, IReadOnlyList<string> fromAttempt, _) = await WritesBothWaysAsync(new EasyBehaviourOptions(), dumpHeaders: true, watchHeaders: true, styled: false);

        Assert.AreEqual("watch:HTTP/1.1 200 OK\r\n", fromOptions[0]);
        Assert.AreEqual("dump:HTTP/1.1 200 OK\r\n", fromOptions[1]);
        Assert.Contains("watch:Content-Disposition: attachment; filename=\"f.txt\"\r\n", fromOptions);
        CollectionAssert.AreEqual(fromAttempt.ToArray(), fromOptions.ToArray());
    }

    [TestMethod]
    public async Task PerformAsync_ToolOutputsWithStyledHeaders_StylesTheIncludedLinesAsTheRunnersContextDoes()
    {
        (IReadOnlyList<string> fromOptions, IReadOnlyList<string> fromAttempt, _) = await WritesBothWaysAsync(new EasyBehaviourOptions { IncludeHeader = true }, dumpHeaders: true, watchHeaders: false, styled: true);

        Assert.IsTrue(fromOptions.Any(write => write.StartsWith("body:\u001b[1m", StringComparison.Ordinal)));
        Assert.IsFalse(fromOptions.Any(write => write.StartsWith("dump:\u001b", StringComparison.Ordinal)));
        CollectionAssert.AreEqual(fromAttempt.ToArray(), fromOptions.ToArray());
    }

    [TestMethod]
    public async Task PerformAsync_ToolOutputsSet_WritesNeitherCallback()
    {
        int callbackWrites = 0;
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(HeadedResponse)])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new() { Write = data => ++callbackWrites, Header = data => ++callbackWrites },
        };
        using MemoryStream output = new();
        easy.ToolOutputs = new EngineToolTransferOutputs(output);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(output.ToArray()));
        Assert.AreEqual(0, callbackWrites);
    }

    [TestMethod]
    public async Task PerformAsync_ToolOutputThatFailsAWrite_EndsWithTheRunnersContextsExit23Text()
    {
        (_, _, (CurlCode Code, string Text, string AttemptText) failure) = await WritesBothWaysAsync(new EasyBehaviourOptions(), dumpHeaders: false, watchHeaders: false, styled: false, failBody: true);

        Assert.AreEqual(CurlCode.WriteError, failure.Code);
        Assert.AreEqual("Failure writing output to destination, passed 5 returned 3", failure.Text);
        Assert.AreEqual(failure.AttemptText, failure.Text);
    }

    [TestMethod]
    public async Task PerformAsync_WriteCallbackThatTakesNothing_EndsWithTheRunnersExit23Text()
    {
        await using IEasyTransfer easy = CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(HeadedResponse)])).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new() { Write = _ => 0 },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.WriteError, code);
        Assert.AreEqual("Failure writing output to destination, passed 5 returned 0", easy.ErrorText);
    }

    [TestMethod]
    public async Task Duplicate_HandleWithToolOutputs_DoesNotCopyThemAndResetClearsThem()
    {
        await using ManagedEasyTransfer easy = EasyOver(RecordingProtocolHandler.WritingPath("http"));
        easy.ToolOutputs = new EngineToolTransferOutputs(Stream.Null);

        await using IEasyTransfer copy = easy.Duplicate();
        easy.Reset();

        Assert.IsNull(((ManagedEasyTransfer)copy).ToolOutputs);
        Assert.IsNull(easy.ToolOutputs);
    }

    // Runs the response once from the options with ToolOutputs and once performed directly on the runner's own
    // context, which its transferContextFactory.Create call builds from the same options and outputs, each writing into
    // its own log; returns both logs and, for a failing body, both texts.
    private static async Task<(IReadOnlyList<string> FromOptions, IReadOnlyList<string> FromAttempt, (CurlCode Code, string Text, string AttemptText) Failure)> WritesBothWaysAsync(
        EasyBehaviourOptions behaviour,
        bool dumpHeaders,
        bool watchHeaders,
        bool styled,
        bool failBody = false)
    {
        EasyOptions options = new() { Network = new EasyNetworkOptions { Url = "http://h/x" }, Behaviour = behaviour };
        List<string> fromOptions = [];
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([Encoding.ASCII.GetBytes(HeadedResponse)])).CreateEasy();
        easy.Options = options;
        easy.ToolOutputs = OutputsOf(fromOptions, dumpHeaders, watchHeaders, styled, failBody);
        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        List<string> fromAttempt = [];
        EngineToolTransferOutputs attemptOutputs = OutputsOf(fromAttempt, dumpHeaders, watchHeaders, styled, failBody);
        TransferContext context = new TransferContextFactory(Stream.Null, TimeProvider.System).Create(
            ManagedEasyTransfer.TransferSettingsOf(options),
            options.Http,
            options.Behaviour,
            options.Transfer,
            options.Protocols,
            options.Proxy,
            options.Authentication,
            options.Network,
            CurlUrl.Parse("http://h/x"),
            attemptOutputs.Output,
            range: null,
            resumeFrom: null,
            attemptOutputs.HeaderOutput,
            watchHeaderOutput: attemptOutputs.WatchHeaderOutput,
            bodyHeaderStyles: attemptOutputs.BodyHeaderStyles);
        TransferResult attempt = await TransferPerformer.FollowWatchingSpeedAsync(
            new EngineTransferState(),
            new RedirectFollower(CreateDispatcher(new ScriptedConnector([Encoding.ASCII.GetBytes(HeadedResponse)]), null)),
            context,
            new RedirectPolicy());

        return (fromOptions, fromAttempt, (code, easy.ErrorText, attempt.ErrorMessage ?? string.Empty));
    }

    private static EngineToolTransferOutputs OutputsOf(List<string> log, bool dumpHeaders, bool watchHeaders, bool styled, bool failBody) =>
        new(
            failBody ? new FailingOutputStream(3) : new LoggingStream("body", log, null),
            dumpHeaders ? new LoggingStream("dump", log, null) : null,
            watchHeaders ? headerOutput => new LoggingStream("watch", log, headerOutput) : null,
            styled ? StyledHeaderLines.ForPlatform(runsOnWindows: false, "http://h/x", vteVersion: null) : null);

    // Logs every write as "<name>:<bytes>" and passes it on, as RemoteHeaderNameStream reads each line first.
    private sealed class LoggingStream(string name, List<string> log, Stream? inner) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => inner?.Flush();

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            log.Add(name + ":" + Encoding.Latin1.GetString(buffer));
            inner?.Write(buffer);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            Write(buffer.Span);
            return ValueTask.CompletedTask;
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    }

    // Fails every write as a full standard-output buffer does, having accepted the given count.
    private sealed class FailingOutputStream(int accepted) : Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new OutputWriteFailedException(accepted, "standard output closed");
    }
}
