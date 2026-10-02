namespace Curl.Networking.Fakes;

/// <summary>Answers every ECHConfigList lookup with one list and records what was asked.</summary>
/// <param name="answer">The list returned, or <see langword="null" /> for none.</param>
public sealed class FakeEchConfigListLookup(byte[]? answer) : IEchConfigListLookup
{
    /// <summary>Gets each host and port asked for, in order.</summary>
    public List<(string Host, int Port)> Asked { get; } = [];

    /// <inheritdoc />
    public ValueTask<byte[]?> FindEchConfigListAsync(string host, int port, CancellationToken cancellationToken)
    {
        Asked.Add((host, port));
        return ValueTask.FromResult(answer);
    }
}
