namespace Curl.Extension.Checksum;

/// <summary>An incremental hash: bytes are appended as they arrive and the digest is taken once at the end.</summary>
internal interface IChecksumHash : IDisposable
{
    /// <summary>Adds bytes to the message being hashed.</summary>
    /// <param name="data">The bytes to add.</param>
    void AppendData(ReadOnlySpan<byte> data);

    /// <summary>Gives the digest of every byte appended and starts a new, empty message.</summary>
    /// <returns>The digest.</returns>
    byte[] GetHashAndReset();
}
