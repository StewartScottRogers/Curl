---
id: BL-805
title: Write curl's verbose line for a scheme --proto disables
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-523]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-805 — Write curl's verbose line for a scheme --proto disables

## Goal

Under `-v`, a URL whose scheme `--proto` excludes writes `* Protocol "http" is disabled` to stderr before the `curl: (1) Protocol "http" is disabled` line, as curl 8.21.0 does.

## Context

- Found while doing BL-523. Measured with `Record-CurlExchange.ps1` against Windows curl 8.21.0 on 2026-09-28:
  `curl -v --proto -http http://127.0.0.1:48523/` exits 1 with stderr
  `* Protocol "http" is disabled` CRLF `curl: (1) Protocol "http" is disabled` CRLF, and opens no connection.
- BL-523 made `ProtocolDispatcher` refuse the scheme (`Curl.Core.UnitLibrary/ProtocolDispatcher.cs`); the verbose line is not written today.
- Check first whether the `(in redirect)` refusal and `Protocol "x" not supported` also have a verbose line in curl, and cover them the same way if they do.

## Acceptance criteria

- [x] Measured with `Record-CurlExchange.ps1` for `-v --proto -http`, `-v --proto =http bogus://...` and `-v -L` with `Location: file:///dir/x`; stderr copied into Notes.
- [x] `Curl.Console.UnitTests` pin the verbose stderr for each measured case byte for byte (platform-neutral, drive-less `file:///dir/x`).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

- Measured 2026-10-01, Windows curl 8.21.0, `Record-CurlExchange.ps1` (all exit 1, CRLF from Windows text mode):
  - `-v --proto -http http://127.0.0.1:48805/`: `* Protocol "http" is disabled` / `curl: (1) Protocol "http" is disabled`, no connection.
  - `-v --proto =http bogus://127.0.0.1:48806/`: `* Protocol "bogus" not supported` / `curl: (1) Protocol "bogus" not supported`.
  - `-v -L`, `302` with `Location: file:///dir/x`: the usual request/response verbose lines, then
    `* Issue another request to this URL: 'file:///dir/x'` / `* Protocol "file" is disabled (in redirect)` /
    `curl: (1) Protocol "file" is disabled (in redirect)`.
- So all three refusals have the verbose line (libcurl's `failf` echoes under `-v`). The refusals live in
  `Curl.Core.UnitLibrary` (`ProtocolDispatcher`, `RedirectFollower`), so the fix reports each refusal message
  to the transfer's `Events.ReportInfo` there; the console's existing `-v` writer prints it as `* ...`.
  `Curl.Core.UnitLibrary` and `Curl.Core.UnitTests` were added to `touches`: no other task in Doing on
  `origin/work/dark-factory` named them.
- The runner writes verbose info lines ending in LF and the `curl: (N)` line in `Environment.NewLine` (the
  existing convention in `Curl.Console.UnitTests`); the new tests pin that.
- The other `RedirectFollower` refusals (`--max-redirs`, unparsable target) would also get a `* ` line from
  curl's `failf`; they were not measured here and are left as they are.
- `Measure-CodeQuality.ps1`: `Curl.Console` 0 failing members; `Curl.Core.UnitLibrary` 0 failing members (its
  pre-existing branch gaps are reported as warnings, not failing members). The report moved into
  `ProtocolDisabledRefusal` to keep `RedirectFollower.Refusal` at complexity 10.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. -v writes curl's * line for --proto disabled, not supported and (in redirect) refusals
