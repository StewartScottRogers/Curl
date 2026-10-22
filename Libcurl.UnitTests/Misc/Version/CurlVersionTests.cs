using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.Mime;
using Curl.Api.EngineContracts.Multi;
using Curl.Api.EngineContracts.Share;
using Curl.Api.EngineContracts.Url;
using Curl.Cli;
using Curl.Engine;
using Libcurl.Global;
using Libcurl.Misc.Version;
using Libcurl.UnitTests.Global;

namespace Libcurl.UnitTests.Misc.Version;

[TestClass]
public sealed class CurlVersionTests
{
    // The Schannel reference build's curl -V, as Curl.Cli's CurlVersionText pins it (ADR-0021):
    // "curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel", its Protocols: line without the
    // tool's own ipfs and ipns, and its Features: line.
    private static readonly CurlVersionInfo SchannelBuild = new()
    {
        Host = "x86_64-w64-mingw32",
        TlsBackend = "Schannel",
        Protocols =
        [
            "dict", "file", "ftp", "ftps", "gopher", "gophers", "http", "https", "imap", "imaps", "ldap", "ldaps",
            "mqtt", "mqtts", "pop3", "pop3s", "rtsp", "scp", "sftp", "smtp", "smtps", "telnet", "tftp", "ws", "wss",
        ],
        Features =
        [
            "alt-svc", "AsynchDNS", "brotli", "HSTS", "HTTPS-proxy", "HTTPSRR", "IDN", "IPv6", "Kerberos", "Largefile",
            "libz", "NTLM", "PSL", "SPNEGO", "SSL", "SSPI", "threadsafe", "UnixSockets", "zstd",
        ],
    };

    private const CurlVersionFeatures SchannelBuildBits =
        CurlVersionFeatures.AltSvc | CurlVersionFeatures.AsynchDns | CurlVersionFeatures.Brotli | CurlVersionFeatures.Hsts
        | CurlVersionFeatures.HttpsProxy | CurlVersionFeatures.Idn | CurlVersionFeatures.IPv6 | CurlVersionFeatures.Kerberos5
        | CurlVersionFeatures.LargeFile | CurlVersionFeatures.Libz | CurlVersionFeatures.Ntlm | CurlVersionFeatures.Psl
        | CurlVersionFeatures.Spnego | CurlVersionFeatures.Ssl | CurlVersionFeatures.Sspi | CurlVersionFeatures.ThreadSafe
        | CurlVersionFeatures.UnixSockets | CurlVersionFeatures.Zstd;

    [TestMethod]
    public async Task Text_EngineWithTlsBackend_IsTheTailOfCurlVsFirstLine()
    {
        await using CurlLibrary library = Init(SchannelBuild);

        string text = CurlVersion.Text(library);

        Assert.AreEqual("libcurl/8.22.0 Schannel", text);
        StringAssert.EndsWith("curl 8.22.0 (x86_64-w64-mingw32) libcurl/8.22.0 Schannel", " " + text);
    }

    [TestMethod]
    public async Task Text_EngineWithoutTls_IsLibcurlAndTheVersionAlone()
    {
        await using CurlLibrary library = Init(new CurlVersionInfo { Version = "9.1.2" });

        Assert.AreEqual("libcurl/9.1.2", CurlVersion.Text(library));
    }

    [TestMethod]
    [DataRow(CurlVersionAge.First)]
    [DataRow(CurlVersionAge.Second)]
    [DataRow(CurlVersionAge.Third)]
    [DataRow(CurlVersionAge.Fourth)]
    [DataRow(CurlVersionAge.Fifth)]
    [DataRow(CurlVersionAge.Sixth)]
    [DataRow(CurlVersionAge.Seventh)]
    [DataRow(CurlVersionAge.Eighth)]
    [DataRow(CurlVersionAge.Ninth)]
    [DataRow(CurlVersionAge.Tenth)]
    [DataRow(CurlVersionAge.Eleventh)]
    [DataRow(CurlVersionAge.Twelfth)]
    [DataRow((CurlVersionAge)12)]
    public async Task Info_AnyAge_AnswersEveryFieldOfTheNewestAge(CurlVersionAge age)
    {
        await using CurlLibrary library = Init(SchannelBuild);

        CurlVersionInfoData info = CurlVersion.Info(library, age);

        Assert.AreEqual(CurlVersionAge.Twelfth, info.Age);
        Assert.AreEqual("8.22.0", info.Version);
        Assert.AreEqual(0x081600, info.VersionNumber);
        Assert.AreEqual("x86_64-w64-mingw32", info.Host);
        Assert.AreEqual(SchannelBuildBits, info.Features);
        Assert.AreEqual("Schannel", info.SslVersion);
        Assert.AreEqual(0L, info.SslVersionNumber);
        CollectionAssert.AreEqual(SchannelBuild.Protocols.ToArray(), info.Protocols.ToArray());
        CollectionAssert.AreEqual(SchannelBuild.Features.ToArray(), info.FeatureNames.ToArray());
    }

    [TestMethod]
    public async Task Info_LibrariesTheEngineDoesNotReport_AreNullOrZero()
    {
        await using CurlLibrary library = Init(SchannelBuild);

        CurlVersionInfoData info = CurlVersion.Info(library);

        Assert.IsNull(info.LibzVersion);
        Assert.IsNull(info.Ares);
        Assert.AreEqual(0, info.AresNumber);
        Assert.IsNull(info.Libidn);
        Assert.AreEqual(0, info.IconvVersionNumber);
        Assert.IsNull(info.LibsshVersion);
        Assert.AreEqual(0, info.BrotliVersionNumber);
        Assert.IsNull(info.BrotliVersion);
        Assert.AreEqual(0, info.Nghttp2VersionNumber);
        Assert.IsNull(info.Nghttp2Version);
        Assert.IsNull(info.QuicVersion);
        Assert.IsNull(info.CaInfo);
        Assert.IsNull(info.CaPath);
        Assert.AreEqual(0, info.ZstdVersionNumber);
        Assert.IsNull(info.ZstdVersion);
        Assert.IsNull(info.HyperVersion);
        Assert.IsNull(info.GsaslVersion);
        Assert.IsNull(info.RtmpVersion);
    }

    [TestMethod]
    public async Task Info_DefaultEngine_HasNoFeaturesNoTlsAndNoProtocols()
    {
        await using CurlLibrary library = Init(new CurlVersionInfo());

        CurlVersionInfoData info = CurlVersion.Info(library);

        Assert.AreEqual(CurlVersionFeatures.None, info.Features);
        Assert.IsNull(info.SslVersion);
        Assert.IsEmpty(info.Protocols);
        Assert.IsEmpty(info.FeatureNames);
        Assert.AreEqual("libcurl/8.22.0", CurlVersion.Text(library));
    }

    [TestMethod]
    public async Task TextAndInfo_AfterGlobalCleanup_StillAnswer()
    {
        CurlLibrary library = Init(SchannelBuild);
        await library.DisposeAsync();

        Assert.AreEqual("libcurl/8.22.0 Schannel", CurlVersion.Text(library));
        Assert.AreEqual("8.22.0", CurlVersion.Info(library).Version);
    }

    [TestMethod]
    public async Task TextAndInfo_ManagedEngine_AgreeWithCurlVOnTheRunningPlatform()
    {
        IReadOnlyList<string> lines = CurlVersionText.Lines(OperatingSystem.IsWindows(), OperatingSystem.IsMacOS());
        await using CurlLibrary library = CurlGlobal.InitCounting(new ManagedCurlEngine(), CurlGlobalFlags.Default, new GlobalInitCounter());

        string text = CurlVersion.Text(library);
        CurlVersionInfoData info = CurlVersion.Info(library, CurlVersionAge.Now);

        StringAssert.EndsWith(lines[0], ") " + text);
        Assert.AreEqual($"curl {info.Version} ({info.Host}) libcurl/{info.Version} {info.SslVersion}", lines[0]);
        string[] protocols = [.. info.Protocols, "ipfs", "ipns"];
        Array.Sort(protocols, StringComparer.Ordinal);
        Assert.AreEqual("Protocols: " + string.Join(' ', protocols), lines[2]);
        Assert.AreEqual("Features: " + string.Join(' ', info.FeatureNames), lines[3]);
        CurlVersionFeatures expectedBits = CurlVersionFeatures.None;
        foreach (string name in info.FeatureNames)
        {
            expectedBits |= CurlVersion.FeatureBit(name);
        }

        Assert.AreEqual(expectedBits, info.Features);
        Assert.AreNotEqual(CurlVersionFeatures.None, info.Features & CurlVersionFeatures.Ssl);
    }

    [TestMethod]
    public void TextAndInfo_NullLibrary_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlVersion.Text(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlVersion.Info(null!));
    }

    // lib/version.c's features table, name for name.
    [TestMethod]
    [DataRow("alt-svc", CurlVersionFeatures.AltSvc)]
    [DataRow("AsynchDNS", CurlVersionFeatures.AsynchDns)]
    [DataRow("brotli", CurlVersionFeatures.Brotli)]
    [DataRow("Debug", CurlVersionFeatures.Debug | CurlVersionFeatures.CurlDebug)]
    [DataRow("gsasl", CurlVersionFeatures.Gsasl)]
    [DataRow("GSS-API", CurlVersionFeatures.GssApi)]
    [DataRow("HSTS", CurlVersionFeatures.Hsts)]
    [DataRow("HTTP2", CurlVersionFeatures.Http2)]
    [DataRow("HTTP3", CurlVersionFeatures.Http3)]
    [DataRow("HTTPS-proxy", CurlVersionFeatures.HttpsProxy)]
    [DataRow("IDN", CurlVersionFeatures.Idn)]
    [DataRow("IPv6", CurlVersionFeatures.IPv6)]
    [DataRow("Kerberos", CurlVersionFeatures.Kerberos5)]
    [DataRow("Largefile", CurlVersionFeatures.LargeFile)]
    [DataRow("libz", CurlVersionFeatures.Libz)]
    [DataRow("MultiSSL", CurlVersionFeatures.MultiSsl)]
    [DataRow("NTLM", CurlVersionFeatures.Ntlm)]
    [DataRow("NTLM_WB", CurlVersionFeatures.NtlmWinbind)]
    [DataRow("PSL", CurlVersionFeatures.Psl)]
    [DataRow("SPNEGO", CurlVersionFeatures.Spnego)]
    [DataRow("SSL", CurlVersionFeatures.Ssl)]
    [DataRow("SSPI", CurlVersionFeatures.Sspi)]
    [DataRow("threadsafe", CurlVersionFeatures.ThreadSafe)]
    [DataRow("TLS-SRP", CurlVersionFeatures.TlsAuthSrp)]
    [DataRow("Unicode", CurlVersionFeatures.Unicode)]
    [DataRow("UnixSockets", CurlVersionFeatures.UnixSockets)]
    [DataRow("zstd", CurlVersionFeatures.Zstd)]
    public void FeatureBit_FeatureWithABit_AnswersItsCurlVersionBit(string name, CurlVersionFeatures expected)
    {
        Assert.AreEqual(expected, CurlVersion.FeatureBit(name));
    }

    [TestMethod]
    [DataRow("ECH")]
    [DataRow("HTTPSRR")]
    [DataRow("SSLS-EXPORT")]
    [DataRow("CharConv")]
    [DataRow("TrackMemory")]
    [DataRow("ssl")]
    [DataRow("")]
    public void FeatureBit_FeatureWithoutABitOrUnknownName_AnswersNone(string name)
    {
        Assert.AreEqual(CurlVersionFeatures.None, CurlVersion.FeatureBit(name));
    }

    // curl 8.22.0's lib/version.c lists these names; CharConv (gone in 7.82.0) and TrackMemory (8.19.0) it does not.
    [TestMethod]
    public void FeatureNames_Always_AreVersionCsFeatureNames()
    {
        string[] expected =
        [
            "alt-svc", "AppleSecTrust", "AsynchDNS", "asyn-rr", "brotli", "Debug", "ECH", "gsasl", "GSS-API", "HSTS",
            "HTTP2", "HTTP3", "HTTPS-proxy", "HTTPSIG", "HTTPSRR", "IDN", "IPv6", "Kerberos", "Largefile", "libz",
            "MultiSSL", "NativeCA", "NTLM", "NTLM_WB", "proxy-HTTP3", "PSL", "SPNEGO", "SSL", "SSLS-EXPORT", "SSPI",
            "threadsafe", "TLS-SRP", "Unicode", "UnixSockets", "zstd",
        ];

        CollectionAssert.AreEquivalent(expected, CurlVersion.FeatureNames.ToArray());
    }

    [TestMethod]
    [DataRow(CurlVersionFeatures.IPv6, 1)]
    [DataRow(CurlVersionFeatures.Kerberos4, 1 << 1)]
    [DataRow(CurlVersionFeatures.GssNegotiate, 1 << 5)]
    [DataRow(CurlVersionFeatures.Kerberos5, 1 << 18)]
    [DataRow(CurlVersionFeatures.ThreadSafe, 1 << 30)]
    public void FeatureBit_CurlHsDefine_HasItsValue(CurlVersionFeatures feature, int expected)
    {
        Assert.AreEqual(expected, (int)feature);
    }

    [TestMethod]
    [DataRow(CurlVersionAge.First, 0)]
    [DataRow(CurlVersionAge.Now, 11)]
    public void Age_CurlHsEnum_HasItsValue(CurlVersionAge age, int expected)
    {
        Assert.AreEqual(expected, (int)age);
    }

    private static CurlLibrary Init(CurlVersionInfo versionInfo) =>
        CurlGlobal.InitCounting(new VersionEngine(versionInfo), CurlGlobalFlags.Default, new GlobalInitCounter());

    /// <summary>An engine that reports <see cref="VersionInfo"/> and hands out inert handles.</summary>
    private sealed class VersionEngine(CurlVersionInfo versionInfo) : ICurlEngine
    {
        public CurlVersionInfo VersionInfo { get; } = versionInfo;

        public IEasyTransfer CreateEasy() => InertHandle.Create<IEasyTransfer>();

        public IMultiTransfer CreateMulti() => InertHandle.Create<IMultiTransfer>();

        public IShareHandle CreateShare() => InertHandle.Create<IShareHandle>();

        public ICurlUrlHandle CreateUrl() => InertHandle.Create<ICurlUrlHandle>();

        public IMimeBuilder CreateMime() => InertHandle.Create<IMimeBuilder>();

        public ICurlFormBuilder CreateForm() => InertHandle.Create<ICurlFormBuilder>();

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
