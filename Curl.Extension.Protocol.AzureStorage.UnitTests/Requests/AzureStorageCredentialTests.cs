namespace Curl.Extension.Protocol.AzureStorage.Requests;

/// <summary>Pins where an Azure Storage request's credential comes from.</summary>
[TestClass]
public sealed class AzureStorageCredentialTests
{
    [TestMethod]
    public void Choose_OAuth2Bearer_WinsOverBothVariables()
    {
        // Authorize with Microsoft Entra ID: the token is sent as a bearer token; curl's --oauth2-bearer names one.
        var credential = AzureStorageCredential.Choose("cli-token", Variables("env-token", "sv=1&sig=x"));

        Assert.AreEqual(new AzureStorageCredential(AzureStorageCredentialKind.Bearer, "cli-token"), credential);
    }

    [TestMethod]
    public void Choose_BearerVariable_WinsOverSasVariable()
    {
        var credential = AzureStorageCredential.Choose(string.Empty, Variables("env-token", "sv=1&sig=x"));

        Assert.AreEqual(new AzureStorageCredential(AzureStorageCredentialKind.Bearer, "env-token"), credential);
    }

    [TestMethod]
    public void Choose_SasVariableOnly_IsASharedAccessSignature()
    {
        var credential = AzureStorageCredential.Choose(null, Variables(string.Empty, "sv=1&sig=x"));

        Assert.AreEqual(new AzureStorageCredential(AzureStorageCredentialKind.SharedAccessSignature, "sv=1&sig=x"), credential);
    }

    [TestMethod]
    public void Choose_NothingSet_IsAnonymous()
    {
        Assert.AreSame(AzureStorageCredential.Anonymous, AzureStorageCredential.Choose(null, Variables(null, null)));
        Assert.AreSame(AzureStorageCredential.Anonymous, AzureStorageCredential.Choose(null, Variables(null, string.Empty)));
    }

    [TestMethod]
    public void Choose_NullReader_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => AzureStorageCredential.Choose(null, null!));
    }

    private static Func<string, string?> Variables(string? bearer, string? sas) =>
        name => name == AzureStorageCredential.BearerTokenVariable ? bearer : name == AzureStorageCredential.SasTokenVariable ? sas : null;
}
