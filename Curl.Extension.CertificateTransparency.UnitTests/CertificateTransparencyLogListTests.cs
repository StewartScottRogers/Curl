using System.Text;
using System.Text.Json;

namespace Curl.Extension.CertificateTransparency.UnitTests;

[TestClass]
public sealed class CertificateTransparencyLogListTests
{
    [TestMethod]
    public void KeysByLogId_Rfc6962Section3_2_FilesEachKeyUnderTheSha256OfIt()
    {
        byte[] key = [1, 2, 3];

        var keys = CertificateTransparencyLogList.KeysByLogId(CertificateTransparencyChain.LogListOf(key));

        CollectionAssert.AreEqual(key, keys[Convert.ToHexString(CertificateTransparencyChain.LogIdOf(key))]);
    }

    [TestMethod]
    public void KeysByLogId_EntriesWithoutOperatorsLogsOrAKeyString_AreSkipped()
    {
        var json = "{\"operators\":[{\"name\":\"no logs\"},{\"logs\":{}},{\"logs\":[{\"description\":\"no key\"},{\"key\":5},7]},3]}";

        Assert.IsEmpty(CertificateTransparencyLogList.KeysByLogId(Encoding.UTF8.GetBytes(json)));
        Assert.IsEmpty(CertificateTransparencyLogList.KeysByLogId(Encoding.UTF8.GetBytes("[]")));
        Assert.IsEmpty(CertificateTransparencyLogList.KeysByLogId(Encoding.UTF8.GetBytes("{\"operators\":1}")));
    }

    [TestMethod]
    public void KeysByLogId_NotJsonOrNotBase64_Throws()
    {
        Assert.Throws<JsonException>(() => CertificateTransparencyLogList.KeysByLogId(Encoding.UTF8.GetBytes("{")));
        Assert.ThrowsExactly<FormatException>(() => CertificateTransparencyLogList.KeysByLogId(Encoding.UTF8.GetBytes("{\"operators\":[{\"logs\":[{\"key\":\"!\"}]}]}")));
    }
}
