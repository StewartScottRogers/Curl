using System.Globalization;
using System.Text;
using System.Xml;

namespace Curl.Extension.Protocol.S3.Xml;

/// <summary>
/// Writes the <c>CompleteMultipartUpload</c> request body of the Amazon S3 API reference
/// "CompleteMultipartUpload": one <c>Part</c> (<c>PartNumber</c>, <c>ETag</c>) per uploaded part, in
/// ascending part order, in the S3 namespace, without an XML declaration or indentation.
/// </summary>
public static class S3CompleteMultipartUploadWriter
{
    /// <summary>Writes the body that completes an upload made of <paramref name="parts"/>.</summary>
    /// <param name="parts">The uploaded parts, in any order.</param>
    /// <returns>The request body.</returns>
    public static string Write(IEnumerable<S3UploadedPart> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var body = new StringBuilder();
        using (var writer = XmlWriter.Create(body, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            writer.WriteStartElement("CompleteMultipartUpload", S3XmlBody.Namespace);
            foreach (var part in parts.OrderBy(part => part.PartNumber))
            {
                writer.WriteStartElement("Part", S3XmlBody.Namespace);
                writer.WriteElementString("PartNumber", S3XmlBody.Namespace, part.PartNumber.ToString(CultureInfo.InvariantCulture));
                writer.WriteElementString("ETag", S3XmlBody.Namespace, part.ETag);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        return body.ToString();
    }
}
