---
id: AF-0865
title: Surviving mutant: an OCI request field name starting with a space, colon or line break is sent
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpRequestWriter.cs:RequireToken-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpRequestWriter.cs:42:<
task: BL-5507
tasks: BL-5507
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed: 2026-11-19
closed-how: mechanical
closed-by: 2026-11-19_0134.md
---
# AF-0865 - Surviving mutant: an OCI request field name starting with a space, colon or line break is sent

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpRequestWriter.cs:42`: Surviving mutant: an OCI request field name starting with a space, colon or line break is sent. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpRequestWriter.cs:42`

Mutant 'value.AsSpan().IndexOfAny(" \t\r\n:") < 0' -> '<= 0' survived Curl.Extended.Protocol.OciRegistry.UnitTests. RequireToken guards methods and field names (RFC 9110 5.6.2). With the mutant, a forbidden character at index 0 is accepted, so a name beginning with CR LF or ':' is written into the request head (header injection, changed request bytes) instead of failing with InvalidRequestField. SendAsync_FieldThatWouldBreakTheHead_FailsWithoutWriting never puts the bad character first.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpRequestWriter.cs:42:< -Member RequireToken -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Targeted run Curl.Extended.Protocol.OciRegistry.UnitLibrary/Http/OciHttpRequestWriter.cs:42:< (resolvedLine 42) gave outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-18: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
