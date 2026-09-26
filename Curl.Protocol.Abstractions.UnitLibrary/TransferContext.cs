namespace Curl.Protocol.Abstractions;

/// <summary>
/// An immutable <see cref="ITransferContext" /> built with an object initializer: only
/// <see cref="Url" /> and <see cref="Output" /> are required, and every option not set
/// reports its "not given" value (ADR-0006).
/// </summary>
public sealed class TransferContext : ITransferContext
{
    /// <summary>
    /// curl's mode for a file an upload creates when <c>--create-file-mode</c> is not
    /// given: <c>0644</c>, read and write for the owner and read for everyone else.
    /// </summary>
    public const UnixFileMode DefaultCreateFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    /// <inheritdoc />
    public required Uri Url { get; init; }

    /// <inheritdoc />
    public required Stream Output { get; init; }

    /// <inheritdoc />
    public Stream? Upload { get; init; }

    /// <inheritdoc />
    public long? ResumeFrom { get; init; }

    /// <inheritdoc />
    public ByteRange? Range { get; init; }

    /// <inheritdoc />
    public bool NoBody { get; init; }

    /// <inheritdoc />
    public TimeCondition? TimeCondition { get; init; }

    /// <inheritdoc />
    public Stream? HeaderOutput { get; init; }

    /// <inheritdoc />
    public ReadOnlyMemory<byte>? PostData { get; init; }

    /// <inheritdoc />
    public System.Net.NetworkCredential? Credentials { get; init; }

    /// <inheritdoc />
    public IReadOnlyList<string> TelnetOptions { get; init; } = [];

    /// <inheritdoc />
    public int? TftpBlockSize { get; init; }

    /// <inheritdoc />
    public bool TftpNoOptions { get; init; }

    /// <inheritdoc />
    public bool ConvertLineEndings { get; init; }

    /// <inheritdoc />
    public UnixFileMode CreateFileMode { get; init; } = DefaultCreateFileMode;

    /// <inheritdoc />
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; init; }
}
