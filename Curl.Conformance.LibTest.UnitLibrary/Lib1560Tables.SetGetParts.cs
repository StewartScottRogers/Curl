using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance.LibTest;

/// <content><c>setget_parts_list</c>: part commands whose parts <c>setget_parts</c> reads back.</content>
public static partial class Lib1560Tables
{
    /// <summary><c>setget_parts_list</c>: part commands whose parts <c>setget_parts</c> reads back.</summary>
    public static IReadOnlyList<Lib1560SetGetCase> SetGetParts { get; } =
    [
        new("https://example.com/", "query=\"\",", "https | [11] | [12] | [13] | example.com | [15] | / |  | [17]", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlFlags.GetEmpty, CurlUrlCode.Ok),
        new("https://example.com/", "fragment=\"\",", "https | [11] | [12] | [13] | example.com | [15] | / | [16] | ", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlFlags.GetEmpty, CurlUrlCode.Ok),
        new("https://example.com/", "query=\"\",", "https | [11] | [12] | [13] | example.com | [15] | / | [16] | [17]", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok),
        new("https://example.com", "path=get,", "https | [11] | [12] | [13] | example.com | [15] | /get | [16] | [17]", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok),
        new("https://example.com", "path=/get,", "https | [11] | [12] | [13] | example.com | [15] | /get | [16] | [17]", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok),
        new("https://example.com", "path=g e t,", "https | [11] | [12] | [13] | example.com | [15] | /g%20e%20t | [16] | [17]", CurlUrlFlags.None, CurlUrlFlags.UrlEncode, CurlUrlFlags.None, CurlUrlCode.Ok),
    ];
}
