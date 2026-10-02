---
id: BL-1196
title: Write the --trace-config ftp lines for active mode (-P)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-02
completed:
---
# BL-1196 — Write the --trace-config ftp lines for active mode (-P)

## Goal

Under `-v --trace-config ftp` an active-mode (`-P`) FTP download writes curl 8.21.0's `[FTP]` lines, in curl's order among the `-v` lines.

## Context

- Follow-up of BL-1162, which wrote the passive download, upload and listing lines through `FtpStateTrace` (`Curl.Protocol.Ftp.UnitLibrary`). Active mode writes nothing for `EPRT`/`PORT` yet.
- Measured 2026-10-02 (BL-1162), `-sS --trace-config ftp -v -P 127.0.0.1 ftp://127.0.0.1:P/a.txt`, the `[FTP]` lines after `DO phase starts`:
  `[STOP] ftp_state_use_port(), opened socket`, `ftp_port_bind_socket(), socket bound to port 0`, `ftp_port_listen(), listening on port`, then after `> EPRT`: `[STOP] -> [PORT]`, `[PORT] perform, awaiting DATA connect`, `[PORT] -> [STOP]`, `[STOP] DO phase is complete2`; then TYPE/SIZE/RETR as passive, but after `[RETR] ftp_domore_pollset()` come `[RETR] -> [STOP]`, `[STOP] ftp_domore_pollset()`, `ftp_initiate_transfer()`. Re-record full stderr to place them, and measure `PORT` (`--disable-eprt`).

## Acceptance criteria

- [ ] A test in `Curl.Protocol.Ftp.UnitTests` pins the measured active-mode `[FTP]` lines in order among the `-v` lines, `EPRT` and `PORT` both.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
