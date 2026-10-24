namespace Curl.Conformance.Differential.Libcurl.Easy;

/// <summary>
/// The tester's header API cases (<c>h_in.json</c>, built by <c>hdrtest.py</c>): each loopback
/// path fetched once without and once with <c>CURLOPT_FOLLOWLOCATION</c>, then queried with
/// <c>curl_easy_header</c> and walked with <c>curl_easy_nextheader</c> across names, indexes,
/// origins and request numbers; and one HTTP proxy tunnel case. Built here rather than embedded
/// because the file is 570 KB of the same few lists multiplied out.
/// </summary>
public static class LibcurlHeaderCases
{
    private const string BaseUrl = "http://127.0.0.1:18080";

    private static readonly string?[] Names = ["x-a", "X-A", "Content-Length", "content-length", "Set-Cookie", "x-b", "x-empty", "X-F", "Trailer", "x-t", "Location", "nope", "", "Date", "Link", "x-nospace", "x-tab", "X-Sp", "Content-Type", "ETag", "Transfer-Encoding"];

    private static readonly int[] Indexes = [0, 1, 2, 7, 100];

    private static readonly int[] Origins = [1, 2, 4, 8, 16, 31, 0, 3, 32];

    private static readonly int[] Requests = [0, 1, -1, 5];

    private static readonly int[] QueryOrigins = [1, 2, 31];

    private static readonly int[] LastRequestOrFirst = [0, -1];

    private static readonly string[] Paths = ["/dupheaders", "/folded", "/chunked", "/r302", "/setcookie", "/early", "/early100", "/hdr-nospace", "/hdr-badline", "/hdr-space-before-colon", "/echo", "/cd", "/lm", "/redir-cookie", "/rrel", "/needauth", "/utf8", "/multienc",
        "/status/100", "/status/404", "/nocontent", "/loc-multi", "/manyhdrs", "/cl-and-te", "/http10"];

    /// <summary>Gets every case, in the order <c>hdrtest.py</c> wrote them.</summary>
    public static IReadOnlyList<LibcurlEasyCase> All { get; } = Build();

    private static List<LibcurlEasyCase> Build()
    {
        var walks = new List<LibcurlEasyHeaderWalk>();
        foreach (var origin in Origins)
        {
            foreach (var request in Requests)
            {
                walks.Add(new LibcurlEasyHeaderWalk(origin, request));
            }
        }

        var cases = new List<LibcurlEasyCase>();
        foreach (var path in Paths)
        {
            cases.Add(Case(path, [Text(10002, BaseUrl + path)], PathQueries(), walks));
            cases.Add(Case(path + " follow", [Text(10002, BaseUrl + path), Number(52, 1)], PathQueries(), walks));
        }

        var proxyQueries = new List<LibcurlEasyHeaderQuery>();
        AddQueries(proxyQueries, ["x-a", "date", "content-length"], [0, 1], [1, 4, 31], LastRequestOrFirst);
        cases.Add(Case("proxy-connect", [Text(10002, "http://example.test/x"), Text(10004, BaseUrl), Number(61, 1)], proxyQueries, walks));
        return cases;
    }

    private static List<LibcurlEasyHeaderQuery> PathQueries()
    {
        var queries = new List<LibcurlEasyHeaderQuery>();
        AddQueries(queries, Names, Indexes[..3], QueryOrigins, LastRequestOrFirst);
        AddQueries(queries, ["x-a", "set-cookie", "location"], [0], Origins, Requests);
        AddQueries(queries, ["x-a"], Indexes, [1], [-1]);
        queries.Add(new LibcurlEasyHeaderQuery(null, 0, 1, 0));
        return queries;
    }

    private static void AddQueries(List<LibcurlEasyHeaderQuery> queries, string?[] names, int[] indexes, int[] origins, int[] requests)
    {
        foreach (var name in names)
        {
            foreach (var index in indexes)
            {
                foreach (var origin in origins)
                {
                    foreach (var request in requests)
                    {
                        queries.Add(new LibcurlEasyHeaderQuery(name, index, origin, request));
                    }
                }
            }
        }
    }

    private static LibcurlEasyCase Case(string name, LibcurlEasyOption[] options, List<LibcurlEasyHeaderQuery> queries, List<LibcurlEasyHeaderWalk> walks)
    {
        LibcurlEasyOption[] callbacks =
        [
            new(20011, LibcurlEasyOptionKind.WriteCallback, 0, null, []),
            new(20079, LibcurlEasyOptionKind.HeaderCallback, 0, null, []),
        ];
        return new LibcurlEasyCase(name, [new LibcurlEasyRound([.. callbacks, .. options], [], queries, walks, false)]);
    }

    private static LibcurlEasyOption Text(int id, string text) => new(id, LibcurlEasyOptionKind.Text, 0, text, []);

    private static LibcurlEasyOption Number(int id, long number) => new(id, LibcurlEasyOptionKind.Number, number, null, []);
}
