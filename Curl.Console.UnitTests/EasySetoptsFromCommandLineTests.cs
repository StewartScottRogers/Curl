using Curl.Api.Options;
using Curl.Api.Options.Authentication;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Libcurl.Options;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="EasySetoptsFromCommandLine" /> (ADR-0631): a behaviour, transfer or network field moved off libcurl's
/// default becomes one <c>curl_easy_setopt</c> call, a field at its default none, and a field libcurl has no
/// <c>CURLOPT_*</c> for, or whose setter would hold it to another value, stays on the hooks instead.
/// </summary>
[TestClass]
public sealed class EasySetoptsFromCommandLineTests
{
    [TestMethod]
    public void Of_LibcurlDefaults_MakesNoCall()
    {
        IReadOnlyList<EasySetopt> setopts = EasySetoptsFromCommandLine.Of(new EasyOptions());

        Assert.IsEmpty(setopts);
    }

    [TestMethod]
    public void Of_OneFieldOfEachGroup_MakesOneCallEachInGroupOrder()
    {
        EasyOptions options = new()
        {
            Behaviour = new EasyBehaviourOptions { Verbose = true, NoProgress = false },
            Transfer = new EasyTransferOptions { Range = "0-3", ResumeFrom = -1, TimeCondition = TimeCondition.IfModifiedSince },
            Network = new EasyNetworkOptions { TcpNoDelay = false, IpResolve = IpResolve.V4, ConnectTo = ["a:80:b:81"] },
        };

        IReadOnlyList<EasySetopt> setopts = EasySetoptsFromCommandLine.Of(options);

        CollectionAssert.AreEqual(
            new[] { CurlOption.Verbose, CurlOption.Noprogress, CurlOption.Range, CurlOption.ResumeFromLarge, CurlOption.Timecondition, CurlOption.ConnectTo, CurlOption.Ipresolve, CurlOption.TcpNodelay },
            setopts.Select(setopt => setopt.Option).ToArray());
        Assert.AreEqual(1L, setopts[0].Value);
        Assert.AreEqual(0L, setopts[1].Value);
        Assert.AreEqual("0-3", setopts[2].Value);
        Assert.AreEqual(-1L, setopts[3].Value);
        Assert.AreEqual((long)TimeCondition.IfModifiedSince, setopts[4].Value);
        CollectionAssert.AreEqual(new[] { "a:80:b:81" }, (string[])setopts[5].Value!);
        Assert.AreEqual((long)IpResolve.V4, setopts[6].Value);
        Assert.AreEqual(0L, setopts[7].Value);
    }

    [TestMethod]
    public void Of_WholeTimes_SetsThemInTheSettersUnits()
    {
        EasyOptions options = new()
        {
            Transfer = new EasyTransferOptions { LowSpeedTime = TimeSpan.FromSeconds(30), Timeout = TimeSpan.FromMilliseconds(1500) },
            Network = new EasyNetworkOptions { ConnectTimeout = TimeSpan.FromSeconds(2), TcpKeepIdle = TimeSpan.FromSeconds(7) },
        };

        IReadOnlyList<EasySetopt> setopts = EasySetoptsFromCommandLine.Of(options);
        EasyOptions kept = EasySetoptsFromCommandLine.KeptOnHooks(options);

        CollectionAssert.AreEqual(
            new[] { new EasySetopt(CurlOption.LowSpeedTime, 30L), new EasySetopt(CurlOption.TimeoutMs, 1500L), new EasySetopt(CurlOption.TcpKeepidle, 7L), new EasySetopt(CurlOption.ConnecttimeoutMs, 2000L) },
            setopts.ToArray());
        Assert.AreEqual(TimeSpan.Zero, kept.Transfer.Timeout);
        Assert.AreEqual(TimeSpan.Zero, kept.Network.ConnectTimeout);
    }

    [TestMethod]
    public void Of_TimesTheSettersWouldCut_LeavesThemOnTheHooks()
    {
        TimeSpan fraction = TimeSpan.FromTicks(15_000);
        TimeSpan pastIntMax = TimeSpan.FromMilliseconds(int.MaxValue + 1L);
        EasyOptions options = new()
        {
            Transfer = new EasyTransferOptions { Timeout = fraction, LowSpeedTime = TimeSpan.FromSeconds(-1) },
            Network = new EasyNetworkOptions { ConnectTimeout = pastIntMax, HappyEyeballsTimeout = fraction, TcpKeepIdle = fraction, TcpKeepInterval = fraction },
        };

        IReadOnlyList<EasySetopt> setopts = EasySetoptsFromCommandLine.Of(options);
        EasyOptions kept = EasySetoptsFromCommandLine.KeptOnHooks(options);

        Assert.IsEmpty(setopts);
        Assert.AreEqual(fraction, kept.Transfer.Timeout);
        Assert.AreEqual(TimeSpan.FromSeconds(-1), kept.Transfer.LowSpeedTime);
        Assert.AreEqual(pastIntMax, kept.Network.ConnectTimeout);
        Assert.AreEqual(fraction, kept.Network.HappyEyeballsTimeout);
        Assert.AreEqual(fraction, kept.Network.TcpKeepIdle);
        Assert.AreEqual(fraction, kept.Network.TcpKeepInterval);
    }

    [TestMethod]
    [DataRow(100L, false)]
    [DataRow(102400L, true)]
    [DataRow(20L * 1024 * 1024, false)]
    public void Of_BufferSize_IsSetOnlyWhereTheSetterKeepsIt(long bufferSize, bool setThroughSetopt)
    {
        EasyOptions options = new() { Transfer = new EasyTransferOptions { BufferSize = bufferSize } };

        IReadOnlyList<EasySetopt> setopts = EasySetoptsFromCommandLine.Of(options);
        EasyOptions kept = EasySetoptsFromCommandLine.KeptOnHooks(options);

        Assert.AreEqual(setThroughSetopt, setopts.Contains(new EasySetopt(CurlOption.Buffersize, bufferSize)));
        Assert.AreEqual(setThroughSetopt ? new EasyTransferOptions().BufferSize : bufferSize, kept.Transfer.BufferSize);
    }

    [TestMethod]
    public void KeptOnHooks_FieldsWithoutCurlopt_StayOnTheHooksAndTheRestAreLibcurlDefaults()
    {
        EasyOptions options = new()
        {
            Behaviour = new EasyBehaviourOptions { Verbose = true },
            Transfer = new EasyTransferOptions { Range = "0-3" },
            Network = new EasyNetworkOptions { IpTypeOfService = 16, VlanPriority = 3, MultipathTcp = true, PathAsIs = true },
        };

        EasyOptions kept = EasySetoptsFromCommandLine.KeptOnHooks(options);

        Assert.IsFalse(kept.Behaviour.Verbose);
        Assert.IsNull(kept.Transfer.Range);
        Assert.IsFalse(kept.Network.PathAsIs);
        Assert.AreEqual(16, kept.Network.IpTypeOfService);
        Assert.AreEqual(3, kept.Network.VlanPriority);
        Assert.IsTrue(kept.Network.MultipathTcp);
        Assert.AreSame(options.Callbacks, kept.Callbacks);
    }

    [TestMethod]
    public void Of_OneFieldOfEachSecurityGroup_MakesOneCallEachInGroupOrder()
    {
        EasyOptions options = new()
        {
            Tls = new EasyTlsOptions
            {
                Origin = new TlsPeerOptions { VerifyPeer = false, VerifyHost = false, SslVersion = SslVersion.Tls12, SslVersionMaximum = SslVersionMaximum.Tls13 },
                Proxy = new TlsPeerOptions { CertificateAuthorityFile = "proxy.pem" },
                DohVerifyHost = false,
            },
            Authentication = new EasyAuthenticationOptions { AwsSigV4 = "aws:amz", HttpAuthentication = HttpAuthenticationMethods.AwsSigV4 | HttpAuthenticationMethods.Digest },
            Proxy = new EasyProxyOptions { NoProxy = "example", ProxyHeaders = ["X: 1"], HaProxyClientIp = "1.2.3.4" },
        };

        IReadOnlyList<EasySetopt> setopts = EasySetoptsFromCommandLine.Of(options);

        CollectionAssert.AreEqual(
            new[]
            {
                new EasySetopt(CurlOption.Sslversion, (long)SslVersion.Tls12 | (long)SslVersionMaximum.Tls13),
                new EasySetopt(CurlOption.SslVerifypeer, 0L),
                new EasySetopt(CurlOption.SslVerifyhost, 0L),
                new EasySetopt(CurlOption.ProxyCainfo, "proxy.pem"),
                new EasySetopt(CurlOption.DohSslVerifyhost, 0L),
                new EasySetopt(CurlOption.AwsSigv4, "aws:amz"),
                new EasySetopt(CurlOption.Httpauth, (long)(HttpAuthenticationMethods.AwsSigV4 | HttpAuthenticationMethods.Digest)),
                new EasySetopt(CurlOption.Noproxy, "example"),
            },
            setopts.Where(setopt => setopt.Option is not (CurlOption.Proxyheader or CurlOption.HaproxyClientIp)).ToArray());
        CollectionAssert.AreEqual(new[] { "X: 1" }, (string[])setopts.Single(setopt => setopt.Option == CurlOption.Proxyheader).Value!);
        Assert.AreEqual(new EasySetopt(CurlOption.HaproxyClientIp, "1.2.3.4"), setopts[^1]);
    }

    [TestMethod]
    public void Of_EchModeAndValues_SetsThemAsCurlsToolDoes()
    {
        EasyOptions options = new() { Tls = new EasyTlsOptions { EncryptedClientHello = "hard", EncryptedClientHelloPublicName = "pn.example", EncryptedClientHelloConfigList = "AAAA" } };

        IReadOnlyList<EasySetopt> setopts = EasySetoptsFromCommandLine.Of(options);
        EasyOptions kept = EasySetoptsFromCommandLine.KeptOnHooks(options);

        CollectionAssert.AreEqual(
            new[] { new EasySetopt(CurlOption.Ech, "hard"), new EasySetopt(CurlOption.Ech, "pn:pn.example"), new EasySetopt(CurlOption.Ech, "ecl:AAAA") },
            setopts.ToArray());
        Assert.IsNull(kept.Tls.EncryptedClientHello);
        Assert.IsNull(kept.Tls.EncryptedClientHelloPublicName);
        Assert.IsNull(kept.Tls.EncryptedClientHelloConfigList);
    }

    [TestMethod]
    public void Of_EchTextTheSetterWouldIgnore_LeavesItOnTheHooks()
    {
        EasyOptions options = new() { Tls = new EasyTlsOptions { EncryptedClientHello = "maybe", EncryptedClientHelloPublicName = "p", EncryptedClientHelloConfigList = "A" } };

        IReadOnlyList<EasySetopt> setopts = EasySetoptsFromCommandLine.Of(options);
        EasyOptions kept = EasySetoptsFromCommandLine.KeptOnHooks(options);

        Assert.IsEmpty(setopts);
        Assert.AreEqual("maybe", kept.Tls.EncryptedClientHello);
        Assert.AreEqual("p", kept.Tls.EncryptedClientHelloPublicName);
        Assert.AreEqual("A", kept.Tls.EncryptedClientHelloConfigList);
    }

    [TestMethod]
    public void KeptOnHooks_SecurityFieldsWithoutAKeptValue_StayOnTheHooksAndTheRestAreLibcurlDefaults()
    {
        EasyOptions options = new()
        {
            Tls = new EasyTlsOptions { Engine = "pkcs11", Origin = new TlsPeerOptions { VerifyPeer = false } },
            Authentication = new EasyAuthenticationOptions { UserPassword = "u:p", HttpSignatureKey = "key.pem", TlsAuthType = "SRP" },
            Proxy = new EasyProxyOptions { Proxy = "http://proxy:3128" },
        };

        EasyOptions kept = EasySetoptsFromCommandLine.KeptOnHooks(options);

        Assert.AreEqual("pkcs11", kept.Tls.Engine);
        Assert.IsTrue(kept.Tls.Origin.VerifyPeer);
        Assert.AreEqual("key.pem", kept.Authentication.HttpSignatureKey);
        Assert.AreEqual("SRP", kept.Authentication.TlsAuthType);
        Assert.IsNull(kept.Authentication.UserPassword);
        Assert.IsNull(kept.Proxy.Proxy);
        CollectionAssert.DoesNotContain(EasySetoptsFromCommandLine.Of(options).Select(setopt => setopt.Option).ToArray(), CurlOption.Sslengine);
    }

    [TestMethod]
    public void Of_HttpAndProtocolFields_MakesOneCallEachInGroupOrderAndOneCookieFileCallPerFile()
    {
        byte[] body = [1, 2, 3];
        EasyOptions options = new()
        {
            Http = new EasyHttpOptions
            {
                UserAgent = "agent/1",
                PostFields = body,
                PostFieldSize = 3,
                Expect100Timeout = TimeSpan.FromSeconds(2),
                SslEnableAlpn = false,
                CookieFiles = ["a.txt", "b.txt"],
            },
            Protocols = new EasyProtocolOptions
            {
                Ftp = new EasyFtpOptions { UsePret = true },
                Tftp = new EasyTftpOptions { BlockSize = 1024 },
                Telnet = new EasyTelnetOptions { TelnetOptions = ["TTYPE=vt100"] },
            },
        };

        IReadOnlyList<EasySetopt> setopts = EasySetoptsFromCommandLine.Of(options);

        CollectionAssert.AreEqual(
            new[]
            {
                CurlOption.Useragent, CurlOption.Postfields, CurlOption.PostfieldsizeLarge, CurlOption.Expect100TimeoutMs, CurlOption.SslEnableAlpn,
                CurlOption.Cookiefile, CurlOption.Cookiefile, CurlOption.FtpUsePret, CurlOption.TftpBlksize, CurlOption.Telnetoptions,
            },
            setopts.Select(setopt => setopt.Option).ToArray());
        Assert.AreEqual("agent/1", setopts[0].Value);
        CollectionAssert.AreEqual(body, ((ReadOnlyMemory<byte>)setopts[1].Value!).ToArray());
        Assert.AreEqual(3L, setopts[2].Value);
        Assert.AreEqual(2000L, setopts[3].Value);
        Assert.AreEqual(0L, setopts[4].Value);
        Assert.AreEqual("a.txt", setopts[5].Value);
        Assert.AreEqual("b.txt", setopts[6].Value);
        Assert.AreEqual(1L, setopts[7].Value);
        Assert.AreEqual(1024L, setopts[8].Value);
        CollectionAssert.AreEqual(new[] { "TTYPE=vt100" }, (string[])setopts[9].Value!);
    }

    [TestMethod]
    public void Of_HttpAndProtocolValuesTheSettersWouldRefuseOrHold_LeavesThemOnTheHooks()
    {
        TimeSpan fraction = TimeSpan.FromTicks(15_000);
        EasyOptions options = new()
        {
            Http = new EasyHttpOptions { MaxRedirects = -2, PostFieldSize = -2, Expect100Timeout = fraction, StreamWeight = 0 },
            Protocols = new EasyProtocolOptions
            {
                Ssh = new EasySshOptions { HostPublicKeyMd5 = "abc" },
                Tftp = new EasyTftpOptions { BlockSize = 4 },
            },
        };

        IReadOnlyList<EasySetopt> setopts = EasySetoptsFromCommandLine.Of(options);
        EasyOptions kept = EasySetoptsFromCommandLine.KeptOnHooks(options);

        Assert.IsEmpty(setopts);
        Assert.AreEqual(-2, kept.Http.MaxRedirects);
        Assert.AreEqual(-2, kept.Http.PostFieldSize);
        Assert.AreEqual(fraction, kept.Http.Expect100Timeout);
        Assert.AreEqual(0, kept.Http.StreamWeight);
        Assert.AreEqual("abc", kept.Protocols.Ssh.HostPublicKeyMd5);
        Assert.AreEqual(4, kept.Protocols.Tftp.BlockSize);
    }

    [TestMethod]
    public void KeptOnHooks_HttpFieldsTheToolDoesNotSetAndTheForm_StayOnTheHooksAndTheRestAreLibcurlDefaults()
    {
        EasyMimeBody form = new() { Parts = [new EasyMimePart { Name = "a" }] };
        EasyStreamDependency dependency = new(new object(), Exclusive: true);
        string md5 = new('a', 32);
        EasyOptions options = new()
        {
            Http = new EasyHttpOptions
            {
                MimePost = form,
                HttpPost = form,
                StreamDependency = dependency,
                CookieList = ["ALL"],
                UserAgent = "agent/1",
                MaxRedirects = 5,
                StreamWeight = 32,
            },
            Protocols = new EasyProtocolOptions
            {
                Ssh = new EasySshOptions { HostPublicKeyMd5 = md5 },
                Tftp = new EasyTftpOptions { BlockSize = 1024 },
            },
        };

        EasyOptions kept = EasySetoptsFromCommandLine.KeptOnHooks(options);
        IReadOnlyList<EasySetopt> setopts = EasySetoptsFromCommandLine.Of(options);

        Assert.AreSame(form, kept.Http.MimePost);
        Assert.AreSame(form, kept.Http.HttpPost);
        Assert.AreSame(dependency, kept.Http.StreamDependency);
        CollectionAssert.AreEqual(new[] { "ALL" }, kept.Http.CookieList.ToArray());
        Assert.IsNull(kept.Http.UserAgent);
        Assert.AreEqual(30, kept.Http.MaxRedirects);
        Assert.AreEqual(16, kept.Http.StreamWeight);
        Assert.IsNull(kept.Protocols.Ssh.HostPublicKeyMd5);
        Assert.AreEqual(512, kept.Protocols.Tftp.BlockSize);
        CollectionAssert.AreEqual(
            new[] { CurlOption.Useragent, CurlOption.Maxredirs, CurlOption.StreamWeight, CurlOption.SshHostPublicKeyMd5, CurlOption.TftpBlksize },
            setopts.Select(setopt => setopt.Option).ToArray());
    }
}
