---
id: AF-0944
title: Surviving mutant: a token-endpoint body of exactly the 1 MiB limit can be refused
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.OAuth2.UnitLibrary/Http/OAuth2HttpBodyReader.cs:RequireRoomFor-gt:surviving-mutant
reproduction: mutation Curl.Extension.OAuth2.UnitLibrary/Http/OAuth2HttpBodyReader.cs:42:>
task: BL-5703
tasks: BL-5703
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0944 - Surviving mutant: a token-endpoint body of exactly the 1 MiB limit can be refused

## Summary

High finding from the quality auditor at `Curl.Extension.OAuth2.UnitLibrary/Http/OAuth2HttpBodyReader.cs:42`: Surviving mutant: a token-endpoint body of exactly the 1 MiB limit can be refused. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Extension.OAuth2.UnitLibrary/Http/OAuth2HttpBodyReader.cs:42`

Drawn library, seed 812510: body.WrittenCount + length > MaxBodyLength mutated to >= survived; a body of exactly the limit fails as BodyTooLarge with a failure exit code instead of being read. PostFormAsync_CloseDelimitedBodyOver1MiB_FailsAsBodyTooLarge covers only a body over the limit.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.OAuth2.UnitLibrary/Http/OAuth2HttpBodyReader.cs:42:> -Member RequireRoomFor -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
