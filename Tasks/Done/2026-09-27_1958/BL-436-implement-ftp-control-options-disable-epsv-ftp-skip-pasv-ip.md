---
id: BL-436
title: Implement FTP control options: --disable-epsv, --ftp-skip-pasv-ip off, --ftp-method, --ftp-create-dirs, -l/--list-only, -Q/--quote
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-431, BL-435]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Record-CurlExchange.ps1, Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md]
requirement: none
created: 2026-09-27
completed: 2026-09-27
---
# BL-436 — Implement FTP control options: --disable-epsv, --ftp-skip-pasv-ip off, --ftp-method, --ftp-create-dirs, -l/--list-only, -Q/--quote

## Goal

`FtpProtocolHandler` sends curl 8.21.0's conversation when the transfer context carries `--disable-epsv`, `--no-ftp-skip-pasv-ip`, `--ftp-method`, `--ftp-create-dirs`, `-l/--list-only` or `-Q/--quote`, with curl's output and exit code for each, including their failures.

## Context

- Second half of the "FTP control options" work; the planner split it (it spans parser, contract, console and handler). BL-435 parses the six options and carries them on `ITransferContext`; this task reads them in the handler. The handler came from BL-431; its passive-only conversation is ADR-0093 (`Documentation/Planning/Decisions/ADR-0093-ftp-downloads-hold-curls-measured-conversation-in-passive-mode-only.md`), whose Consequences list these options as not implemented. If this task turns out too big for one run, split it in two along the line `--disable-epsv`/`--ftp-skip-pasv-ip`/`--ftp-method`/`--ftp-create-dirs` versus `-l`/`-Q`, filing the second half as a new task rather than widening this one.
- Handler code: `Curl.Protocol.Ftp.UnitLibrary/FtpProtocolHandler.cs`, `FtpDownloadSession.cs`, `FtpControlChannel.cs`, `FtpPassiveReply.cs` (the `227` address is ignored today, as the default `--ftp-skip-pasv-ip` does), `FtpUrlPath.cs` (one `CWD` per directory today, which is curl's default `--ftp-method multicwd`).
- The touches include `Curl.Protocol.Abstractions.UnitLibrary` in case a context property from BL-435 needs a small adjustment; the options themselves are BL-435's.
- Measure every case with `Record-CurlExchange.ps1 -Ftp` against curl 8.21.0 before pinning, extending the recorder when it falls short (e.g. `MKD`, `NLST` and arbitrary `-Q` verbs answer `502` by default; `-FtpReply 'MKD=257 created'` overrides one; `NLST` needs a data connection like `LIST`). Cases: `--disable-epsv` (straight to `PASV`), `--no-ftp-skip-pasv-ip` with a `227` naming another address, `--ftp-method singlecwd` and `nocwd` on a two-level path, `--ftp-create-dirs` with `CWD` refused then `MKD` accepted and refused, `-l` on a directory (`NLST`), `-Q` commands before and after the transfer with each prefix curl 8.21.0 accepts, and a `-Q` command the server refuses.
- Exit codes come from `CurlExitCode` (`Curl.Protocol.Abstractions.UnitLibrary/CurlExitCode.cs`). Upstream: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; the measured curl 8.21.0 wins.

## Acceptance criteria

- [x] Named tests in `Curl.Protocol.Ftp.UnitTests` pin, for each case listed in Context, the command bytes sent, output bytes written and exit code, each matching curl 8.21.0 as recorded with `Record-CurlExchange.ps1 -Ftp`.
- [x] A refused `MKD` under `--ftp-create-dirs` and a refused `-Q` command each return the `CurlExitCode` and message curl 8.21.0 printed, with `QUIT` sent or not as curl did.
- [x] ADR-0093 gains an addendum (or a new ADR supersedes its "passive mode only" scope notes) recording the measured behaviour of each option, marked "Decided by Claude under Stewart's delegation".
- [x] `dotnet build Curl.Protocol.Ftp.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; no new test needs `TestCategory=Integration`.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` reports 100% line and branch coverage, complexity at most 10 and CRAP at most 30 for every member of `Curl.Protocol.Ftp.UnitLibrary`.

## Notes

- Added the ADR-0093 file to `touches`: the acceptance criteria ask for its addendum, and no task in `Doing` (only BL-771, `Curl.Networking.UnitTests`) names it.
- Not split: the whole task fitted one run. `Curl.Protocol.Abstractions.UnitLibrary` needed no change; BL-435's properties were enough.
- Recorder: `-FtpReply` overrides for one verb are now answered in turn (the last repeating), `{DATAPORT}`, `{DATAPORT_HI}` and `{DATAPORT_LO}` expand in replies, and `NLST` gets a data connection like `LIST`.
- Measured findings worth knowing: `--ftp-create-dirs` applies to downloads too and never fails on `MKD` itself (only the `CWD` after it); `-l` drops a URL's file name and lists its directory; `+` quotes under `-I` go after `REST 0`; post-transfer (`-`) quotes run on every success path (empty listing, nothing left to resume, whole upload already there, after `ABOR`) and never after a failure; a refused quote before the transfer skips `QUIT`, after it sends `QUIT`.
- Not pinned: curl's exit 7 text for a `227` address it cannot reach (`Failed to connect to <control> via <data> after <n> ms: ...`) is timing-dependent and is the connector's message; the handler passes the connector's result through, as for every data connect failure.
- Follow-up filed: BL-446, `multicwd` drops the leading `/` of a `//abs` path (a divergence that predates this task).
- New code: `FtpQuoteCommand`, `FtpQuoteCommands` (sorting `-Q` values), `FtpUrlPath.Parse(path, method)` with `ListArgument`, `FtpPassiveReply.TryParsePasv` with the address; tests in `FtpProtocolHandlerPathOptionTests` and `FtpProtocolHandlerQuoteTests` (57 new cases, 194 in the project), with a `MutableContext` fake.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Backlog. Orphaned by a stopped shift: no lane worktree or branch held its work; requeued.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. ftp:// honours --disable-epsv, --no-ftp-skip-pasv-ip, --ftp-method, --ftp-create-dirs, -l and -Q as curl 8.21.0 does
