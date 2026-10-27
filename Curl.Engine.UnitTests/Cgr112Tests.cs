using System.Text;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;
using Curl.Api.Options.Proxy;
using Curl.Cli;
using Curl.Console;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// Pins CGR-112 (BL-3641): curl 8.22.0's Windows build reads the standard input a <c>telnet</c>
/// transfer without <c>-T</c> sends in text mode, so a Ctrl-Z (0x1A) ends it and CR LF reads as LF.
/// Measured on Windows with the local curl 8.22.0 on 2026-10-26: <c>a</c> CR LF <c>b</c> CR LF Ctrl-Z
/// CR LF <c>c</c> LF goes on the wire as <c>a</c> LF <c>b</c> LF, and <c>GET /x HTTP/1.0</c> CR LF CR LF
/// as <c>GET /x HTTP/1.0</c> LF LF; <c>-T -</c> sends every byte.
/// </summary>
[TestClass]
public sealed class Cgr112Tests
{
    private const string CrLfAndCtrlZ = "a\r\nb\r\n\u001A\r\nc\n";

    private static readonly CommandLineOptions NoOptions = CommandLineParser.Parse(["telnet://127.0.0.1/"], _ => true).Options!;

    [TestMethod]
    public async Task Create_TelnetWithoutUploadInTextMode_SendsStandardInputAsTextMode()
    {
        string sent = await ReadAllAsync(UploadOf(CrLfAndCtrlZ, readsInTextMode: true, upload: null));

        Assert.AreEqual("a\nb\n", sent);
    }

    [TestMethod]
    public async Task Create_TelnetWithoutUploadOffWindows_SendsEveryByte()
    {
        string sent = await ReadAllAsync(UploadOf(CrLfAndCtrlZ, readsInTextMode: false, upload: null));

        Assert.AreEqual(CrLfAndCtrlZ, sent);
    }

    [TestMethod]
    public async Task Create_TelnetWithUploadInTextMode_SendsTheUploadUnchanged()
    {
        using MemoryStream upload = new(Encoding.Latin1.GetBytes(CrLfAndCtrlZ));

        string sent = await ReadAllAsync(UploadOf(string.Empty, readsInTextMode: true, upload));

        Assert.AreEqual(CrLfAndCtrlZ, sent);
    }

    [TestMethod]
    public async Task ReadAsync_RequestEndingInCrLf_TurnsEachPairIntoLf()
    {
        string sent = await ReadAllAsync(TextMode("GET /x HTTP/1.0\r\n\r\n"));

        Assert.AreEqual("GET /x HTTP/1.0\n\n", sent);
    }

    [TestMethod]
    public async Task ReadAsync_CrSplitFromItsLfAcrossRawReads_StillTurnsThePairIntoLf()
    {
        using WindowsTextModeReadStream stream = new(new OneByteAtATimeStream(Encoding.Latin1.GetBytes("a\r\nb\rc\r")));

        string sent = await ReadAllAsync(stream, readSize: 1);

        Assert.AreEqual("a\nb\rc\r", sent);
    }

    [TestMethod]
    public async Task ReadAsync_CrBeforeCtrlZ_KeepsTheCr()
    {
        string sent = await ReadAllAsync(TextMode("a\r\u001Ab"));

        Assert.AreEqual("a\r", sent);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBuffer_ReadsNothing()
    {
        using WindowsTextModeReadStream stream = TextMode("a");

        int read = await stream.ReadAsync(Memory<byte>.Empty);

        Assert.AreEqual(0, read);
        Assert.AreEqual("a", await ReadAllAsync(stream));
    }

    [TestMethod]
    public async Task ReadAsync_AfterTheEnd_ReadsNothingMore()
    {
        using WindowsTextModeReadStream stream = TextMode("a\u001Ab");
        Assert.AreEqual("a", await ReadAllAsync(stream));

        int read = await stream.ReadAsync(new byte[4]);

        Assert.AreEqual(0, read);
    }

    [TestMethod]
    public void Read_SynchronousRead_ReadsTextMode()
    {
        using WindowsTextModeReadStream stream = TextMode("a\r\n");
        byte[] buffer = new byte[4];

        int read = stream.Read(buffer, 0, buffer.Length);

        Assert.AreEqual("a\n", Encoding.Latin1.GetString(buffer, 0, read));
    }

    [TestMethod]
    public void Members_OfAReadOnlyStream_RefuseEverythingButReading()
    {
        using WindowsTextModeReadStream stream = TextMode(string.Empty);

        stream.Flush();

        Assert.IsTrue(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write([], 0, 0));
    }

    private static WindowsTextModeReadStream TextMode(string raw) => new(new MemoryStream(Encoding.Latin1.GetBytes(raw)));

    private static Stream UploadOf(string standardInput, bool readsInTextMode, Stream? upload)
    {
        using MemoryStream output = new();
        TransferContextFactory factory = new(new MemoryStream(Encoding.Latin1.GetBytes(standardInput))) { ReadsTelnetStandardInputInTextMode = readsInTextMode };
        return factory.Create(
            EasyOptionsFromCommandLine.TransferSettingsOf(NoOptions),
            new EasyHttpOptions(),
            new EasyBehaviourOptions(),
            new EasyTransferOptions(),
            new EasyProtocolOptions(),
            new EasyProxyOptions(),
            new EasyAuthenticationOptions(),
            new EasyNetworkOptions(),
            CurlUrl.Parse("telnet://127.0.0.1/"),
            output,
            null,
            null,
            null,
            upload: upload).Upload!;
    }

    private static async Task<string> ReadAllAsync(Stream stream, int readSize = 4096)
    {
        using MemoryStream all = new();
        byte[] buffer = new byte[readSize];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            all.Write(buffer, 0, read);
        }

        return Encoding.Latin1.GetString(all.ToArray());
    }

    /// <summary>A stream that gives one byte per read, so a CR LF pair arrives in two raw reads.</summary>
    private sealed class OneByteAtATimeStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
