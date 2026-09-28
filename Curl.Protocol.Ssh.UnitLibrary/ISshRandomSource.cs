namespace Curl.Protocol.Ssh;

/// <summary>
/// The random bytes an SSH session needs: the <c>KEXINIT</c> cookie, packet padding and
/// ephemeral key-exchange keys (ADR-0122). Injected so a test can make every session's
/// bytes reproducible.
/// </summary>
public interface ISshRandomSource
{
    /// <summary>
    /// Fills <paramref name="destination" /> with random bytes.
    /// </summary>
    /// <param name="destination">The bytes to overwrite.</param>
    void Fill(Span<byte> destination);
}
