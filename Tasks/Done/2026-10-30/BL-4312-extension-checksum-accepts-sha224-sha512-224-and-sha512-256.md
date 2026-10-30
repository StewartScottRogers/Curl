---
id: BL-4312
title: Extension: --checksum accepts sha224, sha512-224 and sha512-256 with hand-built SHA-2 cores
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-4135]
touches: [Curl.Extension.Checksum.UnitLibrary, Curl.Extension.Checksum.UnitTests, Documentation/Planning/Decisions/ADR-1096-checksum-verifies-the-response-body-against-a-fips-180-4-digest-and-fails-with-exit-61.md]
requirement: none
created: 2026-10-29
completed: 2026-10-30
---
# BL-4312 — Extension: --checksum accepts sha224, sha512-224 and sha512-256 with hand-built SHA-2 cores

## Goal

`--checksum` also accepts `sha224`, `sha512-224` and `sha512-256` (FIPS 180-4 §6.3, §6.7).

## Context

BL-4135 delivered `--checksum` with sha1, sha256, sha384 and sha512 over the BCL's `IncrementalHash` (ADR-1096). The BCL has no SHA-224 or SHA-512/t, so these need hand-built SHA-256 and SHA-512 compression functions (FIPS 180-4 §6.2.2, §6.4.2) with their own initial hash values (§5.3.2, §5.3.6), in `Curl.Extension.Checksum.UnitLibrary`, behind an incremental hashing seam `HashingStream` and `ExpectedChecksum` use. Start at `ExpectedChecksum.DigestOf`.

## Acceptance criteria

- [x] `--checksum sha224:<56 hex>`, `sha512-224:<56 hex>` and `sha512-256:<64 hex>` verify a body; unit tests pin NIST's FIPS 180-4 worked examples ("abc" and the two-block messages) for each.
- [x] ADR-1096's "Left out" section and the option's help name the new algorithms.
- [x] `Curl.Extension.Checksum.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added IChecksumHash (BclChecksumHash for sha1/256/384/512, hand-built Sha256Hash and Sha512Hash over BlockChecksumHash for sha224, sha512-224, sha512-256). Round constants and initial hashes are computed from the primes (FIPS 180-4 §4.2, §5.3; SHA-512/t by the §5.3.6 generation function). Tests pin NIST examples; quality run: 100% line, 100% branch, worst CRAP 8. CI on Linux and macOS: change is platform-neutral, fast tests green here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c8c3c32a d1f0d5bf b9bc50d3 3d836d5e.
- 2026-10-30: Doing -> Done. --checksum accepts sha224, sha512-224 and sha512-256 via hand-built SHA-2 cores
