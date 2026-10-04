# ADR-0394 — The SSH library exposes its readers of server bytes through `SshWireDecoders` for fuzzing

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1267.

## Context

Audit finding AF-0011: the audit office's fuzzer (`--target ssh`) found no public reader
of raw bytes in `Curl.Protocol.Ssh.UnitLibrary`. Its packet, key-exchange, host-key and
SFTP readers are all internal, visible only to `Curl.Protocol.Ssh.UnitTests`, so hostile
server bytes reached them untested by the fuzzer, which exited 2.

## Decision

The library gains one public static class, `SshWireDecoders`, at its root. Each method
runs an internal reader over raw bytes exactly as the transport would and turns the
refusals the transport already treats as a malformed server (`InvalidDataException`,
`SshPacketLengthException`, `EndOfStreamException`, and for host keys
`CryptographicException` and `NotSupportedException`) into a return value. Any other
exception escapes, so a fuzzer sees it as a crash.

- `CountWholePacketsAsync(bytes, token)`: unprotected binary packets, through
  `SshPacketReader` over an internal `SshByteArrayConnection`; returns the whole packets read.
- `TryInflatePayload(bytes)`: `SshZlibDecompressor.Decompress`.
- `TryDecodeKexInit(bytes)`: `SshKexInit.Parse`.
- `TryDecodeSftpAttributes(bytes)`: `SftpAttributes.Read`.
- `TryDecodeHostKeySignature(bytes)`: algorithm name, host key blob and signature blob
  as SSH `string`s, then the exchange hash; runs the named `ISshSignatureVerifier`.

The internal types stay internal; the class adds no behaviour to a transfer.

Pointing the fuzzer's `ssh` target at these methods is a change to `Audit/`, which the
dark factory may not make; it is filed as its own interactive task.

## Consequences

- The fuzzer can reach the SSH parsers without reflection into internals.
- The public surface grows by one class; `SshProtocolHandler` is unchanged.

## Alternatives considered

- **`InternalsVisibleTo` the fuzzer:** a file-based app has no stable assembly name to
  name, and it would let the audit tool depend on every internal detail.
- **Make each reader public:** widens the surface by a dozen types and their own
  exception contracts, where one façade with a stable shape does.
