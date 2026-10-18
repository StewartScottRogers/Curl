using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Conformance.LibTest;

/// <content><c>append_list</c>: queries <c>append</c> adds with <c>CURLU_APPENDQUERY</c>.</content>
public static partial class Lib1560Tables
{
    /// <summary><c>append_list</c>: queries <c>append</c> adds with <c>CURLU_APPENDQUERY</c>.</summary>
    public static IReadOnlyList<Lib1560SetCase> Appends { get; } =
    [
        new("HTTP://test/?s", "name=joe\u0002", "http://test/?s&name=joe%02", CurlUrlFlags.None, CurlUrlFlags.UrlEncode, CurlUrlCode.Ok, CurlUrlCode.Ok),
        new("HTTP://test/?size=2#f", "name=joe=", "http://test/?size=2&name=joe%3D#f", CurlUrlFlags.None, CurlUrlFlags.UrlEncode, CurlUrlCode.Ok, CurlUrlCode.Ok),
        new("HTTP://test/?size=2#f", "name=joe doe", "http://test/?size=2&name=joe+doe#f", CurlUrlFlags.None, CurlUrlFlags.UrlEncode, CurlUrlCode.Ok, CurlUrlCode.Ok),
        new("HTTP://test/", "name=joe", "http://test/?name=joe", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok),
        new("HTTP://test/?size=2", "name=joe", "http://test/?size=2&name=joe", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok),
        new("HTTP://test/?size=2&", "name=joe", "http://test/?size=2&name=joe", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok),
        new("HTTP://test/?size=2#f", "name=joe", "http://test/?size=2&name=joe#f", CurlUrlFlags.None, CurlUrlFlags.None, CurlUrlCode.Ok, CurlUrlCode.Ok),
    ];
}
