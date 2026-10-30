namespace Curl.Protobuf.Descriptors;

/// <summary>A file as <see cref="ProtoFileSetBuilder" /> read it, with the token each type reference was read at.</summary>
/// <param name="File">The file and the path it was read by.</param>
/// <param name="ReferenceTokens">The first token of each type reference, by reference identity.</param>
internal sealed record ProtoParsedFile(ProtoFile File, IReadOnlyDictionary<ProtoTypeReference, ProtoToken> ReferenceTokens);
