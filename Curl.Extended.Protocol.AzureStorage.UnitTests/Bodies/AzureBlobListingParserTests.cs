namespace Curl.Extended.Protocol.AzureStorage.Bodies;

/// <summary>
/// Pins the <c>EnumerationResults</c> body reading of the Blob service REST API "List Blobs", against
/// its sample response.
/// </summary>
[TestClass]
public sealed class AzureBlobListingParserTests
{
    [TestMethod]
    public void Parse_ReferenceSampleResponse_ReadsBlobsPrefixesAndNextMarker()
    {
        // "List Blobs", "Sample response" (the properties the handler needs, others as sent).
        const string Body = """
            <?xml version="1.0" encoding="utf-8"?>
            <EnumerationResults ServiceEndpoint="https://myaccount.blob.core.windows.net/" ContainerName="mycontainer">
              <Prefix>photos/</Prefix>
              <MaxResults>2</MaxResults>
              <Delimiter>/</Delimiter>
              <Blobs>
                <Blob>
                  <Name>photos/cat.jpg</Name>
                  <Properties>
                    <Creation-Time>Wed, 09 Sep 2009 09:20:02 GMT</Creation-Time>
                    <Last-Modified>Wed, 09 Sep 2009 09:20:02 GMT</Last-Modified>
                    <Etag>0x8CBFF45D8A29A19</Etag>
                    <Content-Length>100</Content-Length>
                    <Content-Type>image/jpeg</Content-Type>
                    <BlobType>BlockBlob</BlobType>
                  </Properties>
                </Blob>
                <BlobPrefix>
                  <Name>photos/2009/</Name>
                </BlobPrefix>
              </Blobs>
              <NextMarker>2!80!MDAwMDE2</NextMarker>
            </EnumerationResults>
            """;

        var listing = AzureBlobListingParser.Parse(Body).Value!;

        Assert.AreEqual(
            new AzureListedBlob("photos/cat.jpg", 100, new DateTimeOffset(2009, 9, 9, 9, 20, 2, TimeSpan.Zero), "0x8CBFF45D8A29A19"),
            listing.Blobs.Single());
        CollectionAssert.AreEqual(new[] { "photos/2009/" }, listing.BlobPrefixes.ToArray());
        Assert.AreEqual("2!80!MDAwMDE2", listing.NextMarker);
    }

    [TestMethod]
    [DataRow("<EnumerationResults><Blobs /><NextMarker /></EnumerationResults>", DisplayName = "empty NextMarker")]
    [DataRow("<EnumerationResults><Blobs /></EnumerationResults>", DisplayName = "no NextMarker")]
    public void Parse_LastPage_HasNoNextMarker(string body)
    {
        var result = AzureBlobListingParser.Parse(body);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNull(result.Failure);
        Assert.IsNull(result.Value!.NextMarker);
        Assert.IsEmpty(result.Value.Blobs);
    }

    [TestMethod]
    [DataRow("", DisplayName = "empty body")]
    [DataRow("<EnumerationResults><Blobs>", DisplayName = "truncated XML")]
    [DataRow("<Error><Code>X</Code></Error>", DisplayName = "another root")]
    [DataRow("<EnumerationResults />", DisplayName = "no Blobs")]
    [DataRow("<EnumerationResults><Blobs><Blob><Name>a</Name></Blob></Blobs></EnumerationResults>", DisplayName = "no Properties")]
    [DataRow("<EnumerationResults><Blobs><BlobPrefix /></Blobs></EnumerationResults>", DisplayName = "BlobPrefix without Name")]
    [DataRow(
        "<EnumerationResults><Blobs><Blob><Name>a</Name><Properties><Last-Modified>Wed, 09 Sep 2009 09:20:02 GMT</Last-Modified><Etag>e</Etag><Content-Length>-1</Content-Length></Properties></Blob></Blobs></EnumerationResults>",
        DisplayName = "negative Content-Length")]
    [DataRow(
        "<EnumerationResults><Blobs><Blob><Name>a</Name><Properties><Last-Modified>Wed, 09 Sep 2009 09:20:02 GMT</Last-Modified><Etag>e</Etag><Content-Length>99999999999999999999</Content-Length></Properties></Blob></Blobs></EnumerationResults>",
        DisplayName = "Content-Length past a long")]
    [DataRow(
        "<EnumerationResults><Blobs><Blob><Name>a</Name><Properties><Last-Modified>yesterday</Last-Modified><Etag>e</Etag><Content-Length>1</Content-Length></Properties></Blob></Blobs></EnumerationResults>",
        DisplayName = "Last-Modified not RFC 1123")]
    public void Parse_MalformedListing_YieldsAFailure(string body)
    {
        var result = AzureBlobListingParser.Parse(body);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Value);
        Assert.IsFalse(string.IsNullOrEmpty(result.Failure));
    }
}
