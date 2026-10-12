using System.Text;

namespace Curl.Conformance;

/// <summary>
/// Pins <see cref="UpstreamProtocolDump"/>: the writes of several servers go back into the one
/// <c>server.input</c> upstream's servers append to, in the order they happened (test2003).
/// </summary>
[TestClass]
public sealed class UpstreamProtocolDumpTests
{
    [TestMethod]
    public void Merge_SeveralServersWrote_InterleavesTheirWritesInTheOrderTheyHappened()
    {
        UpstreamProtocolDump http = new();
        UpstreamProtocolDump ftp = new();
        http.Record(Bytes("GET /1\r\n"));
        ftp.Record(Bytes("USER a\r\n"));
        http.Record(Bytes("GET /2\r\n"));
        ftp.Record(Bytes("QUIT\r\n"));

        byte[] merged = UpstreamProtocolDump.Merge([(http.Bytes, http.Entries), (ftp.Bytes, ftp.Entries)]);

        Assert.AreEqual("GET /1\r\nUSER a\r\nGET /2\r\nQUIT\r\n", Encoding.Latin1.GetString(merged));
    }

    [TestMethod]
    public void Merge_OneServerWrote_GivesItsBytesAsTheyAre()
    {
        UpstreamProtocolDump ftp = new();
        ftp.Record(Bytes("USER a\r\n"));

        byte[] merged = UpstreamProtocolDump.Merge([(ReadOnlyMemory<byte>.Empty, []), (Bytes("RETR 1\r\nPASV\r\n"), ftp.Entries)]);

        Assert.AreEqual("RETR 1\r\nPASV\r\n", Encoding.Latin1.GetString(merged));
    }

    [TestMethod]
    public void Forget_CountReachesBackIntoAnEarlierWrite_CutsTheLastWritesAndKeepsTheRest()
    {
        UpstreamProtocolDump dump = new();
        dump.Record(Bytes("abcd"));
        dump.Record(Bytes("ef"));

        dump.Forget(3);

        Assert.AreEqual("abc", Encoding.Latin1.GetString(dump.Bytes));
        Assert.AreEqual(1, dump.Entries.Count);
    }

    [TestMethod]
    public void Forget_CountIsAWholeWrite_RemovesThatWriteOnly()
    {
        UpstreamProtocolDump dump = new();
        dump.Record(Bytes("ab"));
        dump.Record(Bytes("cd"));

        dump.Forget(2);

        Assert.AreEqual("ab", Encoding.Latin1.GetString(dump.Bytes));
        Assert.AreEqual(1, dump.Entries.Count);
    }

    private static byte[] Bytes(string text) => Encoding.Latin1.GetBytes(text);
}
