namespace Curl.Engine;

/// <summary>
/// The <c>HTTPPOST_*</c> flags curl 8.22.0's <c>lib/formdata.c</c> keeps on one node of a <c>curl_formadd</c> call
/// that change what the field sends or which option combinations it refuses.
/// </summary>
[Flags]
internal enum FormFieldFlags
{
    /// <summary>No flag.</summary>
    None = 0,

    /// <summary><c>HTTPPOST_FILENAME</c>: <c>CURLFORM_FILE</c>, a file uploaded as the field.</summary>
    FileName = 1,

    /// <summary><c>HTTPPOST_READFILE</c>: <c>CURLFORM_FILECONTENT</c>, a file whose bytes are the field's content.</summary>
    ReadFile = 2,

    /// <summary><c>HTTPPOST_PTRCONTENTS</c>: <c>CURLFORM_PTRCONTENTS</c>, the content given by reference.</summary>
    PointerContents = 4,

    /// <summary><c>HTTPPOST_BUFFER</c>: <c>CURLFORM_BUFFERPTR</c>, a buffer uploaded as a file.</summary>
    Buffer = 8,

    /// <summary><c>HTTPPOST_CALLBACK</c>: <c>CURLFORM_STREAM</c>, content read through <c>CURLOPT_READFUNCTION</c>.</summary>
    Callback = 16,
}
