---
id: BL-4403
title: Fix AF-0493: Root CLAUDE.md lists the /Wire/ solution group as Http2, Http3, Quic, Zstandard; Curl.slnx also has HttpDatagram, Protobuf, Ssh and WebSocket there
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [CLAUDE.md]
requirement: none
created: 2026-10-31
completed:
---
# BL-4403 — Fix AF-0493: Root CLAUDE.md lists the /Wire/ solution group as Http2, Http3, Quic, Zstandard; Curl.slnx also has HttpDatagram, Protobuf, Ssh and WebSocket there

## Goal

The defect the audit office reported as AF-0493 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0493 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0493-root-claude-md-lists-the-wire-solution-group-as-ht.md`.

Location: `CLAUDE.md:355`

Location: `CLAUDE.md:355`

CLAUDE.md:355 '- `/Wire/`: Http2, Http3, Quic, Zstandard - framing and encoding below the protocols.' The neighbouring group lists (/Hosts/, /CommandLine/, /Transfer/, /Security/) each match Curl.slnx exactly, so a reader takes this one as complete. Curl.slnx also has /Wire/HttpDatagram/ (line 261), /Wire/Protobuf/ (265), /Wire/Ssh/ (273) and /Wire/WebSocket/ (277). The rule 'a new area goes in the group it belongs to' still works, so it misleads only about what the group holds.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern '/Wire/.: Http2, Http3, Quic, Zstandard -'; Select-String -Path Curl.slnx -Pattern '/Wire/(HttpDatagram|Protobuf|Ssh|WebSocket)/' | ForEach-Object LineNumber
```

- Expected: The CLAUDE.md /Wire/ list names every /Wire/ folder in Curl.slnx, so the second command's folders appear in it.
- Actual: CLAUDE.md:355 lists only Http2, Http3, Quic, Zstandard; Curl.slnx has /Wire/ folders for HttpDatagram, Protobuf, Ssh and WebSocket at lines 261, 265, 273, 277.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-31: Created.
- 2026-10-31: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
