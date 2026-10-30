namespace Curl.Extension.AzureStorage.Requests;

/// <summary>Pins the authorization of Azure Storage requests against the SAS, Entra ID and versioning specifications.</summary>
[TestClass]
public sealed class AzureStorageRequestAuthorizerTests
{
    private static readonly AzureStorageEndpoint Blob = new("myaccount.blob.core.windows.net", "/mycontainer/myblob", string.Empty);

    [TestMethod]
    public void Authorize_Anonymous_SendsHostVersionAndDateOnly()
    {
        // Versioning for the Azure Storage services: x-ms-version names the version; x-ms-date is an RFC 1123 date.
        var request = Authorizer().Authorize(Blob, AzureStorageCredential.Anonymous);

        Assert.AreEqual("/mycontainer/myblob", request.Target);
        CollectionAssert.AreEqual(
            new KeyValuePair<string, string>[]
            {
                new("Host", "myaccount.blob.core.windows.net"),
                new("x-ms-version", "2025-01-05"),
                new("x-ms-date", "Sun, 05 Jan 2025 10:20:30 GMT"),
            },
            request.Headers.ToArray());
    }

    [TestMethod]
    public void Authorize_Bearer_AddsAuthorizationBearer()
    {
        // Authorize with Microsoft Entra ID: Authorization: Bearer <token>, with x-ms-version 2017-11-09 or later.
        var request = Authorizer().Authorize(Blob, new AzureStorageCredential(AzureStorageCredentialKind.Bearer, "eyJ0eXAi.abc-_~+/="));

        Assert.AreEqual(new KeyValuePair<string, string>("Authorization", "Bearer eyJ0eXAi.abc-_~+/="), request.Headers[^1]);
        Assert.AreEqual("/mycontainer/myblob", request.Target);
    }

    [TestMethod]
    public void Authorize_SasWithQuestionMark_AppendsItAsTheQuery()
    {
        // Create a service SAS: the SAS token is the query string appended to the resource URI.
        var request = Authorizer().Authorize(Blob, Sas("?sv=2025-01-05&sr=b&sp=r&sig=abc%2Bdef%3D"));

        Assert.AreEqual("/mycontainer/myblob?sv=2025-01-05&sr=b&sp=r&sig=abc%2Bdef%3D", request.Target);
        Assert.AreEqual(3, request.Headers.Count);
    }

    [TestMethod]
    public void Authorize_SasWithExistingQuery_JoinsWithAmpersand()
    {
        var endpoint = Blob with { Query = "comp=metadata" };

        var request = Authorizer().Authorize(endpoint, Sas("sv=2025-01-05&sig=x"));

        Assert.AreEqual("/mycontainer/myblob?comp=metadata&sv=2025-01-05&sig=x", request.Target);
    }

    [TestMethod]
    public void Authorize_AnonymousWithQuery_KeepsTheQuery()
    {
        var request = Authorizer().Authorize(Blob with { Query = "comp=list" }, AzureStorageCredential.Anonymous);

        Assert.AreEqual("/mycontainer/myblob?comp=list", request.Target);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("?")]
    [DataRow("sv=2025-01-05&sr=b")]
    [DataRow("sv=2025-01-05&sig")]
    [DataRow("=x&sig=y")]
    [DataRow("sv=1&sig=a b")]
    [DataRow("sv=1&sig=a#b")]
    [DataRow("sv=1&sig=é")]
    [DataRow("https://myaccount.blob.core.windows.net/c?sig=x&sv")]
    public void Authorize_SasThatIsNotAQueryString_FailsAsInvalidSasToken(string token)
    {
        // Create a service SAS: the token is name=value parameters joined by &, and sig carries the signature.
        var exception = Assert.ThrowsExactly<AzureStorageRequestException>(() => Authorizer().Authorize(Blob, Sas(token)));

        Assert.AreEqual(AzureStorageRequestFailure.InvalidSasToken, exception.Failure);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("abc def")]
    [DataRow("abc\r\nX-Injected: 1")]
    [DataRow("abcé")]
    public void Authorize_BearerThatCannotBeAHeaderValue_FailsAsInvalidBearerToken(string token)
    {
        // RFC 6750 §2.1: b64token is visible ASCII; anything else could break the header field.
        var credential = new AzureStorageCredential(AzureStorageCredentialKind.Bearer, token);

        var exception = Assert.ThrowsExactly<AzureStorageRequestException>(() => Authorizer().Authorize(Blob, credential));

        Assert.AreEqual(AzureStorageRequestFailure.InvalidBearerToken, exception.Failure);
    }

    [TestMethod]
    public void Authorize_NullArguments_Throw()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => Authorizer().Authorize(null!, AzureStorageCredential.Anonymous));
        Assert.ThrowsExactly<ArgumentNullException>(() => Authorizer().Authorize(Blob, null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageRequestAuthorizer.SasQuery(null!));
    }

    private static AzureStorageCredential Sas(string token) => new(AzureStorageCredentialKind.SharedAccessSignature, token);

    private static AzureStorageRequestAuthorizer Authorizer() => new(new FixedTimeProvider());

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2025, 1, 5, 10, 20, 30, TimeSpan.Zero);
    }
}
