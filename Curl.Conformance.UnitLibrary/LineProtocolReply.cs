namespace Curl.Conformance;

/// <summary>What a line-protocol stand-in sends in answer to one command line.</summary>
/// <param name="Bytes">The reply bytes, CRLFs included; empty to send nothing.</param>
/// <param name="ClosesConnection">Whether the server closes the connection once the reply is sent, and reads nothing more.</param>
internal readonly record struct LineProtocolReply(ReadOnlyMemory<byte> Bytes, bool ClosesConnection);
