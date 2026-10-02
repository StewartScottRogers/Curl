namespace Curl.Protocol.Abstractions;

/// <summary>
/// Hands out connection numbers, <c>0</c>, <c>1</c> and on, to a handler that numbers a
/// transfer as a connection without connecting through an <see cref="IConnector" />: curl
/// 8.21.0 numbers a <c>file://</c> transfer in the one count shared with every connection the
/// run opens, so <c>curl -v http://h/ file:///a</c> shuts down <c>#1</c> for the file
/// (measured, BL-977; ADR-0109).
/// </summary>
public interface IConnectionNumbers
{
    /// <summary>Takes the next connection number.</summary>
    /// <returns>The number, counting from <c>0</c>.</returns>
    long NumberNextConnection();
}
