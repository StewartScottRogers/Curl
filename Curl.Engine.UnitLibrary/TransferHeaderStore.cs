using System.Globalization;
using System.Runtime.CompilerServices;

using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;

namespace Curl.Engine;

/// <summary>
/// The headers an easy handle's last transfer received, for libcurl's header API (ADR-0603): a
/// <see cref="HeaderRecordingStream" /> hands it every header line the transfer writes, as libcurl's
/// <c>hds_cw_collect_write</c> client writer pushes every header but the status lines.
/// <see cref="RecordLine" /> numbers the transfer's requests from 0 - each status line after a final
/// response's head begins the next one, so every redirect hop and authentication retry is its own
/// request - and gives each header its origin: <see cref="CurlHeaderOrigin.Informational" /> under a 1xx
/// status line, <see cref="CurlHeaderOrigin.Connect" /> under a CONNECT reply's
/// (<see cref="MarkConnectReplyHead" />), <see cref="CurlHeaderOrigin.Trailer" /> after a final head has
/// ended, <see cref="CurlHeaderOrigin.Header" /> otherwise; an HTTP/2 or HTTP/3 status line also records
/// its <c>:status</c> as a <see cref="CurlHeaderOrigin.Pseudo" /> header, as libcurl's <c>http2.c</c> does.
/// <see cref="Find" /> and <see cref="Next" /> are <c>lib/headers.c</c>'s <c>curl_easy_header</c> and
/// <c>curl_easy_nextheader</c>.
/// </summary>
internal sealed class TransferHeaderStore : ITransferHeaders
{
    /// <summary>Every <c>CURLH_*</c> bit <c>header.h</c> defines.</summary>
    private const CurlHeaderOrigin AllOrigins =
        CurlHeaderOrigin.Header | CurlHeaderOrigin.Trailer | CurlHeaderOrigin.Connect | CurlHeaderOrigin.Informational | CurlHeaderOrigin.Pseudo;

    private readonly List<StoredHeader> headers = [];

    /// <summary>Each header <see cref="Find" /> or <see cref="Next" /> answered, to the stored header it stands for: libcurl's <c>anchor</c>.</summary>
    private readonly ConditionalWeakTable<CurlHeader, StoredHeader> anchors = [];

    /// <summary>The number of the request whose headers arrive now, from 0.</summary>
    private int request;

    /// <summary>Whether the current request's final (not 1xx, not CONNECT) head has ended.</summary>
    private bool finalHeadEnded;

    /// <summary>Whether the next status line begins a CONNECT reply.</summary>
    private bool nextHeadIsConnectReply;

    /// <summary>The origin of the header lines arriving now.</summary>
    private CurlHeaderOrigin lineOrigin = CurlHeaderOrigin.Header;

    /// <summary>
    /// Gets the status of the last status line that was not a CONNECT reply's, 0 before one: what libcurl's
    /// <c>CURLINFO_RESPONSE_CODE</c> answers once that status line is parsed, while the transfer runs (upstream test1485).
    /// </summary>
    internal long ResponseCode { get; private set; }

    /// <summary>
    /// Gets the last <c>Content-Length</c> of the current final head, -1 before one or when it does not parse: what
    /// libcurl's <c>CURLINFO_CONTENT_LENGTH_DOWNLOAD_T</c> answers once that header is parsed, while the transfer runs.
    /// </summary>
    internal long ContentLength { get; private set; } = -1;

    /// <summary>
    /// Gets the <c>CURL_HTTP_VERSION_*</c> number of the last status line that was not a CONNECT reply's, 0 before one: what
    /// libcurl's <c>CURLINFO_HTTP_VERSION</c> keeps when the transfer then fails, after a low-speed abort or a refused redirect hop (BL-3338).
    /// </summary>
    internal long HttpVersion { get; private set; }

    /// <summary>Gets or sets what is called each time <see cref="ResponseCode" /> or <see cref="ContentLength" /> is set by a line.</summary>
    internal Action? ResponseHeadRead { get; set; }

    /// <summary>Forgets every header, as libcurl does when a transfer starts.</summary>
    internal void Clear()
    {
        headers.Clear();
        anchors.Clear();
        request = 0;
        finalHeadEnded = false;
        nextHeadIsConnectReply = false;
        lineOrigin = CurlHeaderOrigin.Header;
        ResponseCode = 0;
        ContentLength = -1;
        HttpVersion = 0;
    }

    /// <summary>Marks the next status line as a CONNECT reply's, whose headers are <see cref="CurlHeaderOrigin.Connect" />.</summary>
    internal void MarkConnectReplyHead() => nextHeadIsConnectReply = true;

    /// <summary>Records one header line as <see cref="TransferHeaderStore" /> describes.</summary>
    /// <param name="line">The line, without its line ending.</param>
    internal void RecordLine(string line)
    {
        if (line.StartsWith("HTTP/", StringComparison.Ordinal))
        {
            StartHead(line);
        }
        else if (line.Length == 0)
        {
            EndHead();
        }
        else if (line[0] is ' ' or '\t')
        {
            Unfold(line);
        }
        else
        {
            Add(line);
        }
    }

    /// <inheritdoc />
    public CurlHeaderCode Find(string name, int index, CurlHeaderOrigin origin, int request, out CurlHeader? header)
    {
        header = null;
        CurlHeaderCode code = CheckArguments(index, origin, request);
        if (code != CurlHeaderCode.Ok)
        {
            return code;
        }

        int wanted = RequestOf(request);
        List<StoredHeader> named = headers.FindAll(stored => stored.Matches(origin, wanted) && stored.IsNamed(name));
        if (named.Count == 0)
        {
            return CurlHeaderCode.Missing;
        }

        if (index >= named.Count)
        {
            return CurlHeaderCode.BadIndex;
        }

        header = Expose(named[index], named.Count, index);
        return CurlHeaderCode.Ok;
    }

    /// <inheritdoc />
    public CurlHeader? Next(CurlHeaderOrigin origin, int request, CurlHeader? previous)
    {
        if (request > this.request)
        {
            return null;
        }

        int wanted = RequestOf(request);
        int start = StartAfter(previous);
        int found = start < 0 ? -1 : headers.FindIndex(start, stored => stored.Matches(origin, wanted));
        if (found < 0)
        {
            return null;
        }

        StoredHeader pick = headers[found];
        List<StoredHeader> named = headers.FindAll(stored => stored.Matches(origin, wanted) && stored.IsNamed(pick.Name));
        return Expose(pick, named.Count, named.IndexOf(pick));
    }

    /// <summary><c>curl_easy_header</c>'s checks, in its order.</summary>
    private CurlHeaderCode CheckArguments(int index, CurlHeaderOrigin origin, int request) =>
        IsBadArgument(index, origin, request) ? CurlHeaderCode.BadArgument
        : headers.Count == 0 ? CurlHeaderCode.NoHeaders
        : request > this.request ? CurlHeaderCode.NoRequest
        : CurlHeaderCode.Ok;

    /// <summary>Whether <c>curl_easy_header</c> refuses the arguments: a negative index, no origin bit or one <c>header.h</c> lacks, or a request below -1.</summary>
    private static bool IsBadArgument(int index, CurlHeaderOrigin origin, int request) =>
        index < 0 || origin == CurlHeaderOrigin.None || (origin & ~AllOrigins) != 0 || request < ITransferHeaders.LastRequest;

    /// <summary>The request number <paramref name="request" /> names: the last for <see cref="ITransferHeaders.LastRequest" />.</summary>
    private int RequestOf(int request) => request == ITransferHeaders.LastRequest ? this.request : request;

    /// <summary>Where a walk after <paramref name="previous" /> starts: the first header for none, -1 for a header this store did not answer.</summary>
    private int StartAfter(CurlHeader? previous)
    {
        if (previous is null)
        {
            return 0;
        }

        int at = anchors.TryGetValue(previous, out StoredHeader? anchor) ? headers.IndexOf(anchor) : -1;
        return at < 0 ? -1 : at + 1;
    }

    private CurlHeader Expose(StoredHeader stored, int amount, int index)
    {
        CurlHeader header = new(stored.Name, stored.Value, amount, index, stored.Origin);
        anchors.Add(header, stored);
        return header;
    }

    private void StartHead(string statusLine)
    {
        BeginNextRequestAfterFinalHead();
        string[] words = statusLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string status = words.Length > 1 ? words[1] : string.Empty;
        lineOrigin = HeadOriginOf(status);
        ReadHttpVersion(words[0]);
        nextHeadIsConnectReply = false;
        if (words[0] is "HTTP/2" or "HTTP/3")
        {
            headers.Add(new StoredHeader(":status", status, CurlHeaderOrigin.Pseudo, request));
        }

        ReadResponseCode(status);
    }

    /// <summary>Takes a status line's first word as <see cref="HttpVersion" />, unless the head is a CONNECT reply's.</summary>
    private void ReadHttpVersion(string protocol)
    {
        if (lineOrigin != CurlHeaderOrigin.Connect)
        {
            HttpVersion = protocol switch
            {
                "HTTP/1.0" => 1,
                "HTTP/1.1" => 2,
                "HTTP/2" => 3,
                "HTTP/3" => 30,
                _ => 0,
            };
        }
    }

    /// <summary>Takes <paramref name="status" /> as <see cref="ResponseCode" />, 0 when it does not parse, and forgets the length, unless the head is a CONNECT reply's.</summary>
    private void ReadResponseCode(string status)
    {
        if (lineOrigin != CurlHeaderOrigin.Connect)
        {
            ResponseCode = long.TryParse(status, NumberStyles.None, CultureInfo.InvariantCulture, out long code) ? code : 0;
            ContentLength = -1;
            ResponseHeadRead?.Invoke();
        }
    }

    /// <summary>Starts the next request when the current one's final head has ended.</summary>
    private void BeginNextRequestAfterFinalHead()
    {
        if (finalHeadEnded)
        {
            request++;
            finalHeadEnded = false;
        }
    }

    /// <summary>The origin of the lines under a status line with <paramref name="status" />: CONNECT when marked, 1xx for an informational status, else a plain header.</summary>
    private CurlHeaderOrigin HeadOriginOf(string status) =>
        nextHeadIsConnectReply ? CurlHeaderOrigin.Connect
        : status.StartsWith('1') ? CurlHeaderOrigin.Informational
        : CurlHeaderOrigin.Header;

    private void EndHead()
    {
        if (lineOrigin == CurlHeaderOrigin.Header)
        {
            finalHeadEnded = true;
            lineOrigin = CurlHeaderOrigin.Trailer;
        }
    }

    /// <summary>
    /// Appends a folded line to the header before it without its leading and trailing blanks, joined by one
    /// space (none to an empty value), as libcurl 8.22.0's <c>unfold_value</c> does (upstream test1941: a fold
    /// <c>"\t\tconnect"</c> after <c>"from the"</c> gives <c>"from the connect"</c>); a fold with no header
    /// before it, or with nothing but blanks, is dropped.
    /// </summary>
    private void Unfold(string line)
    {
        string piece = line.Trim(' ', '\t');
        if (headers.Count == 0 || piece.Length == 0)
        {
            return;
        }

        StoredHeader last = headers[^1];
        string value = last.Value.Length == 0 ? piece : last.Value + " " + piece;
        headers[^1] = new StoredHeader(last.Name, value, last.Origin, last.Request);
    }

    /// <summary>Adds a <c>name: value</c> line, its value without leading or trailing blanks; a line with no colon is not a header and is dropped.</summary>
    private void Add(string line)
    {
        int colon = line.IndexOf(':', StringComparison.Ordinal);
        if (colon >= 0)
        {
            var header = new StoredHeader(line[..colon], line[(colon + 1)..].Trim(' ', '\t'), lineOrigin, request);
            headers.Add(header);
            ReadContentLength(header);
        }
    }

    /// <summary>Takes a final head's <c>Content-Length</c> as <see cref="ContentLength" />, -1 when it does not parse.</summary>
    private void ReadContentLength(StoredHeader header)
    {
        if (header.Origin == CurlHeaderOrigin.Header && header.IsNamed("Content-Length"))
        {
            ContentLength = long.TryParse(header.Value, NumberStyles.None, CultureInfo.InvariantCulture, out long length) ? length : -1;
            ResponseHeadRead?.Invoke();
        }
    }

    /// <summary>One recorded header; compared by reference, so an anchor finds its own place.</summary>
    private sealed class StoredHeader(string name, string value, CurlHeaderOrigin origin, int request)
    {
        public string Name => name;

        public string Value => value;

        public CurlHeaderOrigin Origin => origin;

        public int Request => request;

        public bool Matches(CurlHeaderOrigin wanted, int wantedRequest) => (origin & wanted) != 0 && request == wantedRequest;

        public bool IsNamed(string other) => string.Equals(name, other, StringComparison.OrdinalIgnoreCase);
    }
}
