using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Writes a search's entries to the transfer's output when each build writes them (measured
/// by BL-588): the OpenLDAP build as each entry arrives, the Windows build only once the
/// search has succeeded, because curl's <c>lib/ldap.c</c> waits for the whole result with
/// <c>ldap_search_s</c> before it writes a byte.
/// </summary>
/// <param name="dialect">The build to answer as.</param>
/// <param name="context">The transfer, whose output and progress are written to.</param>
internal sealed class LdapEntryWriter(LdapDialect dialect, ITransferContext context)
{
    /// <summary>The Windows build's entries, held until the search succeeds.</summary>
    private readonly MemoryStream held = new();

    /// <summary>Gets how many bytes have been written to the output.</summary>
    public long BytesWritten { get; private set; }

    /// <summary>Writes <paramref name="entry" />, or holds it for the Windows build.</summary>
    /// <param name="entry">The entry that arrived.</param>
    /// <returns>A task that completes when the entry is written or held.</returns>
    public async ValueTask AddAsync(LdapSearchEntry entry)
    {
        byte[] text = LdapEntryFormatter.Format(dialect, entry);
        if (dialect == LdapDialect.WinLdap)
        {
            held.Write(text);
            return;
        }

        await WriteAsync(text).ConfigureAwait(false);
    }

    /// <summary>Writes the entries the Windows build held, once its search has succeeded.</summary>
    /// <returns>A task that completes when they are written.</returns>
    public ValueTask WriteHeldAsync() => WriteAsync(held.ToArray());

    private async ValueTask WriteAsync(byte[] text)
    {
        if (text.Length == 0)
        {
            return;
        }

        await context.Output.WriteAsync(text, context.CancellationToken).ConfigureAwait(false);
        BytesWritten += text.Length;
        context.Progress.ReportDownloaded(BytesWritten, null);
    }
}
