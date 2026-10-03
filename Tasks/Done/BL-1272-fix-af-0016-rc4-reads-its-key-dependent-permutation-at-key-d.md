---
id: BL-1272
title: Fix AF-0016: RC4 reads its key-dependent permutation at key-dependent indexes (SSH arcfour, NTLM)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cryptography.UnitLibrary]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1272 — Fix AF-0016: RC4 reads its key-dependent permutation at key-dependent indexes (SSH arcfour, NTLM)

## Goal

The defect the audit office reported as AF-0016 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0016 (High, security auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0016-rc4-reads-its-key-dependent-permutation-at-key-dep.md`.

Location: `Curl.Cryptography.UnitLibrary/Rc4.cs:113`

Location: `Curl.Cryptography.UnitLibrary/Rc4.cs:113`

Line 113: `return permutation[(byte)(permutation[first] + permutation[second])];` - the permutation and the index both derive from the secret key, so the memory address is secret. Phase 2: explained by the class's XML docs (line 13, 'Not constant-time'), listed as by design in the library's CLAUDE.md; kept for triage.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cryptography.UnitLibrary/Rc4.cs -Pattern 'permutation\[\(byte\)'
```

- Expected: No match.
- Actual: Rc4.cs:113:        return permutation[(byte)(permutation[first] + permutation[second])];

The finding closes only when a later re-audit by the security auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fixed in `Rc4.cs`: only the counter `i` is public. Every swap at the key-dependent index `j` goes through `SwapWithSecretIndex` (one pass over all 256 entries that keeps the old `S[j]` and writes `S[i]` there by `ConstantTime.EqualMask`, then writes the kept value to `S[i]`), and the keystream output is read by `ReadAtSecretIndex` (masked scan), its index computed from the two values already held. Key schedule uses the same swap. ADR-0399 records it (decided by Claude under Stewart's delegation); the library CLAUDE.md now lists RC4 as constant-time.
- Cost: two 256-entry passes per keystream byte; acceptable for legacy arcfour / rc4-hmac.
- No new tests: RFC 6229 and RFC 4345 vectors in `Rc4Tests` already cover both helpers on every path, and `touches` names only the library.

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. RC4 reads and swaps its permutation by masked scan; no key-dependent address (AF-0016 reproduction gives no match)
