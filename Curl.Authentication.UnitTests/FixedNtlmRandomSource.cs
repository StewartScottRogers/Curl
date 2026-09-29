using Curl.Ntlm;

namespace Curl.Authentication;

/// <summary>An <see cref="INtlmRandomSource" /> that fills every request with the leading bytes it holds.</summary>
internal sealed class FixedNtlmRandomSource(byte[] bytes) : INtlmRandomSource
{
    public void Fill(Span<byte> destination) => bytes.AsSpan(0, destination.Length).CopyTo(destination);
}
