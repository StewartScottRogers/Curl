namespace Curl.Extension.DeltaDownload.UnitTests;

[TestClass]
public sealed class WholeResourceGetTests
{
    private static readonly CurlUrl Url = CurlUrl.Parse("http://example.com/file.bin");

    [TestMethod]
    public void Is_APlainHttpGet_IsOne()
    {
        Assert.IsTrue(WholeResourceGet.Is(new TransferContext { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions() }));
    }

    [TestMethod]
    [DataRow("not-http")]
    [DataRow("method")]
    [DataRow("http-body")]
    [DataRow("post")]
    [DataRow("post-without-data")]
    [DataRow("upload")]
    [DataRow("head")]
    [DataRow("range")]
    [DataRow("resume")]
    [DataRow("time-condition")]
    public void Is_AnythingElse_IsNotOne_Rfc9110Section14_2(string change)
    {
        var plain = new TransferContext { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions() };
        var context = change switch
        {
            "not-http" => new TransferContext { Url = Url, Output = Stream.Null },
            "method" => new TransferContext { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions { CustomMethod = "PUT" } },
            "http-body" => new TransferContext { Url = Url, Output = Stream.Null, Http = new HttpRequestOptions { Body = new BytesBody(new byte[1], "text/plain") } },
            "post" => new TransferContext { Url = Url, Output = Stream.Null, Http = plain.Http, PostData = new byte[1] },
            "post-without-data" => new TransferContext { Url = Url, Output = Stream.Null, Http = plain.Http, PostWithoutData = true },
            "upload" => new TransferContext { Url = Url, Output = Stream.Null, Http = plain.Http, Upload = Stream.Null },
            "head" => new TransferContext { Url = Url, Output = Stream.Null, Http = plain.Http, NoBody = true },
            "range" => new TransferContext { Url = Url, Output = Stream.Null, Http = plain.Http, Range = ByteRange.Bounded(0, 1) },
            "resume" => new TransferContext { Url = Url, Output = Stream.Null, Http = plain.Http, ResumeFrom = 1 },
            _ => new TransferContext { Url = Url, Output = Stream.Null, Http = plain.Http, TimeCondition = new TimeCondition(DateTimeOffset.UnixEpoch, TimeConditionKind.IfUnmodifiedSince) },
        };

        Assert.IsFalse(WholeResourceGet.Is(context));
    }
}
