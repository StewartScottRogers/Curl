using System.Xml.Linq;

namespace Curl.Extension.Protocol.AzureStorage.Bodies;

/// <summary>
/// Reads the <c>Error</c> body of the Azure Storage REST API "Status and error codes": its <c>Code</c>
/// and <c>Message</c>.
/// </summary>
public static class AzureStorageErrorParser
{
    /// <summary>Reads <paramref name="xml"/> as the error Azure Storage returned.</summary>
    /// <param name="xml">The response body.</param>
    /// <returns>The error, or why the body could not be read.</returns>
    public static AzureStorageBodyParseResult<AzureStorageError> Parse(string xml) =>
        AzureStorageXmlBody.Parse(xml, "Error", ReadError);

    private static AzureStorageError ReadError(XElement root) => new(
        AzureStorageXmlBody.RequiredText(root, "Code"),
        AzureStorageXmlBody.OptionalText(root, "Message") ?? string.Empty);
}
