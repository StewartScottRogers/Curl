---
id: BL-578
title: Write curl's -v and --trace lines for scp and sftp transfers
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-576]
touches: [Curl.Protocol.Ssh.UnitLibrary, Curl.Protocol.Ssh.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-578 — Write curl's -v and --trace lines for scp and sftp transfers

## Goal

`-v` on an `sftp://` or `scp://` transfer writes the lines curl 8.21.0 writes (connect lines, the SSH fingerprint and host-key lines, the authentication-method lines, the transfer and closing lines), byte for byte apart from values that vary, and `--trace` writes what curl's trace writes for SSH.

## Context

- Conformance audit 2026-09-28, row 35. Events: `ITransferEvents` (ADR-0046); formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TraceTransferEventWriter.cs`.
- **BCL first.** Anything the BCL lacks is hand-built in its own library (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28): never a package, never a task blocked for a missing primitive.
- Measure with the reference curl against a local OpenSSH server through `Record-CurlExchange.ps1 -NoServer`: `-v` for an SFTP download with password auth, with public-key auth, an SCP download, a host-key mismatch, and `--trace-ascii -` for the SFTP download.

## Acceptance criteria

- [ ] Measured first as above; stderr and trace output copied into Notes, with varying parts (fingerprints, ports, times) marked.
- [ ] `Curl.Console.UnitTests` pin the measured `-v` stderr for each case with fixed test keys, normalised as existing `-v` tests do.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
