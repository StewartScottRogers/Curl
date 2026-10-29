using System.Text;

namespace Curl.Extension.SignalR.HubProtocol;

[TestClass]
public sealed class HubFrameSplitterTests
{
    private const string Stream = "{}\u001e{\"type\":6}\u001e{\"type\":1,\"target\":\"Send\",\"arguments\":[\"a\u001eb\"]}\u001e{\"type\":7}\u001e";

    [TestMethod]
    public void TryTakeRecord_StreamSplitAtEveryPoint_ReadsTheHandshakeResponseAndEveryMessage_JsonEncoding()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Stream);
        for (int split = 0; split <= bytes.Length; split++)
        {
            var splitter = new HubFrameSplitter();
            var records = new List<string>();
            splitter.Append(bytes.AsSpan(0, split));
            TakeAll(splitter, records);
            splitter.Append(bytes.AsSpan(split));
            TakeAll(splitter, records);

            CollectionAssert.AreEqual(new[] { "{}", "{\"type\":6}", "{\"type\":1,\"target\":\"Send\",\"arguments\":[\"a", "b\"]}", "{\"type\":7}" }, records, $"split at {split}");
            Assert.AreEqual(0, splitter.PendingByteCount);
        }
    }

    [TestMethod]
    public void TryTakeRecord_RecordNotYetEnded_TakesNothingAndKeepsTheBytes_JsonEncoding()
    {
        var splitter = new HubFrameSplitter();
        splitter.Append("{\"type\":"u8);

        Assert.IsFalse(splitter.TryTakeRecord(out byte[] record));
        Assert.IsEmpty(record);
        Assert.AreEqual(8, splitter.PendingByteCount);
    }

    private static void TakeAll(HubFrameSplitter splitter, List<string> records)
    {
        while (splitter.TryTakeRecord(out byte[] record))
        {
            records.Add(Encoding.UTF8.GetString(record));
        }
    }
}
