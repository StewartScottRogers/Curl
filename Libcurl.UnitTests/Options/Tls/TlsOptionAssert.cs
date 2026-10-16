using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Tls;
using Libcurl.Options;

namespace Libcurl.UnitTests.Options.Tls;

/// <summary>Sets TLS options through <see cref="EasyOptionDispatch.Default"/> and checks the answer.</summary>
internal static class TlsOptionAssert
{
    internal static EasyTlsOptions SetOk(CurlOption option, object? value) => SetOk(new EasyOptions(), option, value).Tls;

    internal static EasyOptions SetOk(EasyOptions options, CurlOption option, object? value)
    {
        Assert.AreEqual(CurlCode.Ok, EasyOptionDispatch.Default.SetOption(ref options, option, value), $"{option} {value}");
        return options;
    }

    internal static void Answers(CurlCode expected, CurlOption option, object? value)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(expected, EasyOptionDispatch.Default.SetOption(ref options, option, value), $"{option} {value}");
        Assert.AreSame(before, options);
    }

    internal static void Switch(CurlOption option, Func<EasyTlsOptions, bool> read)
    {
        Assert.IsTrue(read(SetOk(option, 2L)), $"{option} 2");
        Assert.IsFalse(read(SetOk(option, 0)), $"{option} 0");
    }

    internal static void Text(CurlOption option, Func<EasyTlsOptions, string?> read)
    {
        var options = SetOk(new EasyOptions(), option, "value");

        Assert.AreEqual("value", read(options.Tls), $"{option}");
        Assert.IsNull(read(SetOk(options, option, null).Tls), $"{option} null");
    }

    /// <summary>Checks <paramref name="origin"/> lands in <see cref="EasyTlsOptions.Origin"/> and <paramref name="proxy"/> in <see cref="EasyTlsOptions.Proxy"/>, each leaving the other at its default.</summary>
    internal static void Twins(CurlOption origin, CurlOption proxy, object? value, Func<TlsPeerOptions, object?> read, object? expected)
    {
        var unset = read(new TlsPeerOptions());
        var originSet = SetOk(origin, value);
        var proxySet = SetOk(proxy, value);

        Assert.AreEqual(expected, read(originSet.Origin), $"{origin}");
        Assert.AreEqual(unset, read(originSet.Proxy), $"{origin} proxy");
        Assert.AreEqual(expected, read(proxySet.Proxy), $"{proxy}");
        Assert.AreEqual(unset, read(proxySet.Origin), $"{proxy} origin");
    }

    internal static void TwinText(CurlOption origin, CurlOption proxy, Func<TlsPeerOptions, string?> read)
    {
        Twins(origin, proxy, "value", read, "value");
        Assert.IsNull(read(SetOk(SetOk(new EasyOptions(), origin, "value"), origin, null).Tls.Origin), $"{origin} null");
        Assert.IsNull(read(SetOk(SetOk(new EasyOptions(), proxy, "value"), proxy, null).Tls.Proxy), $"{proxy} null");
    }

    internal static void TwinSwitch(CurlOption origin, CurlOption proxy, Func<TlsPeerOptions, bool> read)
    {
        Twins(origin, proxy, 0, p => read(p), false);
        Twins(origin, proxy, 5L, p => read(p), true);
    }

    /// <summary>Checks a blob twin takes a byte array and a memory, copies the bytes, and clears on <see langword="null"/>.</summary>
    internal static void TwinBlob(CurlOption origin, CurlOption proxy, Func<TlsPeerOptions, ReadOnlyMemory<byte>?> read)
    {
        foreach (var (option, peer) in new[] { (origin, (Func<EasyTlsOptions, TlsPeerOptions>)(t => t.Origin)), (proxy, t => t.Proxy) })
        {
            var bytes = new byte[] { 1, 2, 3 };
            var fromArray = SetOk(new EasyOptions(), option, bytes);
            bytes[0] = 9;
            var fromMemory = SetOk(new EasyOptions(), option, new ReadOnlyMemory<byte>(bytes, 1, 2));

            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, read(peer(fromArray.Tls))!.Value.ToArray(), $"{option} array");
            CollectionAssert.AreEqual(new byte[] { 2, 3 }, read(peer(fromMemory.Tls))!.Value.ToArray(), $"{option} memory");
            Assert.IsNull(read(peer(SetOk(fromArray, option, null).Tls)), $"{option} null");
        }

        Assert.IsNull(read(SetOk(origin, new byte[] { 1 }).Proxy), $"{origin} proxy");
        Assert.IsNull(read(SetOk(proxy, new byte[] { 1 }).Origin), $"{proxy} origin");
    }
}
