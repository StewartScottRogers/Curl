---
id: BL-5951
title: Report curl's %{redirect_url} for an authentication or 417 resend the redirect limit refuses
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.RedirectLimitVerbose.cs]
requirement: none
created: 2026-11-25
completed: 2026-11-25
---
# BL-5951 — Report curl's %{redirect_url} for an authentication or 417 resend the redirect limit refuses

## Goal

When `--max-redirs` refuses an authentication retry or a resend after a 417, `%{redirect_url}` is what curl 8.22.0 writes: the URL the resend would go to.

## Context

Found by BL-5944. `Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500` for `curl --digest -u a:b --max-redirs 0 -w "%{redirect_url}" http://127.0.0.1:PORT/` against a Digest 401: curl 8.22.0 writes `http://127.0.0.1:PORT/` (the same for `--anyauth`, `-f`, a `Connection: close` 401 and a 417); Curl writes nothing. See `RefusingResendPastRedirectLimit` in `HttpProtocolHandler.ResponseExchange.cs`. Check that the engine does not follow the URL of this failed result under `-L`.

## Acceptance criteria

- [x] For a 401 and a 417 refused at `--max-redirs 0`, `%{redirect_url}` is curl 8.22.0's, pinned by a unit test.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added `HttpRedirectLocation.cs` to `touches`: the URL is built there, the class that gives
  `%{redirect_url}`. No other task in Doing named it.
- Measured curl 8.22.0 (Windows) with `Record-CurlExchange.ps1 -HoldOpenMilliseconds 700`,
  `--max-redirs 0 -w "%{redirect_url}"`: it writes the request's own URL rebuilt, not the
  response's `Location`. Scheme lower case; user information as typed (none from `-u`); host
  as curl holds it (`127.1` -> `127.0.0.1`, `%6cocalhost` -> `localhost`, case kept); a typed
  port kept even when default; dot segments removed even under `--path-as-is`; empty query and
  fragment dropped (`%{url_effective}` keeps them). Same for a 407 and after a followed `-L`
  hop (the hop's URL).
- A 417 is refused only when it arrives while the body waits for `100 Continue`
  (`-RespondAfterBodyBytes 0`); one that arrives after the body was sent is exit 0 with an
  empty `%{redirect_url}`, which Curl already matched (both measured with
  `-RequestReadMilliseconds 4000`, so the 417 cannot race the 1000 ms continue wait).
- The built Curl's stdout and exit code were byte-identical to curl's in the 16 cases compared
  by file hash, and matched in the late-417 case.
- The engine does not follow it: `RedirectFollower.RedirectTarget` takes only a successful
  3xx result.
- The IPv6 zone id form is pinned from curl's `%{url_effective}` (`[::1%251]`); the connect
  itself failed, so no refused resend was measured with a zone id.
- Quality run for `Curl.Original.Protocol.Http.UnitLibrary`: 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: ababe0aa 3d836d5e.
- 2026-11-25: Doing -> Done. A resend --max-redirs refuses now reports curl's %{redirect_url}, the URL it would go to
