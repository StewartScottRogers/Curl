namespace Curl.Extension.Protocol.AzureStorage.Requests;

/// <summary>How an Azure Storage request is authorized.</summary>
public enum AzureStorageCredentialKind
{
    /// <summary>No authorization: anonymous read access to a public container.</summary>
    Anonymous,

    /// <summary>A shared access signature appended to the query ("Create a service SAS", "Create an account SAS").</summary>
    SharedAccessSignature,

    /// <summary>A Microsoft Entra ID OAuth 2.0 access token sent as <c>Authorization: Bearer</c> ("Authorize with Microsoft Entra ID").</summary>
    Bearer,
}
