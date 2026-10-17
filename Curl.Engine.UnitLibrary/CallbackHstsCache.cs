using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.Options.Http;
using Curl.Core.Hsts;
using HstsEntry = Curl.Api.Callbacks.HstsEntry;

namespace Curl.Engine;

/// <summary>
/// <c>CURLOPT_HSTSREADFUNCTION</c> and <c>CURLOPT_HSTSWRITEFUNCTION</c> over a handle's HSTS cache, as curl 8.22.0's
/// <c>hsts_pull</c> and <c>hsts_push</c> do (ADR-0620): <see cref="Load" /> asks the read callback for entries until
/// it stops answering <see cref="HstsResult.Ok" />, and <see cref="Save" /> hands the write callback every entry the
/// cache holds, in order, until it stops answering <see cref="HstsResult.Ok" />. Both go through the cache's file
/// text (<see cref="HstsTransferPolicy.ReadFile" /> and <see cref="HstsTransferPolicy.FormatFile" />), the one way
/// in and out <see cref="HstsTransferPolicy" /> offers, whose expiry text is the callbacks' own
/// <c>YYYYMMDD HH:MM:SS</c>; so an entry the file reader would skip (an expiry already past or unreadable, a
/// name holding a space) is skipped from the read callback too.
/// </summary>
internal static class CallbackHstsCache
{
    /// <summary>What a file line holds in place of an expiry for an entry that never expires.</summary>
    private const string UnlimitedText = "unlimited";

    /// <summary>
    /// Whether a handle's HSTS cache exists for the callbacks: under <c>CURLOPT_HSTS_CTRL</c>'s
    /// <see cref="HstsControl.Enable" />, or with a <c>CURLOPT_HSTS</c> file set, as libcurl creates its cache for either.
    /// </summary>
    /// <param name="http">The handle's HTTP options.</param>
    /// <returns><see langword="true" /> when the callbacks are called.</returns>
    internal static bool IsEnabled(EasyHttpOptions http) =>
        (http.HstsControl & HstsControl.Enable) != 0 || http.Hsts is not null;

    /// <summary>
    /// Asks <paramref name="read" /> for entries and adds each one to <paramref name="hsts" />, as libcurl does before
    /// each transfer: <see cref="HstsResult.Ok" /> adds the entry handed over and asks again, <see cref="HstsResult.Done" />
    /// stops, <see cref="HstsResult.Fail" /> fails the transfer with <see cref="CurlCode.AbortedByCallback" />, and an
    /// <see cref="HstsResult.Ok" /> with no entry or an empty name with <see cref="CurlCode.BadFunctionArgument" />.
    /// </summary>
    /// <param name="read">The read callback.</param>
    /// <param name="hsts">The handle's HSTS cache.</param>
    /// <returns><see cref="CurlCode.Ok" />, or the code that fails the transfer.</returns>
    internal static CurlCode Load(HstsReadCallback read, HstsTransferPolicy hsts)
    {
        HstsResult result;
        while ((result = read(out HstsEntry? entry)) == HstsResult.Ok)
        {
            if (entry is not { Name.Length: > 0 })
            {
                return CurlCode.BadFunctionArgument;
            }

            hsts.ReadFile(FileLineOf(entry));
        }

        return result == HstsResult.Fail ? CurlCode.AbortedByCallback : CurlCode.Ok;
    }

    /// <summary>
    /// Hands <paramref name="write" /> each entry of <paramref name="hsts" /> with its index and the count, as libcurl
    /// does when the handle is cleaned up; any answer but <see cref="HstsResult.Ok" /> stops, and libcurl's cleanup
    /// ignores a <see cref="HstsResult.Fail" />. An entry that never expires has a <see langword="null" />
    /// <see cref="HstsEntry.Expire" />.
    /// </summary>
    /// <param name="write">The write callback.</param>
    /// <param name="hsts">The handle's HSTS cache.</param>
    internal static void Save(HstsWriteCallback write, HstsTransferPolicy hsts)
    {
        // long.MaxValue as the limit: every expiry is written, so the text is never null.
        string[] lines = hsts.FormatFile("\n", long.MaxValue)!
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)[HstsCache.FileHeaderLines.Count..];
        for (int index = 0; index < lines.Length; index++)
        {
            if (write(EntryOf(lines[index]), index, lines.Length) != HstsResult.Ok)
            {
                return;
            }
        }
    }

    /// <summary>The file line of a read callback's entry: <c>[.]name "expiry"</c>, <c>unlimited</c> for none.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The line.</returns>
    private static string FileLineOf(HstsEntry entry) =>
        new StringBuilder()
            .Append(entry.IncludeSubDomains ? "." : string.Empty)
            .Append(entry.Name)
            .Append(" \"")
            .Append(entry.Expire is { Length: > 0 } expire ? expire : UnlimitedText)
            .Append('"')
            .ToString();

    /// <summary>The entry of one line <see cref="HstsTransferPolicy.FormatFile" /> wrote.</summary>
    /// <param name="line">The line: <c>[.]host "expiry"</c>.</param>
    /// <returns>The entry.</returns>
    private static HstsEntry EntryOf(string line)
    {
        int space = line.IndexOf(' ', StringComparison.Ordinal);
        bool includeSubDomains = line[0] == '.';
        string expiry = line[(space + 2)..^1];
        return new HstsEntry(
            line[(includeSubDomains ? 1 : 0)..space],
            includeSubDomains,
            expiry == UnlimitedText ? null : expiry);
    }
}
