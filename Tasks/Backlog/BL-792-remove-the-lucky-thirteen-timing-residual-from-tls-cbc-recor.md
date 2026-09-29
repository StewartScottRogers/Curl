---
id: BL-792
title: Remove the Lucky Thirteen timing residual from TLS CBC records with a fixed-block HMAC
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-702]
touches: [Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests, Curl.Tls.UnitLibrary, Curl.Tls.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-792 — Remove the Lucky Thirteen timing residual from TLS CBC records with a fixed-block HMAC

## Goal

Opening a MAC-then-encrypt CBC record in `Curl.Tls` hashes the same number of compression-function blocks whatever its padding, so neither the padding length nor its validity shows in the time taken (the complete Lucky Thirteen countermeasure).

## Context

- ADR-0148 (BL-702): `Tls12CbcRecordCipher` already checks padding with masks and branches once on padding and MAC together, but computes the HMAC with the BCL over the content the padding implies, so the hash time follows the padding length by a few blocks.
- OpenSSL's `ssl3_cbc_digest_record` (`ssl/record/methods/tls_pad.c` and `ssl/s3_cbc.c`) is the reference: it runs the hash's compression function over a fixed number of blocks, writes the length padding into the right block with masks, and picks the result with masks; `ssl3_cbc_copy_mac` extracts the received MAC with a constant-time rotation.
- The BCL hides the compression functions, so build SHA-1, SHA-256 and SHA-512 (for HMAC-SHA384) compression in `Curl.Cryptography.UnitLibrary` (ADR-0118's API and constant-time rules; `LittleEndianMerkleDamgard` is the little-endian precedent), and the fixed-block HMAC over them.

## Acceptance criteria

- [ ] `Curl.Cryptography.UnitTests` pin the new SHA-1, SHA-256 and SHA-384 digests against the BCL's for lengths 0 to 300.
- [ ] `Curl.Tls.UnitTests` show `Tls12CbcRecordCipher` opening every padding length 0 to 255 with the HMAC-SHA1, -SHA256 and -SHA384 suites, the MAC over each checked against the BCL's HMAC, and bad padding and bad MACs still `bad_record_mac`; ADR-0148's residual paragraph is updated to say it is gone.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for both libraries.

## Notes

## Log

- 2026-09-28: Created.
