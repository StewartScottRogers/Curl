using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Pop3.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Pop3;

/// <summary>
/// Pins <c>--max-filesize</c> on a POP3 body against curl 8.21.0 (BL-1291): measured on
/// 2026-10-02 with <c>Record-CurlExchange.ps1 -Pop3</c> (the default message) and
/// <c>-sv --max-filesize 3 pop3://u:p@127.0.0.1:PORT/1</c>: exit 63, stdout <c>Fro</c>,
/// <c>{ [24 bytes data]</c>, <c>* Exceeded the maximum allowed file size (3) with 3 bytes</c>,
/// <c>* shutting down connection #0</c>, and <c>QUIT</c> still sent.
/// </summary>
[TestClass]
public sealed class Pop3ProtocolHandlerMaxFileSizeTests
{
    /// <summary>Gets or sets the running test's context, which MSTest sets.</summary>
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private const string Opening =
        "+OK POP3 ready <1896.697170952@localhost>\r\n"
        + "+OK Capability list follows\r\nUSER\r\nSASL PLAIN LOGIN\r\nSTLS\r\nTOP\r\nUIDL\r\n.\r\n";

    /// <summary>The recorder's default <c>-Pop3Message</c>, as curl writes it.</summary>
    private const string Message =
        "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\n"
        + "Hello from the recorder.\r\n.A line that starts with a dot.\r\n";

    private const string RetrReply =
        "+OK 133 octets\r\nFrom: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\n"
        + "Hello from the recorder.\r\n..A line that starts with a dot.\r\n.\r\n";

    private const string Listing = "1 133\r\n2 52\r\n3 7\r\n";

    private const string Bye = "+OK Bye\r\n";

    private const string Url = "pop3://127.0.0.1:18110/";

    [TestMethod]
    public async Task ExecuteAsync_RetrPastTheLimit_WritesUpToItFailsWithExit63AndStillQuits()
    {
        (TransferResult result, RecordingTransferEvents events, string output, string sent) = await RunAsync(Url + "1", 3, RetrReply);

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (3) with 3 bytes", 3),
            result);
        Diagnostics.AssertValues("output", "Fro", output);
        Assert.AreEqual("Fro", output);
        CollectionAssert.AreEqual((string[])["{ 24"], events.Transcript.Where(line => line.StartsWith('{')).ToArray());
        CollectionAssert.AreEqual(
            (string[])["* Exceeded the maximum allowed file size (3) with 3 bytes", "* shutting down connection #0"],
            events.Transcript.TakeLast(2).ToArray());
        Assert.EndsWith("RETR 1\r\nQUIT\r\n", sent);
    }

    [TestMethod]
    public async Task ExecuteAsync_LimitInsideTheThirdLine_WritesTwoLinesWholeAndCutsTheThird()
    {
        // 26 + 27 bytes for the first two lines, then 7 of "Subject: Recorded".
        (TransferResult result, _, string output, _) = await RunAsync(Url + "1", 60, RetrReply);

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (60) with 60 bytes", 60),
            result);
        Diagnostics.AssertValues("output", "From: sender@example.com\r\nTo: recipient@example.com\r\nSubject", output);
        Assert.AreEqual("From: sender@example.com\r\nTo: recipient@example.com\r\nSubject", output);
    }

    [TestMethod]
    public async Task ExecuteAsync_ListingPastTheLimit_CutsTheListingAndStillQuits()
    {
        (TransferResult result, _, string output, string sent) = await RunAsync(Url, 8, "+OK 3 messages\r\n" + Listing + ".\r\n");

        Assert.AreEqual(
            TransferResult.Failure(CurlExitCode.FilesizeExceeded, "Exceeded the maximum allowed file size (8) with 8 bytes", 8),
            result);
        Diagnostics.AssertValues("output", "1 133\r\n2", output);
        Assert.AreEqual("1 133\r\n2", output);
        Assert.EndsWith("LIST\r\nQUIT\r\n", sent);
    }

    [TestMethod]
    [DataRow(0L, DisplayName = "0 is no limit")]
    [DataRow(null, DisplayName = "null is no limit")]
    [DataRow(133L, DisplayName = "exactly the message's length")]
    public async Task ExecuteAsync_LimitNotExceeded_WritesTheWholeMessage(long? maxFileSize)
    {
        (TransferResult result, _, string output, string sent) = await RunAsync(Url + "1", maxFileSize, RetrReply);

        Diagnostics.AssertValues("result", TransferResult.Success(133), result);
        Assert.AreEqual(TransferResult.Success(133), result);
        Diagnostics.AssertValues("output", Message, output);
        Assert.AreEqual(Message, output);
        Assert.EndsWith("QUIT\r\n", sent);
    }

    private async Task<(TransferResult Result, RecordingTransferEvents Events, string Output, string Sent)> RunAsync(
        string url, long? maxFileSize, string reply)
    {
        var connection = new ScriptedConnection([.. new[] { Opening, reply, Bye }.Select(Encoding.Latin1.GetBytes)]);
        var events = new RecordingTransferEvents();
        using var output = new MemoryStream();
        var context = new TransferContext { Url = CurlUrl.Parse(url), Output = output, Events = events, MaxFileSize = maxFileSize };

        Diagnostics.ArrangeRun(url, connection.Script);
        Diagnostics.Arrange("max file size", maxFileSize?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "(none)");
        TransferResult result = await new Pop3ProtocolHandler(new QueuedConnector(ConnectResult.Connected(connection)), new QueuedTlsProvider()).ExecuteAsync(context);
        Diagnostics.ActTransfer(result, events, connection.Sent);
        Diagnostics.Bytes("output", output.ToArray());

        return (result, events, Encoding.Latin1.GetString(output.ToArray()), Encoding.Latin1.GetString(connection.Sent));
    }
}
