---
id: BL-795
title: Remove the Lucky Thirteen timing residual from TLS CBC records with a fixed-block HMAC
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-702]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests, Documentation/Planning/Decisions/ADR-0150-tls-1-0-cbc-inserts-openssls-empty-fragment-and-cbc-records-fail-bad-padding-and-bad-macs-alike.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-795 — Remove the Lucky Thirteen timing residual from TLS CBC records with a fixed-block HMAC

## Goal

Opening a MAC-then-encrypt CBC record in `Curl.Tls` hashes the same number of compression-function blocks whatever its padding, so neither the padding length nor its validity shows in the time taken (the complete Lucky Thirteen countermeasure).

## Context

- ADR-0150 (BL-702): `Tls12CbcRecordCipher` already checks padding with masks and branches once on padding and MAC together, but computes the HMAC with the BCL over the content the padding implies, so the hash time follows the padding length by a few blocks.
- OpenSSL's `ssl3_cbc_digest_record` (`ssl/record/methods/tls_pad.c` and `ssl/s3_cbc.c`) is the reference: it runs the hash's compression function over a fixed number of blocks, writes the length padding into the right block with masks, and picks the result with masks; `ssl3_cbc_copy_mac` extracts the received MAC with a constant-time rotation.
- The BCL hides the compression functions, so build SHA-1, SHA-256 and SHA-512 (for HMAC-SHA384) compression in `Curl.Cryptography.UnitLibrary` (ADR-0118's API and constant-time rules; `LittleEndianMerkleDamgard` is the little-endian precedent), and the fixed-block HMAC over them.

## Acceptance criteria

- [x] `Curl.Cryptography.UnitTests` pin the new SHA-1, SHA-256 and SHA-384 digests against the BCL's for lengths 0 to 300.
- [x] `Curl.Tls.UnitTests` show `Tls12CbcRecordCipher` opening every padding length 0 to 255 with the HMAC-SHA1, -SHA256 and -SHA384 suites, the MAC over each checked against the BCL's HMAC, and bad padding and bad MACs still `bad_record_mac`; ADR-0150's residual paragraph is updated to say it is gone.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for both libraries.

## Notes

- Plan as built: `Curl.Cryptography` gains internal `Sha1`, `Sha256` and `Sha384` structs
  (SHA-384 is the SHA-512 compression function from SHA-384's IV) behind
  `IBigEndianCompressionFunction`, `FixedBlockMerkleDamgard<T>` (hashes
  `prefix || header || data[..dataLength]` over a fixed block count: whole blocks every
  allowed length fills are hashed directly, the rest built byte by byte with masks, the
  state after the true last block picked by mask), and public static `FixedBlockHmac`.
  `ConstantTime` gains `LessThanMask` and `EqualMask`. `Tls12RecordMac.VerifyInFixedBlocks`
  uses it and copies the received MAC out with masks over the last 256 + MAC-length
  positions; `Tls12CbcRecordCipher` calls it for MAC-then-encrypt records.
- Choice: the SHA types are internal and the public entry is one static
  `FixedBlockHmac.Compute(HashAlgorithmName, ...)`, since the BCL already hashes whole
  messages and only the fixed-block HMAC is new capability (ADR-0118's one public type per
  primitive). SHA-512 itself is not exposed; nothing needs it.
- Choice: the received-MAC copy is a plain masked double loop (about 300 x 48 byte
  operations), not OpenSSL's rotation - simpler, same constant-time property.
- `touches` widened to ADR-0150 and the Decisions README index line, because the
  acceptance criteria require ADR-0150's residual paragraph updated; no task in Doing
  names either file.
- Measure-CodeQuality: both libraries 100% line and branch, 0 failing members, worst CRAP
  10. Its full test run once hit a 10-second timeout in Conformance's upstream test1117
  (HTTP, unrelated) under coverage load; it passes on its own and in the plain fast run,
  so the report was taken with `-SkipTestRun` over that run's Cobertura files. Also:
  `-Library A,B` measured nothing through `powershell -File`; one library per call works.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. MAC-then-encrypt CBC records are MACed with a fixed-block HMAC over hand-built SHA-1/256/384, so padding length no longer shows in timing
