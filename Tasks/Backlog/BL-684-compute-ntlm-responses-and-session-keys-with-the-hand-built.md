---
id: BL-684
title: Compute NTLM responses and session keys with the hand-built MD4
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-683, BL-675]
touches: [Curl.Ntlm.UnitLibrary, Curl.Ntlm.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-684 — Compute NTLM responses and session keys with the hand-built MD4

## Goal

`Curl.Ntlm.UnitLibrary` computes the NTLMv2 response (and the NTLMv1 and NTLM2 session responses where curl 8.21.0 sends them), the session base key and exported session key, from a user, domain, password, server challenge, client nonce and time, matching MS-NLMP section 4.2's worked examples.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-683. MD4 from `Curl.Cryptography.UnitLibrary` (BL-675; add the reference here); HMAC-MD5, MD5, DES and `RandomNumberGenerator` from the BCL.
- Which response curl chooses and when (NTLMv2 when the server sends target info; what curl does otherwise) comes from `lib/vauth/ntlm.c` and `lib/curl_ntlm_core.c` at tag `curl-8_21_0`; cite the lines in the XML docs.
- MS-NLMP 3.3.1 (NTLM v1), 3.3.2 (NTLM v2), 3.4 (session security keys, needed by SMB signing), 4.2.2 to 4.2.4 (test vectors: `NTOWFv1`, `LMOWFv1`, NTLMv1, NTLMv2, session keys). The client nonce and time are injected (`TimeProvider`, an injected random source) so tests are deterministic.

## Acceptance criteria

- [ ] `Curl.Ntlm.UnitTests` reproduce every value in MS-NLMP 4.2.2 (NTLMv1), 4.2.3 (NTLMv1 with extended session security) and 4.2.4 (NTLMv2): response keys, `NTChallengeResponse`, `LmChallengeResponse`, session base key and the encrypted random session key.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Ntlm.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
