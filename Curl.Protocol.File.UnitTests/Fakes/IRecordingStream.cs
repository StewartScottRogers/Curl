namespace Curl.Protocol.File.Fakes;

/// <summary>
/// A fake stream that remembers whether the code under test disposed it. Every fake
/// stream in this project implements it, so a test can ask about disposal without
/// knowing which fake it was handed.
/// </summary>
public interface IRecordingStream
{
    /// <summary>
    /// Gets a value indicating whether this stream has been disposed.
    /// </summary>
    bool WasDisposed { get; }
}
