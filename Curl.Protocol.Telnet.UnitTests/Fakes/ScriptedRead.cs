namespace Curl.Protocol.Telnet.Fakes;

/// <summary>
/// One read <see cref="ScriptedConnection" /> returns: the bytes the server sends, held
/// back until the client has sent at least <paramref name="AfterBytesSent" /> bytes, so a
/// test can make the server wait for the client as a real one would.
/// </summary>
/// <param name="Bytes">The bytes the read returns; never empty, since empty means closed.</param>
/// <param name="AfterBytesSent">How many bytes the client must have sent first.</param>
public sealed record ScriptedRead(byte[] Bytes, int AfterBytesSent = 0);
