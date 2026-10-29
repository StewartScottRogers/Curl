namespace Curl.Extension.NamedPipe;

/// <summary>A pipe opener with no pipe behind it: it hands out <see cref="RecordingPipeStream" />s, or throws what it was given.</summary>
internal sealed class FakeNamedPipeOpener(Exception? failure = null) : INamedPipeOpener
{
    /// <summary>Gets the server's bytes each opened pipe reads.</summary>
    public byte[] ServerBytes { get; init; } = [];

    /// <summary>Gets the names opened, in order.</summary>
    public List<NamedPipeName> Opened { get; } = [];

    /// <summary>Gets the last pipe opened.</summary>
    public RecordingPipeStream? LastPipe { get; private set; }

    public ValueTask<Stream> OpenAsync(NamedPipeName name, CancellationToken cancellationToken)
    {
        Opened.Add(name);
        if (failure is not null)
        {
            throw failure;
        }

        LastPipe = new RecordingPipeStream(ServerBytes);
        return ValueTask.FromResult<Stream>(LastPipe);
    }
}
