---
id: AF-0928
title: HttpUnixSocketTarget_..._ReachesTheSwsEmulationOnTheHttpPort checks only that some connection exists
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-21_1740.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-21_0835.md, 2026-11-21_1740.md).
key: quality:Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs:HttpUnixSocketTarget_TheHttpUnixPath_ReachesTheSwsEmulationOnTheHttpPort:weak-assertion
reproduction: none
task: BL-5678
tasks: BL-5678
found: 2026-11-21
found-at: 1f330a1f28a1086717db7833947030e8fdc0bf9d
scorecard: 2026-11-21_0114.md
duplicate-of:
closed: 2026-11-21
closed-how: consecutive
closed-by: 2026-11-21_0835.md, 2026-11-21_1740.md
---
# AF-0928 - HttpUnixSocketTarget_..._ReachesTheSwsEmulationOnTheHttpPort checks only that some connection exists

## Summary

Low finding from the quality auditor at `Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs:1517`: HttpUnixSocketTarget_..._ReachesTheSwsEmulationOnTheHttpPort checks only that some connection exists.

## Evidence

Location: `Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs:1517`

The only check of the connection is Assert.IsNotNull(connected.Connection, connected.ErrorMessage); the other assertions check the target's host, port and TLS flag. The name says the connection reaches the SWS emulation, but nothing reads the scripted 'HTTP/1.1 200 OK' reply through it, so a connector that returned any other connection would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs -Pattern 'Assert.IsNotNull\(connected.Connection'
```

- Expected: No match: the test reads the SWS reply through the connection.
- Actual: UpstreamCaseRunnerTests.cs:1517:        Assert.IsNotNull(connected.Connection, connected.ErrorMessage);

## Re-audits

- 2026-11-21 | 2026-11-21_0835.md | reproduces: no | Ran the Select-String reproduction: no match. The test now writes a GET over the connection and asserts Assert.StartsWith("HTTP/1.1 200 OK", ...) on the reply (UpstreamCaseRunnerTests.cs:1521).
- 2026-11-21 | 2026-11-21_1740.md | reproduces: no | Ran the Select-String: no match for Assert.IsNotNull(connected.Connection in UpstreamCaseRunnerTests.cs. The test now asserts the target host, port and no TLS (lines 1514-1516), and that the response starts with HTTP/1.1 200 OK (line 1521).

## Log

- 2026-11-21: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_1740.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-21_0835.md, 2026-11-21_1740.md).
