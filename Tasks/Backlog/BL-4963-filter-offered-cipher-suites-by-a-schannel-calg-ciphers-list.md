---
id: BL-4963
title: Filter offered cipher suites by a Schannel CALG_* --ciphers list in SchannelAlgorithmList
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/SchannelAlgorithmList.cs, Curl.Networking.UnitTests/SchannelAlgorithmFilterTests.cs]
requirement: none
created: 2026-11-11
completed:
---
# BL-4963 — Filter offered cipher suites by a Schannel CALG_* --ciphers list in SchannelAlgorithmList

## Goal

SchannelAlgorithmList.OfferedSuites returns the cipher suites of the Schannel profile that a CALG_* list allows.

## Context

Split from BL-4911 (STD-008 of the 2026-11-11 outside fitness review; REF = curl 8.22.0 Schannel). `SchannelAlgorithmList.cs` only validates a `CALG_*` list; the ALG_IDs are never used to limit offered suites.

## Acceptance criteria

- [ ] `SchannelAlgorithmList.OfferedSuites` filters class by class (key exchange, signature, cipher, hash); a class the list does not name is unrestricted; an AEAD suite counts under its PRF hash.
- [ ] `CALG_AES_128` leaves only AES-128 suites (including `ECDHE-RSA-AES128-GCM-SHA256`); `CALG_AES_128:CALG_SHA_256` leaves the same; `CALG_RSA_KEYX` leaves no suite, which the caller reports as exit 35 algorithm mismatch.
- [ ] Focused tests in `Curl.Networking.UnitTests/SchannelAlgorithmFilterTests.cs` pin these; `dotnet build` is clean and the fast tests pass; `Curl.Networking.UnitLibrary` stays within the quality gates.

## Notes

Split from BL-4911.

Continues the stashed work of BL-4911.

Lane 6 wrote OfferedSuites and SchannelAlgorithmFilterTests.cs (unbuilt) in the stash; build and fix them.

## Log

- 2026-11-11: Created.
