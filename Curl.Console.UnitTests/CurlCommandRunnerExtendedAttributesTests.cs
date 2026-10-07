using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how the runner applies <c>--xattr</c> through a fake <see cref="IExtendedAttributeWriter" />,
/// against curl 8.18.0 measured on Ubuntu on 2026-10-01 (<c>tool_xattr.c</c> is unchanged to
/// 8.21.0): after a successful transfer to an <c>-o</c> file it stores <c>user.creator</c>,
/// <c>user.xdg.referrer.url</c>, <c>user.mime_type</c> and <c>user.xdg.origin.url</c>, in that
/// order, the URL without its credentials; a failure prints curl's warning and still exits 0
/// (BL-651, ADR-0320).
/// </summary>
[TestClass]
public sealed class CurlCommandRunnerExtendedAttributesTests
{
    private const string Url = "http://u:p@127.0.0.1:18653/a?b#frag";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();
    private readonly RecordingExtendedAttributeWriter writer = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_XattrToOutputFile_WritesTheFourAttributesCurlWritesInItsOrder()
    {
        int exitCode = await RunAsync(["--xattr", "-e", "http://ref.example/", "-o", "out2.txt", Url], Replying("text/plain; charset=utf-8"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "attributes written",
            "out2.txt user.creator=curl|out2.txt user.xdg.referrer.url=http://ref.example/|out2.txt user.mime_type=text/plain; charset=utf-8|out2.txt user.xdg.origin.url=http://127.0.0.1:18653/a?b#frag",
            Describe(writer.Written));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            new[]
            {
                ("out2.txt", "user.creator", "curl"),
                ("out2.txt", "user.xdg.referrer.url", "http://ref.example/"),
                ("out2.txt", "user.mime_type", "text/plain; charset=utf-8"),
                ("out2.txt", "user.xdg.origin.url", "http://127.0.0.1:18653/a?b#frag"),
            },
            writer.Written);
    }

    [TestMethod]
    public async Task RunAsync_XattrWithNoRefererAndNoContentType_WritesCreatorAndOriginOnly()
    {
        int exitCode = await RunAsync(["--xattr", "-o", "out.txt", "file:///tmp/in.txt"], Replying(null, "file"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "attributes written",
            "out.txt user.creator=curl|out.txt user.xdg.origin.url=file:///tmp/in.txt",
            Describe(writer.Written));
        Assert.AreEqual(0, exitCode);
        CollectionAssert.AreEqual(
            new[] { ("out.txt", "user.creator", "curl"), ("out.txt", "user.xdg.origin.url", "file:///tmp/in.txt") },
            writer.Written);
    }

    [TestMethod]
    public async Task RunAsync_XattrWithRedirectReferer_WritesTheRefererTheLastRequestSent()
    {
        RecordingProtocolHandler handler = new("http", async context =>
        {
            await context.Output.WriteAsync(Encoding.ASCII.GetBytes("hi"), context.CancellationToken);
            return TransferResult.Success(2) with { Report = new TransferReport { Referer = "http://first.example/" } };
        });

        int exitCode = await RunAsync(["--xattr", "-o", "out.txt", "http://127.0.0.1:1/"], handler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "second attribute written",
            "out.txt user.xdg.referrer.url=http://first.example/",
            Describe(writer.Written.Skip(1).Take(1)));
        Assert.AreEqual(("out.txt", "user.xdg.referrer.url", "http://first.example/"), writer.Written[1]);
    }

    [TestMethod]
    public async Task RunAsync_XattrWithResultWithoutReport_WritesTheRefererOptionAndNoMimeType()
    {
        RecordingProtocolHandler handler = new("http", async context =>
        {
            await context.Output.WriteAsync(Encoding.ASCII.GetBytes("hi"), context.CancellationToken);
            return TransferResult.Success(2);
        });

        int exitCode = await RunAsync(["--xattr", "-e", "http://ref.example/", "-o", "out.txt", "http://127.0.0.1:1/"], handler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert(
            "attributes written",
            "out.txt user.creator=curl|out.txt user.xdg.referrer.url=http://ref.example/|out.txt user.xdg.origin.url=http://127.0.0.1:1/",
            Describe(writer.Written));
        CollectionAssert.AreEqual(
            new[]
            {
                ("out.txt", "user.creator", "curl"),
                ("out.txt", "user.xdg.referrer.url", "http://ref.example/"),
                ("out.txt", "user.xdg.origin.url", "http://127.0.0.1:1/"),
            },
            writer.Written);
    }

    [TestMethod]
    [DataRow(new string[0])]
    [DataRow(new[] { "--xattr", "--no-xattr" })]
    public async Task RunAsync_NoXattr_WritesNoAttribute(string[] options)
    {
        int exitCode = await RunAsync([.. options, "-o", "out.txt", Url], Replying("text/plain"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("attributes written", 0, writer.Written.Count);
        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(writer.Written);
    }

    [TestMethod]
    public async Task RunAsync_XattrToStandardOutput_WritesNoAttribute()
    {
        int exitCode = await RunAsync(["--xattr", Url], Replying("text/plain"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("attributes written", 0, writer.Written.Count);
        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(writer.Written);
    }

    [TestMethod]
    public async Task RunAsync_XattrWithFailedTransfer_WritesNoAttribute()
    {
        RecordingProtocolHandler handler = new("http", async context =>
        {
            await context.Output.WriteAsync(Encoding.ASCII.GetBytes("hi"), context.CancellationToken);
            return TransferResult.Failure(CurlExitCode.PartialFile, "Transferred a partial file");
        });

        int exitCode = await RunAsync(["--xattr", "-o", "out.txt", Url], handler);

        Diagnostics.Assert("exit code", 18, exitCode);
        Diagnostics.Assert("attributes written", 0, writer.Written.Count);
        Assert.AreEqual(18, exitCode);
        Assert.IsEmpty(writer.Written);
    }

    [TestMethod]
    public async Task RunAsync_XattrWithTransferThatOpenedNoFile_WritesNoAttribute()
    {
        RecordingProtocolHandler handler = new("http", _ => ValueTask.FromResult(TransferResult.Success(0)));

        int exitCode = await RunAsync(["--xattr", "-o", "out.txt", Url], handler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("attributes written", 0, writer.Written.Count);
        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(writer.Written);
    }

    [TestMethod]
    public async Task RunAsync_XattrWithUnmetTimeCondition_WritesNoAttribute()
    {
        RecordingProtocolHandler handler = new("http", _ => ValueTask.FromResult(
            TransferResult.TimeConditionNotMet(DateTimeOffset.FromUnixTimeSeconds(1577959445))));

        int exitCode = await RunAsync(["--xattr", "-o", "out.txt", Url], handler);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("attributes written", 0, writer.Written.Count);
        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(writer.Written);
    }

    [TestMethod]
    public async Task RunAsync_XattrWithNoWriter_WritesNothingAndExits0()
    {
        int exitCode = await RunAsync(["--xattr", "-o", "out.txt", Url], Replying("text/plain"), extendedAttributeWriter: null);

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("out.txt", "hello", Encoding.ASCII.GetString(outputFiles.Written["out.txt"].ToArray()));
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(outputFiles.Written["out.txt"].ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_XattrThatFails_StopsAtTheFailureWarnsAndExits0()
    {
        writer.FailOn = "user.mime_type";
        Diagnostics.Arrange("attribute that fails", writer.FailOn);

        int exitCode = await RunAsync(["--xattr", "-o", "out.txt", Url], Replying("text/plain"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("attributes written", 1, writer.Written.Count);
        Diagnostics.Assert(
            "stderr ends with the warning",
            true,
            Encoding.UTF8.GetString(standardError.ToArray()).Replace("\r\n", "\n", StringComparison.Ordinal).EndsWith(
                "Warning: Error setting extended attributes on 'out.txt': Operation not \nWarning: supported\n",
                StringComparison.Ordinal));
        Assert.AreEqual(0, exitCode);
        Assert.HasCount(1, writer.Written);
        Assert.EndsWith(
            "Warning: Error setting extended attributes on 'out.txt': Operation not " + Environment.NewLine
            + "Warning: supported" + Environment.NewLine,
            Encoding.UTF8.GetString(standardError.ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_XattrThatFailsUnderSilent_PrintsNothing()
    {
        writer.FailOn = "user.creator";
        Diagnostics.Arrange("attribute that fails", writer.FailOn);

        int exitCode = await RunAsync(["-s", "--xattr", "-o", "out.txt", Url], Replying("text/plain"));

        Diagnostics.Assert("exit code", 0, exitCode);
        Diagnostics.Assert("stderr length", 0L, standardError.Length);
        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }

    private static string Describe(IEnumerable<(string Path, string Name, string Value)> attributes) =>
        string.Join("|", attributes.Select(attribute => $"{attribute.Path} {attribute.Name}={attribute.Value}"));

    private static RecordingProtocolHandler Replying(string? contentType, string scheme = "http") =>
        new(scheme, async context =>
        {
            byte[] body = Encoding.ASCII.GetBytes("hello");
            await context.Output.WriteAsync(body, context.CancellationToken);

            return TransferResult.Success(body.Length) with { Report = new TransferReport { ContentType = contentType } };
        });

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler) =>
        RunAsync(arguments, handler, writer);

    private async Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler, IExtendedAttributeWriter? extendedAttributeWriter)
    {
        Diagnostics.Arrange("command line", string.Join(" ", arguments));
        Diagnostics.Arrange("extended attribute writer", extendedAttributeWriter is null ? "none" : "recording");
        CurlCommandRunner runner = new(
            _ => new TransferDispatch(new ProtocolDispatcher([handler])),
            outputFiles,
            outputFiles,
            standardOutput,
            standardError,
            new MemoryStream(),
            runsOnWindows: false,
            extendedAttributeWriter: extendedAttributeWriter);

        int exitCode;
        using (Diagnostics.Phase("run"))
        {
            exitCode = await runner.RunAsync(arguments);
        }

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }

    private sealed class RecordingExtendedAttributeWriter : IExtendedAttributeWriter
    {
        public List<(string Path, string Name, string Value)> Written { get; } = [];

        public string? FailOn { get; set; }

        public bool TryWrite(string path, string name, string value, out string errorText)
        {
            if (name == FailOn)
            {
                errorText = "Operation not supported";
                return false;
            }

            Written.Add((path, name, value));
            errorText = string.Empty;
            return true;
        }
    }
}
