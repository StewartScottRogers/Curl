namespace Curl.Extension.VirtualMachineSocket;

/// <summary>A socket opener with no socket behind it: it hands out <see cref="RecordingSocketStream" />s, or throws what it was given.</summary>
internal sealed class FakeVirtualMachineSocketOpener(Exception? failure = null) : IVirtualMachineSocketOpener
{
    /// <summary>Gets the server's bytes each opened socket reads.</summary>
    public byte[] ServerBytes { get; init; } = [];

    /// <summary>Gets the addresses opened, in order.</summary>
    public List<VirtualMachineSocketAddress> Opened { get; } = [];

    /// <summary>Gets the last socket opened.</summary>
    public RecordingSocketStream? LastSocket { get; private set; }

    public ValueTask<Stream> OpenAsync(VirtualMachineSocketAddress address, CancellationToken cancellationToken)
    {
        Opened.Add(address);
        if (failure is not null)
        {
            throw failure;
        }

        LastSocket = new RecordingSocketStream(ServerBytes);
        return ValueTask.FromResult<Stream>(LastSocket);
    }
}
