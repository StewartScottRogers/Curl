---
id: BL-573
title: Download part of an SFTP file with -r and -C
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-569]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-573 — Download part of an SFTP file with -r and -C

## Goal

`-r first-last`, `-r first-`, `-r -suffix` and `-C <n>`/`-C -` on an SFTP download read only the requested bytes (with `FSTAT` for the size where needed), and a range or offset past the end fails with the exit code and message curl 8.21.0 gives.

## Context

- Conformance audit 2026-09-28, row 35. Builds on BL-569. `ITransferContext.Range`/`RangeText`/`ResumeFrom` are already there (`ByteRangeParser` in `Curl.Core.UnitLibrary` parses `-r`).
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: each range form, a range past the end, `-C 3`, `-C -` with an existing shorter output file, `-C` past the end.

## Acceptance criteria

- [ ] Measured first as above; stdout bytes, stderr and exit code copied into Notes.
- [ ] `Curl.Protocol.Ssh.UnitTests` pin the `READ` offsets and lengths and the output and outcome for each case against the in-memory peer.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ssh.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
