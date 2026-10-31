namespace Curl.Extension.GraphQl;

[TestClass]
public sealed class GraphQlTransferContextTests
{
    [TestMethod]
    public void Members_AreTheTransfersOwnButOutputAndHttp()
    {
        TransferContext inner = new()
        {
            Url = CurlUrl.Parse("http://example.com/graphql"),
            Output = new MemoryStream(),
            HeaderOutput = new MemoryStream(),
            DumpHeaderOutput = new MemoryStream(),
            Http = new HttpRequestOptions(),
        };
        MemoryStream output = new();
        HttpRequestOptions http = new() { Headers = ["X: y"] };

        GraphQlTransferContext context = new(inner, output, http);

        Assert.AreSame(output, context.Output);
        Assert.AreSame(http, context.Http);
        Assert.AreSame(inner.Url, context.Url);
        Assert.AreSame(inner.Upload, context.Upload);
        Assert.AreEqual(inner.ResumeFrom, context.ResumeFrom);
        Assert.AreEqual(inner.ResumeUploadFromUnknownOffset, context.ResumeUploadFromUnknownOffset);
        Assert.AreEqual(inner.Range, context.Range);
        Assert.AreEqual(inner.RangeText, context.RangeText);
        Assert.AreEqual(inner.MaxFileSize, context.MaxFileSize);
        Assert.AreEqual(inner.NoBody, context.NoBody);
        Assert.AreEqual(inner.TimeCondition, context.TimeCondition);
        Assert.AreEqual(inner.RemoteTime, context.RemoteTime);
        Assert.AreSame(inner.HeaderOutput, context.HeaderOutput);
        Assert.AreSame(inner.DumpHeaderOutput, context.DumpHeaderOutput);
        Assert.AreEqual(inner.PostData.HasValue, context.PostData.HasValue);
        Assert.AreEqual(inner.PostWithoutData, context.PostWithoutData);
        Assert.AreSame(inner.Credentials, context.Credentials);
        Assert.AreSame(inner.TelnetOptions, context.TelnetOptions);
        Assert.AreEqual(inner.TftpBlockSize, context.TftpBlockSize);
        Assert.AreEqual(inner.TftpNoOptions, context.TftpNoOptions);
        Assert.AreEqual(inner.FtpDisableEpsv, context.FtpDisableEpsv);
        Assert.AreEqual(inner.FtpSkipPasvIp, context.FtpSkipPasvIp);
        Assert.AreEqual(inner.FtpFileMethod, context.FtpFileMethod);
        Assert.AreEqual(inner.FtpCreateDirectories, context.FtpCreateDirectories);
        Assert.AreEqual(inner.FtpAccount, context.FtpAccount);
        Assert.AreEqual(inner.FtpAlternativeToUser, context.FtpAlternativeToUser);
        Assert.AreEqual(inner.FtpSendPret, context.FtpSendPret);
        Assert.AreEqual(inner.ListOnly, context.ListOnly);
        Assert.AreEqual(inner.UseAscii, context.UseAscii);
        Assert.AreEqual(inner.Append, context.Append);
        Assert.AreEqual(inner.FtpPort, context.FtpPort);
        Assert.AreEqual(inner.FtpUseEprt, context.FtpUseEprt);
        Assert.AreEqual(inner.FtpAcceptTimeout, context.FtpAcceptTimeout);
        Assert.AreEqual(inner.FtpServerResponseTimeout, context.FtpServerResponseTimeout);
        Assert.AreEqual(inner.SslLevel, context.SslLevel);
        Assert.AreEqual(inner.FtpSslControlOnly, context.FtpSslControlOnly);
        Assert.AreEqual(inner.FtpCommandChannelClearing, context.FtpCommandChannelClearing);
        Assert.AreSame(inner.QuoteCommands, context.QuoteCommands);
        Assert.AreEqual(inner.ConvertLineEndings, context.ConvertLineEndings);
        Assert.AreEqual(inner.CreateFileMode, context.CreateFileMode);
        Assert.AreEqual(inner.PathAsIs, context.PathAsIs);
        Assert.AreEqual(inner.ConnectTimeout, context.ConnectTimeout);
        Assert.AreEqual(inner.MaxTime, context.MaxTime);
        Assert.AreEqual(inner.OperationStarted, context.OperationStarted);
        Assert.AreSame(inner.Proxy, context.Proxy);
        Assert.AreSame(inner.Mail, context.Mail);
        Assert.AreSame(inner.Ssh, context.Ssh);
        Assert.AreSame(inner.TimeProvider, context.TimeProvider);
        Assert.AreSame(inner.Events, context.Events);
        Assert.AreSame(inner.Progress, context.Progress);
        Assert.AreEqual(inner.CancellationToken, context.CancellationToken);
        Assert.AreSame(((ITransferContext)inner).DiagnosticLog, context.DiagnosticLog);
    }
}
