---
id: AF-0928
title: HttpUnixSocketTarget_..._ReachesTheSwsEmulationOnTheHttpPort checks only that some connection exists
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Conformance.UnitTests/UpstreamCaseRunnerTests.cs:HttpUnixSocketTarget_TheHttpUnixPath_ReachesTheSwsEmulationOnTheHttpPort:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-21
found-at: 1f330a1f28a1086717db7833947030e8fdc0bf9d
scorecard: 2026-11-21_0114.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-21: filed proposed.
