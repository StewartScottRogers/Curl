---
id: BL-4639
title: Fix AF-0533: Surviving mutant: @SECLEVEL=0 at the start of --ciphers can stop being recognised without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests/Cgr094Tests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-02
---
# BL-4639 — Fix AF-0533: Surviving mutant: @SECLEVEL=0 at the start of --ciphers can stop being recognised without a test failing

## Goal

The defect the audit office reported as AF-0533 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0533 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0533-surviving-mutant-seclevel-0-at-the-start-of-cipher.md`.

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:201`

Location: `Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:201`

Mutant 'return at >= 0 && ...StartsWith("0")' -> 'at > 0' survived (Curl.Networking.UnitTests green). A cipher string that begins with '@SECLEVEL=0' (at == 0, as in --ciphers '@SECLEVEL=0:...') would no longer lower the security level to zero. That is the decision that lets the OpenSSL-build hand-built client offer legacy TLS 1.0/1.1, so a handshake curl completes would fail with an SSL exit code. Every test puts the command after another token.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HandBuiltTlsProvider.cs:201:>= -Member LowersSecurityLevelToZero -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added Curl.Networking.UnitTests/Cgr094Tests.cs to touches: the fix is a test (no task in Doing names it). The mutant is killed by a new data row `@SECLEVEL=0:ALL` (the command at index 0; not `@SECLEVEL=0:DEFAULT`, since OpenSSL expands DEFAULT only at the start, so that list has no suite and is exit 59, which Curl already matches) on the OpenSSL-build TLS 1.1 handshake test; OpenSSL accepts a @SECLEVEL command anywhere in the cipher string, so curl completes that handshake too. No production change. Lanes cannot run the mutation tool, so the mutant was applied by hand (`at >= 0` to `at > 0`), the new row was seen to fail, and the mutant was reverted.
- First criterion: checked with the same mutant (`>=` to `>` at HandBuiltTlsProvider.cs:201): Cgr094Tests now fails on the new row (killed) and passes without it. Second: `dotnet build` clean, fast tests green on Windows; the test is platform-neutral (in-memory TLS 1.1 server, OpenSSL build named explicitly).

## Log

- 2026-11-03: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-02: Doing -> Done. A cipher string starting with @SECLEVEL=0 is pinned by a test, so the AF-0533 mutant is killed
