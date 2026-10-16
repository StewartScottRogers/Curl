using Curl.Api.Codes;
using Curl.Engine;
using Curl.Protocol.Abstractions;
using Libcurl.Options;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="EasyHandleWriteOutInfo" /> (ADR-0625): the <c>-w</c> fields come from the handle's
/// <c>curl_easy_getinfo</c> answers, and they describe a result only when it carries the report of the handle's
/// last perform.
/// </summary>
[TestClass]
public sealed class EasyHandleWriteOutInfoTests
{
    [TestMethod]
    public async Task Read_AfterAFilePerform_GivesTheHandlesAnswers()
    {
        string directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            string path = Path.Combine(directory, "body.txt");
            await File.WriteAllTextAsync(path, "hello");
            ToolEasyHandle easy = new ToolCurlEngine(new ManagedCurlEngine()).EasyInit();
            await using (easy.ConfigureAwait(false))
            {
                Assert.AreEqual(CurlCode.Ok, easy.Easy.SetOption(CurlOption.Url, new Uri(path).AbsoluteUri));
                Assert.AreEqual(CurlCode.Ok, await easy.Easy.PerformAsync());

                EasyHandleWriteOutInfo read = EasyHandleWriteOutInfo.Read(easy.Easy, easy.Hooks.LastResult);

                Api.Info.TransferInfo info = easy.Hooks.Info;
                Assert.AreEqual(5, read.Info.SizeDownload);
                Assert.AreEqual(info.EffectiveUrl, read.Info.EffectiveUrl);
                Assert.AreEqual(info.EffectiveMethod, read.Info.EffectiveMethod);
                Assert.AreEqual(info.TotalTime, read.Info.TotalTime);
                Assert.AreEqual(info.SpeedDownload, read.Info.SpeedDownload);
                Assert.AreEqual(info.PrimaryIp, read.Info.PrimaryIp);
                Assert.AreEqual(info.ContentType, read.Info.ContentType);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public async Task Read_WithNoPerformedResult_HasNoReportAndAFreshHandlesAnswers()
    {
        ToolEasyHandle easy = new ToolCurlEngine(new ManagedCurlEngine()).EasyInit();
        await using (easy.ConfigureAwait(false))
        {
            EasyHandleWriteOutInfo read = EasyHandleWriteOutInfo.Read(easy.Easy, null);

            Assert.IsNull(read.PerformedReport);
            Assert.AreEqual(string.Empty, read.Info.EffectiveUrl);
            Assert.AreEqual(0, read.Info.ResponseCode);
        }
    }

    [TestMethod]
    public void Describes_TheLastPerformsReport_IsTrue()
    {
        TransferReport report = new();
        EasyHandleWriteOutInfo read = new(new Api.Info.TransferInfo(), report);

        Assert.IsTrue(read.Describes(new TransferResult(CurlExitCode.WriteError, 0, "rewritten") { Report = report }));
    }

    [TestMethod]
    public void Describes_AnotherReport_IsFalse()
    {
        EasyHandleWriteOutInfo read = new(new Api.Info.TransferInfo(), new TransferReport());

        Assert.IsFalse(read.Describes(new TransferResult(CurlExitCode.Ok, 0) { Report = new TransferReport() }));
    }

    [TestMethod]
    public void Describes_WhenTheHandleReportedNone_IsFalse()
    {
        EasyHandleWriteOutInfo read = new(new Api.Info.TransferInfo(), null);

        Assert.IsFalse(read.Describes(new TransferResult(CurlExitCode.Ok, 0)));
    }
}
