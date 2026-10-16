using System.Text;
using Curl.Api.Codes;
using Curl.Api.Options.Http;

namespace Curl.Engine;

/// <summary>
/// One field a successful <c>curl_formadd</c> call added (upstream's <c>curl_httppost</c> and the files on its
/// <c>more</c> chain), and the <see cref="EasyMimePart" /> curl 8.22.0's <c>Curl_getformdata</c> turns it into.
/// </summary>
internal sealed class FormPost
{
    private const string FileContentTypeDefault = "application/octet-stream";

    private const string StandardInputPath = "-";

    /// <summary>The content types <c>Curl_mime_contenttype</c> knows, by file name ending, matched in any case.</summary>
    private static readonly (string Extension, string ContentType)[] ContentTypesByExtension =
    [
        (".gif", "image/gif"),
        (".jpg", "image/jpeg"),
        (".jpeg", "image/jpeg"),
        (".png", "image/png"),
        (".svg", "image/svg+xml"),
        (".txt", "text/plain"),
        (".htm", "text/html"),
        (".html", "text/html"),
        (".pdf", "application/pdf"),
        (".xml", "application/xml"),
    ];

    private readonly IReadOnlyList<FormFieldInfo> nodes;
    private readonly string name;

    private FormPost(IReadOnlyList<FormFieldInfo> nodes, string name)
    {
        this.nodes = nodes;
        this.name = name;
    }

    private FormFieldInfo Field => nodes[0];

    /// <summary>
    /// Checks a call's nodes as <c>FormAdd</c> does once every option is read, giving a file node with no type the
    /// one its file name ends in, else the type of the node before, else <c>application/octet-stream</c>.
    /// </summary>
    /// <param name="call">The call whose options were all read.</param>
    /// <param name="post">The field, or <see langword="null" /> when the check fails.</param>
    /// <returns>
    /// <see cref="CurlFormAddCode.Ok" />; <see cref="CurlFormAddCode.Incomplete" /> for a field with no name or value,
    /// or a file with a content length; <see cref="CurlFormAddCode.Null" /> for a NUL within the
    /// name's <c>CURLFORM_NAMELENGTH</c>.
    /// </returns>
    internal static CurlFormAddCode Complete(FormAddCall call, out FormPost? post)
    {
        post = null;
        FormFieldInfo field = call.Nodes[0];
        if (field.Name is null || field.Value is null || IsFileWithLength(field))
        {
            return CurlFormAddCode.Incomplete;
        }

        string? previousType = TypeOf(field, null);
        if (NameOf(field) is not { } name)
        {
            return CurlFormAddCode.Null;
        }

        CurlFormAddCode code = CompleteFiles(call.Nodes.Skip(1), previousType);
        post = code == CurlFormAddCode.Ok ? new FormPost(call.Nodes, name) : null;
        return code;
    }

    /// <summary>Checks and types the files after the field's own, in order.</summary>
    /// <returns><see cref="CurlFormAddCode.Ok" />, or <see cref="CurlFormAddCode.Incomplete" /> for a file with a content length.</returns>
    private static CurlFormAddCode CompleteFiles(IEnumerable<FormFieldInfo> files, string? previousType)
    {
        foreach (FormFieldInfo file in files)
        {
            if (IsFileWithLength(file))
            {
                return CurlFormAddCode.Incomplete;
            }

            previousType = TypeOf(file, previousType);
        }

        return CurlFormAddCode.Ok;
    }

    private static bool IsFileWithLength(FormFieldInfo node) => node.ContentsLength != 0 && node.HasAny(FormFieldFlags.FileName);

    /// <summary>Gives a file or buffer node with no type its guessed one, else the type before, else the default.</summary>
    /// <returns>The type the next node falls back on.</returns>
    private static string? TypeOf(FormFieldInfo node, string? previousType)
    {
        if (node.HasAny(FormFieldFlags.FileName | FormFieldFlags.Buffer))
        {
            node.ContentType ??= GuessContentType(node) ?? previousType ?? FileContentTypeDefault;
        }

        return node.ContentType ?? previousType;
    }

    /// <summary>
    /// The part <c>Curl_getformdata</c> makes of the field: the one file or value as a part named by the field, or,
    /// for several files, a part named by the field whose <c>multipart/mixed</c> sub-parts are the files.
    /// </summary>
    /// <returns>The part.</returns>
    internal EasyMimePart ToMimePart() =>
        nodes.Count == 1
            ? PartOf(Field, name)
            : new EasyMimePart { Name = name, SubParts = new EasyMimeBody { Parts = [.. nodes.Select(node => PartOf(node, null))] } };

    private static string? GuessContentType(FormFieldInfo node)
    {
        string? fileName = node.HasAny(FormFieldFlags.Buffer) ? node.ShowFileName : node.Value as string;
        return fileName is null
            ? null
            : ContentTypesByExtension.FirstOrDefault(entry => fileName.EndsWith(entry.Extension, StringComparison.OrdinalIgnoreCase)).ContentType;
    }

    /// <summary>
    /// The field's name as <c>setname</c> sends it: its UTF-8 bytes up to <c>CURLFORM_NAMELENGTH</c>, or up to the
    /// first NUL without one.
    /// </summary>
    /// <returns>The name, or <see langword="null" /> when a NUL falls within <c>CURLFORM_NAMELENGTH</c>.</returns>
    private static string? NameOf(FormFieldInfo field)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(field.Name!);
        if (field.NameLength == 0)
        {
            return Encoding.UTF8.GetString(UpToNul(bytes));
        }

        ReadOnlySpan<byte> named = bytes.AsSpan(0, (int)Math.Min(field.NameLength, bytes.Length));
        return named.Contains((byte)0) ? null : Encoding.UTF8.GetString(named);
    }

    private static ReadOnlySpan<byte> UpToNul(byte[] bytes)
    {
        int nul = Array.IndexOf(bytes, (byte)0);
        return bytes.AsSpan(0, nul < 0 ? bytes.Length : nul);
    }

    /// <summary>The first <paramref name="length" /> bytes, or up to the first NUL for 0: <c>curl_mime_data</c>'s <c>CURL_ZERO_TERMINATED</c>.</summary>
    private static EasyMimeBytes BytesOf(byte[] bytes, long length) =>
        new(length == 0 ? UpToNul(bytes).ToArray() : bytes.AsSpan(0, (int)Math.Min(length, bytes.Length)).ToArray());

    private EasyMimePart PartOf(FormFieldInfo file, string? partName) =>
        new()
        {
            Name = partName,
            Type = file.ContentType,
            Headers = file.ContentHeader ?? [],
            Data = DataOf(file),
            FileName = FileNameOf(file),
        };

    /// <summary>
    /// The part's content as <c>Curl_getformdata</c> sets it from the field's flags: a file (standard input for
    /// <c>-</c>), the buffer, nothing for a stream (the easy handle's read callback is not known here, as
    /// <c>curl_formget</c> has none either), else the value.
    /// </summary>
    private EasyMimeData? DataOf(FormFieldInfo file)
    {
        if (Field.HasAny(FormFieldFlags.FileName | FormFieldFlags.ReadFile))
        {
            return FileDataOf(file.Value);
        }

        if (Field.HasAny(FormFieldFlags.Buffer))
        {
            return BytesOf(Field.Buffer!, Field.BufferLength);
        }

        return Field.HasAny(FormFieldFlags.Callback) ? null : BytesOf(ValueBytesOf(file.Value!), Field.ContentsLength);
    }

    private static EasyMimeData? FileDataOf(object? path) =>
        path switch
        {
            StandardInputPath => new EasyMimeStandardInput(),
            string file => new EasyMimeFile(file),
            _ => null,
        };

    private static byte[] ValueBytesOf(object value) => value as byte[] ?? Encoding.UTF8.GetBytes((string)value);

    /// <summary>
    /// The part's file name: <c>CURLFORM_FILENAME</c> or <c>CURLFORM_BUFFER</c> for a file, buffer, stream or one of
    /// several files, else a file's base name, as <c>curl_mime_filedata</c> sets it, and none for file content.
    /// </summary>
    private string? FileNameOf(FormFieldInfo file)
    {
        if (Field.ShowFileName is { } shown && SendsShownFileName())
        {
            return shown;
        }

        return Field.HasAny(FormFieldFlags.FileName) && file.Value is string path and not StandardInputPath ? Path.GetFileName(path) : null;
    }

    private bool SendsShownFileName() => nodes.Count > 1 || Field.HasAny(FormFieldFlags.FileName | FormFieldFlags.Buffer | FormFieldFlags.Callback);
}
