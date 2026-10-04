using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Protocol.Ldap.Fakes;

namespace Curl.Protocol.Ldap;

/// <content>
/// Pins <see cref="ITransferContext.MaxFileSize" /> on a search as curl 8.21.0's download
/// writer (<c>cw_download_write</c> in <c>lib/sendf.c</c>) applies it to every piece both
/// <c>lib/ldap.c</c> and <c>lib/openldap.c</c> write (BL-1329): the piece that crosses the
/// limit is cut to the bytes left, and the transfer fails with exit 63.
/// </content>
public sealed partial class LdapProtocolHandlerTests
{
    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_OneEntryOverMaxFileSize_WritesTheFirstTenBytesAndFailsWith63(LdapDialect dialect)
    {
        byte[] whole = (await RunWithMaxFileSizeAsync(dialect, null, EntryDcA)).Output;

        (TransferResult result, byte[] output, List<string> lines) = await RunWithMaxFileSizeAsync(dialect, 10, EntryDcA);

        Assert.IsGreaterThan(10, whole.Length);
        Assert.AreEqual(new TransferResult(CurlExitCode.FilesizeExceeded, 10, "Exceeded the maximum allowed file size (10) with 10 bytes"), result);
        CollectionAssert.AreEqual(whole[..10], output);
        CollectionAssert.Contains(lines, "Exceeded the maximum allowed file size (10) with 10 bytes");
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_LimitInsideTheSecondEntry_WritesTheFirstEntryWholeAndCutsTheSecond(LdapDialect dialect)
    {
        byte[] first = (await RunWithMaxFileSizeAsync(dialect, null, EntryDcA)).Output;
        byte[] both = (await RunWithMaxFileSizeAsync(dialect, null, EntryDcA, EntryF)).Output;
        long limit = first.Length + 3;

        (TransferResult result, byte[] output, _) = await RunWithMaxFileSizeAsync(dialect, limit, EntryDcA, EntryF);

        Assert.AreEqual(CurlExitCode.FilesizeExceeded, result.ExitCode);
        Assert.AreEqual($"Exceeded the maximum allowed file size ({limit}) with {limit} bytes", result.ErrorMessage);
        CollectionAssert.AreEqual(first, output[..first.Length]);
        CollectionAssert.AreEqual(both[..(int)limit], output);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap, 0L)]
    [DataRow(LdapDialect.OpenLdap, 0L)]
    [DataRow(LdapDialect.WinLdap, null)]
    [DataRow(LdapDialect.OpenLdap, null)]
    public async Task ExecuteAsync_MaxFileSizeZeroOrUnset_WritesTheWholeOutput(LdapDialect dialect, long? maxFileSize)
    {
        byte[] whole = (await RunWithMaxFileSizeAsync(dialect, null, EntryDcA, EntryF)).Output;

        (TransferResult result, byte[] output, _) = await RunWithMaxFileSizeAsync(dialect, maxFileSize, EntryDcA, EntryF);

        Assert.AreEqual(TransferResult.Success(whole.Length), result);
        CollectionAssert.AreEqual(whole, output);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public async Task ExecuteAsync_OutputExactlyAtMaxFileSize_CompletesWithExit0(LdapDialect dialect)
    {
        byte[] whole = (await RunWithMaxFileSizeAsync(dialect, null, EntryDcA, EntryF)).Output;

        (TransferResult result, byte[] output, _) = await RunWithMaxFileSizeAsync(dialect, whole.Length, EntryDcA, EntryF);

        Assert.AreEqual(TransferResult.Success(whole.Length), result);
        CollectionAssert.AreEqual(whole, output);
    }

    /// <summary>Runs a search whose replies are <paramref name="entries" /> then a success, with <paramref name="maxFileSize" /> as the limit.</summary>
    private static async Task<(TransferResult Result, byte[] Output, List<string> Lines)> RunWithMaxFileSizeAsync(LdapDialect dialect, long? maxFileSize, params string[] entries)
    {
        var connection = new ScriptedConnection([Hex.Bytes(BindSuccess1), Hex.Bytes(string.Join(" ", entries) + " " + SearchSuccess)]);
        var handler = new LdapProtocolHandler(new RecordingConnector(ConnectResult.Connected(connection)), dialect);
        var output = new MemoryStream();
        var events = new RecordingTransferEvents();
        var context = new TransferContext
        {
            Url = CurlUrl.Parse("ldap://127.0.0.1:38901/x"),
            Output = output,
            Credentials = new NetworkCredential("cn=u", "p"),
            Events = events,
            MaxFileSize = maxFileSize,
        };

        TransferResult result = await handler.ExecuteAsync(context);

        return (result, output.ToArray(), events.Lines);
    }
}
