---
id: BL-1271
title: Fix AF-0015: CAST-128 round function indexes the S-boxes with key- and data-dependent bytes (SSH cast128-cbc)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1271 — Fix AF-0015: CAST-128 round function indexes the S-boxes with key- and data-dependent bytes (SSH cast128-cbc)

## Goal

The defect the audit office reported as AF-0015 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0015 (High, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0015-cast-128-round-function-indexes-the-s-boxes-with-k.md`.

Location: `Curl.Cryptography.UnitLibrary/Cast128.cs:242`

Location: `Curl.Cryptography.UnitLibrary/Cast128.cs:242`

Line 242: `uint s1 = boxes[(int)(input >> 24)];` (and line 244), where input is the half-block combined with the secret masking and rotation subkeys. Used for SSH's cast128-cbc. Phase 2: explained by the class's XML docs (line 17, 'Not constant-time'), which the library's CLAUDE.md lists as by design; kept for triage.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Cast128.cs -Pattern 'boxes\[\(int\)\(input >> 24\)\]'
```

- Expected: No match.
- Actual: Cast128.cs:242:        uint s1 = boxes[(int)(input >> 24)];

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Every S-box read in `Cast128` - the four in `Round` and the five per key-schedule row,
  since the key schedule also indexed S5 to S8 with key bytes - now goes through the new
  internal `Cast128.ReadBox`: a masked scan of all 256 entries with
  `ConstantTime.EqualMask`, as BL-1270 did for DES. Output unchanged; RFC 2144's
  known-answer tests pass.
- Decision recorded in ADR-0397 (decided by Claude under Stewart's delegation); the
  library's `CLAUDE.md` and `Cast128`'s XML docs now say constant-time.
- No new test: `Curl.Cryptography.UnitTests` is outside `touches`, and `ReadBox` has no
  branch, so the existing known-answer tests cover every line of it.
- Reproduction gives no match; `dotnet build` clean, fast tests green (Cryptography 1330
  passed).

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. CAST-128 reads its S-boxes by constant-time masked scan in the round function and key schedule (AF-0015)
