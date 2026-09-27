using System.Globalization;
using System.Text;
using Curl.Cli;
using Curl.Cookies;
using Curl.Protocol.Abstractions;

namespace Curl.Console;

/// <summary>
/// The cookies of one run, as curl 8.21.0's tool sets them up from <c>-b</c>, <c>-c</c> and
/// <c>-j</c>: one <see cref="CookieStore" /> shared by every URL, the <c>-b</c> files loaded into
/// it before the first transfer, and the <c>-c</c> jar written from it after each HTTP transfer.
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

    private readonly CookieStore store = new();

    private readonly List<string> cookieFiles = [];

    private readonly string? cookieJar;

    private readonly bool discardSessionCookies;

    private CookieEngine(CommandLineOptions options)
    {
        AddCookies(options.Cookies, sendCookieStrings: !options.Headers.Any(NamesCookie));
        cookieJar = options.CookieJar;
        discardSessionCookies = options.JunkSessionCookies;
        HandlerStore = cookieFiles.Count > 0 || cookieJar is not null ? store : new CookieStringSender(store);
    }

    /// <summary>
    /// Gets the store the HTTP handler reads and writes: the run's <see cref="CookieStore" /> when
    /// the cookie engine is on, or else one that sends the <c>-b</c> strings and stores nothing.
    /// </summary>
    internal ICookieStore HandlerStore { get; }

    /// <summary>
    /// Creates the run's cookies from the parsed command line.
    /// </summary>
    /// <param name="options">The parsed command line.</param>
    /// <returns>The run's cookies, or <see langword="null" /> when neither <c>-b</c> nor <c>-c</c> was given.</returns>
    internal static CookieEngine? FromCommandLine(CommandLineOptions options) =>
        options.Cookies.Count > 0 || options.CookieJar is not null ? new CookieEngine(options) : null;

    /// <summary>
    /// Loads every <c>-b</c> file, in command-line order, dropping session cookies under <c>-j</c>;
    /// a file that cannot be opened loads nothing and is not an error, as in curl.
    /// </summary>
    /// <param name="fileSystem">Opens the files.</param>
    /// <param name="now">The time that decides which loaded cookies have expired.</param>
    /// <returns>A task that completes when every file is loaded.</returns>
    internal async Task LoadCookieFilesAsync(IFileSystem fileSystem, DateTimeOffset now)
    {
        foreach (string cookieFile in cookieFiles)
        {
            await store.LoadCookieFileAsync(fileSystem, cookieFile, discardSessionCookies, now, CancellationToken.None)
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
    /// Keeps each <c>-b</c> file to load and puts each <c>-b name=value</c> string in the store,
    /// unless <paramref name="sendCookieStrings" /> is <see langword="false" />.
    /// </summary>
    /// <param name="cookies">The <c>-b</c> values, in command-line order.</param>
    /// <param name="sendCookieStrings">Whether the <c>name=value</c> strings are sent.</param>
    private void AddCookies(IEnumerable<CommandLineCookie> cookies, bool sendCookieStrings)
    {
        foreach (CommandLineCookie cookie in cookies)
        {
            if (!cookie.IsCookieString)
            {
                cookieFiles.Add(cookie.Value);
            }
            else if (sendCookieStrings)
            {
                store.AddCookieString(cookie.Value);
            }
        }
    }

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
    /// Sends the <c>-b name=value</c> strings and stores nothing, as curl does while its cookie
    /// engine is off.
    /// </summary>
    /// <param name="store">The store that holds the strings.</param>
    private sealed class CookieStringSender(CookieStore store) : ICookieStore
    {
        public string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now) => store.GetCookieHeader(url, secure, now);

        public void StoreFromResponse(CurlUrl url, IReadOnlyList<string> setCookieHeaders, DateTimeOffset now)
        {
        }
    }
}
