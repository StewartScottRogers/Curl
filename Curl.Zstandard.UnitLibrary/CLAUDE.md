# Curl.Zstandard.UnitLibrary

Hand-built Zstandard (RFC 8878) for every platform, per ADR-0185: the frame decoder and
the XXH64 hash its content checksums use. The base class library has no Zstandard
decoder, and two layers need one - HTTP content decoding (`Content-Encoding: zstd` in
`Curl.Protocol.Http.UnitLibrary`) and TLS certificate decompression (RFC 8879 in
`Curl.Tls.UnitLibrary`) - so it lives in neither and both may reference it (ADR-0120's
table, as amended by ADR-0185).

Namespace `Curl.Zstandard`. The project is empty so far (BL-857); the decoder and XXH64
arrive in the tasks ADR-0185 lists.

## Rules

- **Base class library only; references nothing.** No package and no project reference.
  `ProtocolIsolationTests` in `Curl.Protocol.Abstractions.UnitTests` fails the build's
  tests if this project references any other.
- **Bytes in, bytes out.** The library opens no file, socket or stream of its own; its
  callers hand it the coded bytes.
- Tests in `Curl.Zstandard.UnitTests` are platform-neutral.
- Same quality gates as every library: 100% line and branch coverage, cyclomatic
  complexity of at most 10, CRAP of at most 30.
