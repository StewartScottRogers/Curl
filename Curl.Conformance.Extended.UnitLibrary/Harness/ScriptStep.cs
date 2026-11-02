using System.Text;

namespace Curl.Conformance.Extended.Harness;

/// <summary>One step of a <see cref="ScriptedReferenceServer" />'s script: wait for bytes from curl, or send bytes to it.</summary>
public sealed record ScriptStep
{
    private ScriptStep(bool sends, byte[] bytes)
    {
        Sends = sends;
        Bytes = bytes;
    }

    /// <summary>Gets a value indicating whether the step sends <see cref="Bytes" /> rather than waits for them.</summary>
    public bool Sends { get; }

    /// <summary>Gets the bytes sent, or the bytes that end what the step waits for.</summary>
    public ReadOnlyMemory<byte> Bytes { get; }

    /// <summary>Reads until what curl sent since the last such step ends with <paramref name="terminator" />.</summary>
    /// <param name="terminator">The bytes that end the wait, as Latin-1 text.</param>
    /// <returns>The step.</returns>
    public static ScriptStep ReceiveUntil(string terminator)
    {
        ArgumentException.ThrowIfNullOrEmpty(terminator);
        return new ScriptStep(sends: false, Encoding.Latin1.GetBytes(terminator));
    }

    /// <summary>Sends <paramref name="text" /> as Latin-1 bytes.</summary>
    /// <param name="text">The text to send.</param>
    /// <returns>The step.</returns>
    public static ScriptStep Send(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Send(Encoding.Latin1.GetBytes(text));
    }

    /// <summary>Sends <paramref name="bytes" />.</summary>
    /// <param name="bytes">The bytes to send.</param>
    /// <returns>The step.</returns>
    public static ScriptStep Send(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        return new ScriptStep(sends: true, bytes);
    }
}
