namespace Curl.Conformance.Differential.Libcurl.Mime;

/// <summary>
/// The <c>curl_mime_*</c> call a MIME case step makes on a part, in the order the tester's
/// <c>mimetest.py</c> made them; each is logged as its label and the call's result code.
/// </summary>
public enum LibcurlMimeStepKind
{
    /// <summary><c>curl_mime_name</c>, logged as <c>name=</c>.</summary>
    Name,

    /// <summary><c>curl_mime_filename</c>, logged as <c>fn=</c>.</summary>
    FileName,

    /// <summary><c>curl_mime_type</c>, logged as <c>type=</c>.</summary>
    Type,

    /// <summary><c>curl_mime_encoder</c>, logged as <c>enc=</c>.</summary>
    Encoder,

    /// <summary><c>curl_mime_data</c> with the step's hexadecimal bytes, logged as <c>data=</c>.</summary>
    Data,

    /// <summary><c>curl_mime_filedata</c> with a file in the run's directory, logged as <c>file=</c>.</summary>
    File,

    /// <summary><c>curl_mime_headers</c> with the step's header lines, logged as <c>hdrs=</c>.</summary>
    Headers,

    /// <summary><c>curl_mime_subparts</c> with a MIME structure of the step's parts, logged as <c>sub=</c>.</summary>
    SubParts,
}
