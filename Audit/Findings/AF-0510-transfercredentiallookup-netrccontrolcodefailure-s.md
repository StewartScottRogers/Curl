---
id: AF-0510
title: TransferCredentialLookup.NetrcControlCodeFailure's HasControlCode(Login) || HasControlCode(Password) can become && with no test failing, so a netrc entry with a control code in only one of them is sent instead of refused with exit 26
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:NetrcControlCodeFailure-or:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:230:||
task: none
tasks:
found: 2026-10-31
found-at: 23bacbc6c04b8e06a4eecb79636da4891dca4c01
scorecard: 2026-10-31_1149.md
duplicate-of:
closed: 2026-11-01
closed-how: mechanical
closed-by: 2026-11-01_0144.md
---
# AF-0510 - TransferCredentialLookup.NetrcControlCodeFailure's HasControlCode(Login) || HasControlCode(Password) can become && with no test failing, so a netrc entry with a control code in only one of them is sent instead of refused with exit 26

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:230`: TransferCredentialLookup.NetrcControlCodeFailure's HasControlCode(Login) || HasControlCode(Password) can become && with no test failing, so a netrc entry with a control code in only one of them is sent instead of refused with exit 26.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:230`

Invoke-MutationTest.ps1 -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0 reported 'survived Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:230 ||' (member NetrcControlCodeFailure): '&& (HasControlCode(result.Login) || HasControlCode(result.Password))' mutated to '&& (HasControlCode(result.Login) && HasControlCode(result.Password))' and every Curl.Engine.UnitTests test passed. The method is curl's exit-26 refusal (lib/url.c str_has_ctrl, BL-1411) of a matching netrc entry whose login or password holds a byte below 0x20, for every scheme except http, https, ws and wss. Under the mutant, an entry with a control code in only the login, or only the password, is no longer refused: the credentials are sent and the transfer goes on instead of exiting 26. No test has a netrc entry with a control code in exactly one of the two fields. This changes an exit code and request bytes.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferCredentialLookup.cs:230:|| -Member NetrcControlCodeFailure -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test with a control code in only the netrc login (or only the password) fails under the mutant
- Actual: outcome survived

## Re-audits

- 2026-11-01 | 2026-11-01_0144.md | reproduces: no | Ran the -Site command: the mutant HasControlCode(result.Login) && HasControlCode(result.Password) at TransferCredentialLookup.cs:230 (member NetrcControlCodeFailure) was killed (score 1). Baseline left out the already-failing TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-31: filed proposed.
- 2026-11-01: proposed -> closed. Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
