---
id: BL-299
title: Make -m span the whole -L redirect chain, not each hop
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-174]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions/ADR-0040-http-enforces-max-time-and-connect-timeout-in-the-handler.md]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-299 — Make -m span the whole -L redirect chain, not each hop

## Goal

`curl -L -m 2` ends a redirect chain with exit 28 once 2 seconds have passed since the first request, however many hops it has taken.

## Context

- Found in BL-174. `HttpTransferDeadline` (Curl.Protocol.Http.UnitLibrary) starts the `-m` clock when `HttpProtocolHandler.ExecuteAsync` is called, and `RedirectFollower` (Curl.Core.UnitLibrary) calls the handler once per hop, passing `MaxTime` unchanged (RedirectFollower.cs, the context copy), so each hop gets a fresh `-m`. curl's `-m` limits the whole operation (https://curl.se/docs/manpage.html#-m).
- ADR-0040 records the per-handler design and names this gap.
- Likely approach: the follower passes each hop the time left (`MaxTime` minus the elapsed time on `TimeProvider`), and a hop started with none left fails with exit 28. Measure curl 8.21.0 (`/mingw64/bin/curl`) against a loopback redirect chain whose second hop stalls, and pin the N and M it prints.

## Acceptance criteria

- [x] A `RedirectFollowerTests` test on `FakeTimeProvider`: with `MaxTime` 2 s, a first hop that takes 1.5 s and a second hop that stalls ends with exit 28 at 2 s total, with the measured message.
- [x] The measured curl 8.21.0 command and output are recorded in Notes.
- [x] `dotnet build` is clean, `dotnet test --filter "TestCategory!=Integration"` passes, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage for the libraries touched.

## Notes

- **Measured** (curl 8.21.0 mingw Schannel, 2026-09-27, Python loopback server on 127.0.0.1:18299:
  `GET /a` answers `302 Location: /b` with `Content-Length: 0` after 1.5 s on a kept-alive
  connection; `GET /b` gets no answer):
  `curl -sS -L -m 2 http://127.0.0.1:18299/a -o /dev/null -w '%{num_redirects} %{time_total} %{url_effective}
'`
  printed `curl: (28) Operation timed out after 2006 milliseconds with 0 bytes received`,
  then `1 2.006345 http://127.0.0.1:18299/b`, exit 28. With `/b` answering
  `200` + `Content-Length: 100` + `hello` and stalling: `Operation timed out after 2006
  milliseconds with 5 out of 100 bytes received`, `%{num_redirects}` 1. So `-m` and N count
  from the first request (curl's `t_startop`); M and T are the stalled hop's own. Some runs
  on this machine stalled on the first hop instead (`num_redirects` 0) because the test
  server was slow to take the connection; those were discarded.
- **Decision (ADR-0040 amendment, decided by Claude under Stewart's delegation):** new
  `ITransferContext.OperationStarted` (timestamp on the transfer's `TimeProvider`, `null` =
  starts with this call). `RedirectFollower` sets it on every hop after the first (keeping
  one it was given); `HttpTransferDeadline` runs `-m` for what is left since then and prints
  the operation message's N from it. The connect message still counts from the handler call,
  as curl's does (`t_startsingle`). Passing each hop `MaxTime` minus the elapsed time was
  rejected: it would print the hop's elapsed time as N.
- **Touches widened** to `Curl.Protocol.Abstractions.UnitLibrary`/`.UnitTests` (the new
  context property) and ADR-0040. No task in `Doing` (BL-239: Curl.Console; BL-340:
  Gopher) names them.
- `HttpTransferDeadline`'s constructor reached complexity 12 with the new branch; the `-m`
  source moved into `MaxTimeElapsed`.
- Gates: `dotnet build` clean; fast tests green (Core 676, Http 742, Abstractions 371 passed);
  `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary,Curl.Protocol.Http.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary`
  100% line and branch, 0 failing members.
- Review (code-reviewer): no must-fix. The TFTP handler honours `MaxTime` but not
  `OperationStarted`, so a redirect to `tftp://` still restarts `-m`; the remarks and
  ADR-0040 now say so, and BL-348 is filed to fix it.
- Seen, already filed: `RedirectFollower.NextHop` does not carry `ITransferContext.Proxy`
  (BL-329 chooses the proxy again per hop).

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. curl -L -m N now limits the whole redirect chain: -m and the Operation timed out message count from the first request
