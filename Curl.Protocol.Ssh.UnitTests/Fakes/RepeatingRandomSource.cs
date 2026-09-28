namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// An <see cref="ISshRandomSource" /> that fills every buffer with one byte, so every
/// cookie and every padding a test sees is known in advance.
/// </summary>
/// <param name="value">The byte every buffer is filled with.</param>
public sealed class RepeatingRandomSource(byte value) : ISshRandomSource
{
    /// <inheritdoc />
    public void Fill(Span<byte> destination) => destination.Fill(value);
}
