using Curl.Extension.WebDavTools.Multistatus;

namespace Curl.Extension.WebDavTools.UnitTests.Multistatus;

[TestClass]
public sealed class WebDavMultistatusReaderTests
{
    // RFC 4918 section 9.1.3, "Example - Retrieving Named Properties".
    private const string NamedPropertiesExample = """
        <?xml version="1.0" encoding="utf-8" ?>
        <D:multistatus xmlns:D="DAV:">
          <D:response xmlns:R="http://ns.example.com/boxschema/">
            <D:href>http://www.example.com/file</D:href>
            <D:propstat>
              <D:prop>
                <R:bigbox><R:BoxType>Box type A</R:BoxType></R:bigbox>
                <R:author><R:Name>J.J. Johnson</R:Name></R:author>
              </D:prop>
              <D:status>HTTP/1.1 200 OK</D:status>
            </D:propstat>
            <D:propstat>
              <D:prop><R:DingALing/><R:Random/></D:prop>
              <D:status>HTTP/1.1 403 Forbidden</D:status>
              <D:responsedescription> The user does not have access to the DingALing property.
              </D:responsedescription>
            </D:propstat>
          </D:response>
          <D:responsedescription> There has been an access violation error.
          </D:responsedescription>
        </D:multistatus>
        """;

    // RFC 4918 section 9.1.5, "Example - Using So-called 'allprop'" (supportedlock and lockdiscovery shortened).
    private const string AllpropExample = """
        <?xml version="1.0" encoding="utf-8" ?>
        <D:multistatus xmlns:D="DAV:">
          <D:response>
            <D:href>/container/</D:href>
            <D:propstat>
              <D:prop xmlns:R="http://ns.example.com/boxschema/">
                <R:bigbox><R:BoxType>Box type A</R:BoxType></R:bigbox>
                <R:author><R:Name>Hadrian</R:Name></R:author>
                <D:creationdate>1997-12-01T17:42:21-08:00</D:creationdate>
                <D:displayname>Example collection</D:displayname>
                <D:resourcetype><D:collection/></D:resourcetype>
                <D:supportedlock/>
              </D:prop>
              <D:status>HTTP/1.1 200 OK</D:status>
            </D:propstat>
          </D:response>
          <D:response>
            <D:href>/container/front.html</D:href>
            <D:propstat>
              <D:prop xmlns:R="http://ns.example.com/boxschema/">
                <R:bigbox><R:BoxType>Box type B</R:BoxType></R:bigbox>
                <D:creationdate>1997-12-01T18:27:21-08:00</D:creationdate>
                <D:displayname>Example HTML resource</D:displayname>
                <D:getcontentlength>4525</D:getcontentlength>
                <D:getcontenttype>text/html</D:getcontenttype>
                <D:getetag>"zzyzx"</D:getetag>
                <D:getlastmodified>Mon, 12 Jan 1998 09:25:56 GMT</D:getlastmodified>
                <D:resourcetype/>
                <D:supportedlock/>
              </D:prop>
              <D:status>HTTP/1.1 200 OK</D:status>
            </D:propstat>
          </D:response>
        </D:multistatus>
        """;

    [TestMethod]
    public void Read_NamedPropertiesExample_GivesOneEntryWithTheSuccessfulStatus()
    {
        var result = WebDavMultistatusReader.Read(NamedPropertiesExample);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNull(result.FailureKind);
        Assert.IsNull(result.Failure);
        Assert.AreEqual(new WebDavResourceEntry("http://www.example.com/file", false, null, null, null, 200), result.Entries!.Single());
    }

    [TestMethod]
    public void Read_AllpropExample_GivesTheCollectionAndTheFile()
    {
        var entries = WebDavMultistatusReader.Read(AllpropExample).Entries!;

        Assert.HasCount(2, entries);
        Assert.AreEqual(new WebDavResourceEntry("/container/", true, null, null, null, 200), entries[0]);
        Assert.AreEqual(
            new WebDavResourceEntry("/container/front.html", false, 4525, new DateTimeOffset(1998, 1, 12, 9, 25, 56, TimeSpan.Zero), "\"zzyzx\"", 200),
            entries[1]);
    }

    [TestMethod]
    public void Read_AnyPrefixOrDefaultNamespace_ReadsAlike()
    {
        const string Body = """
            <multistatus xmlns="DAV:"><response><href>/a</href>
              <propstat><prop><getetag>"e"</getetag></prop><status>HTTP/1.1 200 OK</status></propstat>
            </response></multistatus>
            """;
        const string Prefixed = """
            <x:multistatus xmlns:x="DAV:"><x:response><x:href>/a</x:href>
              <x:propstat><x:prop><x:getetag>"e"</x:getetag></x:prop><x:status>HTTP/1.1 200 OK</x:status></x:propstat>
            </x:response></x:multistatus>
            """;

        Assert.AreEqual(WebDavMultistatusReader.Read(Body).Entries!.Single(), WebDavMultistatusReader.Read(Prefixed).Entries!.Single());
        Assert.AreEqual("\"e\"", WebDavMultistatusReader.Read(Body).Entries!.Single().ETag);
    }

    [TestMethod]
    public void Read_PropertyInAnotherNamespace_IsNotADavProperty()
    {
        var entry = ReadSingle("<propstat><prop><o:getetag xmlns:o=\"urn:other\">x</o:getetag></prop><status>HTTP/1.1 200 OK</status></propstat>");

        Assert.IsNull(entry.ETag);
    }

    [TestMethod]
    public void Read_PropertiesOnlyInNon2xxPropstat_AreIgnored()
    {
        var entry = ReadSingle("""
            <propstat><prop><getcontentlength>9</getcontentlength><resourcetype><collection/></resourcetype></prop><status>HTTP/1.1 404 Not Found</status></propstat>
            """);

        Assert.AreEqual(new WebDavResourceEntry("/r", false, null, null, null, 404), entry);
    }

    [TestMethod]
    public void Read_PropertiesAcrossTwoSuccessfulPropstats_AreMerged()
    {
        var entry = ReadSingle("""
            <propstat><prop><getcontentlength>9</getcontentlength></prop><status>HTTP/1.1 403 Forbidden</status></propstat>
            <propstat><prop><getcontentlength>5</getcontentlength></prop><status>HTTP/1.1 200 OK</status></propstat>
            <propstat><prop><getetag>"t"</getetag></prop><status>HTTP/1.1 206 Partial</status></propstat>
            """);

        Assert.AreEqual(new WebDavResourceEntry("/r", false, 5, null, "\"t\"", 200), entry);
    }

    [TestMethod]
    public void Read_ResponseWithItsOwnStatus_UsesIt()
    {
        var entry = WebDavMultistatusReader.Read(
            "<D:multistatus xmlns:D=\"DAV:\"><D:response><D:href>/gone</D:href><D:status>HTTP/1.1 404 Not Found</D:status></D:response></D:multistatus>")
            .Entries!.Single();

        Assert.AreEqual(new WebDavResourceEntry("/gone", false, null, null, null, 404), entry);
    }

    [TestMethod]
    public void Read_Href_IsPercentDecodedAndTrimmed()
    {
        var entry = WebDavMultistatusReader.Read(
            "<D:multistatus xmlns:D=\"DAV:\"><D:response><D:href> /dir/a%20b%C3%A9.txt </D:href><D:status>HTTP/1.1 200 OK</D:status></D:response></D:multistatus>")
            .Entries!.Single();

        Assert.AreEqual("/dir/a bé.txt", entry.Href);
    }

    [TestMethod]
    public void Read_NoResponses_GivesNoEntries() =>
        Assert.IsEmpty(WebDavMultistatusReader.Read("<multistatus xmlns=\"DAV:\"/>").Entries!);

    [TestMethod]
    [DataRow("-1")]
    [DataRow("lots")]
    public void Read_ContentLengthNotAWholeNumber_IsNull(string length) =>
        Assert.IsNull(ReadSingleSuccessful($"<getcontentlength>{length}</getcontentlength>").ContentLength);

    [TestMethod]
    [DataRow("1998-01-12T09:25:56Z")]
    [DataRow("Tue, 12 Jan 1998 09:25:56 GMT")]
    public void Read_LastModifiedNotAnRfc1123Date_IsNull(string date) =>
        Assert.IsNull(ReadSingleSuccessful($"<getlastmodified>{date}</getlastmodified>").LastModified);

    [TestMethod]
    public void Read_ResourceTypeWithoutCollection_IsNotACollection() =>
        Assert.IsFalse(ReadSingleSuccessful("<resourcetype><o:principal xmlns:o=\"urn:other\"/></resourcetype>").IsCollection);

    [TestMethod]
    [DataRow("")]
    [DataRow("<unclosed>")]
    [DataRow("<D:multistatus xmlns:D=\"DAV:\"><D:response>")]
    public void Read_NotWellFormed_IsRefused(string body)
    {
        var result = WebDavMultistatusReader.Read(body);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Entries);
        Assert.AreEqual(WebDavMultistatusFailureKind.NotWellFormed, result.FailureKind);
        Assert.IsFalse(string.IsNullOrEmpty(result.Failure));
    }

    [TestMethod]
    public void Read_DocumentTypeDeclaration_IsRefusedWithoutResolvingAnEntity()
    {
        const string Body = """
            <?xml version="1.0"?>
            <!DOCTYPE multistatus [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <multistatus xmlns="DAV:"><response><href>&xxe;</href><status>HTTP/1.1 200 OK</status></response></multistatus>
            """;

        var result = WebDavMultistatusReader.Read(Body);

        Assert.AreEqual(WebDavMultistatusFailureKind.DocumentTypeDeclaration, result.FailureKind);
        Assert.IsNull(result.Entries);
    }

    [TestMethod]
    [DataRow("<multistatus/>")]
    [DataRow("<D:propfind xmlns:D=\"DAV:\"/>")]
    public void Read_RootNotDavMultistatus_IsRefused(string body) =>
        Assert.AreEqual(WebDavMultistatusFailureKind.NotMultistatus, WebDavMultistatusReader.Read(body).FailureKind);

    [TestMethod]
    [DataRow("<response><status>HTTP/1.1 200 OK</status></response>")]
    [DataRow("<response><href>/r</href></response>")]
    [DataRow("<response><href>/r</href><propstat><status>HTTP/1.1 200 OK</status></propstat></response>")]
    [DataRow("<response><href>/r</href><propstat><prop/></propstat></response>")]
    public void Read_MissingRequiredElement_IsRefused(string response)
    {
        var result = WebDavMultistatusReader.Read($"<multistatus xmlns=\"DAV:\">{response}</multistatus>");

        Assert.AreEqual(WebDavMultistatusFailureKind.MissingElement, result.FailureKind);
        Assert.IsFalse(string.IsNullOrEmpty(result.Failure));
    }

    [TestMethod]
    [DataRow("HTTP/1.1")]
    [DataRow("FTP/1.1 200 OK")]
    [DataRow("HTTP/1.1 2000 OK")]
    [DataRow("HTTP/1.1 2x0 OK")]
    public void Read_StatusNotAnHttpStatusLine_IsRefused(string status)
    {
        var result = WebDavMultistatusReader.Read(
            $"<multistatus xmlns=\"DAV:\"><response><href>/r</href><status>{status}</status></response></multistatus>");

        Assert.AreEqual(WebDavMultistatusFailureKind.InvalidStatus, result.FailureKind);
    }

    [TestMethod]
    public void Read_Null_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => WebDavMultistatusReader.Read(null!));

    private static WebDavResourceEntry ReadSingle(string propstats) =>
        WebDavMultistatusReader.Read($"<multistatus xmlns=\"DAV:\"><response><href>/r</href>{propstats}</response></multistatus>").Entries!.Single();

    private static WebDavResourceEntry ReadSingleSuccessful(string properties) =>
        ReadSingle($"<propstat><prop>{properties}</prop><status>HTTP/1.1 200 OK</status></propstat>");
}
