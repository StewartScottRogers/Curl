namespace Curl.Conformance.Extended.Harness;

/// <summary>
/// A UDP reference server that answers the Nth datagram curl sends with the Nth answer, and stays
/// silent past the last; the harness records what curl sent.
/// </summary>
/// <param name="answers">The answers, one per datagram curl sends, in order.</param>
public sealed class ScriptedDatagramServer(params IReadOnlyList<byte[]> answers) : IDatagramReferenceServer
{
    private int received;

    /// <inheritdoc />
    public IReadOnlyList<ReadOnlyMemory<byte>> Answer(ReadOnlyMemory<byte> datagram)
    {
        int index = Interlocked.Increment(ref received) - 1;
        return index < answers.Count ? [answers[index]] : [];
    }
}
