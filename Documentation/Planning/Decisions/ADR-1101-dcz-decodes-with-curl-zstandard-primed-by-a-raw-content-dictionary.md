# ADR-1101: dcz decodes with Curl.Zstandard, primed by a raw content dictionary

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4300
- Decided by Claude under Stewart's delegation.

## Context

RFC 9842 section 5's `dcz` content encoding is a Zstandard frame (RFC 8878) compressed with
the stored dictionary as a raw content dictionary (RFC 8878 section 5), as `zstd -D` and
`zstd --patch-from` produce: the dictionary primes the window before the first block, matches
may reach back into it, and the repeat offsets and entropy tables start at their defaults. A
`dcz` frame may declare a window up to 8 MB or 1.25 times the dictionary, whichever is larger.

The base class library has no Zstandard. Two hand-built decoders exist: `Curl.Zstandard.UnitLibrary`
(ADR-0185), complete, held to the gates and checked against libzstd's golden corpus, and the
Kafka extension's smaller one, which `Curl.Extension.CompressionDictionary.UnitLibrary` may not
reference (ADR-0999). BL-4300's text proposed writing a third decoder in the extension.

## Decision

- `Curl.Zstandard`'s `ZstandardDecoder` gains a second constructor,
  `ZstandardDecoder(ReadOnlyMemory<byte> rawContentDictionary, ulong maxWindowSize)`. At each
  frame's start it sizes the history for the window, libzstd's margin (ADR-0196) and the
  dictionary, and appends the dictionary to it, so the existing match code reaches into it. A
  frame naming a `Dictionary_ID` still fails `DictionaryWrong`, as libzstd does with a raw
  content dictionary (whose ID is 0). The window limit is in bytes, not a power of two, because
  RFC 9842's 1.25 times rule is not one.
- `Curl.Extension.CompressionDictionary.UnitLibrary` references `Curl.Zstandard.UnitLibrary`, a
  shared hand-built layer that references nothing (ADR-0999 forbids only protocol and extension
  references). Its `Zstd/DictionaryZstandardDecompressor` decodes a `dcz` body's frames with a
  window limit of `max(8 MiB, 1.25 x dictionary length)`, reading RFC 9842's "8 MB" as 2^23 bytes
  (zstd's window log 23), and throws `DictionaryZstandardException` naming the
  `ZstandardDecodeError`, or with none when the body ends mid-frame.
- The tests decode frames real libzstd 1.5.7 compressed (`ZSTD_compress_usingDict`, as `zstd -D`;
  `ZSTD_CCtx_refPrefix`, as `zstd --patch-from`), called through P/Invoke from a throwaway C#
  program, since no `zstd` executable is installed on the lanes' machine.

## Consequences

One Zstandard decoder serves HTTP, TLS and `dcz`; dictionary support is covered by the same
gates and golden corpus as the rest of it. Matches into the dictionary are allowed as long as
the history holds it, which is at least as long as RFC 8878's rule (until the output passes
`Window_Size`), matching libzstd's lenience in the same way ADR-0196 does.
