---
id: BL-1712
title: Refuse an empty ALPN protocol list or name in Curl.Tls ApplicationLayerProtocolNegotiationExtension
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1712 — Refuse an empty ALPN protocol list or name in Curl.Tls ApplicationLayerProtocolNegotiationExtension

## Goal

`ApplicationLayerProtocolNegotiationExtension` refuses what RFC 7301 section 3.1 forbids: `Decode` answers an empty protocol list and an empty protocol name with `decode_error`, and `Encode`'s contract covers an empty list and a name over 255 bytes.

## Context

- Found by BL-1523's adversarial tests: `Decode([0x00, 0x00])` (empty `ProtocolNameList`, which is `<2..2^16-1>`) and `Decode([0x00, 0x01, 0x00])` (a zero-length `ProtocolName`, which is `<1..2^8-1>`) both succeed today.
- `Encode([])` returns an extension with an empty list, which a server must reject; `Encode([new string('a', 256)])` throws `ArgumentException` from `TlsWriter.WriteVector`, which `Encode`'s doc comment does not promise.
- Decide whether `Encode` throws `ArgumentException` for both (and document it) or callers are trusted; record the choice in Notes.

## Acceptance criteria

- [x] `ApplicationLayerProtocolNegotiationExtension.Decode` answers `00 00` and `00 01 00` with `TlsAlertDescription.DecodeError`, each pinned by a test in `Curl.Tls.UnitTests`.
- [x] `Encode` on an empty list and on a 256-byte name behaves as its doc comment states, each pinned by a test.
- [x] `dotnet build` is clean and the fast tests pass; Curl.Tls.UnitLibrary keeps 100% line and branch coverage.

## Notes

- Decision (sensible default, rule 1): `Encode` throws `ArgumentException` (ParamName `protocols`) for an empty list and for a name that is empty or over 255 bytes in Latin-1, checked before anything is written. Its doc comment now says so, along with the 65,535-byte list limit `TlsWriter` already enforced. Why: RFC 7301 forbids all of these and a peer rejects such a hello. Every caller in the solution already skips the extension when the list is empty (`Tls12ClientHelloBuilder`, `Tls13ClientHelloBuilder`) or passes a fixed non-empty list (`ClientHelloProfile`, test servers), so refusing at the source costs callers nothing and stops a malformed hello going out silently.
- `Decode` reads the list as before, then records `DecodeError` when it holds no name or an empty name; truncated and trailing bytes still answer `DecodeError`.
- Tests in `TlsAdversarialTests`: `00 00`, `00 01 00`, an empty name after `h2`, `Encode([])`, and `Encode` with a 0-byte and a 256-byte name.
- Measured with `Measure-CodeQuality.ps1 -Library Curl.Tls.UnitLibrary`: 100% line, 99.95% branch; the changed file is fully covered. The one failing member is `Tls13ClientHelloBuilder.BuildOptionalExtension` (9/10 branches on its switch), which this task did not change; filed as BL-1714 rather than widening this task.
- Fast tests: Curl.Tls (1316), Curl.Quic (485) and Curl.Networking (3121, every other project that calls `Encode`), then the whole fast run: all green.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. ALPN Decode refuses an empty list or name with decode_error; Encode throws ArgumentException for both and documents it
