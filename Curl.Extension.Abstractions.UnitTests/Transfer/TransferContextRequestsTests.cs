namespace Curl.Extension.Abstractions.Transfer;

[TestClass]
public sealed class TransferContextRequestsTests
{
    [TestMethod]
    public void WithRequest_UrlMethodAndBody_GivesThemInPlaceOfItsOwn()
    {
        TransferContext own = new() { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream(), PostData = new byte[] { 1 }, Http = new HttpRequestOptions { Headers = ["X-Curl: own"], CustomMethod = "PUT" } };
        CurlUrl url = CurlUrl.Parse("http://example.com/dav/");

        ITransferContext given = own.WithRequest(url, "PROPFIND", new byte[] { 2, 3 });

        Assert.AreSame(url, given.Url);
        Assert.AreEqual("PROPFIND", given.Http!.CustomMethod);
        CollectionAssert.AreEqual(new[] { "X-Curl: own" }, given.Http.Headers.ToArray());
        CollectionAssert.AreEqual(new byte[] { 2, 3 }, given.PostData!.Value.ToArray());
        Assert.AreEqual("PUT", own.Http.CustomMethod);
    }

    [TestMethod]
    public void WithRequest_Body_IsSentAsTheHttpBodyAndPostData()
    {
        TransferContext own = new() { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream(), Http = new HttpRequestOptions() };

        ITransferContext given = own.WithRequest(CurlUrl.Parse("http://example.com/dav/"), "PROPFIND", new byte[] { 2, 3 });

        BytesBody body = (BytesBody)given.Http!.Body!;
        CollectionAssert.AreEqual(new byte[] { 2, 3 }, body.Content.ToArray());
        Assert.AreEqual("application/x-www-form-urlencoded", body.ContentType);
        CollectionAssert.AreEqual(new byte[] { 2, 3 }, given.PostData!.Value.ToArray());
        Assert.IsNull(own.Http.Body);
    }

    [TestMethod]
    public void WithRequest_NoBody_KeepsTheHttpBodyItHas()
    {
        BytesBody ownBody = new(new byte[] { 9 }, "text/plain");
        TransferContext own = new() { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream(), Http = new HttpRequestOptions { Body = ownBody } };

        ITransferContext given = own.WithRequest(CurlUrl.Parse("http://example.com/other"), "GET", null);

        Assert.AreSame(ownBody, given.Http!.Body);
    }

    [TestMethod]
    public void WithRequest_NoMethodOrBody_KeepsItsOwn()
    {
        TransferContext own = new() { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream(), PostData = new byte[] { 1 }, Http = new HttpRequestOptions { CustomMethod = "PUT" } };

        ITransferContext given = own.WithRequest(CurlUrl.Parse("http://example.com/other"), null, null);

        Assert.AreSame(own.Http, given.Http);
        CollectionAssert.AreEqual(new byte[] { 1 }, given.PostData!.Value.ToArray());
    }

    [TestMethod]
    public void WithRequest_TransferThatIsNotHttp_HasNoHttpOptions()
    {
        TransferContext own = new() { Url = CurlUrl.Parse("ftp://example.com/"), Output = new MemoryStream() };

        ITransferContext given = own.WithRequest(CurlUrl.Parse("ftp://example.com/b"), "LIST", null);

        Assert.IsNull(given.Http);
        Assert.IsNull(given.PostData);
    }

    [TestMethod]
    public void WithRequest_WithoutAContextOrUrl_Throws()
    {
        TransferContext own = new() { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream() };

        Assert.ThrowsExactly<ArgumentNullException>(() => TransferContextRequests.WithRequest(null!, own.Url, null, null));
        Assert.ThrowsExactly<ArgumentNullException>(() => own.WithRequest(null!, null, null));
    }

    [TestMethod]
    public void WithRequest_EveryOtherMember_IsTheTransfersOwn()
    {
        TransferContext context = new() { Url = CurlUrl.Parse("http://example.com/"), Output = new MemoryStream(), HeaderOutput = new MemoryStream(), DumpHeaderOutput = new MemoryStream() };
        ITransferContext own = context;

        ITransferContext given = own.WithRequest(CurlUrl.Parse("http://example.com/other"), null, null);

        Assert.AreSame(own.Output, given.Output);
        Assert.AreEqual(own.Upload, given.Upload);
        Assert.AreEqual(own.ResumeFrom, given.ResumeFrom);
        Assert.AreEqual(own.ResumeUploadFromUnknownOffset, given.ResumeUploadFromUnknownOffset);
        Assert.AreEqual(own.Range, given.Range);
        Assert.AreEqual(own.RangeText, given.RangeText);
        Assert.AreEqual(own.MaxFileSize, given.MaxFileSize);
        Assert.AreEqual(own.NoBody, given.NoBody);
        Assert.AreEqual(own.TimeCondition, given.TimeCondition);
        Assert.AreEqual(own.RemoteTime, given.RemoteTime);
        Assert.AreSame(own.HeaderOutput, given.HeaderOutput);
        Assert.AreSame(own.DumpHeaderOutput, given.DumpHeaderOutput);
        Assert.AreEqual(own.PostWithoutData, given.PostWithoutData);
        Assert.AreEqual(own.Credentials, given.Credentials);
        Assert.AreSame(own.TelnetOptions, given.TelnetOptions);
        Assert.AreEqual(own.TftpBlockSize, given.TftpBlockSize);
        Assert.AreEqual(own.TftpNoOptions, given.TftpNoOptions);
        Assert.AreEqual(own.FtpDisableEpsv, given.FtpDisableEpsv);
        Assert.AreEqual(own.FtpSkipPasvIp, given.FtpSkipPasvIp);
        Assert.AreEqual(own.FtpFileMethod, given.FtpFileMethod);
        Assert.AreEqual(own.FtpCreateDirectories, given.FtpCreateDirectories);
        Assert.AreEqual(own.FtpAccount, given.FtpAccount);
        Assert.AreEqual(own.FtpAlternativeToUser, given.FtpAlternativeToUser);
        Assert.AreEqual(own.FtpSendPret, given.FtpSendPret);
        Assert.AreEqual(own.ListOnly, given.ListOnly);
        Assert.AreEqual(own.UseAscii, given.UseAscii);
        Assert.AreEqual(own.Append, given.Append);
        Assert.AreEqual(own.FtpPort, given.FtpPort);
        Assert.AreEqual(own.FtpUseEprt, given.FtpUseEprt);
        Assert.AreEqual(own.FtpAcceptTimeout, given.FtpAcceptTimeout);
        Assert.AreEqual(own.FtpServerResponseTimeout, given.FtpServerResponseTimeout);
        Assert.AreEqual(own.SslLevel, given.SslLevel);
        Assert.AreEqual(own.FtpSslControlOnly, given.FtpSslControlOnly);
        Assert.AreEqual(own.FtpCommandChannelClearing, given.FtpCommandChannelClearing);
        Assert.AreSame(own.QuoteCommands, given.QuoteCommands);
        Assert.AreEqual(own.ConvertLineEndings, given.ConvertLineEndings);
        Assert.AreEqual(own.CreateFileMode, given.CreateFileMode);
        Assert.AreEqual(own.PathAsIs, given.PathAsIs);
        Assert.AreEqual(own.ConnectTimeout, given.ConnectTimeout);
        Assert.AreEqual(own.MaxTime, given.MaxTime);
        Assert.AreEqual(own.OperationStarted, given.OperationStarted);
        Assert.AreEqual(own.Proxy, given.Proxy);
        Assert.AreEqual(own.Mail, given.Mail);
        Assert.AreEqual(own.Ssh, given.Ssh);
        Assert.AreEqual(own.TimeProvider, given.TimeProvider);
        Assert.AreEqual(own.Events, given.Events);
        Assert.AreEqual(own.Progress, given.Progress);
        Assert.AreEqual(own.CancellationToken, given.CancellationToken);
        Assert.AreSame(own.DiagnosticLog, given.DiagnosticLog);
    }
}
