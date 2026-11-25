using System.Net;

namespace Curl.Extended.Protocol.AzureStorage.Requests;

/// <summary>Pins Shared Key authorization ("Authorize with Shared Key") against the documented string-to-sign and Azurite's development account.</summary>
[TestClass]
public sealed class AzureStorageSharedKeyTests
{
    // Azurite's well-known development account and key.
    private const string Account = "devstoreaccount1";
    private const string Key = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    [TestMethod]
    public void Build_ListContainerMetadataExample_MatchesTheDocumentedString()
    {
        // The documentation's Get Container Metadata example: canonicalized headers, then the resource, then sorted query parameters.
        var headers = new KeyValuePair<string, string>[] { new("x-ms-version", "2015-02-21"), new("x-ms-date", "Fri, 26 Jun 2015 23:39:12 GMT") };

        var text = AzureStorageSharedKeyStringToSign.Build("get", Account, "/mycontainer", "timeout=20&restype=container&comp=metadata", headers, 0);

        Assert.AreEqual("GET\n\n\n\n\n\n\n\n\n\n\n\nx-ms-date:Fri, 26 Jun 2015 23:39:12 GMT\nx-ms-version:2015-02-21\n/devstoreaccount1/mycontainer\ncomp:metadata\nrestype:container\ntimeout:20", text);
        Assert.AreEqual(
            "SharedKey devstoreaccount1:NpSCSjvDEU2u4o4Lo6MDCJInwPAoMzWC8rMVmx3r3aA=",
            AzureStorageSharedKeySigner.AuthorizationOf(Account, Key, text));
    }

    [TestMethod]
    public void Build_ListBlobsWithPrefix_CanonicalizesEveryQueryParameterSorted()
    {
        // The documentation's List Blobs example: query parameters are lowercased, sorted and each on its own line.
        var headers = new KeyValuePair<string, string>[] { new("x-ms-version", "2015-02-21") };

        var text = AzureStorageSharedKeyStringToSign.Build("GET", "myaccount", "/mycontainer", "restype=container&comp=list&delimiter=%2F&prefix=logs%2F", headers, 0);

        Assert.EndsWith("/myaccount/mycontainer\ncomp:list\ndelimiter:/\nprefix:logs/\nrestype:container", text);
    }
    [TestMethod]
    public void Build_PutWithBodyAndStandardHeaders_SignsLengthTypeAndRepeatedQueryValues()
    {
        var headers = new KeyValuePair<string, string>[] { new("content-type", "text/plain"), new("x-ms-blob-type", " BlockBlob "), new("Range", "bytes=0-1") };

        var text = AzureStorageSharedKeyStringToSign.Build("PUT", "acct", "/c/b", "a=2&b&a=1&A=%33", headers, 12);

        Assert.AreEqual("PUT\n\n\n12\n\ntext/plain\n\n\n\n\n\nbytes=0-1\nx-ms-blob-type:BlockBlob\n/acct/c/b\na:1,2,3\nb:", text);
    }

    [TestMethod]
    public void Build_NullArguments_Throw()
    {
        IReadOnlyList<KeyValuePair<string, string>> none = [];
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageSharedKeyStringToSign.Build(null!, "a", "/", "", none, 0));
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageSharedKeyStringToSign.Build("GET", null!, "/", "", none, 0));
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageSharedKeyStringToSign.Build("GET", "a", null!, "", none, 0));
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageSharedKeyStringToSign.Build("GET", "a", "/", null!, none, 0));
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageSharedKeyStringToSign.Build("GET", "a", "/", "", null!, 0));
    }

    [TestMethod]
    public void AuthorizationOf_BadKeyOrNullArguments_Throws()
    {
        Assert.AreEqual(AzureStorageRequestFailure.InvalidSharedKey, Assert.ThrowsExactly<AzureStorageRequestException>(() => AzureStorageSharedKeySigner.AuthorizationOf("a", "not base64!", "s")).Failure);
        Assert.AreEqual(AzureStorageRequestFailure.InvalidSharedKey, Assert.ThrowsExactly<AzureStorageRequestException>(() => AzureStorageSharedKeySigner.AuthorizationOf("a", string.Empty, "s")).Failure);
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageSharedKeySigner.AuthorizationOf(null!, Key, "s"));
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageSharedKeySigner.AuthorizationOf("a", null!, "s"));
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageSharedKeySigner.AuthorizationOf("a", Key, null!));
    }

    [TestMethod]
    public void Authorize_SharedKey_SignsTheDocumentedStringToSign()
    {
        var endpoint = new AzureStorageEndpoint("127.0.0.1", "/devstoreaccount1/c/b", string.Empty, 10000, true);
        var credential = new AzureStorageCredential(AzureStorageCredentialKind.SharedKey, Key, Account);
        IReadOnlyList<KeyValuePair<string, string>> extra = [new("x-ms-blob-type", "BlockBlob")];

        var request = new AzureStorageRequestAuthorizer(new FixedTimeProvider()).Authorize(endpoint, credential, "PUT", extra, 5);

        var signed = new KeyValuePair<string, string>[] { new("x-ms-version", "2025-01-05"), new("x-ms-date", "Sun, 05 Jan 2025 10:20:30 GMT"), extra[0] };
        var expected = AzureStorageSharedKeySigner.AuthorizationOf(Account, Key, AzureStorageSharedKeyStringToSign.Build("PUT", Account, "/devstoreaccount1/c/b", string.Empty, signed, 5));
        Assert.AreEqual(expected, request.Headers.Single(header => header.Key == "Authorization").Value);
        Assert.IsFalse(request.Headers.Any(header => header.Value.Contains(Key, StringComparison.Ordinal)), "the key is never sent");
        Assert.IsFalse(request.Target.Contains(Key, StringComparison.Ordinal));
    }

    [TestMethod]
    public void Authorize_SharedKeyWithBadKey_FailsWithoutEchoingIt()
    {
        var credential = new AzureStorageCredential(AzureStorageCredentialKind.SharedKey, "secret-not-base64!", Account);

        var exception = Assert.ThrowsExactly<AzureStorageRequestException>(() => new AzureStorageRequestAuthorizer(new FixedTimeProvider()).Authorize(new("h", "/", string.Empty), credential));

        Assert.IsFalse(exception.Message.Contains("secret", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Choose_AccountAndKey_BeatsEnvironmentButNotOAuth2Bearer()
    {
        var login = new NetworkCredential(Account, Key);
        Func<string, string?> environment = _ => "from-env";

        var shared = AzureStorageCredential.Choose(null, environment, login);
        var bearer = AzureStorageCredential.Choose("cli", environment, login);

        Assert.AreEqual(new AzureStorageCredential(AzureStorageCredentialKind.SharedKey, Key, Account), shared);
        Assert.AreEqual(AzureStorageCredentialKind.Bearer, bearer.Kind);
    }

    [TestMethod]
    public void Choose_AccountWithoutKey_FallsBackToTheEnvironment()
    {
        Assert.AreSame(AzureStorageCredential.Anonymous, AzureStorageCredential.Choose(null, _ => null, new NetworkCredential(Account, string.Empty)));
        Assert.AreSame(AzureStorageCredential.Anonymous, AzureStorageCredential.Choose(null, _ => null, new NetworkCredential(string.Empty, Key)));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2025, 1, 5, 10, 20, 30, TimeSpan.Zero);
    }
}
