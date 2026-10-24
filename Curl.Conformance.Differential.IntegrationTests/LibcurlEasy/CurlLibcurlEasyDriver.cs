using System.Globalization;
using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Headers;
using Curl.Conformance.Differential.Libcurl.Easy;
using Libcurl.Easy;
using Libcurl.Global;
using Libcurl.Headers;
using Libcurl.Info;
using Libcurl.Misc.DatesAndLists;
using Libcurl.Options;

namespace Curl.Conformance.Differential.IntegrationTests.LibcurlEasy;

/// <summary>Runs an easy-handle case's rounds through Curl's Libcurl, as the tester's <c>EasyMode.cs</c> did.</summary>
internal static class CurlLibcurlEasyDriver
{
    /// <summary>Runs <paramref name="rounds"/> on one easy handle.</summary>
    public static IReadOnlyList<string> Run(CurlLibrary library, IReadOnlyList<LibcurlEasyRound> rounds)
    {
        using var easy = library.EasyInit();
        var body = new MemoryStream();
        var header = new MemoryStream();
        var lines = new List<string>();
        foreach (var round in rounds)
        {
            body.SetLength(0);
            header.SetLength(0);
            var failures = round.Options
                .Select(option => (option.Id, Code: easy.SetOption((CurlOption)option.Id, Value(option, body, header))))
                .Where(set => set.Code != CurlCode.Ok)
                .Select(set => LibcurlEasyLineWriter.SetFailure(set.Id, (int)set.Code))
                .ToList();
            var code = easy.Perform();
            var line = new StringBuilder(LibcurlEasyLineWriter.Round(lines.Count, (int)code, Encoding.UTF8.GetBytes(easy.ErrorText ?? ""), failures, body.ToArray(), header.ToArray()));
            AppendReads(easy, round, line);
            lines.Add(line.ToString());
            if (round.Resets)
            {
                easy.Reset();
            }
        }

        return lines;
    }

    private static void AppendReads(CurlEasy easy, LibcurlEasyRound round, StringBuilder line)
    {
        foreach (var id in round.Info)
        {
            var infoCode = easy.GetInfo((CurlInfo)id, out var value);
            line.Append(LibcurlEasyLineWriter.Info(id, (int)infoCode, value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "null"));
        }

        foreach (var query in round.HeaderQueries)
        {
            var headerCode = easy.Header(query.Name, query.Index, (CurlHeaderOrigin)query.Origin, query.Request, out var found);
            line.Append(LibcurlEasyLineWriter.HeaderQuery(query, (int)headerCode, found is null ? null : Fields(found)));
        }

        foreach (var walk in round.HeaderWalks)
        {
            line.Append(LibcurlEasyLineWriter.HeaderWalk(walk, Walk(easy, walk)));
        }
    }

    private static List<LibcurlEasyHeaderFields> Walk(CurlEasy easy, LibcurlEasyHeaderWalk walk)
    {
        var headers = new List<LibcurlEasyHeaderFields>();
        CurlHeader? previous = null;
        while (headers.Count < LibcurlEasyLineWriter.WalkLimit && (previous = easy.NextHeader((CurlHeaderOrigin)walk.Origin, walk.Request, previous)) is not null)
        {
            headers.Add(Fields(previous));
        }

        return headers;
    }

    private static LibcurlEasyHeaderFields Fields(CurlHeader header) =>
        new(Encoding.UTF8.GetBytes(header.Name), Encoding.UTF8.GetBytes(header.Value), header.Amount, header.Index, (int)header.Origin);

    private static object? Value(LibcurlEasyOption option, MemoryStream body, MemoryStream header) => option.Kind switch
    {
        LibcurlEasyOptionKind.Number => option.Number,
        LibcurlEasyOptionKind.Text => option.Text,
        LibcurlEasyOptionKind.List => option.List.Aggregate((CurlSlist?)null, CurlSlist.Append),
        LibcurlEasyOptionKind.WriteCallback => (WriteCallback)(data => Collect(body, data)),
        _ => (HeaderCallback)(data => Collect(header, data)),
    };

    private static long Collect(MemoryStream sink, ReadOnlySpan<byte> data)
    {
        sink.Write(data);
        return data.Length;
    }
}
