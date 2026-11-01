using Curl.Engine;
using System.Text;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Http;
using Curl.Testing;

namespace Curl.Console;

/// <summary>
/// Pins how <c>%{filename_effective}</c> and the <c>File exists</c> warning print an output name
/// holding a byte that is not valid UTF-8 (BL-4032, ADR-0981), with the files in an
/// <see cref="InMemoryFileSystem" />. Measured with curl 8.18.0 OpenSSL on Linux (WSL) on
/// 2026-10-28: <c>-o $'\xff.txt' -w "%{filename_effective}"</c> prints FF 2E 74 78 74, and with
/// <c>--no-clobber</c> and every name taken standard error gets
/// <c>Warning: Failed to open the file \xff.txt: File exists</c>. On Windows both are as before this
/// change: <c>-w</c> already wrote FF there, and the warning writes UTF-8, the surrogate as U+FFFD.
/// </summary>
[TestClass]
public sealed class Cgr051RawByteOutputTextTests
{
    private const string Url = "http://127.0.0.1:18492/";

    private const string Ok = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello";

    private const string EscapedName = "\uDCFF.txt";

    private readonly MemoryStream standardOutput = new();
    private readonly MemoryStream standardError = new();
    private readonly InMemoryFileSystem outputFiles = new();

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public async Task RunAsync_EscapedOutputNameOffWindows_FilenameEffectiveWritesTheRawByte()
    {
        await RunAsync(runsOnWindows: false, "-s", "-o", EscapedName, "-w", "%{filename_effective}", Url);

        CollectionAssert.AreEqual(new byte[] { 0xFF, 0x2E, 0x74, 0x78, 0x74 }, standardOutput.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_EscapedNameTakenOffWindows_FileExistsWarningWritesTheRawByte()
    {
        TakeNameAndNumbers();

        int exitCode = await RunAsync(runsOnWindows: false, "--no-progress-meter", "--no-clobber", "-o", EscapedName, Url);

        byte[] expected = [.. "Warning: Failed to open the file "u8, 0xFF, .. ".txt: File exists"u8];
        Assert.AreEqual((int)CurlExitCode.WriteError, exitCode);
        CollectionAssert.AreEqual(expected, standardError.ToArray()[..expected.Length]);
    }

    [TestMethod]
    public async Task RunAsync_EscapedOutputNameOnWindows_FilenameEffectiveKeepsItsBytes()
    {
        await RunAsync(runsOnWindows: true, "-s", "-o", EscapedName, "-w", "%{filename_effective}", Url);

        CollectionAssert.AreEqual(new byte[] { 0xFF, 0x2E, 0x74, 0x78, 0x74 }, standardOutput.ToArray());
    }

    [TestMethod]
    public async Task RunAsync_EscapedNameTakenOnWindows_FileExistsWarningWritesUtf8ReplacementCharacter()
    {
        TakeNameAndNumbers();

        await RunAsync(runsOnWindows: true, "--no-progress-meter", "--no-clobber", "-o", EscapedName, Url);

        byte[] expected = [.. "Warning: Failed to open the file "u8, 0xEF, 0xBF, 0xBD, .. ".txt: File exists"u8];
        CollectionAssert.AreEqual(expected, standardError.ToArray()[..expected.Length]);
    }

    private void TakeNameAndNumbers()
    {
        outputFiles.ExistingPaths.Add(EscapedName);
        for (int number = 1; number <= 99; number++)
        {
            outputFiles.ExistingPaths.Add(EscapedName + "." + number);
        }
    }

    private async Task<int> RunAsync(bool runsOnWindows, params string[] arguments)
    {
        Diagnostics.Arrange("runs on Windows", runsOnWindows);
        Diagnostics.Arrange("command line", string.Join(' ', arguments));
        ScriptedConnector server = new([Encoding.Latin1.GetBytes(Ok)]);
        HttpProtocolHandler http = new(server, new BasicAndBearerAuthenticator(CredentialEncoding.ForPlatform(isWindows: false)));

        int exitCode = await new CurlCommandRunner(
                _ => new TransferDispatch(new ProtocolDispatcher([http])),
                outputFiles,
                outputFiles,
                standardOutput,
                standardError,
                new MemoryStream(),
                runsOnWindows: runsOnWindows,
                outputPaths: outputFiles)
            .RunAsync(arguments);

        Diagnostics.Act("exit code", exitCode);
        Diagnostics.Bytes("stdout", standardOutput.ToArray());
        Diagnostics.Bytes("stderr", standardError.ToArray());
        return exitCode;
    }
}
