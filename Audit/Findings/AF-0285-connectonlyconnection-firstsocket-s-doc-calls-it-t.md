---
id: AF-0285
title: ConnectOnlyConnection.FirstSocket's doc calls it the first socket number a connection is given; the first one given is FirstSocket + 1
auditor: truthfulness
severity: Low
status: accepted
reason: 
key: truthfulness:Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs:FirstSocket:false-doc-comment
reproduction: none
task: none
tasks:
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0285 - ConnectOnlyConnection.FirstSocket's doc calls it the first socket number a connection is given; the first one given is FirstSocket + 1

## Summary

Low finding from the truthfulness auditor at `Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs:52`: ConnectOnlyConnection.FirstSocket's doc calls it the first socket number a connection is given; the first one given is FirstSocket + 1.

## Evidence

Location: `Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs:52`

Doc on FirstSocket (lines 48-52): "Gets the first socket number a connection is given." Doc on nextSocket (line 30): "The next socket number OpenAsync gives". Code: line 31 "private static long nextSocket = FirstSocket;" and OpenAsync line 78 "Socket = new CurlSocket((nint)Interlocked.Increment(ref nextSocket));". Interlocked.Increment returns the incremented value, so the first connection is numbered 0x60000001 and nextSocket always holds the last number given, not the next. The tests assert the number is greater than FirstSocket (ConnectOnlyConnectionTests.cs:232, ManagedEasyTransferTests.ConnectOnly.cs:69), confirming the code, not the doc. Contrast ManagedMultiTransfer.FirstTransferSocket, which is the first number given (post-increment, line 570). Found while checking ADR-0686, whose 'numbers each connection it keeps from FirstSocket (0x60000000) upward' is loose in the same way.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs -Pattern 'first socket number a connection is given|nextSocket = FirstSocket|Interlocked.Increment\(ref nextSocket\)' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }
```

- Expected: The doc's first number matches the code: either a post-increment (nextSocket++ / Interlocked.Increment(...) - 1) or a doc saying numbers start above FirstSocket.
- Actual: 31: private static long nextSocket = FirstSocket; / 48: /// Gets the first socket number a connection is given. ... / 78: Socket = new CurlSocket((nint)Interlocked.Increment(ref nextSocket));

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
