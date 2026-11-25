using Curl.Protocol.Abstractions;

namespace Curl.Extension.ResumableUpload.Transfer;

/// <summary>
/// Gives a <c>-T</c> upload as a stream that can be read again from any offset, as a resumable upload needs: both
/// protocols announce the upload's length on creation and a resumption sends again from the server's offset (ADR-1401).
/// </summary>
internal static class RereadableUpload
{
    private const int ChunkSize = 81920;

    /// <summary>
    /// Opens a rereadable view of what is left of <paramref name="upload" />: a window over a seekable upload from where
    /// it stands; otherwise a copy, in memory up to <paramref name="memoryLimit" /> bytes and in a spool file past that.
    /// </summary>
    /// <param name="upload">The transfer's upload.</param>
    /// <param name="memoryLimit">The most bytes of an unseekable upload held in memory.</param>
    /// <param name="createSpoolFile">Creates the empty, seekable stream a longer unseekable upload is copied into.</param>
    /// <param name="cancellationToken">Cancels the copy.</param>
    /// <returns>A seekable stream the caller disposes; disposing it removes the copy and leaves <paramref name="upload" /> open.</returns>
    /// <exception cref="ResumableUploadTransferException">
    /// <paramref name="upload" /> could not be read (<see cref="CurlExitCode.ReadError" />), or the spool file could not
    /// be created or written (<see cref="CurlExitCode.WriteError" />).
    /// </exception>
    public static async Task<Stream> OpenAsync(Stream upload, int memoryLimit, Func<Stream> createSpoolFile, CancellationToken cancellationToken)
    {
        if (upload.CanSeek)
        {
            return new UploadRemainderStream(upload, upload.Position);
        }

        MemoryStream? memory = new();
        Stream copy = memory;
        try
        {
            var chunk = new byte[ChunkSize];
            int read;
            while ((read = await ReadAsync(upload, chunk, cancellationToken)) > 0)
            {
                if (memory is not null && memory.Length + read > memoryLimit)
                {
                    copy = Create(createSpoolFile);
                    await WriteAsync(copy, memory.ToArray(), cancellationToken);
                    memory = null;
                }

                await WriteAsync(copy, chunk.AsMemory(0, read), cancellationToken);
            }

            return copy;
        }
        catch
        {
            copy.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates an empty file in the temporary directory, readable by its owner only on Linux and macOS, that is deleted
    /// when the stream is disposed.
    /// </summary>
    /// <returns>The file, open for reading and writing without a buffer.</returns>
    public static Stream CreateTemporaryFile() => new FileStream(
        Path.GetTempFileName(),
        new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.Asynchronous | FileOptions.DeleteOnClose,
            BufferSize = 0,
        });

    private static async ValueTask<int> ReadAsync(Stream upload, Memory<byte> chunk, CancellationToken cancellationToken)
    {
        try
        {
            return await upload.ReadAsync(chunk, cancellationToken);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            throw Failure(CurlExitCode.ReadError, "Failed reading the upload", exception);
        }
    }

    private static Stream Create(Func<Stream> createSpoolFile)
    {
        try
        {
            return createSpoolFile();
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            throw SpoolFailure(exception);
        }
    }

    private static async ValueTask WriteAsync(Stream copy, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        try
        {
            await copy.WriteAsync(bytes, cancellationToken);
        }
        catch (Exception exception) when (IsFileFailure(exception))
        {
            throw SpoolFailure(exception);
        }
    }

    private static bool IsFileFailure(Exception exception) => exception is IOException or UnauthorizedAccessException;

    private static ResumableUploadTransferException SpoolFailure(Exception exception) =>
        Failure(CurlExitCode.WriteError, "Failed writing the upload to a temporary file", exception);

    private static ResumableUploadTransferException Failure(CurlExitCode exitCode, string what, Exception exception) =>
        new(exitCode, $"{what}: {exception.Message}", canResume: false);
}
