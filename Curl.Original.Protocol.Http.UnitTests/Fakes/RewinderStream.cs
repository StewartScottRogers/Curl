using Curl.Protocol.Abstractions;

namespace Curl.Original.Protocol.Http.Fakes;

/// <summary>
/// A body stream that cannot seek but rewinds to its start through <see cref="IUploadRewinder" />,
/// as libcurl's read callback does through its seek callback, counting each rewind and failing
/// each with <paramref name="failure" /> when one is given.
/// </summary>
/// <param name="content">The bytes it reads.</param>
/// <param name="failure">The message every rewind fails with, or <see langword="null" /> to rewind.</param>
public sealed class RewinderStream(byte[] content, string? failure = null) : MemoryStream(content), IUploadRewinder
{
    /// <summary>Gets how many times <see cref="RewindToStart" /> was called.</summary>
    public int Rewinds { get; private set; }

    /// <inheritdoc />
    public override bool CanSeek => false;

    /// <inheritdoc />
    public bool HasBeenRead => Position > 0;

    /// <inheritdoc />
    public string? RewindToStart()
    {
        Rewinds++;
        if (failure is null)
        {
            Position = 0;
        }

        return failure;
    }
}
