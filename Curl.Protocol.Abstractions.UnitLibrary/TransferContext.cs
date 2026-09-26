namespace Curl.Protocol.Abstractions;

/// <summary>
/// An immutable <see cref="ITransferContext" /> built with an object initializer: only
/// <see cref="Url" /> and <see cref="Output" /> are required, and every option not set
/// reports its "not given" value (ADR-0006).
/// </summary>
public sealed class TransferContext : ITransferContext
{
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
    public TimeProvider TimeProvider { get; init; } = TimeProvider.System;

    /// <inheritdoc />
    public CancellationToken CancellationToken { get; init; }
}
