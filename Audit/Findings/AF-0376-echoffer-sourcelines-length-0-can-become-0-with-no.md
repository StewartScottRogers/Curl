---
id: AF-0376
title: EchOffer.SourceLines' Length > 0 can become >= 0 with no test failing, so an empty ECHConfigList from DNS writes the wrong -v ECH lines
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/EchOffer.cs:SourceLines-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/EchOffer.cs:92:>
task: BL-3353
tasks: BL-3353
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed: 2026-10-24
closed-how: mechanical
closed-by: 2026-10-24_0845.md
---
# AF-0376 - EchOffer.SourceLines' Length > 0 can become >= 0 with no test failing, so an empty ECHConfigList from DNS writes the wrong -v ECH lines

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/EchOffer.cs:92`: EchOffer.SourceLines' Length > 0 can become >= 0 with no test failing, so an empty ECHConfigList from DNS writes the wrong -v ECH lines.

## Evidence

Location: `Curl.Networking.UnitLibrary/EchOffer.cs:92`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 reported this mutant as survived. Original: : encoded is not { Length: > 0 } ? [NoListLine]; mutated: { Length: >= 0 }. SourceLines writes curl's ECH: setup lines, which go to -v (EchOffer.InfoLines). With the mutant, an HTTPS record whose ech value is present but empty no longer writes NoListLine. It writes DnsListLine plus either 'ECH: imported ECHConfigList of length 0' or DnsListFailedLine. The -v output of an --ech transfer then differs, and no test covers an empty, non-null list from DNS.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/EchOffer.cs:92:> -Member SourceLines -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-23 | 2026-10-23_2322.md | not re-audited | Ran the -Site reproduction: EchOffer.cs:92 'encoded is not { Length: > 0 }' -> '>= 0' survived, but only with three baseline-failing tests excluded - HandBuiltTlsProviderTests.AuthenticateAsClientAsync_WithEch_WritesCurlsEchLinesBeforeTheHello, ..._WithEchHardAndNoUsableList_FailsWithExit35BeforeSendingAByte and ..._WithEchTrueAndNoUsableList_SendsAPlainHello. These are the tests that drive EchOffer.SourceLines and pin its -v ECH lines; they fail unmutated (TlsReader.Take bounds defect, see this audit's failing-baseline finding), so the survival cannot tell whether the suite would kill the mutant.
- 2026-10-24 | 2026-10-24_0845.md | reproduces: no | Ran the -Site Curl.Networking.UnitLibrary/EchOffer.cs:92:> run: resolvedLine 92, 'encoded is not { Length: > 0 }' -> '>= 0', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> accepted.
- 2026-10-24: accepted -> closed. Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
