using System.Globalization;
using System.Text;
using Curl.Api.Options.Http;
using Curl.Cookies;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// The cookies of one run, as curl 8.21.0's tool sets them up from <c>-b</c>, <c>-c</c> and
/// <c>-j</c>: one <see cref="CookieStore" /> shared by every URL, the <c>-b</c> files loaded into
/// it before the first <c>http</c> or <c>https</c> transfer, and the <c>-c</c> jar written from it after each HTTP transfer.
/// </summary>
/// <remarks>
/// Measured on curl 8.21.0 (mingw, Schannel) against a loopback recorder on 2026-09-26 (BL-237
/// Notes). A <c>-b</c> value holding a <c>=</c> is sent verbatim after the stored cookies. The
/// cookie engine - storing the <c>Set-Cookie</c> headers received and sending them to later
/// requests - is on only when a <c>-b</c> file is named (one that does not exist included) or
/// <c>-c</c> is given; with nothing but <c>-b name=value</c> strings, received cookies are
/// never stored. The <c>-b name=value</c> strings are left out altogether when an <c>-H</c> value
/// names <c>Cookie</c> (<c>-H "Cookie:"</c> included), while the stored cookies are still sent
/// (BL-182 Notes, BL-291).
/// </remarks>
internal sealed class CookieEngine
{
    /// <summary>The <c>-c</c> value that writes the jar to standard output.</summary>
    private const string StandardOutputJar = "-";

    /// <summary>The <c>-b</c> file name that reads the cookies from standard input.</summary>
    private const string StandardInputCookieFile = "-";

    private readonly CookieStore store;

    /// <summary>
    /// curl's <c>MAX_COOKIE_LINE</c>: the joined <c>-b name=value</c> strings must stay shorter
    /// than this many bytes.
    /// </summary>
    internal const int LongestCookieLine = 8200;

    /// <summary>
    /// The warning text, after <c>Warning: </c>, curl 8.21.0's <c>cookie_setopts</c> writes before
    /// it fails a transfer whose joined <c>-b name=value</c> strings reach <see cref="LongestCookieLine" />.
    /// </summary>
    internal const string CookieLineTooLongWarning = "skipped provided cookie, the cookie header would go over 8200 bytes";

    private readonly string joinedCookieString;

    private readonly IReadOnlyList<string> cookieFiles;

    private readonly string? cookieJar;

    private readonly bool discardSessionCookies;

    private bool cookieFilesLoaded;

    private CookieEngine(EasyHttpOptions http, CookieStore store)
    {
        this.store = store;
        joinedCookieString = http.Cookie ?? string.Empty;
        cookieFiles = http.CookieFiles;
        cookieJar = http.CookieJar;
        discardSessionCookies = http.CookieSession;
        HandlerStore = HandlerStoreOf(http, store, SentCookieStringsOf(joinedCookieString, http.HttpHeaders));
    }

    /// <summary>
    /// Gets a value indicating whether the group's joined <c>-b name=value</c> strings
    /// (<see cref="EasyHttpOptions.Cookie" />) reach <see cref="LongestCookieLine" /> bytes in
    /// UTF-8, so every transfer of the group fails with exit 100 before it connects, as curl
    /// 8.21.0's <c>cookie_setopts</c> fails it - whether or not an <c>-H</c> value names
    /// <c>Cookie</c> (measured 2026-10-03, BL-1391).
    /// </summary>
    internal bool CookieLineTooLong => Encoding.UTF8.GetByteCount(joinedCookieString) >= LongestCookieLine;

    /// <summary>
    /// Gets the store the HTTP handler reads and writes: the run's <see cref="CookieStore" />, with
    /// this option group's <c>-b</c> strings, when the group's cookie engine is on, or else one that
    /// sends the group's <c>-b</c> strings and stores nothing.
    /// </summary>
    internal ICookieStore HandlerStore { get; }

    /// <summary>
    /// Creates the cookies of a run of one option group from its HTTP options, with a store of
    /// their own.
    /// </summary>
    /// <param name="http">
    /// The group's HTTP options: <see cref="EasyHttpOptions.Cookie" /> (the <c>-b name=value</c>
    /// strings, joined), <see cref="EasyHttpOptions.CookieFiles" />, <see cref="EasyHttpOptions.CookieJar" />,
    /// <see cref="EasyHttpOptions.CookieSession" /> and <see cref="EasyHttpOptions.HttpHeaders" />.
    /// </param>
    /// <returns>The run's cookies, or <see langword="null" /> when neither <c>-b</c> nor <c>-c</c> was given.</returns>
    internal static CookieEngine? FromHttpOptions(EasyHttpOptions http) =>
        FromHttpOptions(http, new CookieStore());

    /// <summary>
    /// Creates one <c>-:</c> / <c>--next</c> option group's cookies over the run's
    /// <paramref name="runCookies" />, which every group shares, as curl 8.21.0 shares its cookie
    /// list between groups while each group's <c>-b</c>, <c>-c</c> and <c>-j</c> are its own:
    /// <c>-c j1 A --next -c j2 B</c> sent A's cookie to B and wrote both to <c>j2</c>, while
    /// <c>-c j A --next B</c> sent B none and <c>A --next -c j B</c> kept only B's (measured
    /// 2026-09-28, BL-509 Notes).
    /// </summary>
    /// <param name="http">The option group's HTTP options.</param>
    /// <param name="runCookies">The cookies stored and loaded by the run so far.</param>
    /// <returns>The group's cookies, or <see langword="null" /> when the group gives neither <c>-b</c> nor <c>-c</c>.</returns>
    internal static CookieEngine? FromHttpOptions(EasyHttpOptions http, CookieStore runCookies) =>
        http.Cookie is not null || http.CookieFiles.Count > 0 || http.CookieJar is not null ? new CookieEngine(http, runCookies) : null;

    /// <summary>
    /// Loads every <c>-b</c> file, in command-line order, dropping session cookies under <c>-j</c>,
    /// the first time it is called; later calls load nothing. A file that cannot be opened loads
    /// nothing and is not an error, as in curl. <c>-b -</c> reads standard input to its end, so a
    /// second <c>-b -</c> finds it empty (measured on curl 8.21.0 on 2026-09-27, BL-316 Notes).
    /// </summary>
    /// <param name="fileSystem">Opens the files.</param>
    /// <param name="standardInput">What <c>-b -</c> reads; it is left open.</param>
    /// <param name="now">The time that decides which loaded cookies have expired.</param>
    /// <param name="events">Where the <c>-v</c> line for each refused <c>Set-Cookie:</c> line is reported.</param>
    /// <returns>A task that completes when every file is loaded.</returns>
    internal async Task LoadCookieFilesAsync(IFileSystem fileSystem, Stream standardInput, DateTimeOffset now, ITransferEvents events)
    {
        if (cookieFilesLoaded)
        {
            return;
        }

        cookieFilesLoaded = true;
        foreach (string cookieFile in cookieFiles)
        {
            if (cookieFile == StandardInputCookieFile)
            {
                using StreamReader reader = new(standardInput, Encoding.Latin1, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                using StringReader text = new(await reader.ReadToEndAsync().ConfigureAwait(false));
                store.LoadCookieFile(text, discardSessionCookies, now, events);
                continue;
            }

            await store.LoadCookieFileAsync(fileSystem, cookieFile, discardSessionCookies, now, events, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes the <c>-c</c> jar, when one was given: to standard output for <c>-c -</c>, its lines
    /// ending in a line feed, or else to the named file through
    /// <see cref="CookieStore.SaveCookieJarAsync" />, replacing it. A jar that cannot be written is
    /// ignored, as curl 8.21.0 ignores it.
    /// </summary>
    /// <param name="fileSystem">Opens the jar file.</param>
    /// <param name="standardOutput">Where <c>-c -</c> writes, already in the mode standard output is in.</param>
    /// <param name="now">The time that decides which cookies have expired.</param>
    /// <returns>A task that completes when the jar is written.</returns>
    internal async Task WriteCookieJarAsync(IFileSystem fileSystem, Stream standardOutput, DateTimeOffset now)
    {
        if (cookieJar is null)
        {
            return;
        }

        if (cookieJar != StandardOutputJar)
        {
            await store.SaveCookieJarAsync(fileSystem, cookieJar, now, CancellationToken.None).ConfigureAwait(false);
            return;
        }

        using StringWriter jar = new(CultureInfo.InvariantCulture) { NewLine = "\n" };
        store.WriteCookieJar(jar, now);
        await standardOutput.WriteAsync(Encoding.Latin1.GetBytes(jar.ToString())).ConfigureAwait(false);
        await standardOutput.FlushAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// The <c>-b name=value</c> strings sent: the joined string, unless it is empty or an <c>-H</c>
    /// value names <c>Cookie</c>.
    /// </summary>
    /// <param name="joinedCookieString">The joined <c>-b name=value</c> strings.</param>
    /// <param name="headers">The <c>-H</c> values.</param>
    /// <returns>The strings sent, none or one.</returns>
    private static string[] SentCookieStringsOf(string joinedCookieString, IReadOnlyList<string> headers) =>
        joinedCookieString.Length > 0 && !headers.Any(NamesCookie) ? [joinedCookieString] : [];

    /// <summary>
    /// The store the HTTP handler reads and writes: the run's, while a <c>-b</c> file or <c>-c</c>
    /// turns the cookie engine on, or else one that only sends <paramref name="sentCookieStrings" />.
    /// </summary>
    /// <param name="http">The option group's HTTP options.</param>
    /// <param name="store">The run's store.</param>
    /// <param name="sentCookieStrings">The <c>-b name=value</c> strings sent.</param>
    /// <returns>The handler's store.</returns>
    private static ICookieStore HandlerStoreOf(EasyHttpOptions http, CookieStore store, string[] sentCookieStrings) =>
        http.CookieFiles.Count > 0 || http.CookieJar is not null
            ? new GroupCookies(store, sentCookieStrings)
            : new CookieStringSender(sentCookieStrings);

    /// <summary>
    /// Tells whether an <c>-H</c> value names <c>Cookie</c> as curl's <c>Curl_checkheaders</c>
    /// matches it: the name, in any case, followed by <c>:</c> or <c>;</c>.
    /// </summary>
    /// <param name="header">The <c>-H</c> value.</param>
    /// <returns><see langword="true" /> when it names <c>Cookie</c>.</returns>
    private static bool NamesCookie(string header) =>
        header.StartsWith("Cookie:", StringComparison.OrdinalIgnoreCase)
        || header.StartsWith("Cookie;", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Sends the run's stored cookies and then one option group's <c>-b name=value</c> strings, and
    /// stores what the responses set in the run's store, as curl does while the group's cookie
    /// engine is on.
    /// </summary>
    /// <param name="store">The run's store.</param>
    /// <param name="cookieStrings">The group's <c>-b name=value</c> strings.</param>
    private sealed class GroupCookies(CookieStore store, IReadOnlyList<string> cookieStrings) : ICookieStore
    {
        public string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now, ITransferEvents events) =>
            store.GetCookieHeader(url, secure, now, cookieStrings, events);

        public string? GetStoredCookieHeader(CurlUrl url, bool secure, DateTimeOffset now, ITransferEvents events) =>
            store.GetCookieHeader(url, secure, now, events);

        public int StoreFromResponse(CurlUrl url, string setCookieHeader, int storedFromResponse, DateTimeOffset now, ITransferEvents events) =>
            store.StoreFromResponse(url, setCookieHeader, storedFromResponse, now, events);
    }

    /// <summary>
    /// Sends one option group's <c>-b name=value</c> strings and stores nothing, as curl does while
    /// the group's cookie engine is off.
    /// </summary>
    /// <param name="cookieStrings">The group's <c>-b name=value</c> strings.</param>
    private sealed class CookieStringSender(IReadOnlyList<string> cookieStrings) : ICookieStore
    {
        public string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now, ITransferEvents events) =>
            cookieStrings.Count == 0 ? null : string.Join("; ", cookieStrings);

        public string? GetStoredCookieHeader(CurlUrl url, bool secure, DateTimeOffset now, ITransferEvents events) =>
            null;

        public int StoreFromResponse(CurlUrl url, string setCookieHeader, int storedFromResponse, DateTimeOffset now, ITransferEvents events) =>
            storedFromResponse;
    }
}
