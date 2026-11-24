---
id: BL-5877
title: Finish an active-mode FTP listing with -P when the server answers slowly (upstream test 251)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Ftp.UnitLibrary/FtpActiveCommand.cs, Curl.Original.Protocol.Ftp.UnitLibrary/FtpPortArgument.cs, Curl.Original.Protocol.Ftp.UnitTests/FtpActiveCommand]
requirement: none
created: 2026-11-24
completed:
---
# BL-5877 — Finish an active-mode FTP listing with -P when the server answers slowly (upstream test 251)

## Goal

`curl ftp://<host>/ -P <address>` against a server that answers slowly sends `PWD`, `PORT`, `TYPE A`, `LIST`, `QUIT`, completes the listing and exits 0, as upstream curl 8.21.0 does, so upstream test case 251 ("FTP dir list, PORT with specified IP and slow response") passes.

## Context

From Surl's Phase 7 handoff `Z:\repos\Surl\Documentation\Conformance\CurlPort\Port-Board-Handoff.md`, entry 24 (Surl finding UTC-4 in `Upstream-Test-Cases.md`), measured on win-x64 at port commit `74f3f45` (2026-11-16) beside upstream curl 8.21.0 Schannel. Check it still reproduces on the current branch first.

Command: `curl ftp://127.0.0.1:<port>/ -P 127.0.0.1` against test 251's FTP server half with `SLOWDOWN` (the server sends each reply byte by byte with a delay). Upstream logs in, sends `PWD`, `PORT`, `TYPE A`, `LIST`, `QUIT` and exits 0. The port did not finish within the case's 60 s limit on all three runs. Likely the active-mode accept or the reply reader times out or waits on a reply split across many reads; earlier active-mode hangs were BL-1978 and BL-464. Start at `FtpActiveCommand.cs`. What decides: RFC 959 section 3.2 and the case's `<protocol>` section.

## Acceptance criteria

- [ ] Upstream test case 251 passes with the port in its upstream-case runner.
- [ ] A focused unit test in `Curl.Original.Protocol.Ftp.UnitTests` with a test double that drips each reply slowly (fake `TimeProvider`, no sleeps) pins the completed listing and the command order (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-24: Created from Surl Port-Board-Handoff.md entry 24.
