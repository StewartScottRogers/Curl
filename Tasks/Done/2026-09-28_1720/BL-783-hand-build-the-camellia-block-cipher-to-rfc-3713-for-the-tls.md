---
id: BL-783
title: Hand-build the Camellia block cipher to RFC 3713 for the TLS Camellia suites
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-670]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-783 — Hand-build the Camellia block cipher to RFC 3713 for the TLS Camellia suites

## Goal

`Curl.Cryptography.UnitLibrary` has a public `Camellia` type (128-, 192- and 256-bit keys, ECB block operation and CBC) that the hand-built TLS client uses for the Camellia-CBC suites curl's LibreSSL and OpenSSL builds offer.

## Context

- ADR-0140 (BL-695) amends ADR-0118's list with Camellia: curl.se's official Windows build (LibreSSL 4.2.1) offers `00c4 0088 00c0 0084 00be 0045 00ba 0041` in its default ClientHello, and OpenSSL 3.5.5 lists the `*-CAMELLIA*` suites (RFC 5932). The BCL has no Camellia on any platform.
- Follow ADR-0118's API shape and constant-time rules (Camellia's S-boxes are table lookups; state in the XML docs whether the implementation is constant-time, as ADR-0118 requires for Blowfish and CAST-128).
- Consumer: BL-702 (TLS 1.2/1.1/1.0 CBC records).

## Acceptance criteria

- [x] `Camellia` encrypts and decrypts RFC 3713 Appendix A's test vectors for 128-, 192- and 256-bit keys, pinned in `Curl.Cryptography.UnitTests` with the source cited.
- [x] CBC over `Camellia` matches RFC 5932's cited vectors or, failing those, a vector recorded from `openssl enc -camellia-128-cbc` with the command in a comment.
- [x] A wrong key length throws `ArgumentException`; `Dispose` zeroes the key schedule and use after `Dispose` throws `ObjectDisposedException`.
- [x] The library meets the quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30).

## Notes

- Plan: `Camellia` modelled on `Blowfish` (same constructor, `EncryptBlock`/`DecryptBlock`,
  `EncryptCbc`/`DecryptCbc`, argument and disposal rules). The key schedule is two tables of
  (KL/KR/KA/KB, rotation, half) in the order encryption consumes the subkeys; decryption
  uses the same array reversed with each whitening pair kept in order. KA and KB come from
  `UInt128` and are zeroed from a `stackalloc` in a `finally`. The feature stages were run
  in-session: the primitive is a single class against a fixed specification, so no
  separate architect plan was needed.
- Decision (ADR-0145, decided by Claude under Stewart's delegation): Camellia indexes
  RFC 3713's fixed S-boxes directly and is not constant-time, as OpenSSL's and LibreSSL's
  Camellia are; a masked full-table scan would cost about a hundred times a look-up. ADR-0145
  amends ADR-0118's exception list. `Documentation/Planning/Decisions` was added to
  `touches` for that ADR and its README row; no task in `Doing` names it.
- RFC 5932 publishes no CBC vectors, so CBC is pinned to OpenSSL 3.5.7's
  `openssl enc -camellia-128-cbc` and `-camellia-256-cbc` output (commands in
  `CamelliaTests.cs`). The 128-bit ECB vector was also cross-checked with
  `openssl enc -camellia-128-ecb`.
- Coverage of `Curl.Cryptography.UnitLibrary` measured with the MSTest coverage collector:
  100% line, 100% branch; complexity is held by `CA1502` at build.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Camellia (RFC 3713, 128/192/256-bit keys, ECB and CBC) passes RFC 3713 Appendix A and OpenSSL CBC vectors at 100% coverage
