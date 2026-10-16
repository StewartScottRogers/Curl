using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Protocols;
using Libcurl.Options;

namespace Libcurl.UnitTests.Options.Protocols;

/// <summary>Sets protocol options through <see cref="EasyOptionDispatch.Default"/> and checks the answer.</summary>
internal static class ProtocolOptionAssert
{
    internal static EasyProtocolOptions SetOk(CurlOption option, object? value) => SetOk(new EasyOptions(), option, value).Protocols;

    internal static EasyOptions SetOk(EasyOptions options, CurlOption option, object? value)
    {
        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, option, value), $"{option} {value}");
        return options;
    }

    internal static void Refused(CurlOption option, object? value)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, EasyOptionDispatch.Default.SetOption(ref options, option, value), $"{option} {value}");
        Assert.AreSame(before, options);
    }

    internal static void Switch(CurlOption option, Func<EasyProtocolOptions, bool> read)
    {
        Assert.IsTrue(read(SetOk(option, 2L)), $"{option} 2");
        Assert.IsFalse(read(SetOk(option, 0)), $"{option} 0");
    }

    internal static void Text(CurlOption option, Func<EasyProtocolOptions, string?> read)
    {
        var options = SetOk(new EasyOptions(), option, "value");

        Assert.AreEqual("value", read(options.Protocols), $"{option}");
        Assert.IsNull(read(SetOk(options, option, null).Protocols), $"{option} null");
    }

    internal static void List(CurlOption option, Func<EasyProtocolOptions, IReadOnlyList<string>> read)
    {
        var lines = new List<string> { "one", "two" };
        var options = SetOk(new EasyOptions(), option, lines);
        lines.Add("three");

        CollectionAssert.AreEqual(new[] { "one", "two" }, read(options.Protocols).ToArray(), $"{option}");
        Assert.AreEqual(0, read(SetOk(options, option, null).Protocols).Count, $"{option} null");
    }
}
