---
id: AF-0793
title: Surviving mutant: an unusable -x behind --preproxy can be ignored instead of failing the transfer
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-15_1610.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/TransferProxySelection.cs:TrySelectProxyBehindPreProxy-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferProxySelection.cs:181:false
task: BL-5244
tasks: BL-5244
found: 2026-11-15
found-at: 065474f7431e6647cda727e7f0fd5a25c9859a90
scorecard: 2026-11-15_1159.md
duplicate-of:
closed: 2026-11-15
closed-how: mechanical
closed-by: 2026-11-15_1610.md
---
# AF-0793 - Surviving mutant: an unusable -x behind --preproxy can be ignored instead of failing the transfer

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferProxySelection.cs:181`: Surviving mutant: an unusable -x behind --preproxy can be ignored instead of failing the transfer.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferProxySelection.cs:181`

Mutant 'return false;' -> 'return true;' in TrySelectProxyBehindPreProxy survived (seed 5851). When selector.TrySelect refuses the proxy text it sets failure and the method must return false. The mutant returns true, so the caller at line 124 goes on with no proxy and the transfer runs straight to the origin instead of ending with the selector's exit code and message. The exit code and the request route both change.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferProxySelection.cs:181:false -Member TrySelectProxyBehindPreProxy -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-15 | 2026-11-15_1610.md | reproduces: no | Ran the -Site command: site Curl.Engine.UnitLibrary/TransferProxySelection.cs:181:false, resolvedLine 181, outcome killed; excludedTests named the two AwsSigV4HttpAuthenticatorTests that fail unmutated. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-15: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_1610.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
