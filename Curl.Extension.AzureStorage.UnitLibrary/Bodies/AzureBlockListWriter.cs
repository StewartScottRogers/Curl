using System.Text;
using System.Xml;

namespace Curl.Extension.AzureStorage.Bodies;

/// <summary>
/// Writes the <c>BlockList</c> request body of the Blob service REST API "Put Block List": one
/// <c>Latest</c>, <c>Committed</c> or <c>Uncommitted</c> element per block, in the order given (the
/// order the blob's blocks take), without an XML declaration or indentation.
/// </summary>
public static class AzureBlockListWriter
{
    /// <summary>Writes the body that commits <paramref name="blocks"/> as the blob's content.</summary>
    /// <param name="blocks">The blocks, in the order they make up the blob.</param>
    /// <returns>The request body.</returns>
    public static string Write(IEnumerable<AzureBlockListEntry> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        var body = new StringBuilder();
        using (var writer = XmlWriter.Create(body, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            writer.WriteStartElement("BlockList");
            foreach (var block in blocks)
            {
                writer.WriteElementString(block.Source.ToString(), block.BlockId);
            }

            writer.WriteEndElement();
        }

        return body.ToString();
    }
}
