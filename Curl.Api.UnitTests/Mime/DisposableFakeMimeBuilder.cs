namespace Curl.Api.Mime;

/// <summary>A <see cref="FakeMimeBuilder"/> that counts its disposals, as an engine's freeable MIME handle.</summary>
internal sealed class DisposableFakeMimeBuilder : FakeMimeBuilder, IDisposable
{
    public int Disposals { get; private set; }

    public void Dispose() => Disposals++;
}
