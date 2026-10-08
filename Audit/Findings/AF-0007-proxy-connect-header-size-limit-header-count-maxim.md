---
id: AF-0007
title: Proxy CONNECT header size limit `header.Count > MaximumHeaderBytes` can become >= with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:ReplyAfterLatestByte-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170:>
task: BL-1263
tasks: BL-1263
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
duplicate-of:
closed: 2026-10-07
closed-how: mechanical
closed-by: 2026-10-07_1336.md
---
# AF-0007 - Proxy CONNECT header size limit `header.Count > MaximumHeaderBytes` can become >= with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170`: Proxy CONNECT header size limit `header.Count > MaximumHeaderBytes` can become >= with no test failing. Reported by an auditor flagged unreliable in 2026-10-02_1400.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170`

Mutant survived (seed 0): `>` became `>=`. The failure message "Too large response headers: N > Max" is user-visible, yet no test sends a header exactly MaximumHeaderBytes long, so the boundary is unpinned.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170:> -Member ReplyAfterLatestByte -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170 (>) is killed.
- Actual: survived  Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:170 >

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: yes | Same red baseline, no mutants run. HttpProxyTunnel.cs:170 still reads 'if (header.Count > MaximumHeaderBytes)'. A fix is not shown.
- 2026-10-03 | 2026-10-03_1233.md | reproduces: no | Networking run, seed 0: no mutant sampled at HttpProxyTunnel.cs:170 (code unchanged); not shown surviving by the reproduction, unverified.
- 2026-10-03 | 2026-10-03_1459.md | reproduces: no | Seed-0 run did not sample the site. Hand-mutated HttpProxyTunnel.cs:170 to >= in a scratch copy: ReadReplyAsync_WhenALineEndsTheHeaderBlockAt307200Bytes_KeepsReading failed, so the mutant is killed.
- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | By hand, same method as AF-0006: HttpProxyTunnel.cs:203 'header.Count > MaximumHeaderBytes' -> '>='. Killed by ReadReplyAsync_WhenALineEndsTheHeaderBlockAt307200Bytes_KeepsReading.
- 2026-10-07 | 2026-10-07_1336.md | reproduces: no | Ran the -Site reproduction: line 170 had moved, so the tool resolved the site in ReplyAfterLatestByte to line 193. Outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
- 2026-10-07: accepted -> closed. Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
