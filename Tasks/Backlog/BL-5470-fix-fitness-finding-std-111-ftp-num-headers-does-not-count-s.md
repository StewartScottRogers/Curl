---
id: BL-5470
title: Fix fitness finding STD-111: FTP %{num_headers} does not count server reply lines; curl counts every header-stream line that contains a colon (STILL OPEN; the old acceptance check passes only because its mock had no colons)
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary/TransferWriteOutVariables.cs, Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5470 — Fix fitness finding STD-111: FTP %{num_headers} does not count server reply lines; curl counts every header-stream line that contains a colon (STILL OPEN; the old acceptance check passes only because its mock had no colons)

## Goal

STD-111 from the outside fitness review of 2026-11-18 06:33 no longer reproduces: FTP %{num_headers} does not count server reply lines; curl counts every header-stream line that contains a colon (STILL OPEN; the old acceptance check passes only because its mock had no colons). Curl matches the reference curl as the finding's acceptance check states.

## Context

From the outside fitness review `Curl-Fitness-Review-2026-11-18T0633` (commit `ec53afc`, preview release binaries). In the finding, PORT is this Curl, REF is curl 8.22.0 Schannel on Windows (Git for Windows `curl.exe`), REF2 is curl 8.18.0 OpenSSL on WSL Ubuntu. Mock servers bind 127.0.0.1 and port numbers are examples. Where the finding refers to another finding's setup (e.g. "the setup from STD-116"), find the task titled with that finding ID. Measure real curl with `Record-CurlExchange.ps1` before pinning output text. The finding, verbatim:

- **Severity:** Low
- **Confidence:** Confirmed (2 runs, two servers)
- **Protocol/area:** ftp:// write-out, Windows
- **Repro:** an FTP mock on 127.0.0.1:44030 (passive data ports 44031–44059) whose replies contain colons: greeting `220 hello: ready`; `USER` → `331 pass: please`; `PASS` → `230 login: ok`; `PWD` → `257 "/" is cwd`; `TYPE x` → `200 type: x`; `SIZE` → `213 10`; `MDTM` → `213 20200101000000`; `REST n` → `350 rest: n`; `EPSV` → `229 Entering Extended Passive Mode (|||p|)`; `RETR` → `150 opening: data`, 10 bytes on the data connection, `226 done`; `NOOP` → `200 noop: ok`.
  - `curl -sS -o NUL -w "%{num_headers}\n" ftp://colon:p@127.0.0.1:44030/f.txt`
  - `curl -sS -I -o NUL -w "%{num_headers}\n" ftp://colon:p@127.0.0.1:44030/f.txt`
  - `curl -sS -o NUL -Q NOOP -Q -NOOP -w "%{num_headers}\n" ftp://colon:p@127.0.0.1:44030/f.txt`
- **Expected (REF):** `5`, `8`, `7`. (Against pyftpdlib, REF `-I` prints `4`: three header lines plus `200 Type set to: Binary.`)
- **Actual (PORT):** `1`, `3`, `1` (pyftpdlib `-I`: `3`). The `-D` output is byte-identical to REF's.
- **Likely location:** `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` (~line 146) counts `report.ResponseHeaders` / `PseudoHeaders`; `Curl.Original.Protocol.Ftp.UnitLibrary/FtpSession.cs` and `FtpHeadHeaderLines.cs` must count every reply line written to the header stream that contains a colon.
- **Acceptance check:** the three commands print `5`, `8`, `7`.

## Acceptance criteria

- [ ] the three commands print `5`, `8`, `7`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project pins the corrected behaviour (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-18: Created from fitness review Curl-Fitness-Review-2026-11-18T0633, finding STD-111 (Low).