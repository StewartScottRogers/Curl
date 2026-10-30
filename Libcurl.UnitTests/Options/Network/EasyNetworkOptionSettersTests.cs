using Curl.Api.Codes;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Libcurl.Options;
using Libcurl.UnitTests.Url;
using Libcurl.Url;

namespace Libcurl.UnitTests.Options.Network;

[TestClass]
public sealed class EasyNetworkOptionSettersTests
{
    private static readonly (CurlOption Option, Func<EasyNetworkOptions, string?> Field)[] Strings =
    [
        (CurlOption.Url, static n => n.Url),
        (CurlOption.DefaultProtocol, static n => n.DefaultProtocol),
        (CurlOption.Interface, static n => n.Interface),
        (CurlOption.UnixSocketPath, static n => n.UnixSocketPath),
        (CurlOption.AbstractUnixSocket, static n => n.AbstractUnixSocket),
        (CurlOption.DohUrl, static n => n.DohUrl),
    ];

    private static readonly (CurlOption Option, Func<EasyNetworkOptions, bool> Field)[] Flags =
    [
        (CurlOption.PathAsIs, static n => n.PathAsIs),
        (CurlOption.DnsShuffleAddresses, static n => n.DnsShuffleAddresses),
        (CurlOption.TcpNodelay, static n => n.TcpNoDelay),
        (CurlOption.TcpKeepalive, static n => n.TcpKeepAlive),
        (CurlOption.FreshConnect, static n => n.FreshConnect),
        (CurlOption.ForbidReuse, static n => n.ForbidReuse),
    ];

    private static readonly (CurlOption Option, Func<EasyNetworkOptions, TimeSpan> Field, TimeSpan Unit)[] Times =
    [
        (CurlOption.TcpKeepidle, static n => n.TcpKeepIdle, TimeSpan.FromSeconds(1)),
        (CurlOption.TcpKeepintvl, static n => n.TcpKeepInterval, TimeSpan.FromSeconds(1)),
        (CurlOption.Connecttimeout, static n => n.ConnectTimeout, TimeSpan.FromSeconds(1)),
        (CurlOption.ConnecttimeoutMs, static n => n.ConnectTimeout, TimeSpan.FromMilliseconds(1)),
        (CurlOption.HappyEyeballsTimeoutMs, static n => n.HappyEyeballsTimeout, TimeSpan.FromMilliseconds(1)),
        (CurlOption.MaxageConn, static n => n.MaxIdleConnectionAge, TimeSpan.FromSeconds(1)),
        (CurlOption.MaxlifetimeConn, static n => n.MaxConnectionLifetime, TimeSpan.FromSeconds(1)),
        (CurlOption.AccepttimeoutMs, static n => n.AcceptTimeout, TimeSpan.FromMilliseconds(1)),
        (CurlOption.UpkeepIntervalMs, static n => n.UpkeepInterval, TimeSpan.FromMilliseconds(1)),
        (CurlOption.ServerResponseTimeout, static n => n.ServerResponseTimeout, TimeSpan.FromSeconds(1)),
        (CurlOption.ServerResponseTimeoutMs, static n => n.ServerResponseTimeout, TimeSpan.FromMilliseconds(1)),
    ];

    [TestMethod]
    public void SetOption_EachStringOption_SetsItsFieldAndNullClearsIt()
    {
        foreach (var (option, field) in Strings)
        {
            var options = new EasyOptions();

            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, "value"), option.ToString());
            Assert.AreEqual("value", field(options.Network), option.ToString());
            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, null), option.ToString());
            Assert.IsNull(field(options.Network), option.ToString());
        }
    }

    [TestMethod]
    public void SetOption_Url_LeavesTheOtherNetworkFieldsAlone()
    {
        var options = new EasyOptions();

        Set(ref options, CurlOption.Url, "http://example.com/");

        Assert.AreEqual(new EasyNetworkOptions { Url = "http://example.com/" }, options.Network);
    }

    [TestMethod]
    public void SetOption_EachOnOffOption_SetsItsFieldOnForNonZeroAndOffForZero()
    {
        foreach (var (option, field) in Flags)
        {
            var options = new EasyOptions();

            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, 2L), option.ToString());
            Assert.IsTrue(field(options.Network), option.ToString());
            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, 0), option.ToString());
            Assert.IsFalse(field(options.Network), option.ToString());
        }
    }

    [TestMethod]
    public void SetOption_EachTimeOption_SetsItsFieldInItsUnitAndCutsPastIntMax()
    {
        foreach (var (option, field, unit) in Times)
        {
            var options = new EasyOptions();

            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, 7L), option.ToString());
            Assert.AreEqual(unit * 7, field(options.Network), option.ToString());
            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, 0), option.ToString());
            Assert.AreEqual(TimeSpan.Zero, field(options.Network), option.ToString());
            Assert.AreEqual(CurlCode.Ok, Set(ref options, option, long.MaxValue), option.ToString());
            Assert.AreEqual(unit * int.MaxValue, field(options.Network), option.ToString());
        }
    }

    [TestMethod]
    public void SetOption_EachTimeOptionGivenANegativeValue_ReturnsBadFunctionArgument()
    {
        foreach (var (option, _, _) in Times)
        {
            AssertBadFunctionArgument(option, -1L);
        }
    }

    [TestMethod]
    public void SetOption_TcpKeepcnt_SetsTheCountAndRefusesANegativeOne()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.TcpKeepcnt, 3));
        Assert.AreEqual(3, options.Network.TcpKeepCount);
        AssertBadFunctionArgument(CurlOption.TcpKeepcnt, -1);
    }

    [TestMethod]
    [DataRow(CurlOption.Port, 0L, DisplayName = "PORT 0")]
    [DataRow(CurlOption.Port, 65535L, DisplayName = "PORT 65535")]
    [DataRow(CurlOption.Localport, 0L, DisplayName = "LOCALPORT 0")]
    [DataRow(CurlOption.Localport, 65535L, DisplayName = "LOCALPORT 65535")]
    [DataRow(CurlOption.Localportrange, 0L, DisplayName = "LOCALPORTRANGE 0")]
    [DataRow(CurlOption.Localportrange, 65535L, DisplayName = "LOCALPORTRANGE 65535")]
    public void SetOption_APortOptionInRange_SetsItsField(CurlOption option, long value)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, value));

        var set = option switch
        {
            CurlOption.Port => options.Network.Port,
            CurlOption.Localport => options.Network.LocalPort,
            _ => options.Network.LocalPortRange,
        };
        Assert.AreEqual(value, set);
    }

    [TestMethod]
    [DataRow(CurlOption.Port, -1L, DisplayName = "PORT -1")]
    [DataRow(CurlOption.Port, 65536L, DisplayName = "PORT 65536")]
    [DataRow(CurlOption.Localport, -1L, DisplayName = "LOCALPORT -1")]
    [DataRow(CurlOption.Localport, 65536L, DisplayName = "LOCALPORT 65536")]
    [DataRow(CurlOption.Localportrange, -1L, DisplayName = "LOCALPORTRANGE -1")]
    [DataRow(CurlOption.Localportrange, 65536L, DisplayName = "LOCALPORTRANGE 65536")]
    [DataRow(CurlOption.AddressScope, 4294967296L, DisplayName = "ADDRESS_SCOPE 2^32")]
    [DataRow(CurlOption.Ipresolve, -1L, DisplayName = "IPRESOLVE -1")]
    [DataRow(CurlOption.Ipresolve, 3L, DisplayName = "IPRESOLVE 3")]
    [DataRow(CurlOption.ConnectOnly, -1L, DisplayName = "CONNECT_ONLY -1")]
    [DataRow(CurlOption.ConnectOnly, 3L, DisplayName = "CONNECT_ONLY 3")]
    [DataRow(CurlOption.UseSsl, -1L, DisplayName = "USE_SSL -1")]
    [DataRow(CurlOption.UseSsl, 4L, DisplayName = "USE_SSL 4")]
    [DataRow(CurlOption.DnsCacheTimeout, -2L, DisplayName = "DNS_CACHE_TIMEOUT -2")]
    [DataRow(CurlOption.Port, "80", DisplayName = "PORT given a string")]
    [DataRow(CurlOption.Url, 80L, DisplayName = "URL given a number")]
    public void SetOption_AValueOutOfRangeOrOfTheWrongType_ReturnsBadFunctionArgument(CurlOption option, object value)
    {
        AssertBadFunctionArgument(option, value);
    }

    [TestMethod]
    public void SetOption_AddressScope_TakesUpToUIntMax()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.AddressScope, (long)uint.MaxValue));
        Assert.AreEqual(uint.MaxValue, options.Network.AddressScope);
    }

    [TestMethod]
    public void SetOption_Maxconnects_SetsTheCacheSize()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Maxconnects, 1));
        Assert.AreEqual(1, options.Network.MaxConnects);
    }

    [TestMethod]
    public void SetOption_Ipresolve_TakesTheEnumOrItsNumber()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Ipresolve, IpResolve.V6));
        Assert.AreEqual(IpResolve.V6, options.Network.IpResolve);
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.Ipresolve, 1L));
        Assert.AreEqual(IpResolve.V4, options.Network.IpResolve);
    }

    [TestMethod]
    public void SetOption_ConnectOnly_TakesConnectAndWebSocket()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.ConnectOnly, 1L));
        Assert.AreEqual(ConnectOnly.Connect, options.Network.ConnectOnly);
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.ConnectOnly, 2L));
        Assert.AreEqual(ConnectOnly.WebSocket, options.Network.ConnectOnly);
    }

    [TestMethod]
    public void SetOption_UseSsl_TakesEveryLevel()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.UseSsl, UseSsl.All));
        Assert.AreEqual(UseSsl.All, options.Network.UseSsl);
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.UseSsl, 0));
        Assert.AreEqual(UseSsl.None, options.Network.UseSsl);
    }

    [TestMethod]
    public void SetOption_DnsCacheTimeout_TakesSecondsMinusOneForForeverAndCutsPastIntMax()
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.DnsCacheTimeout, 30L));
        Assert.AreEqual(TimeSpan.FromSeconds(30), options.Network.DnsCacheTimeout);
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.DnsCacheTimeout, -1L));
        Assert.AreEqual(Timeout.InfiniteTimeSpan, options.Network.DnsCacheTimeout);
        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.DnsCacheTimeout, long.MaxValue));
        Assert.AreEqual(TimeSpan.FromSeconds(int.MaxValue), options.Network.DnsCacheTimeout);
    }

    [TestMethod]
    public void SetOption_DnsUseGlobalCache_IsIgnored()
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.Ok, Set(ref options, CurlOption.DnsUseGlobalCache, 1L));
        Assert.AreSame(before, options);
    }

    [TestMethod]
    [DataRow(CurlOption.ConnectTo, DisplayName = "CONNECT_TO")]
    [DataRow(CurlOption.Resolve, DisplayName = "RESOLVE")]
    public void SetOption_AListOption_StoresACopyOfTheEntriesAndNullEmptiesIt(CurlOption option)
    {
        var options = new EasyOptions();
        var entries = new List<string> { "example.com:443:127.0.0.1" };

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, entries));
        entries.Add("later");
        CollectionAssert.AreEqual(new[] { "example.com:443:127.0.0.1" }, ListOf(options.Network, option).ToArray());
        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, null));
        Assert.IsEmpty(ListOf(options.Network, option));
    }

    [TestMethod]
    [DataRow(CurlOption.ProtocolsStr, "http,https", DisplayName = "PROTOCOLS_STR http,https")]
    [DataRow(CurlOption.ProtocolsStr, "ALL", DisplayName = "PROTOCOLS_STR ALL")]
    [DataRow(CurlOption.ProtocolsStr, ",HTTP,http,", DisplayName = "PROTOCOLS_STR with empty entries")]
    [DataRow(CurlOption.RedirProtocolsStr, "https", DisplayName = "REDIR_PROTOCOLS_STR https")]
    [DataRow(CurlOption.RedirProtocolsStr, "http,MQTTS", DisplayName = "REDIR_PROTOCOLS_STR mqtts")]
    public void SetOption_AProtocolListNamingKnownSchemes_StoresItAsGiven(CurlOption option, string names)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, names));
        Assert.AreEqual(names, ProtocolsOf(options.Network, option));
    }

    [TestMethod]
    [DataRow(CurlOption.ProtocolsStr, "", DisplayName = "PROTOCOLS_STR empty")]
    [DataRow(CurlOption.ProtocolsStr, ",", DisplayName = "PROTOCOLS_STR a lone comma")]
    [DataRow(CurlOption.RedirProtocolsStr, "", DisplayName = "REDIR_PROTOCOLS_STR empty")]
    public void SetOption_AProtocolListNamingNothing_ReturnsBadFunctionArgument(CurlOption option, string? names)
    {
        AssertBadFunctionArgument(option, names);
    }

    [TestMethod]
    [DataRow(CurlOption.ProtocolsStr, "string", DisplayName = "PROTOCOLS_STR string")]
    [DataRow(CurlOption.ProtocolsStr, "nope", DisplayName = "PROTOCOLS_STR nope")]
    [DataRow(CurlOption.ProtocolsStr, "http,http,ft", DisplayName = "PROTOCOLS_STR an unknown name after known ones")]
    [DataRow(CurlOption.ProtocolsStr, "goobar", DisplayName = "PROTOCOLS_STR goobar")]
    [DataRow(CurlOption.ProtocolsStr, "http ", DisplayName = "PROTOCOLS_STR trailing space")]
    [DataRow(CurlOption.ProtocolsStr, " http", DisplayName = "PROTOCOLS_STR leading space")]
    [DataRow(CurlOption.RedirProtocolsStr, "goobar", DisplayName = "REDIR_PROTOCOLS_STR goobar")]
    [DataRow(CurlOption.RedirProtocolsStr, "string", DisplayName = "REDIR_PROTOCOLS_STR string")]
    public void SetOption_AProtocolListNamingAnUnknownScheme_ReturnsUnsupportedProtocol(CurlOption option, string names)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.UnsupportedProtocol, Set(ref options, option, names));
        Assert.IsNull(ProtocolsOf(options.Network, option));
    }

    [TestMethod]
    [DataRow(CurlOption.ProtocolsStr, DisplayName = "PROTOCOLS_STR null")]
    [DataRow(CurlOption.RedirProtocolsStr, DisplayName = "REDIR_PROTOCOLS_STR null")]
    public void SetOption_AProtocolListNull_ResetsToTheDefault(CurlOption option)
    {
        var options = new EasyOptions();
        Set(ref options, option, "http");

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, null));
        Assert.IsNull(ProtocolsOf(options.Network, option));
    }

    [TestMethod]
    [DataRow(CurlOption.Protocols, 3L, "http,https", DisplayName = "PROTOCOLS HTTP|HTTPS")]
    [DataRow(CurlOption.Protocols, 0x400L | 0x40000000L, "file,ws", DisplayName = "PROTOCOLS FILE|WS")]
    [DataRow(CurlOption.Protocols, -1L, "all", DisplayName = "PROTOCOLS CURLPROTO_ALL")]
    [DataRow(CurlOption.Protocols, 0L, "", DisplayName = "PROTOCOLS 0")]
    [DataRow(CurlOption.RedirProtocols, 4L, "ftp", DisplayName = "REDIR_PROTOCOLS FTP")]
    public void SetOption_AProtocolBitmask_StoresTheNamesOfItsBits(CurlOption option, long bits, string names)
    {
        var options = new EasyOptions();

        Assert.AreEqual(CurlCode.Ok, Set(ref options, option, bits));
        Assert.AreEqual(names, ProtocolsOf(options.Network, option));
    }

    [TestMethod]
    public void SetOption_CurluWithAUrlHandle_StoresItsEngineHandle()
    {
        var engine = new FakeCurlUrlHandle();
        using var url = new CurlUrlHandle(engine);
        var options = new EasyOptions();

        var code = Set(ref options, CurlOption.Curlu, url);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreSame(engine, options.Network.UrlHandle);
    }

    [TestMethod]
    public void SetOption_CurluNull_ClearsTheUrlHandle()
    {
        using var url = new CurlUrlHandle(new FakeCurlUrlHandle());
        var options = new EasyOptions();
        Set(ref options, CurlOption.Curlu, url);

        var code = Set(ref options, CurlOption.Curlu, null);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.IsNull(options.Network.UrlHandle);
    }

    [TestMethod]
    public void SetOption_CurluAnyOtherObject_AnswersBadFunctionArgument()
    {
        AssertBadFunctionArgument(CurlOption.Curlu, "http://example.com/");
        AssertBadFunctionArgument(CurlOption.Curlu, new FakeCurlUrlHandle());
    }

    private static string? ProtocolsOf(EasyNetworkOptions network, CurlOption option) =>
        option is CurlOption.Protocols or CurlOption.ProtocolsStr ? network.Protocols : network.RedirectProtocols;

    private static IReadOnlyList<string> ListOf(EasyNetworkOptions network, CurlOption option) =>
        option == CurlOption.ConnectTo ? network.ConnectTo : network.Resolve;

    private static CurlCode Set(ref EasyOptions options, CurlOption option, object? value) =>
        EasyOptionDispatch.Default.SetOption(ref options, option, value);

    private static void AssertBadFunctionArgument(CurlOption option, object? value)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(CurlCode.BadFunctionArgument, Set(ref options, option, value), option.ToString());
        Assert.AreSame(before, options, option.ToString());
    }
}
