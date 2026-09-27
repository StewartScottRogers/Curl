---
id: BL-271
title: Fail a -L hop whose redirect URL does not parse with exit 1 instead of throwing
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-179]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-271 — Fail a -L hop whose redirect URL does not parse with exit 1 instead of throwing

## Goal

Under `-L`, a 3xx whose `TransferReport.RedirectUrl` is not a valid URL (for example `Location: http://[bad`) ends the transfer with exit 1 and curl's message instead of throwing `UriFormatException` from `RedirectFollower`.

## Context

- Found in BL-179 (2026-09-26). The HTTP handler reports a `Location` that does not parse exactly as curl 8.21.0's `%{redirect_url}` does: whole and unchanged (measured: `Location: http://[bad` gives `%{redirect_url}` `http://[bad`).
- `Curl.Core.UnitLibrary/RedirectFollower.cs` does `Uri next = new(target);` before its refusal checks, so that value throws.
- Measure curl 8.21.0 with `-L` against a server sending `Location: http://[bad` for the exact exit code and message before pinning them; the follower already uses `The redirect target URL could not be parsed: ...` for an unknown scheme.

## Acceptance criteria

- [x] A `RedirectFollowerTests` test with a hop reporting `RedirectUrl = "http://[bad"` and `ResponseCode = 302` gets the measured exit code and message, and no exception.
- [x] `dotnet build -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary` reports no failing member.

## Notes

- Measured curl 8.21.0 (Schannel, Windows) on 2026-09-26 against a local server replying `302` with the `Location` below, `curl -sS -L`:
  - `http://[bad` and `http://[::1/x` -> exit 3, `The redirect target URL could not be parsed: Bad IPv6 address`.
  - `http://%zz/` -> exit 3, `... Bad hostname`.
  - `http://a:99999/` -> exit 3, `... Port number was not a decimal number between 0 and 65535`.
  - `http://[bad` with `--max-redirs 0` -> exit 47 `Maximum (0) redirects followed`: the limit is checked before the URL is parsed, so the follower keeps that order.
  - The acceptance criterion's "exit 1" guess was wrong; the measured exit 3 (`CurlExitCode.UrlMalformat`) is what is pinned.
- `RedirectFollower` now uses `Uri.TryCreate`; when it fails the reason is picked from the authority (after the last `@`): a leading `[` is `Bad IPv6 address`, a `:` followed by anything but a decimal up to 65535 is the port reason, anything else `Bad hostname`. The reason strings reuse `ProxyUrlParser`'s constants, which are curl's URL-parser texts. Default taken (rule 1): no attempt to reproduce every other curl URL-parser reason; these three cover what `Uri` rejects in practice for a redirect.
- Also measured: on every follower refusal except the limit, curl writes an empty `%{redirect_url}`; ours keeps the last hop's value. Filed as BL-289 rather than widening this task.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. -L on a redirect URL that does not parse exits 3 with curl's 'redirect target URL could not be parsed' reason instead of throwing
