---
id: BL-5951
title: Report curl's %{redirect_url} for an authentication or 417 resend the redirect limit refuses
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.ResponseExchange.cs, Curl.Original.Protocol.Http.UnitTests/HttpProtocolHandlerTests.RedirectLimitVerbose.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5951 — Report curl's %{redirect_url} for an authentication or 417 resend the redirect limit refuses

## Goal

When `--max-redirs` refuses an authentication retry or a resend after a 417, `%{redirect_url}` is what curl 8.22.0 writes: the URL the resend would go to.

## Context

Found by BL-5944. `Record-CurlExchange.ps1 -HoldOpenMilliseconds 1500` for `curl --digest -u a:b --max-redirs 0 -w "%{redirect_url}" http://127.0.0.1:PORT/` against a Digest 401: curl 8.22.0 writes `http://127.0.0.1:PORT/` (the same for `--anyauth`, `-f`, a `Connection: close` 401 and a 417); Curl writes nothing. See `RefusingResendPastRedirectLimit` in `HttpProtocolHandler.ResponseExchange.cs`. Check that the engine does not follow the URL of this failed result under `-L`.

## Acceptance criteria

- [ ] For a 401 and a 417 refused at `--max-redirs 0`, `%{redirect_url}` is curl 8.22.0's, pinned by a unit test.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-25: Created.
