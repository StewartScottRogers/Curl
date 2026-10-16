using Curl.Api.Codes;
using Curl.Api.Options.Http;
using Libcurl.Mime;

namespace Curl.Console;

/// <summary>
/// Builds the <c>-F</c> and <c>--form-string</c> form, as <see cref="EasyOptionsFromCommandLine.HttpOf" /> maps it, into a
/// <c>curl_mime</c> tree with the <c>curl_mime_*</c> calls curl's tool's <c>tool2curlmime</c> makes, for the runner to
/// set as <c>CURLOPT_MIMEPOST</c> (BL-2493, ADR-0650).
/// </summary>
internal static class CurlMimeFromFormBody
{
    /// <summary>
    /// The form as a <see cref="CurlMime" /> from <paramref name="mimeInit" />, or <see langword="null" /> when there is
    /// none or a part cannot be built as the form holds it: a <c>@-</c> or <c>&lt;-</c> part (the tool's own standard
    /// input reader, which no <c>curl_mime_*</c> call makes) or an encoder <c>curl_mime_encoder</c> refuses. The form
    /// then stays on the hooks.
    /// </summary>
    /// <param name="mimeInit"><c>curl_mime_init</c> on the runner's library.</param>
    /// <param name="form">The form, or <see langword="null" />.</param>
    /// <returns>The built form, or <see langword="null" />.</returns>
    internal static CurlMime? Of(Func<CurlMime> mimeInit, EasyMimeBody? form)
    {
        if (form is null)
        {
            return null;
        }

        CurlMime mime = mimeInit();
        if (AddParts(mime, form, mimeInit))
        {
            return mime;
        }

        mime.Dispose();
        return null;
    }

    private static bool AddParts(CurlMime mime, EasyMimeBody body, Func<CurlMime> mimeInit) =>
        body.Parts.All(part => AddPart(mime.AddPart(), part, mimeInit));

    // curl_mime_name, _filename, _type and _headers answer CURLE_OK for any value. The file name comes after the data,
    // as curl_mime_filedata names the part after its path's base name and the form's name (or none) replaces it.
    private static bool AddPart(CurlMimePart target, EasyMimePart part, Func<CurlMime> mimeInit)
    {
        _ = target.SetName(part.Name);
        bool dataSet = SetDataOf(target, part, mimeInit);
        _ = target.SetFileName(part.FileName);
        _ = target.SetType(part.Type);
        _ = target.SetHeaders(part.Headers);
        return dataSet && target.SetEncoder(part.Encoder) == CurlCode.Ok;
    }

    // Only the tool's standard input reader cannot be set.
    private static bool SetDataOf(CurlMimePart target, EasyMimePart part, Func<CurlMime> mimeInit) => part switch
    {
        { SubParts: { } subParts } => SetSubPartsOf(target, subParts, mimeInit),
        { Data: EasyMimeStandardInput } => false,
        _ => SetBytesOrFile(target, part.Data),
    };

    // curl_mime_data answers CURLE_OK; a file that is not there is still set (curl_mime_filedata's CURLE_READ_ERROR is
    // only a warning), so the transfer fails reading it, as the tool's -F name=@file does.
    private static bool SetBytesOrFile(CurlMimePart target, EasyMimeData? data)
    {
        _ = data switch
        {
            EasyMimeBytes bytes => target.SetData(bytes.Bytes.ToArray()),
            EasyMimeFile file => target.SetFileData(file.Path),
            _ => CurlCode.Ok,
        };
        return true;
    }

    // curl_mime_subparts takes a new body no part holds yet without fail.
    private static bool SetSubPartsOf(CurlMimePart target, EasyMimeBody subParts, Func<CurlMime> mimeInit)
    {
        CurlMime nested = mimeInit();
        bool added = AddParts(nested, subParts, mimeInit);
        _ = target.SetSubParts(nested);
        return added;
    }
}
