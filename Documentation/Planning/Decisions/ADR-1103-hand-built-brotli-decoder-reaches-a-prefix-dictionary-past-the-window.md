# ADR-1103 — A hand-built Brotli decoder reaches a prefix dictionary past the window, as the reference decoder does

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4301.

## Context

The `dcb` coding (RFC 9842 section 4) is a Brotli stream (RFC 7932) compressed against the dictionary the server
named. .NET 10's `BrotliDecoder` cannot take a dictionary, so `Curl.Extension.CompressionDictionary.UnitLibrary/Brotli`
holds a hand-built decoder, `BrotliDictionaryDecoder`. RFC 9842 says only that the dictionary is a prefix; it does
not say how a distance reaches it once the output is longer than the window, nor whether such a copy enters the
ring of last distances. The reference `brotli` tool 1.2.0 (`-D`, a raw LZ77 dictionary) is what servers use.

## Decision

- **Where the dictionary sits.** As in the reference decoder: with `reachable` = min(bytes decoded, window − 16),
  a distance up to `reachable` copies from the output, a distance d up to `reachable` + the dictionary's length
  copies from dictionary byte `length − (d − reachable)`, and a larger one is a static dictionary reference with
  the dictionary's length taken off first. A copy that runs past the dictionary's end is corrupt. So with a small
  window the dictionary stays reachable after the output outgrows the window.
- **The ring of last distances.** A copy from the prefix dictionary enters the ring, as an output copy does;
  distance code 0 and static dictionary references do not (RFC 7932 section 4). The recorded streams
  decode only this way.
- **Literal context.** The two bytes before the output count as zero, dictionary or not (RFC 7932 section 7.1).
- **The static dictionary.** Its 122,784 bytes (RFC 7932 Appendix A, SHA-256 `20e42eb1…5c70`, checked by a test)
  are a Base64 constant in `BrotliStaticDictionaryData.cs`, not an embedded resource: that needs no project file
  change, no resource lookup and nothing for Native AOT to keep. The 121 transforms of Appendix B are the
  reference decoder's own affix and triple tables.
- **Errors.** Every rule the stream breaks, a truncation and bytes after the last meta-block throw
  `BrotliFormatException` (a `FormatException`), naming the RFC section.
- **Shape.** One call decodes a whole stream to a byte array; streaming is left to BL-4132, which wires the coding in.

## Consequences

The decoder agrees with the reference encoder's output at lgwin 10 and 24 with a dictionary and at 16 to 22
without one; the tests hold those recorded streams. A server whose encoder placed the dictionary differently would
fail to decode, as it would with the reference decoder.
