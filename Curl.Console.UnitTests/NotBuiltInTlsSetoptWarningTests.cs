using Curl.Api.Codes;
using Curl.Protocol.Abstractions;
using Libcurl.Options;

namespace Curl.Console;

/// <summary>
/// Pins that <see cref="ToolEasyHandle.SetOptions(IEnumerable{EasySetopt}, Func{EasySetopt, CurlCode})" /> carries on
/// past a <c>CURLE_NOT_BUILT_IN</c> answer to <c>CURLOPT_CAPATH</c>, <c>CURLOPT_PROXY_CAPATH</c>,
/// <c>CURLOPT_TLS13_CIPHERS</c>, <c>CURLOPT_PROXY_TLS13_CIPHERS</c>, <c>CURLOPT_CRLFILE</c>,
/// <c>CURLOPT_PROXY_CRLFILE</c>, <c>CURLOPT_SSL_EC_CURVES</c>, <c>CURLOPT_SSL_SIGNATURE_ALGORITHMS</c> and
/// <c>CURLOPT_TCP_FASTOPEN</c>, as curl 8.22.0's <c>config2setopts</c> does, and still stops at any other refusal
/// (BL-4029, ADR-0975, BL-4182, ADR-1036).
/// </summary>
[TestClass]
public sealed class NotBuiltInTlsSetoptWarningTests
{
    [TestMethod]
    [DataRow(CurlOption.Capath)]
    [DataRow(CurlOption.ProxyCapath)]
    [DataRow(CurlOption.Tls13Ciphers)]
    [DataRow(CurlOption.ProxyTls13Ciphers)]
    [DataRow(CurlOption.Crlfile)]
    [DataRow(CurlOption.ProxyCrlfile)]
    [DataRow(CurlOption.SslEcCurves)]
    [DataRow(CurlOption.SslSignatureAlgorithms)]
    [DataRow(CurlOption.TcpFastopen)]
    public void SetOptions_NotBuiltInForAnIgnoredTlsOption_CarriesOnWithTheCallsAfterIt(CurlOption option)
    {
        List<CurlOption> made = [];

        TransferResult? failure = ToolEasyHandle.SetOptions(
            [new EasySetopt(option, "x"), new EasySetopt(CurlOption.Verbose, 1L)],
            s =>
            {
                made.Add(s.Option);
                return s.Option == option ? CurlCode.NotBuiltIn : CurlCode.Ok;
            });

        Assert.IsNull(failure);
        CollectionAssert.AreEqual(new[] { option, CurlOption.Verbose }, made);
    }

    [TestMethod]
    public void SetOptions_NotBuiltInForAnotherOption_FailsTheTransferWithItsCode()
    {
        TransferResult? failure = ToolEasyHandle.SetOptions(
            [new EasySetopt(CurlOption.Sslengine, "x"), new EasySetopt(CurlOption.Verbose, 1L)],
            s => s.Option == CurlOption.Sslengine ? CurlCode.NotBuiltIn : CurlCode.Ok);

        Assert.IsNotNull(failure);
        Assert.AreEqual(CurlExitCode.NotBuiltIn, failure.ExitCode);
        Assert.AreEqual($"setopt 0x{(int)CurlOption.Sslengine:x} got bad argument", failure.ErrorMessage);
    }

    [TestMethod]
    public void SetOptions_OtherRefusalOfAnIgnoredTlsOption_FailsTheTransferWithItsCode()
    {
        TransferResult? failure = ToolEasyHandle.SetOptions(
            [new EasySetopt(CurlOption.Tls13Ciphers, 1L)],
            _ => CurlCode.BadFunctionArgument);

        Assert.IsNotNull(failure);
        Assert.AreEqual(CurlExitCode.BadFunctionArgument, failure.ExitCode);
    }

    [TestMethod]
    public void SetOptions_EveryCallAnswersOk_AnswersNull()
    {
        TransferResult? failure = ToolEasyHandle.SetOptions([new EasySetopt(CurlOption.Capath, ".")], _ => CurlCode.Ok);

        Assert.IsNull(failure);
    }
}
