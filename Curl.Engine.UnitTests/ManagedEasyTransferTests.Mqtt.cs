using System.Text;
using Curl.Api.Options;
using Curl.Api.Options.Http;
using Curl.Api.Options.Network;

namespace Curl.Engine;

// CURLOPT_POSTFIELDS and CURLOPT_POST on an mqtt URL (BL-2673, upstream tests 1916 and 1917).
public sealed partial class ManagedEasyTransferTests
{
    private static EngineTransferSettings MqttSettingsOf(string url, EasyHttpOptions http) =>
        ManagedEasyTransfer.TransferSettingsOf(new EasyOptions { Network = new EasyNetworkOptions { Url = url }, Http = http });

    [TestMethod]
    public void TransferSettingsOf_EmptyPostFieldsOnAnMqttUrl_PublishesAnEmptyPayload()
    {
        EngineTransferSettings settings = MqttSettingsOf("mqtt://h/%20", new EasyHttpOptions { PostFields = ReadOnlyMemory<byte>.Empty });

        Assert.AreEqual(0, settings.PostData?.Length);
        Assert.IsFalse(settings.PostWithoutData);
    }

    [TestMethod]
    public void TransferSettingsOf_PostFieldSizeShorterThanTheFieldsOnAnMqttsUrl_CutsThePayload()
    {
        EngineTransferSettings settings = MqttSettingsOf("MQTTS://h/t", new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("hello"), PostFieldSize = 2 });

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("he"), settings.PostData?.ToArray());
    }

    [TestMethod]
    public void TransferSettingsOf_PostFieldSizeLongerThanTheFieldsOnAnMqttUrl_KeepsTheWholePayload()
    {
        EngineTransferSettings settings = MqttSettingsOf("mqtt://h/t", new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("hi"), PostFieldSize = 9 });

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("hi"), settings.PostData?.ToArray());
    }

    [TestMethod]
    public void TransferSettingsOf_PostFieldsOnAnHttpUrl_LeavesPostDataToTheHttpHandler()
    {
        EngineTransferSettings settings = MqttSettingsOf("http://h/t", new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("a=1") });

        Assert.IsNull(settings.PostData);
    }

    [TestMethod]
    public void TransferSettingsOf_PostWithoutPostFieldsOnAnMqttUrl_IsPostWithoutData()
    {
        EngineTransferSettings settings = MqttSettingsOf("mqtt://h/%20", new EasyHttpOptions { Post = true });

        Assert.IsNull(settings.PostData);
        Assert.IsTrue(settings.PostWithoutData);
    }

    [TestMethod]
    public void TransferSettingsOf_PostWithoutPostFieldsOnAnHttpUrl_IsNotPostWithoutData()
    {
        Assert.IsFalse(MqttSettingsOf("http://h/p", new EasyHttpOptions { Post = true }).PostWithoutData);
    }

    [TestMethod]
    public void TransferSettingsOf_NoUrl_IsNotPostWithoutData()
    {
        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(new EasyOptions { Http = new EasyHttpOptions { Post = true } });

        Assert.IsFalse(settings.PostWithoutData);
    }
}
