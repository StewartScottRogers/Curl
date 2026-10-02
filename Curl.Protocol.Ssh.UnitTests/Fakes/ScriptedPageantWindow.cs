using Curl.Protocol.Ssh.Authentication;

namespace Curl.Protocol.Ssh.Fakes;

/// <summary>
/// A Pageant window in memory: found or not as each call to <see cref="IsRunning" /> is
/// scripted, and answering each mapping with a function of its bytes.
/// </summary>
/// <param name="exchange">Rewrites the mapping in place and says whether the message returned nonzero.</param>
/// <param name="running">Whether the window is found, call by call; the last value repeats.</param>
internal sealed class ScriptedPageantWindow(Func<byte[], bool> exchange, params bool[] running) : IPageantWindow
{
    private int finds;

    /// <summary>Gets how many mappings were sent.</summary>
    internal int Exchanges { get; private set; }

    /// <summary>Gets a window that is not running.</summary>
    internal static ScriptedPageantWindow Absent => new(_ => throw new InvalidOperationException("No window to send to."), false);

    /// <summary>Gets a window that answers each request as <paramref name="agent" /> does.</summary>
    /// <param name="agent">The agent whose answers Pageant gives.</param>
    /// <returns>The window.</returns>
    internal static ScriptedPageantWindow AnsweringAs(InMemorySshAgent agent) => new(mapping => AnswerInPlace(mapping, agent.Answer), true);

    /// <inheritdoc />
    public bool IsRunning() => running[Math.Min(finds++, running.Length - 1)];

    /// <inheritdoc />
    public bool Exchange(byte[] mapping)
    {
        Exchanges++;
        return exchange(mapping);
    }

    /// <summary>Reads the request frame from the mapping and writes the answer frame over it, as Pageant does.</summary>
    /// <param name="mapping">The mapping.</param>
    /// <param name="answer">Answers one request body with one answer body.</param>
    /// <returns><see langword="true" />.</returns>
    internal static bool AnswerInPlace(byte[] mapping, Func<byte[], byte[]> answer)
    {
        int length = (int)System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(mapping);
        byte[] reply = answer(mapping[4..(4 + length)]);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(mapping, (uint)reply.Length);
        reply.CopyTo(mapping, 4);
        return true;
    }
}
