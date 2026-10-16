using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Libcurl.Options;

namespace Libcurl.UnitTests.Options;

[TestClass]
public sealed class EasyOptionDispatchTests
{
    private static readonly Dictionary<CurlOption, EasyOptionSetter> NoSetters = [];

    [TestMethod]
    public void SetOption_ANumberUpstreamLacks_ReturnsUnknownOption()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.UnknownOption, EasyOptionDispatch.Default.SetOption(ref options, (CurlOption)99999, 1L));
    }

    [TestMethod]
    [DataRow(CurlOption.Port, "80", DisplayName = "CURLOT_LONG given a string")]
    [DataRow(CurlOption.HttpVersion, "2", DisplayName = "CURLOT_VALUES given a string")]
    [DataRow(CurlOption.PostfieldsizeLarge, 1.5, DisplayName = "CURLOT_OFF_T given a double")]
    [DataRow(CurlOption.Url, 1L, DisplayName = "CURLOT_STRING given a long")]
    [DataRow(CurlOption.Httpheader, "X-A: 1", DisplayName = "CURLOT_SLIST given a string")]
    [DataRow(CurlOption.SslcertBlob, "pem", DisplayName = "CURLOT_BLOB given a string")]
    [DataRow(CurlOption.Writefunction, 1L, DisplayName = "CURLOT_FUNCTION given a long")]
    [DataRow(CurlOption.Port, null, DisplayName = "CURLOT_LONG given null")]
    public void SetOption_AValueOfTheWrongType_ReturnsBadFunctionArgument(CurlOption option, object? value)
    {
        AssertBadFunctionArgument(option, value);
    }

    [TestMethod]
    public void SetOption_CurlotObjectGivenADelegate_ReturnsBadFunctionArgument()
    {
        AssertBadFunctionArgument(CurlOption.Errorbuffer, new Func<int>(() => 1));
    }

    [TestMethod]
    public void SetOption_CurlotCbptrGivenADelegate_ReturnsBadFunctionArgument()
    {
        AssertBadFunctionArgument(CurlOption.Writedata, new Func<int>(() => 1));
    }

    [TestMethod]
    public void SetOption_AValueOfTheRightTypeEachTypeTakes_ReachesTheSetter()
    {
        object?[][] cases =
        [
            [CurlOption.Port, 80L], [CurlOption.Port, 80],
            [CurlOption.HttpVersion, 2L], [CurlOption.HttpVersion, 2], [CurlOption.HttpVersion, HttpVersion.Http2],
            [CurlOption.PostfieldsizeLarge, 5L], [CurlOption.PostfieldsizeLarge, 5],
            [CurlOption.Errorbuffer, new byte[256]], [CurlOption.Errorbuffer, null],
            [CurlOption.Url, "http://example.com/"], [CurlOption.Url, null],
            [CurlOption.Httpheader, new[] { "X-A: 1" }], [CurlOption.Httpheader, new List<string>()], [CurlOption.Httpheader, null],
            [CurlOption.Writedata, new object()], [CurlOption.Writedata, null],
            [CurlOption.SslcertBlob, new byte[] { 1 }], [CurlOption.SslcertBlob, new ReadOnlyMemory<byte>([1])], [CurlOption.SslcertBlob, null],
            [CurlOption.Writefunction, new Func<int>(() => 1)], [CurlOption.Writefunction, null],
        ];

        foreach (var pair in cases)
        {
            var option = (CurlOption)pair[0]!;
            object? seen = "unset";
            var dispatch = new EasyOptionDispatch(new Dictionary<CurlOption, EasyOptionSetter>
            {
                [option] = (ref EasyOptions _, object? value) => { seen = value; return CurlCode.Ok; },
            });
            var options = new EasyOptions();

            Assert.AreEqual(CurlCode.Ok, dispatch.SetOption(ref options, option, pair[1]), $"{option} {pair[1]}");
            Assert.AreEqual(pair[1], seen, $"{option} {pair[1]}");
        }
    }

    [TestMethod]
    public void SetOption_TheSetterChangesTheOptions_ReplacesTheCallersOptions()
    {
        var verbose = new EasyOptions { Behaviour = new() { Verbose = true } };
        var dispatch = new EasyOptionDispatch(new Dictionary<CurlOption, EasyOptionSetter>
        {
            [CurlOption.Verbose] = (ref EasyOptions options, object? _) => { options = verbose; return CurlCode.Ok; },
        });
        var options = new EasyOptions();

        dispatch.SetOption(ref options, CurlOption.Verbose, 1L);

        Assert.AreSame(verbose, options);
    }

    [TestMethod]
    public void SetOption_TheSettersAnswer_IsReturned()
    {
        var dispatch = new EasyOptionDispatch(new Dictionary<CurlOption, EasyOptionSetter>
        {
            [CurlOption.Port] = (ref EasyOptions _, object? _) => CurlCode.BadFunctionArgument,
        });
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.BadFunctionArgument, dispatch.SetOption(ref options, CurlOption.Port, 70000L));
    }

    [TestMethod]
    public void SetOption_AnOptionNoGroupSetsYet_ReturnsNotBuiltInAndLeavesTheOptions()
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.NotBuiltIn, new EasyOptionDispatch(NoSetters).SetOption(ref options, CurlOption.Url, "http://example.com/"));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    public void SetOption_AnAliasNumber_DispatchesToTheRealOptionsSetter()
    {
        object? seen = null;
        var dispatch = new EasyOptionDispatch(new Dictionary<CurlOption, EasyOptionSetter>
        {
            [CurlOption.Headerdata] = (ref EasyOptions _, object? value) => { seen = value; return CurlCode.Ok; },
        });
        var options = new EasyOptions();
        var data = new object();

        Assert.AreEqual(CurlCode.Ok, dispatch.SetOption(ref options, CurlOption.Writeheader, data));
        Assert.AreSame(data, seen);
    }

    private static void AssertBadFunctionArgument(CurlOption option, object? value)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, EasyOptionDispatch.Default.SetOption(ref options, option, value));
        Assert.AreSame(before, options);
    }
}
