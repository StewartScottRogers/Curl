using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
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
    [DataRow("mcp://h/t")]
    [DataRow("mcp+stdio://h/t")]
    [DataRow("webtransport://h/t")]
    [DataRow("COAP://h/t")]
    public void TransferSettingsOf_PostFieldsOnAnExtensionUrl_ReachTheHandlerAsPostData(string url)
    {
        EngineTransferSettings settings = MqttSettingsOf(url, new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("a=1") });

        Assert.AreEqual("a=1", Encoding.ASCII.GetString(settings.PostData!.Value.Span));
    }

    private static EngineTransferSettings ReadCallbackPostSettingsOf(string url, byte[] body, long size, bool post = true, long? overReport = null)
    {
        int offset = 0;
        long Read(Span<byte> buffer)
        {
            if (overReport is { } funny)
            {
                return funny;
            }

            int count = Math.Min(Math.Min(buffer.Length, 3), body.Length - offset);
            body.AsSpan(offset, count).CopyTo(buffer);
            offset += count;
            return count;
        }

        return ManagedEasyTransfer.TransferSettingsOf(new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = url },
            Http = new EasyHttpOptions { Post = post, PostFieldSize = size },
            Callbacks = new Curl.Api.Callbacks.EasyCallbackOptions { Read = Read },
        });
    }

    [TestMethod]
    [DataRow("mcp://h/")]
    [DataRow("mcps://h/")]
    [DataRow("mcp+stdio://localhost/server")]
    public void TransferSettingsOf_ReadCallbackPostOfUnknownSizeOnAnExtensionUrl_ReachesTheHandlerAsPostData(string url)
    {
        EngineTransferSettings settings = ReadCallbackPostSettingsOf(url, Encoding.ASCII.GetBytes("{\"name\":\"version\"}"), -1);

        Assert.AreEqual("{\"name\":\"version\"}", Encoding.ASCII.GetString(settings.PostData!.Value.Span));
    }

    [TestMethod]
    public void TransferSettingsOf_ReadCallbackPostOfKnownSizeOnAnExtensionUrl_ReadsNoMoreThanTheSize()
    {
        EngineTransferSettings settings = ReadCallbackPostSettingsOf("mcp://h/", Encoding.ASCII.GetBytes("abcdefgh"), 5);

        Assert.AreEqual("abcde", Encoding.ASCII.GetString(settings.PostData!.Value.Span));
    }

    [TestMethod]
    [DataRow(0x10000000L, CurlCode.AbortedByCallback, "operation aborted by callback")]
    [DataRow(0x10000001L, CurlCode.ReadError, "Read callback asked for PAUSE when not supported")]
    [DataRow(20000L, CurlCode.ReadError, "read function returned funny value")]
    [DataRow(-1L, CurlCode.ReadError, "read function returned funny value")]
    public void TransferSettingsOf_ReadCallbackAnsweringACodeOrFunnyValueOnAnExtensionUrl_FailsTheTransferAsLibcurlDoes(long answer, CurlCode code, string message)
    {
        ManagedEasyTransfer.ExtensionPostReadFailedException failure = Assert.ThrowsExactly<ManagedEasyTransfer.ExtensionPostReadFailedException>(
            () => ReadCallbackPostSettingsOf("mcp://h/", [], -1, overReport: answer));

        Assert.AreEqual(code, failure.Code);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    public async Task PerformAsync_ReadCallbackAbortingOnAnExtensionUrl_EndsWithAbortedByCallbackAndRunsNoHandler()
    {
        ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new ScriptedConnector([])).CreateEasy();
        await using IEasyTransfer handle = easy;
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "mcp://h/" },
            Http = new EasyHttpOptions { Post = true },
            Callbacks = new Curl.Api.Callbacks.EasyCallbackOptions { Read = _ => 0x10000000 },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.AbortedByCallback, code);
        Assert.AreEqual("operation aborted by callback", easy.ErrorText);
    }
    [TestMethod]
    [DataRow("ftp://h/t", true)]
    [DataRow("mcp://h/", false)]
    public void TransferSettingsOf_ReadCallbackOnACurlSchemeOrWithoutPost_LeavesNoPostData(string url, bool post)
    {
        EngineTransferSettings settings = ReadCallbackPostSettingsOf(url, Encoding.ASCII.GetBytes("a=1"), -1, post);

        Assert.IsNull(settings.PostData);
    }

    [TestMethod]
    public void TransferSettingsOf_PostWithoutFieldsOrReadCallbackOnAnExtensionUrl_LeavesNoPostData()
    {
        EngineTransferSettings settings = MqttSettingsOf("mcp://h/", new EasyHttpOptions { Post = true });

        Assert.IsNull(settings.PostData);
    }

    [TestMethod]
    [DataRow("ftp://h/t")]
    [DataRow("no-scheme")]
    public void TransferSettingsOf_PostFieldsOnACurlSchemeOrNoScheme_LeaveNoPostData(string url)
    {
        EngineTransferSettings settings = MqttSettingsOf(url, new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("a=1") });

        Assert.IsNull(settings.PostData);
    }
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
    public void TransferSettingsOf_PostFieldSizeZeroWithFieldsOnAnMqttUrl_PublishesAnEmptyPayload()
    {
        EngineTransferSettings settings = MqttSettingsOf("mqtt://h/t", new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("hello"), PostFieldSize = 0 });

        Assert.AreEqual(0, settings.PostData?.Length);
    }

    [TestMethod]
    public void TransferSettingsOf_PostFieldSizeLongerThanTheFieldsOnAnMqttUrl_KeepsTheWholePayload()
    {
        EngineTransferSettings settings = MqttSettingsOf("mqtt://h/t", new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("hi"), PostFieldSize = 9 });

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("hi"), settings.PostData?.ToArray());
    }

    [TestMethod]
    [DataRow("mqtt5://h/t")]
    [DataRow("mqtt5s://h/t")]
    [DataRow("mqtt5+ws://h/t")]
    public void TransferSettingsOf_PostFieldsOnAnMqtt5Url_PublishesThem(string url)
    {
        EngineTransferSettings settings = MqttSettingsOf(url, new EasyHttpOptions { PostFields = Encoding.ASCII.GetBytes("21.5") });

        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("21.5"), settings.PostData?.ToArray());
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

public sealed partial class ManagedEasyTransferTests
{
    [TestMethod]
    [DataRow("localhost:8080/x")]
    [DataRow("user:pw@host/")]
    [DataRow("[::1]:8080/")]
    [DataRow("a_b:c@host/")]
    [DataRow(":8080/")]
    public void TransferSettingsOf_ReadCallbackOnAUrlWithNoScheme_LeavesNoPostDataAndReadsNothing(string url)
    {
        EngineTransferSettings settings = ReadCallbackPostSettingsOf(url, Encoding.ASCII.GetBytes("abc"), -1);

        Assert.IsNull(settings.PostData);
    }

    [TestMethod]
    [DataRow("nats://h/t")]
    [DataRow("mcp+stdio:server")]
    public void TransferSettingsOf_ReadCallbackOnAnExtensionScheme_GivesTheBodyAsPostData(string url)
    {
        EngineTransferSettings settings = ReadCallbackPostSettingsOf(url, Encoding.ASCII.GetBytes("abc"), -1);

        Assert.AreEqual("abc", Encoding.ASCII.GetString(settings.PostData!.Value.Span));
    }

    [TestMethod]
    public void TransferSettingsOf_PostFieldsOnAUrlWithNoScheme_LeaveNoPostData()
    {
        EngineTransferSettings settings = MqttSettingsOf("localhost:8080/x", new EasyHttpOptions { Post = true, PostFields = Encoding.ASCII.GetBytes("a=1") });

        Assert.IsNull(settings.PostData);
    }

    [TestMethod]
    public async Task PerformAsync_ReadCallbackPostToAUrlWithNoScheme_PostsTheWholeBodyReadingTheCallbackOnce()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n"), Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n")]);
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(connector).CreateEasy();
        int calls = 0;
        Curl.Api.Callbacks.ReadCallback inner = ReaderOf("hello");
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "localhost:8080/x" },
            Http = new EasyHttpOptions { Post = true, PostFieldSize = 5 },
            Callbacks = new Curl.Api.Callbacks.EasyCallbackOptions { Read = buffer => { calls++; return inner(buffer); } },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.EndsWith(Encoding.ASCII.GetString(connector.Written), "\r\n\r\nhello");
        Assert.IsTrue(calls >= 1);
    }
}
