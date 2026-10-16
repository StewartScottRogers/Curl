namespace Curl.Engine;

/// <summary>
/// One node of a <c>curl_formadd</c> call's description, as curl 8.22.0's <c>lib/formdata.c</c> keeps its
/// <c>FormInfo</c>: the first node is the field, and each further <c>CURLFORM_FILE</c> or <c>CURLFORM_CONTENTTYPE</c>
/// given after a file adds a node for one more file of the same field.
/// </summary>
internal sealed class FormFieldInfo
{
    /// <summary>Gets or sets <c>CURLFORM_COPYNAME</c> or <c>CURLFORM_PTRNAME</c>: the field name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets <c>CURLFORM_NAMELENGTH</c>: the name's length in bytes, 0 for up to its first NUL.</summary>
    public long NameLength { get; set; }

    /// <summary>
    /// Gets or sets the node's value: the content (a <see cref="string" /> or <see cref="byte" /> array), the
    /// file's path, the buffer or the stream object, whichever option gave it.
    /// </summary>
    public object? Value { get; set; }

    /// <summary>Gets or sets <c>CURLFORM_CONTENTSLENGTH</c> or <c>CURLFORM_CONTENTLEN</c>: the content's length, 0 for up to its first NUL.</summary>
    public long ContentsLength { get; set; }

    /// <summary>Gets or sets the node's <c>HTTPPOST_*</c> flags.</summary>
    public FormFieldFlags Flags { get; set; }

    /// <summary>Gets or sets <c>CURLFORM_BUFFERPTR</c>: the buffer uploaded as a file.</summary>
    public byte[]? Buffer { get; set; }

    /// <summary>Gets or sets <c>CURLFORM_BUFFERLENGTH</c>: the buffer's length, 0 for up to its first NUL.</summary>
    public long BufferLength { get; set; }

    /// <summary>Gets or sets <c>CURLFORM_CONTENTTYPE</c>, or the type <c>curl_formadd</c> chose for a file.</summary>
    public string? ContentType { get; set; }

    /// <summary>Gets or sets <c>CURLFORM_CONTENTHEADER</c>: the extra header lines.</summary>
    public IReadOnlyList<string>? ContentHeader { get; set; }

    /// <summary>Gets or sets <c>CURLFORM_FILENAME</c> or <c>CURLFORM_BUFFER</c>: the file name sent.</summary>
    public string? ShowFileName { get; set; }

    /// <summary>Gets or sets <c>CURLFORM_STREAM</c>: the object the read callback is given.</summary>
    public object? StreamObject { get; set; }

    /// <summary>Whether the node has any of <paramref name="flags" /> set.</summary>
    /// <param name="flags">The flags to look for.</param>
    /// <returns>Whether any of <paramref name="flags" /> is set.</returns>
    public bool HasAny(FormFieldFlags flags) => (Flags & flags) != FormFieldFlags.None;
}
