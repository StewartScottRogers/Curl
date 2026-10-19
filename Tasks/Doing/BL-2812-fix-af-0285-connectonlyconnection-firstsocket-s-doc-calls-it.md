---
id: BL-2812
title: Fix AF-0285: ConnectOnlyConnection.FirstSocket's doc calls it the first socket number a connection is given; the first one given is FirstSocket + 1
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-19
completed:
---
# BL-2812 — Fix AF-0285: ConnectOnlyConnection.FirstSocket's doc calls it the first socket number a connection is given; the first one given is FirstSocket + 1

## Goal

The defect the audit office reported as AF-0285 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0285 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0285-connectonlyconnection-firstsocket-s-doc-calls-it-t.md`.

Location: `Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs:52`

Location: `Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs:52`

Doc on FirstSocket (lines 48-52): "Gets the first socket number a connection is given." Doc on nextSocket (line 30): "The next socket number OpenAsync gives". Code: line 31 "private static long nextSocket = FirstSocket;" and OpenAsync line 78 "Socket = new CurlSocket((nint)Interlocked.Increment(ref nextSocket));". Interlocked.Increment returns the incremented value, so the first connection is numbered 0x60000001 and nextSocket always holds the last number given, not the next. The tests assert the number is greater than FirstSocket (ConnectOnlyConnectionTests.cs:232, ManagedEasyTransferTests.ConnectOnly.cs:69), confirming the code, not the doc. Contrast ManagedMultiTransfer.FirstTransferSocket, which is the first number given (post-increment, line 570). Found while checking ADR-0686, whose 'numbers each connection it keeps from FirstSocket (0x60000000) upward' is loose in the same way.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/ConnectOnlyConnection.cs -Pattern 'first socket number a connection is given|nextSocket = FirstSocket|Interlocked.Increment\(ref nextSocket\)' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }
```

- Expected: The doc's first number matches the code: either a post-increment (nextSocket++ / Interlocked.Increment(...) - 1) or a doc saying numbers start above FirstSocket.
- Actual: 31: private static long nextSocket = FirstSocket; / 48: /// Gets the first socket number a connection is given. ... / 78: Socket = new CurlSocket((nint)Interlocked.Increment(ref nextSocket));

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
