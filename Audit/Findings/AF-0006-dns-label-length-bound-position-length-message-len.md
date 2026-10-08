---
id: AF-0006
title: DNS label length bound `position + length > message.Length` can become >= with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:AppendLabel-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319:>
task: BL-1262
tasks: BL-1262
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
duplicate-of:
closed: 2026-10-07
closed-how: mechanical
closed-by: 2026-10-07_1336.md
---
# AF-0006 - DNS label length bound `position + length > message.Length` can become >= with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319`: DNS label length bound `position + length > message.Length` can become >= with no test failing. Reported by an auditor flagged unreliable in 2026-10-02_1400.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319`

Mutant survived (seed 0): `>` became `>=` in the check that returns DnsMessageFailure.BadLabel. No test has a label that ends exactly at the end of the message, so the off-by-one is unpinned. This is a refused-input decision on untrusted network data.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319:> -Member AppendLabel -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319 (>) is killed.
- Actual: survived  Curl.Networking.UnitLibrary/DnsAnswerDecoder.cs:319 >

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: yes | The Networking mutation run stopped on a red baseline (5 failing ECH tests) and ran no mutants, so killing the mutant is not shown. DnsAnswerDecoder.cs:319 still reads 'if (position + length > message.Length)'.
- 2026-10-03 | 2026-10-03_1233.md | reproduces: no | Networking run, seed 0: no mutant sampled at DnsAnswerDecoder.cs:319 (code unchanged there); the only survivors are CertificateRevocationListFile.cs:116, TcpPendingConnection.cs:86, TlsFailureMessages.cs:320. Not shown surviving by the reproduction; the site was not sampled so this is unverified.
- 2026-10-03 | 2026-10-03_1459.md | reproduces: no | Seed-0 run did not sample the site. Hand-mutated DnsAnswerDecoder.cs:319 to >= in a scratch copy: Decode_ACnameLabelEndingExactlyAtTheMessageEnd_IsReadAndThenFailsWithOutOfRange failed, so the mutant is killed.
- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | The tool reproduction cannot run: the Networking baseline is red (AF-0026), so it stops with 'the unmutated tests failed or timed out (exit 1)'. Applied by hand in a scratch export and run with the 5 failing ECH tests filtered out (baseline 2996 passed): DnsAnswerDecoder.cs:319 'position + length > message.Length' -> '>='. Killed by Decode_ACnameLabelEndingExactlyAtTheMessageEnd_IsReadAndThenFailsWithOutOfRange.
- 2026-10-07 | 2026-10-07_1336.md | reproduces: no | Ran the -Site reproduction: resolvedLine 319, outcome killed. excludedTests: ConnectAsync_WithAHostThatDoesNotResolveOnWindows_ReportsTheResolveAndBindFailures (a red baseline test, filed this audit). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
- 2026-10-07: accepted -> closed. Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
