---
id: BL-1322
title: Hand -r text that names no range to every handler but SSH's instead of refusing it before the transfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1332, BL-1333, BL-1334]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: FR-007
created: 2026-10-03
completed:
---
# BL-1322 — Hand -r text that names no range to every handler but SSH's instead of refusing it before the transfer

## Goal

`curl -r 5-2` (and `abc`, `-0`) on any URL but `sftp://` and `scp://` runs the transfer as curl 8.21.0 does (the text is ignored where curl ignores it); FTP logs in and ends with exit 0 after `EPSV` (BL-1333); file opens the file, writes the progress meter and `* shutting down connection #0`, and ends with `curl: (33) Requested range was not delivered by the server` (BL-1334); `-I` over file exits 0 and a missing file exits 37.

## Context

- Today `Curl.Console/CurlCommandRunner.cs` `TryParseRange` (near line 3860) returns `false` for range text that names no range on any scheme but `http`/`https`, and the transfer path near line 3246 returns `ByteRangeParser.NotDeliveredFailure` before any handler, connection or progress meter.
- After BL-1333 and BL-1334 the FTP and file handlers act on `ITransferContext.RangeText` with a `null` `Range` themselves, so `TryParseRange` must let `ftp`, `ftps` and `file` through with `range = null` exactly as it does `http`/`https`.
- No other curl 8.21.0 handler parses `-r` text: `Curl_range` is called only from `lib/ftp.c` (line 2327) and `lib/file.c` (line 476), `lib/rtsp.c` lines 463-470 and the HTTP/WebSocket request writer send it verbatim as `Range`, and SSH has its own `Curl_ssh_range` (`lib/vssh/libssh2.c` line 1339). Measured 2026-10-03: `curl -s -r 5-2 dict://127.0.0.1:1/x` and `gopher://127.0.0.1:1/x` both exit 7 (they connect; the text is ignored). So only `sftp` and `scp` keep the up-front refusal here.
- Measured 2026-10-03 with the installed curl 8.21.0 (mingw, Schannel) against a 12-byte file:
  - `curl -r 5-2 file:///.../f.txt`: stderr is the progress meter (`  % Total    % Received % Xferd  Average Speed ...` header lines and one `\r  0      0   0      0 ...` row), then `curl: (33) Requested range was not delivered by the server`, exit 33.
  - `curl -v -r 5-2 file:///.../f.txt`: the progress meter, then `* shutting down connection #0`, then the `curl: (33)` line.
  - `curl -sv -r 5-2 file:///.../f.txt`: only `* shutting down connection #0`, exit 33.
  - `curl -sv -I -r 5-2 file:///.../f.txt`: `* shutting down connection #0` and the header block on stdout, exit 0.
  - `curl -v -r 5-2 file:///.../nonexist.txt`: `* Could not open file ...` and `curl: (37) ...`, no `shutting down` line, exit 37.
  - `Record-CurlExchange.ps1 -Ftp -CurlArgs '-sv','-r','5-2','ftp://127.0.0.1:PORT/f.txt'`: exit 0, stdout empty, the session ending `* Remembering we are in directory ""`, `* Connection #0 to host 127.0.0.1:PORT left intact` (BL-1333 has the full transcript).
- The console already writes `* shutting down connection #0` for a finished file transfer (`CurlCommandRunnerFileConnectionNumberTests`); the exit 33 path must reach that same code rather than return early.
- No option is added or changed, so `--ai-help` needs no change.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` runs `-sv -r 5-2 file://<temp file>` (a path from `Path.GetTempPath()`, drive-less on every platform as the existing file tests build it) and asserts standard error is exactly `* shutting down connection #0` + newline and exit 33.
- [ ] A test runs `-v -r 5-2 file://<temp file>` with the progress meter enabled the way `CurlCommandRunnerVerboseProgressMeterTests` enables it and asserts the meter comes before `* shutting down connection #0`, which comes before `curl: (33) Requested range was not delivered by the server`.
- [ ] A test asserts `-sv -I -r 5-2 file://<temp file>` exits 0 and writes the header block, and one asserts `-r 5-2` on a missing file exits 37 with no `shutting down` line.
- [ ] A test runs `-sv -r 5-2 ftp://...` through the console's FTP test fakes and asserts exit 0 with no `curl: (33)` line.
- [ ] A test pins that `-r 5-2` on `dict://127.0.0.1:1/x` is not refused: the transfer goes on to connect (curl 8.21.0 exits 7 there, measured 2026-10-03, as it does for `gopher://`), since no curl handler but FTP's, file's and SSH's parses `-r`; and that a `ws://` upgrade under `-r 5-2` sends `Range: bytes=5-2` from `RangeText`, as `WsUpgradeRequestFormatter.RangeValue` already builds it.
- [ ] `-r 5-2` on `sftp://` and `scp://` keeps today's exit 33 refusal (curl parses it in `Curl_ssh_range`; matching that is a separate task), and a test pins it.
- [ ] `dotnet build Curl.Console.UnitTests -warnaserror` is clean; `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes; `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` reports no failing member.

## Notes

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
