---
id: BL-683
title: Encode and decode NTLM negotiate, challenge and authenticate messages
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-682]
touches: [Curl.Ntlm.UnitLibrary, Curl.Ntlm.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-683 — Encode and decode NTLM negotiate, challenge and authenticate messages

## Goal

`Curl.Ntlm.UnitLibrary` writes the NTLM NEGOTIATE (Type 1) message curl 8.21.0 sends byte for byte, reads a server's CHALLENGE (Type 2) message including its target information AV pairs, and writes an AUTHENTICATE (Type 3) message from supplied responses, per MS-NLMP section 2.2.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Design and seam: BL-525's ADR. Consumers: BL-526 (HTTP), BL-604 (proxy), BL-538 (SASL), BL-692 (SPNEGO), BL-595 (SMB).
- curl's own NTLM code is the reference for the bytes (`lib/vauth/ntlm.c` at tag `curl-8_21_0`: the flags it sets in Type 1, which fields it fills in Type 3, the workstation name, Unicode vs OEM); BL-526 measures the Type 1 bytes curl sends, and this task may use `Record-CurlExchange.ps1` itself for them (`--ntlm -u u:p` against a `401 NTLM`).
- MS-NLMP 2.2.1.1 to 2.2.1.3, 2.2.2 (AV_PAIR, VERSION, flags). Decoding is defensive: offsets and lengths are checked, a malformed Type 2 is a typed failure, never an exception from an out-of-range read.

## Acceptance criteria

- [x] `Curl.Ntlm.UnitTests` pin the Type 1 bytes for the flag set curl sends (with the measurement or source line cited), decode MS-NLMP 4.2's example CHALLENGE messages and a Type 2 with target info, reject truncated and out-of-range messages with the typed failure, and pin a Type 3 layout for fixed inputs.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Ntlm.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Reference read: `lib/vauth/ntlm.c` at tag `curl-8_21_0`. Type 1 bytes pinned to the
  measurement already in ADR-0142 (`TlRMTVNTUAABAAAABoIIAAAAAAAAAAAAAAAAAAAAAAA=`, flags
  `0x00088206`), which matches the source's flag set; no new recording was needed.
- Every behaviour follows curl's source, so no ADR: Type 2 needs 32 bytes, signature and
  type; target information is read only with `NEGOTIATE_TARGET_INFO` and a 48-byte
  header, and a non-zero length must lie inside the message at offset 48 or later. The
  target name, which curl never reads, is returned empty rather than rejected when its
  buffer is out of range. AV_PAIR parsing is a separate `NtlmTargetInformation.Decode`
  because curl copies the blob without parsing it, so a bad pair must not fail the Type 2.
- Type 3 mirrors curl: 64-byte header, payload order LM, NT, domain, user, workstation,
  session key buffer zero, flags as supplied, strings as UTF-8 bytes widened byte by byte
  under `NEGOTIATE_UNICODE` (curl's `unicodecpy`), and curl's two `NTLM_BUFSIZE` (1024)
  checks as `TryEncode` returning false (curl's `CURLE_TOO_LARGE`).
- MS-NLMP 4.2.2.3 and 4.2.4.3 CHALLENGE messages are pinned in `NtlmChallengeMessageTests`.
- Tick: `Curl.Ntlm.UnitTests` 34 tests green; `Measure-CodeQuality.ps1 -Library
  Curl.Ntlm.UnitLibrary`: 100% line, 100% branch, 22 members, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Curl.Ntlm writes curl 8.21.0's NTLM Type 1 and Type 3 byte for byte and reads Type 2 with its AV pairs, malformed messages a typed failure
