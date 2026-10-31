---
id: AF-0493
title: Root CLAUDE.md lists the /Wire/ solution group as Http2, Http3, Quic, Zstandard; Curl.slnx also has HttpDatagram, Protobuf, Ssh and WebSocket there
auditor: truthfulness
severity: Low
status: proposed
reason:
key: truthfulness:CLAUDE.md:Wire-solution-folder-list:false-statement
reproduction: none
task: none
tasks:
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0493 - Root CLAUDE.md lists the /Wire/ solution group as Http2, Http3, Quic, Zstandard; Curl.slnx also has HttpDatagram, Protobuf, Ssh and WebSocket there

## Summary

Low finding from the truthfulness auditor at `CLAUDE.md:355`: Root CLAUDE.md lists the /Wire/ solution group as Http2, Http3, Quic, Zstandard; Curl.slnx also has HttpDatagram, Protobuf, Ssh and WebSocket there. Reported by an auditor flagged unreliable in 2026-10-31_0358.md.

## Evidence

Location: `CLAUDE.md:355`

CLAUDE.md:355 '- `/Wire/`: Http2, Http3, Quic, Zstandard - framing and encoding below the protocols.' The neighbouring group lists (/Hosts/, /CommandLine/, /Transfer/, /Security/) each match Curl.slnx exactly, so a reader takes this one as complete. Curl.slnx also has /Wire/HttpDatagram/ (line 261), /Wire/Protobuf/ (265), /Wire/Ssh/ (273) and /Wire/WebSocket/ (277). The rule 'a new area goes in the group it belongs to' still works, so it misleads only about what the group holds.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path CLAUDE.md -Pattern '/Wire/.: Http2, Http3, Quic, Zstandard -'; Select-String -Path Curl.slnx -Pattern '/Wire/(HttpDatagram|Protobuf|Ssh|WebSocket)/' | ForEach-Object LineNumber
```

- Expected: The CLAUDE.md /Wire/ list names every /Wire/ folder in Curl.slnx, so the second command's folders appear in it.
- Actual: CLAUDE.md:355 lists only Http2, Http3, Quic, Zstandard; Curl.slnx has /Wire/ folders for HttpDatagram, Protobuf, Ssh and WebSocket at lines 261, 265, 273, 277.

## Re-audits

## Log

- 2026-10-31: filed proposed.
