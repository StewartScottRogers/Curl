namespace Curl.Extended.Protocol.AzureStorage.Requests;

/// <summary>Pins the mapping of <c>az</c> and <c>abfs</c> URLs against "Addressing Blob Service Resources" and the ADLS Gen2 URI.</summary>
[TestClass]
public sealed class AzureStorageUrlMappingTests
{
    [TestMethod]
    public void Map_AzWithBareAccount_AddressesTheBlobEndpoint()
    {
        // Addressing Blob Service Resources: https://myaccount.blob.core.windows.net/mycontainer/myblob.
        var endpoint = AzureStorageUrlMapping.Map(new Uri("az://myaccount/mycontainer/myblob"));

        Assert.AreEqual(new AzureStorageEndpoint("myaccount.blob.core.windows.net", "/mycontainer/myblob", string.Empty), endpoint);
    }

    [TestMethod]
    public void Map_AzWithFullHostAndQuery_KeepsTheHostAndQuery()
    {
        // Addressing Blob Service Resources: an endpoint of another cloud is named by its full host.
        var endpoint = AzureStorageUrlMapping.Map(new Uri("az://myaccount.blob.core.usgovcloudapi.net/mycontainer?restype=container&comp=list"));

        Assert.AreEqual(new AzureStorageEndpoint("myaccount.blob.core.usgovcloudapi.net", "/mycontainer", "restype=container&comp=list"), endpoint);
    }

    [TestMethod]
    public void Map_AzWithSpaceInBlobName_PercentEncodesThePath()
    {
        // Naming and Referencing Containers, Blobs, and Metadata: reserved URL characters in a blob name are escaped.
        var endpoint = AzureStorageUrlMapping.Map(new Uri("az://myaccount/mycontainer/my blob"));

        Assert.AreEqual("/mycontainer/my%20blob", endpoint.Path);
    }

    [TestMethod]
    public void Map_AzAccountOnly_AddressesTheAccount()
    {
        var endpoint = AzureStorageUrlMapping.Map(new Uri("az://myaccount"));

        Assert.AreEqual("/", endpoint.Path);
    }

    [TestMethod]
    public void Map_AbfsWithFileSystemAndPath_AddressesTheDfsEndpoint()
    {
        // ADLS Gen2 URI: abfs://<file_system>@<account_name>.dfs.core.windows.net/<path>.
        var endpoint = AzureStorageUrlMapping.Map(new Uri("abfs://myfs@myaccount.dfs.core.windows.net/dir/file.txt"));

        Assert.AreEqual(new AzureStorageEndpoint("myaccount.dfs.core.windows.net", "/myfs/dir/file.txt", string.Empty), endpoint);
    }

    [TestMethod]
    public void Map_AbfssWithBareAccountAndNoPath_AddressesTheFileSystem()
    {
        // ADLS Gen2 URI: abfss is the TLS form; a bare account name gets the DFS suffix.
        var endpoint = AzureStorageUrlMapping.Map(new Uri("abfss://myfs@myaccount?resource=filesystem"));

        Assert.AreEqual(new AzureStorageEndpoint("myaccount.dfs.core.windows.net", "/myfs", "resource=filesystem"), endpoint);
    }

    [TestMethod]
    [DataRow("abfss://fs@localhost/x", "localhost")]
    [DataRow("abfss://fs@[::1]/x", "[::1]")]
    [DataRow("abfs://fs@127.0.0.1/x", "127.0.0.1")]
    public void Map_AbfsWithEmulatorHost_UsesTheHostAsGivenWithoutThePublicSuffix(string url, string expectedHost)
    {
        // EXT-085: a token must never go to localhost.dfs.core.windows.net.
        var endpoint = AzureStorageUrlMapping.Map(new Uri(url));

        Assert.AreEqual(expectedHost, endpoint.Host);
        Assert.AreEqual("/fs/x", endpoint.Path);
    }

    [TestMethod]
    [DataRow("abfss://fs@0x7f000001/x", "0x7f000001")]
    [DataRow("abfs://fs@2130706433/x", "2130706433")]
    public void Map_AbfsWithNumericIpv4Spelling_UsesTheHostAsGivenWithoutThePublicSuffix(string url, string expectedHost)
    {
        // EXT-105: a token must never go to 0x7f000001.dfs.core.windows.net.
        var endpoint = AzureStorageUrlMapping.Map(new Uri(url));

        Assert.AreEqual(expectedHost, endpoint.Host);
        Assert.AreEqual("/fs/x", endpoint.Path);
    }

    [TestMethod]
    [DataRow("abfss://fs@0xstore/x", "0xstore.dfs.core.windows.net")]
    [DataRow("abfss://fs@12345678/x", "12345678.dfs.core.windows.net")]
    [DataRow("abfs://fs@123/x", "123.dfs.core.windows.net")]
    [DataRow("abfs://fs@1.2.3.4.5/x", "1.2.3.4.5")]
    public void Map_AbfsWithAllDigitOrHexLookingAccountName_GetsThePublicDfsSuffix(string url, string expectedHost)
    {
        // EXT-117: a valid account name that is no IPv4 address must never be sent bare.
        Assert.AreEqual(expectedHost, AzureStorageUrlMapping.Map(new Uri(url)).Host);
    }

    [TestMethod]
    public void Map_AzWithAllDigitAccountName_GetsThePublicBlobSuffix()
    {
        var endpoint = AzureStorageUrlMapping.Map(new Uri("az://12345678/ctr/b"));

        Assert.AreEqual("12345678.blob.core.windows.net", endpoint.Host);
        Assert.AreEqual("/ctr/b", endpoint.Path);
    }

    [TestMethod]
    [DataRow("abfs://fs@0177.0.0.1/x")]
    [DataRow("abfs://fs@127.0x0.0.1/x")]
    [DataRow("abfs://fs@0x7f.1/x")]
    [DataRow("abfs://fs@256.0.0.1/x")]
    [DataRow("abfs://fs@1.2.3.256/x")]
    public void Map_AbfsWithDottedOrOctalIpv4Spelling_UsesTheHostAsGiven(string url)
    {
        Assert.AreEqual(new Uri(url).Host, AzureStorageUrlMapping.Map(new Uri(url)).Host);
    }

    [TestMethod]
    [DataRow("abfs://fs@4294967296/x")]
    [DataRow("abfs://fs@08/x")]
    [DataRow("abfs://fs@0x/x")]
    [DataRow("abfs://fs@999999999999/x")]
    public void Map_AbfsWithOutOfRangeNumericHost_IsAnAccountName(string url)
    {
        Assert.EndsWith(".dfs.core.windows.net", AzureStorageUrlMapping.Map(new Uri(url)).Host);
    }

    [TestMethod]
    public void Map_AbfsWithPlainAccountName_StillGetsThePublicDfsSuffix()
    {
        Assert.AreEqual("acct.dfs.core.windows.net", AzureStorageUrlMapping.Map(new Uri("abfss://fs@acct/x")).Host);
    }

    [TestMethod]
    public void Map_AbfsWithoutFileSystem_FailsAsMissingFileSystem()
    {
        var exception = Assert.ThrowsExactly<AzureStorageRequestException>(() => AzureStorageUrlMapping.Map(new Uri("abfs://myaccount.dfs.core.windows.net/dir")));

        Assert.AreEqual(AzureStorageRequestFailure.MissingFileSystem, exception.Failure);
    }

    [TestMethod]
    public void Map_AzWithoutAccount_FailsAsMissingAccount()
    {
        var exception = Assert.ThrowsExactly<AzureStorageRequestException>(() => AzureStorageUrlMapping.Map(new Uri("az:///mycontainer/myblob")));

        Assert.AreEqual(AzureStorageRequestFailure.MissingAccount, exception.Failure);
    }

    [TestMethod]
    public void Map_OtherScheme_FailsAsUnsupportedScheme()
    {
        var exception = Assert.ThrowsExactly<AzureStorageRequestException>(() => AzureStorageUrlMapping.Map(new Uri("s3://bucket/key")));

        Assert.AreEqual(AzureStorageRequestFailure.UnsupportedScheme, exception.Failure);
    }

    [TestMethod]
    public void Map_Null_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageUrlMapping.Map(null!));
    }
}
