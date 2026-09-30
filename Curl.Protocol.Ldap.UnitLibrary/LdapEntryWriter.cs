using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Writes a search's entries to the transfer's output when each build writes them (measured
/// by BL-588): the OpenLDAP build as each entry arrives, the Windows build only once the
/// search has succeeded, because curl's <c>lib/ldap.c</c> waits for the whole result with
/// <c>ldap_search_s</c> before it writes a byte.
/// </summary>
/// <remarks>
/// Each entry is written in the pieces <see cref="LdapEntryFormatter.FormatPieces" /> gives,
/// one write each, as curl does, and reported to <see cref="ITransferContext.Events" /> as
/// received data first, for <c>-v</c>'s <c>{ [N bytes data]</c> line. An output that stops accepting bytes ends the writing with
/// exit 23 <c>Failure writing output to destination, passed N returned M</c>: <c>N</c> the
/// size of the piece it failed on, <c>M</c> the bytes of it the output accepted, as
/// <see cref="OutputWriteFailedException.BytesAccepted" /> says, and 0 for any other
/// <see cref="IOException" /> (measured by BL-845).
/// </remarks>
/// <param name="dialect">The build to answer as.</param>
/// <param name="context">The transfer, whose output and progress are written to.</param>
internal sealed class LdapEntryWriter(LdapDialect dialect, ITransferContext context)
{
    /// <summary>The Windows build's pieces, held until the search succeeds.</summary>
    private readonly List<byte[]> held = [];

    /// <summary>Gets how many bytes have been written to the output.</summary>
    public long BytesWritten { get; private set; }

    /// <summary>Gets how many entries have been added, written or held.</summary>
    public int EntryCount { get; private set; }

    /// <summary>Gets the exit 23 failure the output ended the writing with; <see langword="null" /> while it accepts every byte.</summary>
    public TransferResult? WriteFailure { get; private set; }

    /// <summary>Writes <paramref name="entry" />, or holds it for the Windows build.</summary>
    /// <param name="entry">The entry that arrived.</param>
    /// <returns><see langword="false" /> when the output failed and <see cref="WriteFailure" /> says how.</returns>
    public ValueTask<bool> AddAsync(LdapSearchEntry entry)
    {
        IReadOnlyList<byte[]> pieces = LdapEntryFormatter.FormatPieces(dialect, entry);
        EntryCount++;
        if (dialect == LdapDialect.WinLdap)
        {
            held.AddRange(pieces);
            return ValueTask.FromResult(true);
        }

        return WriteAsync(pieces);
    }

    /// <summary>Writes the entries the Windows build held, once its search has succeeded.</summary>
    /// <returns><see langword="false" /> when the output failed and <see cref="WriteFailure" /> says how.</returns>
    public ValueTask<bool> WriteHeldAsync() => WriteAsync(held);

    private async ValueTask<bool> WriteAsync(IReadOnlyList<byte[]> pieces)
    {
        foreach (byte[] piece in pieces.Where(piece => piece.Length > 0))
        {
            context.Events.ReportDataReceived(piece);
            try
            {
                await context.Output.WriteAsync(piece, context.CancellationToken).ConfigureAwait(false);
            }
            catch (IOException exception)
            {
                WriteFailure = TransferResult.Failure(CurlExitCode.WriteError, OutputWriteFailed(piece.Length, BytesAcceptedBy(exception)));
                return false;
            }

            BytesWritten += piece.Length;
            context.Progress.ReportDownloaded(BytesWritten, null);
        }

        return true;
    }

    /// <summary>curl's exit 23 message for a write of <paramref name="passed" /> bytes of which the output took <paramref name="returned" />.</summary>
    private static string OutputWriteFailed(int passed, int returned) =>
        string.Create(CultureInfo.InvariantCulture, $"Failure writing output to destination, passed {passed} returned {returned}");

    /// <summary>
    /// How many bytes of a failed write the output accepted: the count an
    /// <see cref="OutputWriteFailedException" /> carries, and 0 for any other
    /// <see cref="IOException" />.
    /// </summary>
    private static int BytesAcceptedBy(IOException exception) =>
        exception is OutputWriteFailedException failed ? failed.BytesAccepted : 0;
}
