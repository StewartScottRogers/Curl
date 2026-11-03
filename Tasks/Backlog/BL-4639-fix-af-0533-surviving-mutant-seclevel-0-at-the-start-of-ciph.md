---
id: BL-4639
title: Fix AF-0533: Surviving mutant: @SECLEVEL=0 at the start of --ciphers can stop being recognised without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
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

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
