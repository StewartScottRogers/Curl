namespace Curl.Extension.GoogleCloudStorage.Xml;

/// <summary>
/// Pins the <c>ListBucketResult</c> reading of the Cloud Storage XML API "List objects", against its
/// example bodies.
/// </summary>
[TestClass]
public sealed class GcsListBucketResultParserTests
{
    [TestMethod]
    public void Parse_SpecificationExampleListing_ReadsEveryObject()
    {
        // "List objects", the example response body (two of its entries).
        const string Body = """
            <?xml version='1.0' encoding='UTF-8'?>
            <ListBucketResult xmlns='http://doc.s3.amazonaws.com/2006-03-01'>
              <Name>travel-maps</Name>
              <Prefix></Prefix>
              <Marker></Marker>
              <IsTruncated>false</IsTruncated>
              <Contents>
                <Key>europe/france/paris.jpg</Key>
                <Generation>1360887759327000</Generation>
                <MetaGeneration>1</MetaGeneration>
                <LastModified>2013-02-15T00:22:39.322Z</LastModified>
                <ETag>"53fc311c15eda0a2c2a1a4ab3d6bf4ab"</ETag>
                <Size>132813</Size>
              </Contents>
              <Contents>
                <Key>europe/germany/berlin.jpg</Key>
                <Generation>1360887759327000</Generation>
                <MetaGeneration>1</MetaGeneration>
                <LastModified>2013-02-15T00:22:39.322Z</LastModified>
                <ETag>"0e9d2e1e2e0e5c0f6e5c0e4f2d4b0e2c"</ETag>
                <Size>96785</Size>
              </Contents>
            </ListBucketResult>
            """;

        var result = GcsListBucketResultParser.Parse(Body);

        Assert.IsTrue(result.Succeeded);
        Assert.IsNull(result.Failure);
        var listing = result.Value!;
        Assert.HasCount(2, listing.Objects);
        Assert.AreEqual(
            new GcsListedObject("europe/france/paris.jpg", 132813, new DateTimeOffset(2013, 2, 15, 0, 22, 39, 322, TimeSpan.Zero), "\"53fc311c15eda0a2c2a1a4ab3d6bf4ab\""),
            listing.Objects[0]);
        Assert.AreEqual("europe/germany/berlin.jpg", listing.Objects[1].Key);
        Assert.AreEqual(string.Empty, listing.Prefix);
        Assert.IsNull(listing.Delimiter);
        Assert.IsEmpty(listing.CommonPrefixes);
        Assert.IsFalse(listing.IsTruncated);
        Assert.IsNull(listing.ContinuationValue);
    }

    [TestMethod]
    public void Parse_ListingWithPrefixAndDelimiter_ReadsThemAndTheCommonPrefixes()
    {
        // "List objects" with prefix=europe/ and delimiter=/: objects rolled up under CommonPrefixes.
        const string Body = """
            <ListBucketResult xmlns='http://doc.s3.amazonaws.com/2006-03-01'>
              <Name>travel-maps</Name>
              <Prefix>europe/</Prefix>
              <Marker></Marker>
              <Delimiter>/</Delimiter>
              <IsTruncated>false</IsTruncated>
              <CommonPrefixes>
                <Prefix>europe/france/</Prefix>
              </CommonPrefixes>
              <CommonPrefixes>
                <Prefix>europe/germany/</Prefix>
              </CommonPrefixes>
            </ListBucketResult>
            """;

        var listing = GcsListBucketResultParser.Parse(Body).Value!;

        Assert.AreEqual("europe/", listing.Prefix);
        Assert.AreEqual("/", listing.Delimiter);
        CollectionAssert.AreEqual(new[] { "europe/france/", "europe/germany/" }, listing.CommonPrefixes.ToArray());
        Assert.IsEmpty(listing.Objects);
    }

    [TestMethod]
    public void Parse_TruncatedListTypeTwoListing_YieldsItsNextContinuationToken()
    {
        // "List objects" with list-type=2 and max-keys: a truncated page.
        const string Body = """
            <ListBucketResult xmlns='http://doc.s3.amazonaws.com/2006-03-01'>
              <Name>travel-maps</Name>
              <Prefix></Prefix>
              <MaxKeys>1</MaxKeys>
              <IsTruncated>true</IsTruncated>
              <NextContinuationToken>CiBldXJvcGUvZnJhbmNlL3BhcmlzLmpwZw==</NextContinuationToken>
            </ListBucketResult>
            """;

        var listing = GcsListBucketResultParser.Parse(Body).Value!;

        Assert.IsTrue(listing.IsTruncated);
        Assert.AreEqual("CiBldXJvcGUvZnJhbmNlL3BhcmlzLmpwZw==", listing.ContinuationValue);
    }

    [TestMethod]
    public void Parse_TruncatedMarkerListing_YieldsItsNextMarker()
    {
        // "List objects" with marker paging: a truncated page.
        const string Body = """
            <ListBucketResult xmlns='http://doc.s3.amazonaws.com/2006-03-01'>
              <Name>travel-maps</Name>
              <Prefix></Prefix>
              <Marker></Marker>
              <NextMarker>europe/france/paris.jpg</NextMarker>
              <IsTruncated>true</IsTruncated>
            </ListBucketResult>
            """;

        var listing = GcsListBucketResultParser.Parse(Body).Value!;

        Assert.IsTrue(listing.IsTruncated);
        Assert.AreEqual("europe/france/paris.jpg", listing.ContinuationValue);
    }

    [TestMethod]
    public void Parse_BodyWithoutANamespace_ReadsAlike()
    {
        var listing = GcsListBucketResultParser.Parse("<ListBucketResult><IsTruncated>false</IsTruncated></ListBucketResult>").Value!;

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
    [DataRow("<ListBucketResult><IsTruncated>false</IsTruncated><Contents><Key>a</Key><Size>1</Size><LastModified>2013-02-15T00:22:39Z</LastModified></Contents></ListBucketResult>", DisplayName = "Contents without ETag")]
    [DataRow("<ListBucketResult><IsTruncated>false</IsTruncated><CommonPrefixes/></ListBucketResult>", DisplayName = "CommonPrefixes without Prefix")]
    public void Parse_MalformedBody_YieldsAFailure(string body)
    {
        var result = GcsListBucketResultParser.Parse(body);

        Assert.IsFalse(result.Succeeded);
        Assert.IsNull(result.Value);
        Assert.IsFalse(string.IsNullOrEmpty(result.Failure));
    }

    [TestMethod]
    public void Parse_Null_Throws() => Assert.ThrowsExactly<ArgumentNullException>(() => GcsListBucketResultParser.Parse(null!));
}
