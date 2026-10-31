namespace Curl.Extension.AzureStorage.Requests;

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
