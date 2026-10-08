---
id: BL-1715
title: Build protocol handlers lazily per scheme to cut startup working set (AF-0063)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1715 — Build protocol handlers lazily per scheme to cut startup working set (AF-0063)

## Goal

`CurlComposition.CreateProtocolHandlers` builds only the handler a transfer's scheme needs, on first use, so a plain `http://` GET no longer pays to construct the other 16 handlers, the security context factory and the SASL authenticators at startup.

## Context

First half of BL-1682 (audit finding AF-0063: small-get peak working set 2.017x curl's). BL-1682's Notes measured that building only the HTTP handler saves ~370 KB (14.18 -> 13.81 MB, 2.6%) on a loopback 1 KiB GET.

Where to start: `Curl.Console/CurlComposition.cs`, `CreateProtocolHandlers` (around line 116). Suggested shape: a `LazyProtocolHandler` in `Curl.Console` that takes the schemes it serves and a factory, reports `SupportedSchemes` without building anything, and constructs the real handler on its first `ExecuteAsync` (single run, so no locking beyond `Lazy<T>`'s default is needed). The security context factory, the HTTP authenticator and the AWS signer move behind the same laziness so a `file://` or `http://` run without auth never builds them. Keep `EndPointReportingProtocolHandler` wrapping each handler, and keep `RoutingFtpProtocolHandler` sharing the one HTTP handler. Tests in `Curl.Console.UnitTests` that look handlers up by type may need to unwrap the lazy handler.

## Acceptance criteria

- [ ] A run whose only URL is `http://` constructs no handler other than HTTP's (a unit test with counting factories, or equivalent, shows it).
- [ ] Every scheme still reaches its handler; the existing Curl.Console tests pass unchanged in behaviour.
- [ ] Curl.Console stays at 100% line and branch coverage, complexity at most 10, CRAP at most 30.
- [ ] Measured: median peak working set of the native AOT build on a loopback 1 KiB `-s -o NUL` GET (BL-1682's method), before and after, recorded under Notes.
- [ ] `dotnet build` and the fast tests pass.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
