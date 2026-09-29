namespace Curl.Cryptography;

/// <summary>
/// The extendable-output functions SHAKE128 and SHAKE256 (FIPS 202 section 6.2),
/// hand-built because the BCL's <c>Shake128</c> and <c>Shake256</c> are not supported on
/// macOS (ADR-0118). The static <c>HashData</c> methods give one output of any length; an
/// instance absorbs with <see cref="AppendData" /> and then reads its output a piece at a
/// time with <see cref="Read" />, each piece continuing where the last ended, as ML-KEM's
/// XOF and ML-DSA's sampling need.
/// </summary>
/// <remarks>
/// Constant-time: the running time depends only on the lengths of the input and output.
/// <see cref="Dispose" /> zeroes the state.
/// </remarks>
public sealed class Shake : IDisposable
{
    private const byte DomainPadding = 0x1F;
    private const int Shake128Rate = 168;
    private const int Shake256Rate = 136;

    private readonly KeccakSponge sponge;
    private bool disposed;

    private Shake(int rate)
    {
        sponge = new KeccakSponge(rate, DomainPadding);
    }

    /// <summary>Creates an empty SHAKE128 instance.</summary>
    public static Shake Create128() => new(Shake128Rate);

    /// <summary>Creates an empty SHAKE256 instance.</summary>
    public static Shake Create256() => new(Shake256Rate);

    /// <summary>
    /// Fills <paramref name="destination" /> with SHAKE128 of <paramref name="source" />;
    /// its length is the output length.
    /// </summary>
    public static void HashData128(ReadOnlySpan<byte> source, Span<byte> destination) =>
        HashData(Shake128Rate, source, destination);

    /// <summary>
    /// Fills <paramref name="destination" /> with SHAKE256 of <paramref name="source" />;
    /// its length is the output length.
    /// </summary>
    public static void HashData256(ReadOnlySpan<byte> source, Span<byte> destination) =>
        HashData(Shake256Rate, source, destination);

    /// <summary>Absorbs <paramref name="data" /> after everything appended before it.</summary>
    /// <exception cref="InvalidOperationException"><see cref="Read" /> has already been called since the last <see cref="Reset" />.</exception>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void AppendData(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        sponge.Absorb(data);
    }

    /// <summary>
    /// Fills <paramref name="destination" /> with the next bytes of the output of everything
    /// appended; after the first call nothing more can be appended until <see cref="Reset" />.
    /// </summary>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void Read(Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        sponge.Squeeze(destination);
    }

    /// <summary>Returns the instance to empty, as if just created.</summary>
    /// <exception cref="ObjectDisposedException">The instance has been disposed.</exception>
    public void Reset()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        sponge.Reset();
    }

    /// <summary>Zeroes the state; every later call throws <see cref="ObjectDisposedException" />.</summary>
    public void Dispose()
    {
        sponge.Dispose();
        disposed = true;
    }

    private static void HashData(int rate, ReadOnlySpan<byte> source, Span<byte> destination)
    {
        using KeccakSponge sponge = new(rate, DomainPadding);
        sponge.Absorb(source);
        sponge.Squeeze(destination);
    }
}
