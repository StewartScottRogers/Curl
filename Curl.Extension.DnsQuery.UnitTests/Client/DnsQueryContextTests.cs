using Curl.Extension.DnsQuery.Client;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.DnsQuery.UnitTests.Client;

/// <summary>Checks that the context <c>--dns-transport</c> and <c>--dns-json</c> mark a transfer with gives their choice and every member of the transfer's own.</summary>
[TestClass]
public sealed class DnsQueryContextTests
{
    [TestMethod]
    public void Members_OfAMarkedTransfer_AreTheTransfersOwn()
    {
        using var cancellation = new CancellationTokenSource();
        var inner = new TransferContext
        {
            Url = CurlUrl.Parse("dns://h/example.com"),
            Output = new MemoryStream(),
            HeaderOutput = new MemoryStream(),
            DumpHeaderOutput = new MemoryStream(),
            MaxTime = TimeSpan.FromSeconds(9),
            PostData = new byte[] { 1 },
            TimeProvider = TimeProvider.System,
            CancellationToken = cancellation.Token,
        };

        ITransferContext observed = new DnsQueryContext(inner, "tcp", json: true);

        Assert.AreSame(inner.Output, observed.Output);
        Assert.AreSame(inner.HeaderOutput, observed.HeaderOutput);
        Assert.AreSame(inner.DumpHeaderOutput, observed.DumpHeaderOutput);
        Assert.AreSame(inner.Url, observed.Url);
        Assert.AreSame(inner.TimeProvider, observed.TimeProvider);
        Assert.AreSame(inner.Events, observed.Events);
        Assert.AreSame(inner.Progress, observed.Progress);
        Assert.AreSame(inner.DiagnosticLog, observed.DiagnosticLog);
        Assert.AreSame(inner.TelnetOptions, observed.TelnetOptions);
        Assert.AreSame(inner.QuoteCommands, observed.QuoteCommands);
        Assert.AreEqual(inner.CancellationToken, observed.CancellationToken);
        Assert.AreEqual(inner.MaxTime, observed.MaxTime);
        Assert.AreEqual(inner.PostData, observed.PostData);
        Assert.AreEqual(inner.Http, observed.Http);
        Assert.AreEqual(inner.Upload, observed.Upload);
        Assert.AreEqual(inner.ResumeFrom, observed.ResumeFrom);
        Assert.AreEqual(inner.ResumeUploadFromUnknownOffset, observed.ResumeUploadFromUnknownOffset);
        Assert.AreEqual(inner.Range, observed.Range);
        Assert.AreEqual(inner.RangeText, observed.RangeText);
        Assert.AreEqual(inner.MaxFileSize, observed.MaxFileSize);
        Assert.AreEqual(inner.NoBody, observed.NoBody);
        Assert.AreEqual(inner.TimeCondition, observed.TimeCondition);
        Assert.AreEqual(inner.RemoteTime, observed.RemoteTime);
        Assert.AreEqual(inner.PostWithoutData, observed.PostWithoutData);
        Assert.AreEqual(inner.Credentials, observed.Credentials);
        Assert.AreEqual(inner.TftpBlockSize, observed.TftpBlockSize);
        Assert.AreEqual(inner.TftpNoOptions, observed.TftpNoOptions);
        Assert.AreEqual(inner.FtpDisableEpsv, observed.FtpDisableEpsv);
        Assert.AreEqual(inner.FtpSkipPasvIp, observed.FtpSkipPasvIp);
        Assert.AreEqual(inner.FtpFileMethod, observed.FtpFileMethod);
        Assert.AreEqual(inner.FtpCreateDirectories, observed.FtpCreateDirectories);
        Assert.AreEqual(inner.FtpAccount, observed.FtpAccount);
        Assert.AreEqual(inner.FtpAlternativeToUser, observed.FtpAlternativeToUser);
        Assert.AreEqual(inner.FtpSendPret, observed.FtpSendPret);
        Assert.AreEqual(inner.ListOnly, observed.ListOnly);
        Assert.AreEqual(inner.UseAscii, observed.UseAscii);
        Assert.AreEqual(inner.Append, observed.Append);
        Assert.AreEqual(inner.FtpPort, observed.FtpPort);
        Assert.AreEqual(inner.FtpUseEprt, observed.FtpUseEprt);
        Assert.AreEqual(inner.FtpAcceptTimeout, observed.FtpAcceptTimeout);
        Assert.AreEqual(inner.FtpServerResponseTimeout, observed.FtpServerResponseTimeout);
        Assert.AreEqual(inner.SslLevel, observed.SslLevel);
        Assert.AreEqual(inner.FtpSslControlOnly, observed.FtpSslControlOnly);
        Assert.AreEqual(inner.FtpCommandChannelClearing, observed.FtpCommandChannelClearing);
        Assert.AreEqual(inner.ConvertLineEndings, observed.ConvertLineEndings);
        Assert.AreEqual(inner.CreateFileMode, observed.CreateFileMode);
        Assert.AreEqual(inner.PathAsIs, observed.PathAsIs);
        Assert.AreEqual(inner.ConnectTimeout, observed.ConnectTimeout);
        Assert.AreEqual(inner.OperationStarted, observed.OperationStarted);
        Assert.AreEqual(inner.Proxy, observed.Proxy);
        Assert.AreEqual(inner.Mail, observed.Mail);
        Assert.AreEqual(inner.Ssh, observed.Ssh);
    }
}
