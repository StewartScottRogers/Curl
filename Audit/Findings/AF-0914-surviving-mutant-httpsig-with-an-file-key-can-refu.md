---
id: AF-0914
title: Surviving mutant: --httpsig with an @file key can refuse every non-empty key file and accept an empty one
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-21_0114.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:WithKey-eq:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:81:==
task: BL-5649
tasks: BL-5649
found: 2026-11-20
found-at: e78a1e70bbba7541cf5f1abfedb0ef1c8ae8e2a0
scorecard: 2026-11-20_1740.md
duplicate-of:
closed: 2026-11-21
closed-how: mechanical
closed-by: 2026-11-21_0114.md
---
# AF-0914 - Surviving mutant: --httpsig with an @file key can refuse every non-empty key file and accept an empty one

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:81`: Surviving mutant: --httpsig with an @file key can refuse every non-empty key file and accept an empty one.

## Evidence

Location: `Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:81`

Line 81 `return text.Length == 0 ? Refused(CurlExitCode.BadFunctionArgument, $"curl: httpsig: key file '{fileName}' is empty") : new(new HttpSignatureSettings(algorithm, text, keyId, components), CurlExitCode.Ok, null);` mutated to `text.Length != 0` survived Curl.Engine.UnitTests (seed 994434 sample, 12.3 s). With the mutant every readable, non-empty key file ends the transfer with exit 43 and 'key file ... is empty', and an empty file is signed with an empty key. No test reads a key from a file through WithKey, so the exit code a user sees for --httpsig @key.pem is unpinned.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:81:== -Member WithKey -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-21 | 2026-11-21_0114.md | reproduces: no | Ran the -Site Curl.Engine.UnitLibrary/HttpSignatureSetup.cs:81:== command: resolvedLine 81, 'return text.Length == 0' -> 'return text.Length != 0', outcome killed. Two baseline failures in HttpSignatureHttpAuthenticatorTests were excluded (filed separately). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-20: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_0114.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
