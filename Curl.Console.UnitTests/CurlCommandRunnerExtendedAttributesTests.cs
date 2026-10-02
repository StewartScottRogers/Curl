using System.Text;
using Curl.Core;
using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public async Task RunAsync_XattrToOutputFile_WritesTheFourAttributesCurlWritesInItsOrder()
    {
        int exitCode = await RunAsync(["--xattr", "-e", "http://ref.example/", "-o", "out2.txt", Url], Replying("text/plain; charset=utf-8"));

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

        await RunAsync(["--xattr", "-o", "out.txt", "http://127.0.0.1:1/"], handler);

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

        await RunAsync(["--xattr", "-e", "http://ref.example/", "-o", "out.txt", "http://127.0.0.1:1/"], handler);

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

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(writer.Written);
    }

    [TestMethod]
    public async Task RunAsync_XattrToStandardOutput_WritesNoAttribute()
    {
        int exitCode = await RunAsync(["--xattr", Url], Replying("text/plain"));

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

        Assert.AreEqual(18, exitCode);
        Assert.IsEmpty(writer.Written);
    }

    [TestMethod]
    public async Task RunAsync_XattrWithTransferThatOpenedNoFile_WritesNoAttribute()
    {
        RecordingProtocolHandler handler = new("http", _ => ValueTask.FromResult(TransferResult.Success(0)));

        int exitCode = await RunAsync(["--xattr", "-o", "out.txt", Url], handler);

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(writer.Written);
    }

    [TestMethod]
    public async Task RunAsync_XattrWithUnmetTimeCondition_WritesNoAttribute()
    {
        RecordingProtocolHandler handler = new("http", _ => ValueTask.FromResult(
            TransferResult.TimeConditionNotMet(DateTimeOffset.FromUnixTimeSeconds(1577959445))));

        int exitCode = await RunAsync(["--xattr", "-o", "out.txt", Url], handler);

        Assert.AreEqual(0, exitCode);
        Assert.IsEmpty(writer.Written);
    }

    [TestMethod]
    public async Task RunAsync_XattrWithNoWriter_WritesNothingAndExits0()
    {
        int exitCode = await RunAsync(["--xattr", "-o", "out.txt", Url], Replying("text/plain"), extendedAttributeWriter: null);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(outputFiles.Written["out.txt"].ToArray()));
    }

    [TestMethod]
    public async Task RunAsync_XattrThatFails_StopsAtTheFailureWarnsAndExits0()
    {
        writer.FailOn = "user.mime_type";

        int exitCode = await RunAsync(["--xattr", "-o", "out.txt", Url], Replying("text/plain"));

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

        int exitCode = await RunAsync(["-s", "--xattr", "-o", "out.txt", Url], Replying("text/plain"));

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, standardError.Length);
    }

    private static RecordingProtocolHandler Replying(string? contentType, string scheme = "http") =>
        new(scheme, async context =>
        {
            byte[] body = Encoding.ASCII.GetBytes("hello");
            await context.Output.WriteAsync(body, context.CancellationToken);

            return TransferResult.Success(body.Length) with { Report = new TransferReport { ContentType = contentType } };
        });

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler) =>
        RunAsync(arguments, handler, writer);

    private Task<int> RunAsync(IReadOnlyList<string> arguments, IProtocolHandler handler, IExtendedAttributeWriter? extendedAttributeWriter) =>
        new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([handler])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: false,
                extendedAttributeWriter: extendedAttributeWriter)
            .RunAsync(arguments);

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
