using System.Text;
using Curl.Core;
using Curl.Core.Multipart;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins AF-0982: curl 8.22.0 sizes an <c>-F</c> file part before its trace file opens, so
/// <c>-F f=@f.txt --trace-ascii f.txt</c> declares the bytes <c>f.txt</c> held before the trace truncated it.
/// </summary>
[TestClass]
public sealed class FormFileSizedBeforeTraceTests
{
    [TestMethod]
    public async Task RunAsync_TraceFileIsTheFormFile_DeclaresTheSizeTheFileHadBeforeTheTrace()
    {
        long traced = await BodyLengthAsync("--trace-ascii", "f.txt");
        long untraced = await BodyLengthAsync("--trace-ascii", "t.txt");

        Assert.AreEqual(untraced, traced);
    }

    [TestMethod]
    public async Task RunAsync_TraceFileIsAnotherFile_DeclaresTheFileAsItIs()
    {
        long untraced = await BodyLengthAsync(null, null);
        long traced = await BodyLengthAsync("--trace-ascii", "t.txt");

        Assert.AreEqual(untraced, traced);
    }

    private static async Task<long> BodyLengthAsync(string? traceOption, string? traceFile)
    {
        InMemoryFileSystem files = new();
        files.ExistingContent["f.txt"] = "file body\n"u8.ToArray();
        long length = -2;
        RecordingProtocolHandler http = new("http", context =>
        {
            length = (context.Http?.Body as StreamBody)?.Length ?? -1;
            return ValueTask.FromResult(TransferResult.Success(0) with { Report = new TransferReport { ResponseCode = 200 } });
        });
        List<string> arguments = ["-F", "f=@f.txt"];
        if (traceOption is not null)
        {
            arguments.AddRange([traceOption, traceFile!]);
        }

        arguments.Add("http://127.0.0.1:22000/");

        TruncatingFileSystem truncating = new(files);
        await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                truncating,
                files,
                new MemoryStream(),
                new MemoryStream(),
                new MemoryStream(),
                runsOnWindows: false,
                outputPaths: files,
                formBodyBuilder: new MultipartFormBodyBuilder(truncating, Encoding.UTF8, () => "B", standardInput: null))
            .RunAsync([.. arguments]);
        return length;
    }

    private sealed class TruncatingFileSystem(InMemoryFileSystem inner) : IFileSystem
    {
        public ValueTask<FileOpenResult> OpenForReadAsync(string path, CancellationToken cancellationToken) =>
            inner.OpenForReadAsync(path, cancellationToken);

        public ValueTask<FileOpenResult> OpenForWriteAsync(string path, FileWriteMode mode, UnixFileMode createMode, CancellationToken cancellationToken)
        {
            if (mode == FileWriteMode.Truncate && inner.ExistingContent.ContainsKey(path))
            {
                inner.ExistingContent[path] = [];
            }

            return inner.OpenForWriteAsync(path, mode, createMode, cancellationToken);
        }
    }
}
