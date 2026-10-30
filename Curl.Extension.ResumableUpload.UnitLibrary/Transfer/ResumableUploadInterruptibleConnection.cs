using System.Net;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// Passes an <see cref="IConnection" /> through, turning a broken connection into a resumable
/// <see cref="ResumableUploadTransferException" />: curl's <see cref="CurlExitCode.SendError" /> (55) while sending and
/// <see cref="CurlExitCode.RecvError" /> (56) while receiving.
/// </summary>
/// <param name="inner">The connection curl's connector opened.</param>
internal sealed class ResumableUploadInterruptibleConnection(IConnection inner) : IConnection
{
    /// <inheritdoc />
    public bool IsSecure => inner.IsSecure;

    /// <inheritdoc />
    public EndPoint? RemoteEndPoint => inner.RemoteEndPoint;

    /// <inheritdoc />
    public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.ReadAsync(buffer, cancellationToken);
        }
        catch (IOException exception)
        {
            throw Interrupted(CurlExitCode.RecvError, "Recv failure", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
    {
        try
        {
            await inner.WriteAsync(buffer, cancellationToken);
        }
        catch (IOException exception)
        {
            throw Interrupted(CurlExitCode.SendError, "Send failure", exception);
        }
    }

    /// <inheritdoc />
    public async ValueTask FlushAsync(CancellationToken cancellationToken)
    {
        try
        {
            await inner.FlushAsync(cancellationToken);
        }
        catch (IOException exception)
        {
            throw Interrupted(CurlExitCode.SendError, "Send failure", exception);
        }
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private static ResumableUploadTransferException Interrupted(CurlExitCode exitCode, string failure, IOException exception) =>
        new(exitCode, $"{failure}: {exception.Message}", canResume: true);
}
