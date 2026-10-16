using Curl.Api.Info;

namespace Curl.Mcp;

[TestClass]
public sealed class McpTransferInfoTests
{
    [TestMethod]
    public void From_Info_CopiesEveryValueWithTimesInSeconds()
    {
        TransferInfo info = new()
        {
            TotalTime = TimeSpan.FromMilliseconds(1500),
            NameLookupTime = TimeSpan.FromMilliseconds(10),
            ConnectTime = TimeSpan.FromMilliseconds(20),
            AppConnectTime = TimeSpan.FromMilliseconds(30),
            StartTransferTime = TimeSpan.FromMilliseconds(40),
            EffectiveUrl = "https://h/",
            EffectiveMethod = "POST",
            RedirectCount = 2,
            ContentType = "text/plain",
            SizeDownload = 100,
            SizeUpload = 5,
            HeaderSize = 70,
            RequestSize = 80,
            SpeedDownload = 1000,
            SpeedUpload = 50,
            HttpVersion = 3,
            PrimaryIp = "::1",
            PrimaryPort = 443,
        };

        McpTransferInfo named = McpTransferInfo.From(info);

        Assert.AreEqual(
            new McpTransferInfo(1.5, 0.01, 0.02, 0.03, 0.04, "https://h/", "POST", 2, "text/plain", 100, 5, 70, 80, 1000, 50, "2", "::1", 443),
            named);
    }

    [TestMethod]
    [DataRow(0L, null)]
    [DataRow(1L, "1.0")]
    [DataRow(2L, "1.1")]
    [DataRow(3L, "2")]
    [DataRow(30L, "3")]
    [DataRow(4L, null)]
    public void From_HttpVersion_IsTheVersionNumber(long version, string? text)
    {
        Assert.AreEqual(text, McpTransferInfo.From(new TransferInfo { HttpVersion = version }).HttpVersion);
    }

    [TestMethod]
    public void From_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => McpTransferInfo.From(null!));
    }
}
