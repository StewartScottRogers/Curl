---
id: BL-5115
title: Leave %{redirect_url} empty when -f ends the transfer on a 4xx response
priority: Low
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Original.Protocol.Http.UnitTests/FailedResponseRedirectUrlTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5115 — Leave %{redirect_url} empty when -f ends the transfer on a 4xx response

## Goal

With `-f`, a 4xx response that carries a `Location` header leaves `%{redirect_url}` empty (and `%{json}` `redirect_url` null), as upstream curl does, instead of reporting the Location URL.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-065 (Low, Confirmed twice; the Linux OpenSSL build agrees with Windows curl). Area: `-w %{redirect_url}` and `%{json}` with `--fail`.

**Repro.** With a server answering `/status/404` with `404`, `Location: /redir-target` and a short body: `curl -s -o NUL -f -w "[%{redirect_url}]\n" http://127.0.0.1:20000/status/404; echo $?`

**Expected (upstream curl).** `[]`, exit 22. (Without `-f` both print `[http://127.0.0.1:20000/redir-target]`.)

**Actual.** `[http://127.0.0.1:20000/redir-target]`, exit 22; `%{json}` has the URL where upstream has `null`.

**Where to start.** `Curl.Original.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs`: `exchange.RedirectUrl` is set (about line 1454) and reported (about line 3675) even when the `--fail` path ends the transfer. Keep it unset (or unreported) when `--fail` ends the transfer on a 4xx/5xx response; the non-`-f` behaviour and real redirects (3xx) must not change.

## Acceptance criteria

- [ ] New tests in `Curl.Original.Protocol.Http.UnitTests/FailedResponseRedirectUrlTests.cs` prove that with `--fail` a 404 with `Location: /redir-target` yields an empty redirect URL in the transfer result (so `%{redirect_url}` is empty and `%{json}` reports null) and exit 22.
- [ ] Regression tests prove that without `--fail` the same 404 still reports the absolute Location URL, and a 302 with `-f` still reports it.
- [ ] Tests are platform-neutral, using the in-memory connection stand-ins already in the HTTP unit tests.
- [ ] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python (CLAUDE.md).
- Review acceptance check, as a hand check: the repro command prints `[]` and exits 22.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 890da18b a2c2112a 8d854bd5 a0b49a29.
