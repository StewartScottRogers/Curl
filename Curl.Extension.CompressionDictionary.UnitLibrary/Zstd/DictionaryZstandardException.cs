using Curl.Zstandard;

namespace Curl.Extension.CompressionDictionary.Zstd;

/// <summary>A <c>dcz</c> body's Zstandard frames could not be decoded: <see cref="Error" /> says why.</summary>
public sealed class DictionaryZstandardException : Exception
{
    /// <summary>Creates the exception for a frame that breaks RFC 8878 or RFC 9842 with <paramref name="error" />.</summary>
    public DictionaryZstandardException(ZstandardDecodeError error)
        : base($"The dictionary-compressed Zstandard frame failed to decode: {error}.") => Error = error;

    /// <summary>Creates the exception for a body that ends in the middle of a frame.</summary>
    public DictionaryZstandardException()
        : base("The dictionary-compressed Zstandard body ends in the middle of a frame.")
    {
    }

    /// <summary>Why a frame failed, named after the libzstd error code; <see langword="null" /> when the body ends mid-frame.</summary>
    public ZstandardDecodeError? Error { get; }
}
