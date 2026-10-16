namespace Curl.Api.EngineContracts.Mime;

/// <summary>
/// An option of the deprecated <c>curl_formadd</c> (<c>CURLformoption</c>, <c>CURLFORM_*</c>),
/// with upstream curl 8.22.0's numbers from <c>include/curl/curl.h</c>.
/// </summary>
public enum CurlFormOption
{
    /// <summary><c>CURLFORM_NOTHING</c> (0): no option.</summary>
    Nothing = 0,

    /// <summary><c>CURLFORM_COPYNAME</c> (1): the field name, copied.</summary>
    CopyName = 1,

    /// <summary><c>CURLFORM_PTRNAME</c> (2): the field name, referenced.</summary>
    PointerName = 2,

    /// <summary><c>CURLFORM_NAMELENGTH</c> (3): the field name's length.</summary>
    NameLength = 3,

    /// <summary><c>CURLFORM_COPYCONTENTS</c> (4): the field's content, copied.</summary>
    CopyContents = 4,

    /// <summary><c>CURLFORM_PTRCONTENTS</c> (5): the field's content, referenced.</summary>
    PointerContents = 5,

    /// <summary><c>CURLFORM_CONTENTSLENGTH</c> (6): the content's length, as a <c>long</c>.</summary>
    ContentsLength = 6,

    /// <summary><c>CURLFORM_FILECONTENT</c> (7): a file whose bytes are the field's content.</summary>
    FileContent = 7,

    /// <summary><c>CURLFORM_ARRAY</c> (8): more options, from a <c>curl_forms</c> array.</summary>
    Array = 8,

    /// <summary><c>CURLFORM_OBSOLETE</c> (9): unused.</summary>
    Obsolete = 9,

    /// <summary><c>CURLFORM_FILE</c> (10): a file uploaded as the field.</summary>
    File = 10,

    /// <summary><c>CURLFORM_BUFFER</c> (11): the file name of a buffer uploaded as a file.</summary>
    Buffer = 11,

    /// <summary><c>CURLFORM_BUFFERPTR</c> (12): the buffer uploaded as a file.</summary>
    BufferPointer = 12,

    /// <summary><c>CURLFORM_BUFFERLENGTH</c> (13): the buffer's length.</summary>
    BufferLength = 13,

    /// <summary><c>CURLFORM_CONTENTTYPE</c> (14): the field's <c>Content-Type</c>.</summary>
    ContentType = 14,

    /// <summary><c>CURLFORM_CONTENTHEADER</c> (15): the field's extra headers.</summary>
    ContentHeader = 15,

    /// <summary><c>CURLFORM_FILENAME</c> (16): the remote file name of a <see cref="File"/> upload.</summary>
    FileName = 16,

    /// <summary><c>CURLFORM_END</c> (17): the end of the option list.</summary>
    End = 17,

    /// <summary><c>CURLFORM_OBSOLETE2</c> (18): unused.</summary>
    Obsolete2 = 18,

    /// <summary><c>CURLFORM_STREAM</c> (19): content read through <c>CURLOPT_READFUNCTION</c>.</summary>
    Stream = 19,

    /// <summary><c>CURLFORM_CONTENTLEN</c> (20): the content's length, as a <c>curl_off_t</c>.</summary>
    ContentLength = 20,
}
