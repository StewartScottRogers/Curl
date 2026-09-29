---
id: BL-684
title: Compute NTLM responses and session keys with the hand-built MD4
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-683, BL-675]
touches: [Curl.Ntlm.UnitLibrary, Curl.Ntlm.UnitTests, Curl.Cryptography.UnitLibrary, Curl.Cryptography.UnitTests, Documentation/Planning/Decisions/ADR-0118-curl-cryptography-hand-builds-every-primitive-the-bcl-lacks-on-a-ci-platform.md, Documentation/Planning/Decisions/ADR-0156-ntlm-hand-builds-des-and-answers-as-curl-8-21-0-does.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-684 — Compute NTLM responses and session keys with the hand-built MD4

## Goal

`Curl.Ntlm.UnitLibrary` computes the NTLMv2 response (and the NTLMv1 and NTLM2 session responses where curl 8.21.0 sends them), the session base key and exported session key, from a user, domain, password, server challenge, client nonce and time, matching MS-NLMP section 4.2's worked examples.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-683. MD4 from `Curl.Cryptography.UnitLibrary` (BL-675; add the reference here); HMAC-MD5, MD5, DES and `RandomNumberGenerator` from the BCL.
- Which response curl chooses and when (NTLMv2 when the server sends target info; what curl does otherwise) comes from `lib/vauth/ntlm.c` and `lib/curl_ntlm_core.c` at tag `curl-8_21_0`; cite the lines in the XML docs.
- MS-NLMP 3.3.1 (NTLM v1), 3.3.2 (NTLM v2), 3.4 (session security keys, needed by SMB signing), 4.2.2 to 4.2.4 (test vectors: `NTOWFv1`, `LMOWFv1`, NTLMv1, NTLMv2, session keys). The client nonce and time are injected (`TimeProvider`, an injected random source) so tests are deterministic.

## Acceptance criteria

- [x] `Curl.Ntlm.UnitTests` reproduce every value in MS-NLMP 4.2.2 (NTLMv1), 4.2.3 (NTLMv1 with extended session security) and 4.2.4 (NTLMv2): response keys, `NTChallengeResponse`, `LmChallengeResponse`, session base key and the encrypted random session key.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Ntlm.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: DES hand-built as `Curl.Cryptography.Des` (the BCL's `DES` throws on weak keys, measured, and an empty password's LM hash is DES under the weak all-zero key); `Curl.Ntlm` gains `NtlmOneWayFunctions`, `NtlmResponseComputation`, `NtlmResponses`, `NtlmChallengeAnswerer`, `INtlmRandomSource`/`SystemNtlmRandomSource`, internal `NtlmDes` and `NtlmCurlString`. Decision recorded in ADR-0156; ADR-0118's table gains the DES row.
- Touches widened (rule 3): `Curl.Cryptography.UnitLibrary`/`.UnitTests` for DES, and ADR-0118, ADR-0156 and the decisions README for the record. No task in Doing named any of them (BL-546 touches SMTP, Output and Console tests only).
- curl 8.21.0 (`lib/vauth/ntlm.c` 609-667, `lib/curl_ntlm_core.c`, fetched at tag `curl-8_21_0`): NTLMv2 + LMv2 when the challenge sets NTLM2_KEY (extended session security), NTLMv1 + LM otherwise with that flag cleared; never the NTLM2 session response; timestamp is `time(NULL)`, whole seconds; strings are UTF-8 bytes widened, ASCII-only uppercasing of user (NTOWFv2) and password (LMOWFv1), domain never uppercased. The library follows curl on all of these.
- MS-NLMP 4.2.1 to 4.2.4 values were checked against Microsoft Learn (2026-09-28) before pinning; every one reproduced on the first run, including the LM_KEY and NON_NT_SESSION_KEY key exchange variants.
- Measure-CodeQuality: Curl.Ntlm.UnitLibrary 100/100, 45 members, 0 failing, worst CRAP 10; Curl.Cryptography.UnitLibrary 100/100, 284 members, 0 failing.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Ntlm computes NTLMv1, NTLM2 session and NTLMv2 responses and session keys matching MS-NLMP 4.2, and answers a challenge as curl 8.21.0 does, on a hand-built DES
