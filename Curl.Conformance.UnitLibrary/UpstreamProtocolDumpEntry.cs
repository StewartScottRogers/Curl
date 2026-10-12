namespace Curl.Conformance;

/// <summary>One write to an <see cref="UpstreamProtocolDump"/>.</summary>
/// <param name="Sequence">Its place in the order of every write, across servers.</param>
/// <param name="Bytes">The bytes written.</param>
internal readonly record struct UpstreamProtocolDumpEntry(long Sequence, byte[] Bytes);
