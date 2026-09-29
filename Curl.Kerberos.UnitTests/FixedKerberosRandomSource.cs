namespace Curl.Kerberos;

/// <summary>An <see cref="IKerberosRandomSource" /> that gives the same bytes every time: a published confounder, so encryption reproduces a test vector.</summary>
internal sealed class FixedKerberosRandomSource(byte[] bytes) : IKerberosRandomSource
{
    public void Fill(Span<byte> destination) => bytes.AsSpan(0, destination.Length).CopyTo(destination);
}
