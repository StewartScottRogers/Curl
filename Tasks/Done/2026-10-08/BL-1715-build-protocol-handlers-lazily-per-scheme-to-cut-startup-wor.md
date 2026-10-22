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
completed: 2026-10-07
---
# BL-1715 — Build protocol handlers lazily per scheme to cut startup working set (AF-0063)

## Goal

`CurlComposition.CreateProtocolHandlers` builds only the handler a transfer's scheme needs, on first use, so a plain `http://` GET no longer pays to construct the other 16 handlers, the security context factory and the SASL authenticators at startup.

## Context

First half of BL-1682 (audit finding AF-0063: small-get peak working set 2.017x curl's). BL-1682's Notes measured that building only the HTTP handler saves ~370 KB (14.18 -> 13.81 MB, 2.6%) on a loopback 1 KiB GET.

Where to start: `Curl.Console/CurlComposition.cs`, `CreateProtocolHandlers` (around line 116). Suggested shape: a `LazyProtocolHandler` in `Curl.Console` that takes the schemes it serves and a factory, reports `SupportedSchemes` without building anything, and constructs the real handler on its first `ExecuteAsync` (single run, so no locking beyond `Lazy<T>`'s default is needed). The security context factory, the HTTP authenticator and the AWS signer move behind the same laziness so a `file://` or `http://` run without auth never builds them. Keep `EndPointReportingProtocolHandler` wrapping each handler, and keep `RoutingFtpProtocolHandler` sharing the one HTTP handler. Tests in `Curl.Console.UnitTests` that look handlers up by type may need to unwrap the lazy handler.

## Acceptance criteria

- [x] A run whose only URL is `http://` constructs no handler other than HTTP's (a unit test with counting factories, or equivalent, shows it).
- [x] Every scheme still reaches its handler; the existing Curl.Console tests pass unchanged in behaviour.
- [x] Curl.Console stays at 100% line and branch coverage, complexity at most 10, CRAP at most 30.
- [x] Measured: median peak working set of the native AOT build on a loopback 1 KiB `-s -o NUL` GET (BL-1682's method), before and after, recorded under Notes.
- [x] `dotnet build` and the fast tests pass.

## Notes

- 2026-10-07, lane 9: `LazyProtocolHandler` (Curl.Console) names its schemes and builds its
  handler on first use through `Lazy<T>` (default thread safety, so `-Z` transfers racing to a
  scheme build it once). Every handler in `CreateProtocolHandlers` sits behind one, and the
  security context factory and the `RankedHttpAuthenticator` are `Lazy<T>` too, so a plain
  `http://` run builds only the HTTP handler (with the authenticator, which it needs);
  `RoutingFtpProtocolHandler` shares the one lazy HTTP handler. The scheme lists are written
  out in the composition; `CreateProtocolHandlers_EachLazyHandler_ReportsTheSchemesItsBuiltHandlerServes`
  builds every handler and checks they match, so a handler that adds a scheme fails it.
- `EndPointReportingProtocolHandler.Handler` now unwraps a lazy handler (building it), so the
  existing tests that look handlers up by type are unchanged; `GivenHandler` keeps the lazy one.
  No ADR: the shape is the task's own suggestion and changes no output.
- Coverage (Measure-CodeQuality, Curl.Console): 100% line, 100% branch, 0 failing members,
  worst CRAP 10.
- Measured (BL-1682's method: loopback HTTP/1.1 server answering 1 KiB,
  `curl -s -o NUL http://127.0.0.1:<port>/`, median `GetProcessMemoryInfo` peak working set
  over 15 runs, this machine): before 14.19 MB (BL-1682's Notes, same method and machine; this
  lane could not publish the pre-change commit in a second worktree), after 13,885,440 bytes
  (13.89 MB), about 300 KB less. Windows curl.exe 6,922,240 bytes in the same run: 2.006x.
  The rest of AF-0063 is BL-1682's second half.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Handlers built lazily per scheme; http-only run builds only HTTP's; 13.89 MB peak (was 14.19)
