---
id: AF-0493
title: Root CLAUDE.md lists the /Wire/ solution group as Http2, Http3, Quic, Zstandard; Curl.slnx also has HttpDatagram, Protobuf, Ssh and WebSocket there
auditor: truthfulness
severity: Low
status: closed
reason: Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces.
key: truthfulness:CLAUDE.md:Wire-solution-folder-list:false-statement
reproduction: none
task: BL-4403
tasks: BL-4403
found: 2026-10-31
found-at: bfe08061f844163d0f86f77dc0af8971b7b1b179
scorecard: 2026-10-31_0358.md
duplicate-of:
closed: 2026-11-01
closed-how: reliable-reaudit
closed-by: 2026-11-01_0144.md
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

- 2026-10-31 | 2026-10-31_1149.md | reproduces: no | Ran the reproduction: the old pattern '/Wire/.: Http2, Http3, Quic, Zstandard -' no longer matches CLAUDE.md. CLAUDE.md now lists 'Http2, Http3, HttpDatagram, Protobuf, Quic, Ssh, WebSocket, Zstandard', which covers the Curl.slnx lines 261, 265, 273 and 277.
- 2026-11-01 | 2026-11-01_0144.md | reproduces: no | Ran the reproduction: CLAUDE.md no longer matches '/Wire/: Http2, Http3, Quic, Zstandard -'. It now reads '/Wire/: Http2, Http3, HttpDatagram, Protobuf, Quic, Ssh, WebSocket, Zstandard', matching Curl.slnx's /Wire/ folders (lines 292, 296, 304, 308 among them).

## Log

- 2026-10-31: filed proposed.
- 2026-10-31: proposed -> accepted.
- 2026-11-01: accepted -> closed. Re-audit 2026-11-01_0144.md: the reproduction no longer reproduces.
