using System.Globalization;
using System.Text;
using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// The cookies a run of curl has received, kept and sent back the way libcurl 8.21.0 does. Every rule
/// here was measured on curl 8.21.0 on 2026-09-26 against a loopback server, as
/// <c>CookieStoreTests</c> records.
/// </summary>
/// <remarks>
/// The store never reads a clock: every call is handed the time it acts at. It refuses a received
/// cookie whose domain is a public suffix, by the embedded Public Suffix List snapshot. It loads Netscape cookie files (<c>-b</c>, <c>-j</c>), keeps
/// <c>-b name=value</c> strings and writes the <c>-c</c> jar, as <see cref="NetscapeCookieFile"/> reads and
/// writes them.
/// <para>
/// Every member may be called from several transfers at once, as a <c>-Z</c> run's transfers share one
/// store (ADR-0127): each call that reads or changes the stored cookies or strings does so whole, under
/// one lock, and <see cref="Cookies"/> is a copy taken under it. The <c>-v</c> lines are reported, and
/// the jar is written, outside the lock.
/// </para>
/// </remarks>
public sealed class CookieStore : ICookieStore
{
    /// <summary>The most cookies curl stores from one response; the <c>Set-Cookie</c> headers after that are ignored.</summary>
    public const int MostCookiesStoredPerResponse = 50;

    /// <summary>The most cookies curl sends in one <c>Cookie</c> header.</summary>
    public const int MostCookiesSent = 150;

    /// <summary>The longest <c>Cookie</c> header value curl sends; the cookie that would make it longer, and every one after it, is left out.</summary>
    public const int LongestCookieHeader = 8183;

    /// <summary>
    /// The permission bits a new jar file is created with on a POSIX system, <c>0666</c> before the umask:
    /// curl creates the jar with <c>fopen</c>.
    /// </summary>
    public const UnixFileMode JarCreateMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite;

    /// <summary>The stored cookies, oldest first; a replaced cookie keeps its place.</summary>
    private readonly List<Cookie> cookies = [];

    /// <summary>The <c>-b</c> arguments that hold a <c>=</c>, in the order given.</summary>
    private readonly List<string> cookieStrings = [];

    /// <summary>Held by every call that reads or changes <see cref="cookies"/> or <see cref="cookieStrings"/>.</summary>
    private readonly Lock storeLock = new();

    /// <summary>
    /// A copy of the stored cookies, oldest first, taken at the call. A cookie that replaced a namesake sits
    /// where the namesake was.
    /// </summary>
    public IReadOnlyList<Cookie> Cookies
    {
        get
        {
            lock (storeLock)
            {
                return [.. cookies];
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// A cookie is sent when it has not expired (its expiry, when it has one, is not before
    /// <paramref name="now"/>), its domain matches the host (the host itself for a host-only cookie, the
    /// host or a parent of it otherwise, in any case), its path matches the URL's path (the path is
    /// <c>/</c>, or is the URL's path or the start of it up to a <c>/</c>, case-sensitively), and, when
    /// it is <c>Secure</c>, the request is secure: <paramref name="secure"/>, or a loopback host
    /// (<c>localhost</c>, <c>127.0.0.1</c> or <c>::1</c>), as curl's <c>Curl_secure_context</c> decides.
    /// </para>
    /// <para>
    /// Only the <see cref="MostCookiesSent"/> oldest matching cookies are sent. They are ordered longest
    /// path first, then longest domain, then longest name, then newest first, and written
    /// <c>name=value</c>, separated by <c>; </c>. Writing stops at the first cookie that would make the
    /// value longer than <see cref="LongestCookieHeader"/> characters.
    /// </para>
    /// <para>
    /// The strings given to <see cref="AddCookieString"/> follow, verbatim, each after <c>; </c>, however
    /// long they make the value; they are left out when a stored cookie was.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> is <see langword="null"/>.</exception>
    public string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now) =>
        GetCookieHeader(url, secure, now, cookieStrings);

    /// <summary>
    /// Builds the <c>Cookie</c> header value as <see cref="GetCookieHeader(CurlUrl, bool, DateTimeOffset)"/>
    /// does, reporting to <paramref name="events"/> the <c>-v</c> lines
    /// <see cref="GetCookieHeader(CurlUrl, bool, DateTimeOffset, IReadOnlyList{string}, ITransferEvents)"/>
    /// reports when a limit cuts the header short.
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="secure">Whether the request goes over TLS.</param>
    /// <param name="now">The time that decides which stored cookies have expired.</param>
    /// <param name="events">Where the <c>-v</c> lines are reported.</param>
    /// <returns>The header value, or <see langword="null"/> when there is nothing to send.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> or <paramref name="events"/> is <see langword="null"/>.</exception>
    public string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now, ITransferEvents events) =>
        GetCookieHeader(url, secure, now, cookieStrings, events);

    /// <summary>
    /// Builds the <c>Cookie</c> header value as <see cref="GetCookieHeader(CurlUrl, bool, DateTimeOffset)"/>
    /// does, with <paramref name="cookieStrings"/> in place of the strings given to
    /// <see cref="AddCookieString"/>: the <c>-b name=value</c> strings of one <c>-:</c> / <c>--next</c>
    /// option group, whose stored cookies every group shares while its strings are its own, as curl
    /// 8.21.0 sends them (measured 2026-09-28, BL-509 Notes).
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="secure">Whether the request goes over TLS.</param>
    /// <param name="now">The time that decides which stored cookies have expired.</param>
    /// <param name="cookieStrings">The strings sent after the stored cookies, verbatim, in order.</param>
    /// <returns>The header value, or <see langword="null"/> when there is nothing to send.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url"/> or <paramref name="cookieStrings"/> is <see langword="null"/>.</exception>
    public string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now, IReadOnlyList<string> cookieStrings) =>
        GetCookieHeader(url, secure, now, cookieStrings, NoTransferEvents.Instance);

    /// <summary>
    /// Builds the <c>Cookie</c> header value as
    /// <see cref="GetCookieHeader(CurlUrl, bool, DateTimeOffset, IReadOnlyList{string})"/> does, reporting to
    /// <paramref name="events"/> the <c>-v</c> lines curl 8.21.0 prints when a limit cuts the header short:
    /// <c>Included max number of cookies (150) in request!</c> once <see cref="MostCookiesSent"/> cookies match
    /// (curl's <c>Curl_cookie_getlist</c>, also when exactly that many match), then
    /// <c>Restricted outgoing cookies due to header size, '&lt;name&gt;' not sent</c>, naming the first cookie
    /// left out to keep the value within <see cref="LongestCookieHeader"/> (curl's <c>http.c</c>).
    /// </summary>
    /// <param name="url">The request URL.</param>
    /// <param name="secure">Whether the request goes over TLS.</param>
    /// <param name="now">The time that decides which stored cookies have expired.</param>
    /// <param name="cookieStrings">The strings sent after the stored cookies, verbatim, in order.</param>
    /// <param name="events">Where the <c>-v</c> lines are reported, outside the store's lock.</param>
    /// <returns>The header value, or <see langword="null"/> when there is nothing to send.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="url"/>, <paramref name="cookieStrings"/> or <paramref name="events"/> is <see langword="null"/>.</exception>
    public string? GetCookieHeader(CurlUrl url, bool secure, DateTimeOffset now, IReadOnlyList<string> cookieStrings, ITransferEvents events)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(cookieStrings);
        ArgumentNullException.ThrowIfNull(events);

        string host = CookieOrigin.HostOf(url);
        bool secureContext = secure || CookieOrigin.IsLoopback(host);
        string path = url.AbsolutePath;
        StringBuilder header = new();
        List<string> verboseLines;
        lock (storeLock)
        {
            RemoveExpired(now);
            List<Cookie> matching = [.. cookies.Where(cookie => IsSentTo(cookie, host, path, secureContext)).Take(MostCookiesSent)];
            verboseLines = AppendCookieHeader(header, matching, cookieStrings);
        }

        foreach (string verboseLine in verboseLines)
        {
            events.ReportInfo(verboseLine);
        }

        return header.Length == 0 ? null : header.ToString();
    }

    /// <summary>
    /// Appends the matching cookies in sending order, then <paramref name="cookieStrings"/> unless a cookie was left
    /// out, and returns the <c>-v</c> lines curl prints for the limits that cut the header short.
    /// </summary>
    private static List<string> AppendCookieHeader(StringBuilder header, List<Cookie> matching, IReadOnlyList<string> cookieStrings)
    {
        List<string> verboseLines = [];
        if (matching.Count == MostCookiesSent)
        {
            verboseLines.Add($"Included max number of cookies ({MostCookiesSent}) in request!");
        }

        string? leftOut = AppendCookiesWithinLongestHeader(header, InSendingOrder(matching));
        if (leftOut is not null)
        {
            verboseLines.Add($"Restricted outgoing cookies due to header size, '{leftOut}' not sent");
        }
        else if (cookieStrings.Count > 0)
        {
            header.Append(header.Length == 0 ? string.Empty : "; ").AppendJoin("; ", cookieStrings);
        }

        return verboseLines;
    }

    /// <summary>
    /// Keeps a <c>-b</c> argument that holds a <c>=</c> (curl's <c>CURLOPT_COOKIE</c>): it is sent verbatim
    /// at the end of every <c>Cookie</c> header, after the stored cookies, and is never stored or written to
    /// the jar.
    /// </summary>
    /// <param name="cookieString">The argument as given, spaces and separators included.</param>
    /// <exception cref="ArgumentNullException"><paramref name="cookieString"/> is <see langword="null"/>.</exception>
    public void AddCookieString(string cookieString)
    {
        ArgumentNullException.ThrowIfNull(cookieString);

        lock (storeLock)
        {
            cookieStrings.Add(cookieString);
        }
    }

    /// <summary>Loads a Netscape cookie file (<c>-b</c> naming a file) as curl does.</summary>
    /// <remarks>
    /// Each cookie <see cref="NetscapeCookieFile.Read(TextReader, DateTimeOffset)"/> accepts is stored in file order, replacing a
    /// namesake in its place as a received cookie does, except that under <c>-j</c>
    /// (<paramref name="discardSessionCookies"/>) a session cookie is skipped. Cookies that have expired by
    /// <paramref name="now"/> are then removed.
    /// </remarks>
    /// <param name="reader">The file's text, one character per byte.</param>
    /// <param name="discardSessionCookies"><see langword="true"/> for <c>-j</c> (<c>--junk-session-cookies</c>).</param>
    /// <param name="now">The time that decides which loaded cookies have expired.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> is <see langword="null"/>.</exception>
    public void LoadCookieFile(TextReader reader, bool discardSessionCookies, DateTimeOffset now) =>
        LoadCookieFile(reader, discardSessionCookies, now, NoTransferEvents.Instance);

    /// <summary>
    /// Loads a Netscape cookie file as <see cref="LoadCookieFile(TextReader, bool, DateTimeOffset)"/> does,
    /// reporting to <paramref name="events"/> the <c>-v</c> line curl prints for each <c>Set-Cookie:</c>
    /// line it refuses with one (<see cref="NetscapeCookieFile.Read(TextReader, DateTimeOffset, ITransferEvents)"/>).
    /// </summary>
    /// <param name="reader">The file's text, one character per byte.</param>
    /// <param name="discardSessionCookies"><see langword="true"/> for <c>-j</c> (<c>--junk-session-cookies</c>).</param>
    /// <param name="now">The time that decides which loaded cookies have expired.</param>
    /// <param name="events">Where the refusal lines are reported.</param>
    /// <exception cref="ArgumentNullException"><paramref name="reader"/> or <paramref name="events"/> is <see langword="null"/>.</exception>
    public void LoadCookieFile(TextReader reader, bool discardSessionCookies, DateTimeOffset now, ITransferEvents events)
    {
        IReadOnlyList<Cookie> loaded = NetscapeCookieFile.Read(reader, now, events);
        lock (storeLock)
        {
            foreach (Cookie cookie in loaded.Where(cookie => !discardSessionCookies || !cookie.IsSessionCookie))
            {
                Store(cookie);
            }

            RemoveExpired(now);
        }
    }

    /// <summary>
    /// Loads the cookie file at <paramref name="path"/> with <see cref="LoadCookieFile(TextReader, bool, DateTimeOffset)"/>, reading its bytes
    /// as Latin-1. A file that cannot be opened loads nothing and is not an error, as in curl.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    public Task LoadCookieFileAsync(IFileSystem fileSystem, string path, bool discardSessionCookies, DateTimeOffset now, CancellationToken cancellationToken) =>
        LoadCookieFileAsync(fileSystem, path, discardSessionCookies, now, NoTransferEvents.Instance, cancellationToken);

    /// <summary>
    /// Loads the cookie file at <paramref name="path"/> as
    /// <see cref="LoadCookieFileAsync(IFileSystem, string, bool, DateTimeOffset, CancellationToken)"/> does,
    /// reporting its refused <c>Set-Cookie:</c> lines to <paramref name="events"/> as
    /// <see cref="LoadCookieFile(TextReader, bool, DateTimeOffset, ITransferEvents)"/> does. A file that cannot
    /// be opened, a directory included, reports curl 8.21.0's <c>WARNING: failed to open cookie file "&lt;path&gt;"</c>
    /// line with <paramref name="path"/> as given (BL-487).
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/>, <paramref name="path"/> or <paramref name="events"/> is <see langword="null"/>.</exception>
    public async Task LoadCookieFileAsync(IFileSystem fileSystem, string path, bool discardSessionCookies, DateTimeOffset now, ITransferEvents events, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(events);

        FileOpenResult opened = await fileSystem.OpenForReadAsync(path, cancellationToken).ConfigureAwait(false);
        if (!opened.IsOpen)
        {
            events.ReportInfo($"WARNING: failed to open cookie file \"{path}\"");
            return;
        }

        string text;
        using (StreamReader reader = new(opened.Content!, Encoding.Latin1, detectEncodingFromByteOrderMarks: false))
        {
            text = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        using StringReader textReader = new(text);
        LoadCookieFile(textReader, discardSessionCookies, now, events);
    }

    /// <summary>Writes the <c>-c</c> jar as curl does: every cookie that has not expired by <paramref name="now"/>, newest first.</summary>
    /// <remarks>
    /// Expired cookies are removed first. A replaced cookie keeps the place of the one it replaced, so it is
    /// written where that one would have been. The lines end with <paramref name="writer"/>'s
    /// <see cref="TextWriter.NewLine"/>.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="writer"/> is <see langword="null"/>.</exception>
    public void WriteCookieJar(TextWriter writer, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(writer);

        List<Cookie> newestFirst;
        lock (storeLock)
        {
            RemoveExpired(now);
            newestFirst = [.. cookies];
        }

        newestFirst.Reverse();
        NetscapeCookieFile.Write(writer, newestFirst);
    }

    /// <summary>
    /// Writes the jar with <see cref="WriteCookieJar"/> to the file at <paramref name="path"/>, replacing it,
    /// as Latin-1 with this platform's line ending (CR LF on Windows, LF elsewhere), as curl does.
    /// </summary>
    /// <remarks>
    /// A jar that cannot be written is not an error: curl 8.21.0 prints nothing, even with <c>-v</c>, and
    /// exits as the transfer did. The result tells the caller, which must stay silent too.
    /// </remarks>
    /// <returns><see langword="true"/> when the jar was written; <see langword="false"/> when the file could not be opened or written.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="fileSystem"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    public async Task<bool> SaveCookieJarAsync(IFileSystem fileSystem, string path, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(path);

        using StringWriter jar = new(CultureInfo.InvariantCulture);
        WriteCookieJar(jar, now);
        byte[] bytes = Encoding.Latin1.GetBytes(jar.ToString());

        FileOpenResult opened = await fileSystem.OpenForWriteAsync(path, FileWriteMode.Truncate, JarCreateMode, cancellationToken).ConfigureAwait(false);
        if (!opened.IsOpen)
        {
            return false;
        }

        Stream content = opened.Content!;
        await using (content.ConfigureAwait(false))
        {
            try
            {
                await content.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await content.FlushAsync(cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }
    }

    /// <summary>curl's tests in <c>Curl_cookie_getlist</c>: secure, domain and path.</summary>
    private static bool IsSentTo(Cookie cookie, string host, string path, bool secureContext) =>
        (secureContext || !cookie.IsSecure) && DomainMatches(cookie, host) && PathMatches(cookie.Path, path);

    /// <summary>curl's <c>cookie_sort</c>: longest path, then longest domain, then longest name, then newest first.</summary>
    private static List<Cookie> InSendingOrder(IEnumerable<Cookie> oldestFirst)
    {
        List<Cookie> newestFirst = [.. oldestFirst];
        newestFirst.Reverse();
        return [.. newestFirst.Order(SendingOrder.Instance)];
    }

    /// <summary>
    /// Stores the cookies from every <c>Set-Cookie</c> header of one response to a request for
    /// <paramref name="url"/>, in order, as successive
    /// <see cref="StoreFromResponse(CurlUrl, string, int, DateTimeOffset, ITransferEvents)"/> calls
    /// counting from 0.
    /// </summary>
    /// <param name="url">The URL of the request the response answered.</param>
    /// <param name="setCookieHeaders">The value of each <c>Set-Cookie</c> header, verbatim and in the order received.</param>
    /// <param name="now">The receive time that relative expiry (<c>Max-Age</c>) counts from.</param>
    /// <param name="events">Where the store reports, as curl's <c>-v</c> lines, each cookie it adds, replaces or drops.</param>
    /// <exception cref="ArgumentNullException"><paramref name="url"/>, <paramref name="setCookieHeaders"/> or <paramref name="events"/> is <see langword="null"/>.</exception>
    public void StoreFromResponse(CurlUrl url, IReadOnlyList<string> setCookieHeaders, DateTimeOffset now, ITransferEvents events)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(setCookieHeaders);
        ArgumentNullException.ThrowIfNull(events);

        int stored = 0;
        foreach (string header in setCookieHeaders)
        {
            stored = StoreFromResponse(url, header, stored, now, events);
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <para>
    /// Each header is read by <see cref="SetCookieParser"/>; one it refuses is skipped, and so is one whose
    /// domain the host may not set by the Public Suffix List: a domain other than the host itself must be
    /// longer than the public suffix the host ends in (<c>Domain=co.uk</c> from <c>www.example.co.uk</c> is
    /// dropped, <c>Domain=example.co.uk</c> kept), and a host longer than 255 characters sets no cookie. A cookie with
    /// the name (case-sensitively), domain (in any case), host-only flag and path (case-sensitively) of
    /// a stored one replaces it in its place; any other is added as the newest. A cookie that is not
    /// <c>Secure</c>, from an origin that is not secure, is dropped when a stored <c>Secure</c> cookie
    /// has its name, a domain that is its domain or a parent or child of it, and a path whose first
    /// segment starts the new cookie's path: curl will not let it overlay the secure one.
    /// </para>
    /// <para>
    /// Once <see cref="MostCookiesStoredPerResponse"/> cookies have been stored or replaced from the
    /// response, the rest of its headers are ignored. A cookie that arrived already expired replaces its namesake and is then
    /// removed with every other expired cookie, so it deletes the namesake.
    /// </para>
    /// <para>
    /// Each cookie stored is reported to <paramref name="events"/> as curl 8.21.0's <c>-v</c> line,
    /// <c>Added cookie n="v" for domain d, path p, expire e</c> (<c>Replaced</c> for one that replaced a
    /// namesake), and so is each one this store drops by the Public Suffix List or to protect a
    /// <c>Secure</c> cookie. A header <see cref="SetCookieParser"/> refuses is reported with the line
    /// <see cref="SetCookieParser.Parse(string, CurlUrl, DateTimeOffset, out string?)"/> gives, or not at all
    /// where curl prints nothing; one past the limit is not reported.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="url"/>, <paramref name="setCookieHeader"/> or <paramref name="events"/> is <see langword="null"/>.</exception>
    public int StoreFromResponse(CurlUrl url, string setCookieHeader, int storedFromResponse, DateTimeOffset now, ITransferEvents events)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(setCookieHeader);
        ArgumentNullException.ThrowIfNull(events);

        if (storedFromResponse >= MostCookiesStoredPerResponse)
        {
            return storedFromResponse;
        }

        Cookie? cookie = ParseReportingRefusal(setCookieHeader, url, now, events);
        string host = CookieOrigin.HostOf(url);
        bool secureOrigin = CookieOrigin.IsSecure(url);
        ReceivedCookieOutcome? outcome = null;
        lock (storeLock)
        {
            if (cookie is not null)
            {
                outcome = StoreReceived(cookie, host, secureOrigin);
            }

            RemoveExpired(now);
        }

        if (outcome is not { } reported)
        {
            return storedFromResponse;
        }

        events.ReportInfo(reported.VerboseLine);
        return reported.IsStored ? storedFromResponse + 1 : storedFromResponse;
    }

    /// <summary>What <see cref="StoreReceived"/> did with a received cookie, and the <c>-v</c> line curl prints for it.</summary>
    private readonly record struct ReceivedCookieOutcome(bool IsStored, string VerboseLine);

    /// <summary>
    /// Stores a received cookie the host may set, in its namesake's place when there is one; called under
    /// <see cref="storeLock"/>. The <c>-v</c> line is <c>Added</c> or <c>Replaced cookie …</c>, or the
    /// refusal <see cref="RefusalOf"/> gives.
    /// </summary>
    private ReceivedCookieOutcome StoreReceived(Cookie cookie, string host, bool secureOrigin)
    {
        if (RefusalOf(cookie, host, secureOrigin) is string refusal)
        {
            return new ReceivedCookieOutcome(IsStored: false, refusal);
        }

        string action = Store(cookie) ? "Replaced" : "Added";
        return new ReceivedCookieOutcome(
            IsStored: true,
            string.Create(CultureInfo.InvariantCulture, $"{action} cookie {cookie.Name}=\"{cookie.Value}\" for domain {cookie.Domain}, path {cookie.Path}, expire {cookie.ExpiresUnixSeconds}"));
    }

    /// <summary>Reads <paramref name="header"/> with <see cref="SetCookieParser"/>, reporting the <c>-v</c> line curl prints when it refuses it.</summary>
    private static Cookie? ParseReportingRefusal(string header, CurlUrl url, DateTimeOffset now, ITransferEvents events)
    {
        Cookie? cookie = SetCookieParser.Parse(header, url, now, out string? refusal);
        if (refusal is not null)
        {
            events.ReportInfo(refusal);
        }

        return cookie;
    }

    /// <summary>
    /// curl's <c>-v</c> line for a cookie the store refuses: one whose domain the host may not set by the
    /// Public Suffix List, or one that would overlay a <c>Secure</c> cookie it may not.
    /// </summary>
    /// <returns>The line, or <see langword="null"/> when the cookie may be stored.</returns>
    private string? RefusalOf(Cookie cookie, string host, bool secureOrigin)
    {
        if (!PublicSuffixList.Embedded.IsCookieDomainAcceptable(host, cookie.Domain!))
        {
            return $"cookie '{cookie.Name}' dropped, domain '{host}' must not set cookies for '{cookie.Domain}'";
        }

        return !secureOrigin && OverlaysSecureCookie(cookie)
            ? $"cookie '{cookie.Name}' for domain '{cookie.Domain}' dropped, would overlay an existing cookie"
            : null;
    }

    /// <summary>Stores <paramref name="cookie"/>, in its namesake's place when there is one.</summary>
    /// <returns><see langword="true"/> when it replaced a namesake; <see langword="false"/> when it was added as the newest.</returns>
    private bool Store(Cookie cookie)
    {
        int namesake = cookies.FindIndex(stored => IsNamesake(stored, cookie));
        if (namesake < 0)
        {
            cookies.Add(cookie);
            return false;
        }

        cookies[namesake] = cookie;
        return true;
    }

    /// <summary>curl's <c>replace_existing</c>: the same name, domain, host-only flag and path.</summary>
    private static bool IsNamesake(Cookie stored, Cookie cookie) =>
        string.Equals(stored.Name, cookie.Name, StringComparison.Ordinal)
        && string.Equals(stored.Domain, cookie.Domain, StringComparison.OrdinalIgnoreCase)
        && stored.IncludesSubdomains == cookie.IncludesSubdomains
        && string.Equals(stored.Path, cookie.Path, StringComparison.Ordinal);

    /// <summary>
    /// curl's rule that a cookie from an origin that is not secure may not overlay a <c>Secure</c> one:
    /// the same name, domains where one is the other or a parent of it, and the stored path's first
    /// segment (<c>/</c> for the path <c>/</c>) starting the new path. A stored cookie without a domain or
    /// a path (read from a <c>Set-Cookie:</c> line of a cookie file) never blocks one.
    /// </summary>
    private bool OverlaysSecureCookie(Cookie cookie) => cookies.Exists(stored => IsSecureCookieOverlaidBy(stored, cookie));

    private static bool IsSecureCookieOverlaidBy(Cookie stored, Cookie cookie) =>
        stored.IsSecure
        && string.Equals(stored.Name, cookie.Name, StringComparison.Ordinal)
        && stored.Domain is string storedDomain
        && stored.Path.Length > 0
        && CoversDomainAndPath(storedDomain, stored.Path, cookie);

    /// <summary>One domain is the other or a parent of it, and the stored path's first segment starts the new path.</summary>
    private static bool CoversDomainAndPath(string storedDomain, string storedPath, Cookie cookie) =>
        (CookieOrigin.IsDomainOrSubdomain(storedDomain, cookie.Domain!) || CookieOrigin.IsDomainOrSubdomain(cookie.Domain!, storedDomain))
        && cookie.Path.StartsWith(FirstPathSegment(storedPath), StringComparison.Ordinal);

    private static string FirstPathSegment(string path)
    {
        int secondSlash = path.IndexOf('/', 1);
        return secondSlash < 0 ? path : path[..secondSlash];
    }

    /// <summary>Removes every cookie whose expiry is before <paramref name="now"/>; a session cookie never expires.</summary>
    private void RemoveExpired(DateTimeOffset now)
    {
        long nowUnixSeconds = now.ToUnixTimeSeconds();
        cookies.RemoveAll(cookie => !cookie.IsSessionCookie && cookie.ExpiresUnixSeconds < nowUnixSeconds);
    }

    /// <summary>curl's domain test in <c>Curl_cookie_getlist</c>; a cookie without a domain goes to every host.</summary>
    private static bool DomainMatches(Cookie cookie, string host) =>
        cookie.Domain switch
        {
            null => true,
            string domain when cookie.IncludesSubdomains => CookieOrigin.IsDomainOrSubdomain(domain, host),
            string domain => string.Equals(domain, host, StringComparison.OrdinalIgnoreCase),
        };

    /// <summary>curl's <c>pathmatch</c>: empty, <c>/</c>, or the request path itself or up to a <c>/</c>, case-sensitively.</summary>
    private static bool PathMatches(string cookiePath, string requestPath) =>
        cookiePath == "/"
        || (requestPath.StartsWith(cookiePath, StringComparison.Ordinal)
            && (requestPath.Length == cookiePath.Length || requestPath[cookiePath.Length] == '/'));

    /// <summary>
    /// Appends <c>name=value</c> pairs until one would make the value longer than <see cref="LongestCookieHeader"/>;
    /// returns that cookie's name, or <see langword="null"/> when every cookie was appended.
    /// </summary>
    private static string? AppendCookiesWithinLongestHeader(StringBuilder header, IEnumerable<Cookie> sent)
    {
        foreach (Cookie cookie in sent)
        {
            string separator = header.Length == 0 ? string.Empty : "; ";
            if (header.Length + separator.Length + cookie.Name.Length + 1 + cookie.Value.Length > LongestCookieHeader)
            {
                return cookie.Name;
            }

            header.Append(separator).Append(cookie.Name).Append('=').Append(cookie.Value);
        }

        return null;
    }

    /// <summary>Compares two cookies by curl's <c>cookie_sort</c> keys, leaving ties to a stable sort.</summary>
    private sealed class SendingOrder : IComparer<Cookie>
    {
        public static readonly SendingOrder Instance = new();

        public int Compare(Cookie? x, Cookie? y)
        {
            int byPath = y!.Path.Length.CompareTo(x!.Path.Length);
            int byDomain = byPath != 0 ? byPath : DomainLength(y).CompareTo(DomainLength(x));
            return byDomain != 0 ? byDomain : y.Name.Length.CompareTo(x.Name.Length);
        }

        /// <summary>The domain's length; <c>0</c> for a cookie without one, as curl sorts it.</summary>
        private static int DomainLength(Cookie cookie) => cookie.Domain?.Length ?? 0;
    }
}
