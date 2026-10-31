namespace Curl.Extension.Protocol.S3.Xml;

/// <summary>
/// Pins the <c>ListBucketResult</c> reading of the Amazon S3 API reference "ListObjectsV2", against
/// its example bodies.
/// </summary>
[TestClass]
public sealed class S3ListBucketResultParserTests
{
    [TestMethod]
    public void Parse_ReferenceExampleListingKeys_ReadsEveryObject()
    {
        // "ListObjectsV2", "Example 1: Listing keys" (two of its entries).
        const string Body = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">
                <Name>bucket</Name>
                <Prefix/>
                <KeyCount>205</KeyCount>
                <MaxKeys>1000</MaxKeys>
                <IsTruncated>false</IsTruncated>
                <Contents>
                    <Key>my-image.jpg</Key>
                    <LastModified>2009-10-12T17:50:30.000Z</LastModified>
                    <ETag>"fba9dede5f27731c9771645a39863328"</ETag>
                    <Size>434234</Size>
                    <StorageClass>STANDARD</StorageClass>
                </Contents>
                <Contents>
                    <Key>my-third-image.jpg</Key>
                    <LastModified>2009-10-12T17:50:30.000Z</LastModified>
                    <ETag>"1b2cf535f27731c974343645a3985328"</ETag>
                    <Size>64994</Size>
                    <StorageClass>STANDARD_IA</StorageClass>
                </Contents>
            </ListBucketResult>
            """;

        var result = S3ListBucketResultParser.Parse(Body);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNull(result.Failure);
        var listing = result.Value!;
        Assert.HasCount(2, listing.Objects);
        Assert.AreEqual(
            new S3ListedObject("my-image.jpg", 434234, new DateTimeOffset(2009, 10, 12, 17, 50, 30, TimeSpan.Zero), "\"fba9dede5f27731c9771645a39863328\""),
            listing.Objects[0]);
        Assert.AreEqual("my-third-image.jpg", listing.Objects[1].Key);
        Assert.IsEmpty(listing.CommonPrefixes);
        Assert.IsFalse(listing.IsTruncated);
        Assert.IsNull(listing.NextContinuationToken);
    }

    [TestMethod]
    public void Parse_ReferenceExampleTruncatedListing_YieldsItsContinuationToken()
    {
        // "ListObjectsV2", "Example 2: Listing keys using the max-keys, prefix, and start-after parameters"
        // and "Example 3: Listing keys using the continuation-token parameter" (a truncated page).
        const string Body = """
            <?xml version="1.0" encoding="UTF-8"?>
            <ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">
              <Name>bucket</Name>
              <Prefix></Prefix>
              <NextContinuationToken>1ueGcxLPRx1Tr/XYExHnhbYLgveDs2J/wm36Hy4vbOwM=</NextContinuationToken>
              <KeyCount>1000</KeyCount>
              <MaxKeys>1000</MaxKeys>
              <IsTruncated>true</IsTruncated>
              <Contents>
                <Key>happyface.jpg</Key>
                <LastModified>2014-11-21T19:40:05.000Z</LastModified>
                <ETag>"70ee1738b6b21e2c8a43f3a5ab0eee71"</ETag>
                <Size>11</Size>
                <StorageClass>STANDARD</StorageClass>
              </Contents>
            </ListBucketResult>
            """;

        var listing = S3ListBucketResultParser.Parse(Body).Value!;

        Assert.IsTrue(listing.IsTruncated);
        Assert.AreEqual("1ueGcxLPRx1Tr/XYExHnhbYLgveDs2J/wm36Hy4vbOwM=", listing.NextContinuationToken);
    }

    [TestMethod]
    public void Parse_ReferenceExampleWithDelimiter_ReadsTheCommonPrefixes()
    {
        // "ListObjectsV2", the delimiter example: keys rolled up under CommonPrefixes.
        const string Body = """
            <ListBucketResult xmlns="http://s3.amazonaws.com/doc/2006-03-01/">
              <Name>example-bucket</Name>
              <Prefix></Prefix>
              <KeyCount>2</KeyCount>
              <MaxKeys>1000</MaxKeys>
              <Delimiter>/</Delimiter>
              <IsTruncated>false</IsTruncated>
              <Contents>
                <Key>sample.jpg</Key>
                <LastModified>2017-02-26T01:56:20.000Z</LastModified>
                <ETag>"bf1d737a4d46a19f3bced6905cc8b902"</ETag>
                <Size>142863</Size>
                <StorageClass>STANDARD</StorageClass>
              </Contents>
              <CommonPrefixes>
                <Prefix>photos/</Prefix>
              </CommonPrefixes>
            </ListBucketResult>
            """;

        var listing = S3ListBucketResultParser.Parse(Body).Value!;

        CollectionAssert.AreEqual(new[] { "photos/" }, listing.CommonPrefixes.ToArray());
        Assert.AreEqual("sample.jpg", listing.Objects.Single().Key);
    }

    [TestMethod]
    public void Parse_BodyWithoutTheS3Namespace_ReadsAlike()
    {
        var listing = S3ListBucketResultParser.Parse("<ListBucketResult><IsTruncated>false</IsTruncated></ListBucketResult>").Value!;

        Assert.IsEmpty(listing.Objects);
        Assert.IsFalse(listing.IsTruncated);
    }

    [TestMethod]
    [DataRow("<ListBucketResult><IsTruncated>false</IsTruncated>", DisplayName = "not well-formed")]
    [DataRow("<Error><Code>NoSuchBucket</Code></Error>", DisplayName = "another root")]
    [DataRow("<ListBucketResult/>", DisplayName = "no IsTruncated")]
    [DataRow("<ListBucketResult><IsTruncated>maybe</IsTruncated></ListBucketResult>", DisplayName = "IsTruncated not a boolean")]
    [DataRow("<ListBucketResult><IsTruncated>false</IsTruncated><Contents><Size>1</Size></Contents></ListBucketResult>", DisplayName = "Contents without Key")]
    [DataRow("<ListBucketResult><IsTruncated>false</IsTruncated><Contents><Key>a</Key><Size>-1</Size></Contents></ListBucketResult>", DisplayName = "Size negative")]
    [DataRow("<ListBucketResult><IsTruncated>false</IsTruncated><Contents><Key>a</Key><Size>99999999999999999999</Size></Contents></ListBucketResult>", DisplayName = "Size too large")]
    [DataRow("<ListBucketResult><IsTruncated>false</IsTruncated><Contents><Key>a</Key><Size>1</Size><LastModified>yesterday</LastModified></Contents></ListBucketResult>", DisplayName = "LastModified not a time")]
    [DataRow("<ListBucketResult><IsTruncated>false</IsTruncated><CommonPrefixes/></ListBucketResult>", DisplayName = "CommonPrefixes without Prefix")]
    public void Parse_MalformedBody_YieldsAFailure(string body)
    {
        var result = S3ListBucketResultParser.Parse(body);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Value);
        Assert.IsFalse(string.IsNullOrEmpty(result.Failure));
    }

    [TestMethod]
    public void Parse_Null_Throws() => Assert.ThrowsExactly<ArgumentNullException>(() => S3ListBucketResultParser.Parse(null!));
}
