using System.Xml;
using System.Xml.Linq;
using Curl.Extended.Protocol.Xmpp.Client;

namespace Curl.Extended.Protocol.Xmpp.Wire;

/// <summary>Checks reading the server's stream against RFC 6120, 4.2 "Opening a Stream", 4.4 "Closing a Stream" and 11.1 "XML Restrictions".</summary>
[TestClass]
public sealed class XmppStreamReaderTests
{
    private const string Header = "<?xml version='1.0'?><stream:stream xmlns='jabber:client' xmlns:stream='http://etherx.jabber.org/streams' id='s1' from='example.com' version='1.0'>";

    [TestMethod]
    public async Task ReadAsync_CompleteElement_GivesItWithoutReadingPastIt()
    {
        ScriptedXmppConnection connection = new([.. new[] { Header + "<stream:features><a/></stream:features>" }.Select(System.Text.Encoding.UTF8.GetBytes)]) { FailsWhenDrained = true };

        XElement? features = await new XmppStreamReader(connection, CancellationToken.None).ReadAsync();

        Assert.AreEqual(XmppNamespaces.Stream + "features", features!.Name);
        Assert.AreEqual(XmppNamespaces.Client + "a", features.Elements().Single().Name);
    }

    [TestMethod]
    public async Task ReadAsync_SplitAcrossReads_GivesEachElementThenNullAtTheStreamsClose()
    {
        XmppStreamReader reader = new(ScriptedXmppConnection.Replaying(Header[..20], Header[20..] + "<success xmlns='urn:ietf:params:xml:ns:xmpp-sasl'", "/>\n<presence/>", "</stream:stream>"), CancellationToken.None);

        Assert.AreEqual(XmppNamespaces.Sasl + "success", (await reader.ReadAsync())!.Name);
        Assert.AreEqual(XmppNamespaces.Client + "presence", (await reader.ReadAsync())!.Name);
        Assert.IsNull(await reader.ReadAsync());
        Assert.IsNull(await reader.ReadAsync());
    }

    [TestMethod]
    public async Task ReadAsync_ConnectionClosesInsideTheStream_GivesNull() =>
        Assert.IsNull(await new XmppStreamReader(ScriptedXmppConnection.Replaying(Header), CancellationToken.None).ReadAsync());

    [TestMethod]
    [DataRow(Header + "<a></b>")]
    [DataRow("<?xml version='1.0'?><!DOCTYPE x [<!ENTITY a 'b'>]><x>")]
    public async Task ReadAsync_MalformedOrDtd_Throws(string served) =>
        await Assert.ThrowsExactlyAsync<XmlException>(async () => await new XmppStreamReader(ScriptedXmppConnection.Replaying(served, "<more/>"), CancellationToken.None).ReadAsync());

    [TestMethod]
    public async Task ConnectionReadStream_ReadsBothOverloadsAndRefusesTheRest()
    {
        ConnectionReadStream stream = new(ScriptedXmppConnection.Replaying("ab"), CancellationToken.None);
        byte[] buffer = new byte[4];

        Assert.AreEqual(1, await stream.ReadAsync(buffer, 0, 1, CancellationToken.None));
        Assert.AreEqual(1, await stream.ReadAsync(buffer.AsMemory(1)));
        Assert.IsFalse(stream.ReachedEnd);
        Assert.AreEqual(0, await stream.ReadAsync(buffer.AsMemory()));
        Assert.IsTrue(stream.ReachedEnd);
        Assert.IsTrue(stream.CanRead);
        Assert.IsFalse(stream.CanSeek);
        Assert.IsFalse(stream.CanWrite);
        stream.Flush();
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Length);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Position = 0);
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Read(buffer, 0, 1));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.SetLength(0));
        Assert.ThrowsExactly<NotSupportedException>(() => stream.Write(buffer, 0, 1));
    }
}
