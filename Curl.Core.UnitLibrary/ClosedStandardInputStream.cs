namespace Curl.Core;

/// <summary>
/// The stream standard input is when the process was started with it closed: it reads as end of
/// file, but a reader that must fail on it, as curl 8.22.0's <c>fread</c> on a closed <c>stdin</c>
/// fails, can tell it apart from an empty standard input by its type (CGR-110, ADR-0949).
/// </summary>
/// <remarks>
/// .NET hands back <see cref="Stream.Null"/> for a closed standard input, which reads as an empty
/// one, so <c>-d @-</c> would send an empty body where curl stops with exit 26.
/// <see cref="InPlaceOfNull(Stream)"/> swaps this stream in for it.
/// </remarks>
public sealed class ClosedStandardInputStream : Stream
{
    /// <summary>
    /// The stream to read standard input from: <paramref name="standardInput"/> itself, or a
    /// <see cref="ClosedStandardInputStream"/> when it is <see cref="Stream.Null"/>, which is how
    /// <see cref="Console.OpenStandardInput()"/> reports a closed standard input.
    /// </summary>
    /// <param name="standardInput">The stream <see cref="Console.OpenStandardInput()"/> returned.</param>
    /// <returns>The stream to read standard input from.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="standardInput"/> is <see langword="null"/>.</exception>
    public static Stream InPlaceOfNull(Stream standardInput) => InPlaceOfNull(standardInput, Console.IsInputRedirected);

    /// <summary>
    /// As <see cref="InPlaceOfNull(Stream)"/>, with whether standard input is redirected given. A
    /// terminal standard input is never probed: off Windows the probe read would run .NET's terminal
    /// setup, which writes to the terminal, and a closed descriptor always reports as redirected.
    /// </summary>
    /// <param name="standardInput">The stream <see cref="Console.OpenStandardInput()"/> returned.</param>
    /// <param name="isInputRedirected">Whether standard input is not a terminal.</param>
    /// <returns>The stream to read standard input from.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="standardInput"/> is <see langword="null"/>.</exception>
    public static Stream InPlaceOfNull(Stream standardInput, bool isInputRedirected)
    {
        ArgumentNullException.ThrowIfNull(standardInput);

        return ReferenceEquals(standardInput, Null) || (isInputRedirected && ReadsWithBadDescriptor(standardInput))
            ? new ClosedStandardInputStream()
            : standardInput;
    }

    /// <summary>
    /// Whether a zero-length read of <paramref name="standardInput"/> fails because the descriptor
    /// is closed (EBADF, which Linux reports for a closed standard input instead of handing back
    /// <see cref="Stream.Null"/>). Any other failure is not swallowed.
    /// </summary>
    private static bool ReadsWithBadDescriptor(Stream standardInput)
    {
        try
        {
            _ = standardInput.Read(Span<byte>.Empty);
            return false;
        }
        catch (Exception exception) when (IsBadDescriptor(exception))
        {
            return true;
        }
    }

    private static bool IsBadDescriptor(Exception exception)
    {
        const int badDescriptorErrno = 9;
        return exception is IOException { HResult: badDescriptorErrno }
            || exception.InnerException is IOException { HResult: badDescriptorErrno };
    }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    /// <returns>Always 0: end of file.</returns>
    public override int Read(byte[] buffer, int offset, int count) => 0;

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
