namespace Curl.Conformance.Differential.Libcurl.Formadd;

/// <summary>What a <c>curl_formadd</c> argument's value is, so each library's driver can pass it as its API takes it.</summary>
public enum LibcurlFormaddValueKind
{
    /// <summary>No value: the option is passed alone, as <c>CURLFORM_END</c> or an unknown option is.</summary>
    None,

    /// <summary>A string (<see cref="LibcurlFormaddArgument.Text"/>; <see langword="null"/> passes a null pointer).</summary>
    Text,

    /// <summary>Bytes given as hexadecimal in <see cref="LibcurlFormaddArgument.Text"/>, for contents and buffers that hold any byte.</summary>
    Bytes,

    /// <summary>A length (<see cref="LibcurlFormaddArgument.Number"/>).</summary>
    Length,

    /// <summary>The path of the file <see cref="LibcurlFormaddArgument.Text"/> names in the run's directory.</summary>
    File,

    /// <summary>A header list of <see cref="LibcurlFormaddArgument.Lines"/>.</summary>
    Headers,

    /// <summary>A <c>CURLFORM_ARRAY</c> of <see cref="LibcurlFormaddArgument.Array"/>.</summary>
    Array,

    /// <summary>An opaque non-null pointer, as <c>CURLFORM_STREAM</c> takes.</summary>
    Pointer,
}
