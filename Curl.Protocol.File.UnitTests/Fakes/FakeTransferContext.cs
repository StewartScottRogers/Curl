using Curl.Protocol.Abstractions;

namespace Curl.Protocol.File.Fakes;

/// <summary>
/// A settable <see cref="ITransferContext" />, so each test states only the options it is
/// about.
/// </summary>
public sealed class FakeTransferContext : ITransferContext
{
    /// <inheritdoc cref="ITransferContext.Url" />
    public Uri Url { get; set; } = new("file:///C:/dir/hello.txt");

    /// <inheritdoc cref="ITransferContext.Output" />
    public Stream Output { get; set; } = new ChunkRecordingStream();

    /// <inheritdoc cref="ITransferContext.Upload" />
    public Stream? Upload { get; set; }

    /// <inheritdoc cref="ITransferContext.ResumeFrom" />
    public long? ResumeFrom { get; set; }

    /// <inheritdoc cref="ITransferContext.Range" />
    public ByteRange? Range { get; set; }

    /// <inheritdoc cref="ITransferContext.NoBody" />
    public bool NoBody { get; set; }

    /// <inheritdoc cref="ITransferContext.TimeCondition" />
    public TimeCondition? TimeCondition { get; set; }

    /// <inheritdoc cref="ITransferContext.HeaderOutput" />
    public Stream? HeaderOutput { get; set; }

    /// <inheritdoc cref="ITransferContext.PostData" />
    public ReadOnlyMemory<byte>? PostData { get; set; }

    /// <inheritdoc cref="ITransferContext.Credentials" />
    public System.Net.NetworkCredential? Credentials { get; set; }

    /// <inheritdoc cref="ITransferContext.TelnetOptions" />
    public IReadOnlyList<string> TelnetOptions { get; set; } = [];

    /// <inheritdoc cref="ITransferContext.TftpBlockSize" />
    public int? TftpBlockSize { get; set; }

    /// <inheritdoc cref="ITransferContext.TftpNoOptions" />
    public bool TftpNoOptions { get; set; }

    /// <inheritdoc cref="ITransferContext.TimeProvider" />
    public TimeProvider TimeProvider { get; set; } =
        new FakeTimeProvider(FakeFileSystem.DefaultLastWriteTimeUtc);

    /// <inheritdoc cref="ITransferContext.CancellationToken" />
    public CancellationToken CancellationToken { get; set; }
}
