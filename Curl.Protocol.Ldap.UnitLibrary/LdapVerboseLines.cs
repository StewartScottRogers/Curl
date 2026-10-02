using System.Globalization;
using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Ldap;

/// <summary>
/// The <c>-v</c> information lines each reference build writes during an LDAP transfer,
/// measured with <c>Record-CurlExchange.ps1 -Script</c> (BL-589): curl 8.21.0 with WinLDAP
/// names the library and the URL once connected and says whether the connection is cleartext
/// or encrypted, curl 8.18.0 with OpenLDAP names the URL once bound.
/// </summary>
internal static class LdapVerboseLines
{
    /// <summary>The line WinLDAP's build writes first once connected.</summary>
    internal const string WinLdapVendor = "LDAP local: LDAP Vendor = Microsoft Corporation. ; LDAP Version = 510";

    /// <summary>The OpenLDAP build's connection number for a URL refused before a connection was made.</summary>
    internal const long NoConnection = -1;

    /// <summary>curl's texts for its exit codes, which it returns without writing them as a <c>-v</c> line.</summary>
    private static readonly string[] UnwrittenMessages = ["Login denied", "LDAP: cannot bind", "Failure when receiving data from the peer"];

    /// <summary>Formats the line naming the URL as curl holds it.</summary>
    /// <param name="url">The transfer's URL.</param>
    /// <returns>The line, such as <c>LDAP local: ldap://127.0.0.1:18389/dc=example</c>.</returns>
    /// <remarks>
    /// curl holds the URL as typed, with its scheme in lower case and a <c>/</c> path when the
    /// URL has none; user information, port, query and fragment stay as typed.
    /// </remarks>
    internal static string Url(CurlUrl url)
    {
        string typed = url.OriginalString;
        int schemeEnd = typed.IndexOf("://", StringComparison.Ordinal);
        string rest = schemeEnd < 0 ? typed : typed[(schemeEnd + 3)..];
        int authorityEnd = rest.IndexOfAny(['/', '?', '#']);
        string withPath = authorityEnd < 0 ? rest + "/"
            : rest[authorityEnd] == '/' ? rest
            : rest.Insert(authorityEnd, "/");
        return "LDAP local: " + url.Scheme + "://" + withPath;
    }

    /// <summary>Formats WinLDAP's build's line before it binds.</summary>
    /// <param name="encrypted">Whether the connection is <c>ldaps</c>.</param>
    /// <returns>The line.</returns>
    internal static string Establishing(bool encrypted) =>
        "LDAP local: trying to establish " + (encrypted ? "encrypted" : "cleartext") + " connection";

    /// <summary>Reports a failed transfer's message, unless it is curl's text for the exit code, which curl does not write.</summary>
    /// <param name="events">Where the line goes.</param>
    /// <param name="result">The transfer's result.</param>
    internal static void ReportFailure(ITransferEvents events, TransferResult result)
    {
        if (result.ExitCode != CurlExitCode.Ok && !UnwrittenMessages.Contains(result.ErrorMessage!))
        {
            events.ReportInfo(result.ErrorMessage!);
        }
    }

    /// <summary>Formats the line both builds write after a search the server ended with <c>sizeLimitExceeded</c>.</summary>
    /// <param name="entryCount">The number of entries written.</param>
    /// <returns>The line, such as <c>There are more than 2 entries</c>.</returns>
    internal static string MoreThan(int entryCount) =>
        string.Create(CultureInfo.InvariantCulture, $"There are more than {entryCount} entries");

    /// <summary>Formats the line for a connection curl keeps once the transfer is done.</summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <param name="host">The host the connection was opened to.</param>
    /// <param name="port">The port the connection was opened to.</param>
    /// <returns>The line, such as <c>Connection #0 to host 127.0.0.1:18389 left intact</c>.</returns>
    internal static string LeftIntact(long connectionNumber, string host, int port) =>
        string.Create(CultureInfo.InvariantCulture, $"Connection #{connectionNumber} to host {host}:{port} left intact");

    /// <summary>Formats the line for the connection WinLDAP's build closes after every transfer.</summary>
    /// <param name="connectionNumber">curl's number for the connection.</param>
    /// <returns>The line, such as <c>shutting down connection #0</c>.</returns>
    internal static string ShuttingDown(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"shutting down connection #{connectionNumber}");

    /// <summary>Formats the line for the connection the OpenLDAP build closes after a failure.</summary>
    /// <param name="connectionNumber">curl's number for the connection; <see cref="NoConnection" /> when none was made.</param>
    /// <returns>The line, such as <c>closing connection #0</c>.</returns>
    internal static string Closing(long connectionNumber) =>
        string.Create(CultureInfo.InvariantCulture, $"closing connection #{connectionNumber}");
}
