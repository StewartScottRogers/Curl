---
id: BL-1356
title: Cover the eleven Curl.Console members below the quality gates
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1356 — Cover the eleven Curl.Console members below the quality gates

## Goal

Every member of `Curl.Console` meets the quality gates: `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage, no method above cyclomatic complexity 10 and no CRAP score above 30.

## Context

- Found in BL-1287 (2026-10-03): `Measure-CodeQuality.ps1 -Library Curl.Console` reported 100% line, 99.06% branch and 11 failing members, none of them changed by BL-1287: `CurlCommandRunner.TransferAllGroupsAsync` (branch 85.71, complexity 14), `CurlCommandRunner.WriteProgressAsync` (91.67, 12), `CurlComposition.CreateProtocolHandlers` (83.33, 12), `UrlTransfer..ctor` (66.67, 12), `TransferCredentialLookup.CredentialsOf` (70), `HttpVersionMapping.ToHttpVersionPreference` (88.89), `CurlCommandRunner.RunAsync` (66.67), `CurlCommandRunner.TransferAllAsync` (66.67), `OutputFileOpenWarning.ReasonFor` (83.33), `TransferProgressRecorder.ReportUploaded` (75), `CurlCommandRunner.UrlOutputOf` (50).
- Complexity above 10 is split into private methods; uncovered branches get tests in `Curl.Console.UnitTests`. Thresholds in `CodeMetricsConfig.txt` stay as they are.

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line, 100% branch and 0 failing members.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- Pipeline `feature`, delivered directly: coverage and complexity work in one project needs no
  architect plan. Result: 994 members, 100% line, 100% branch, 0 failing, worst CRAP 10.
- The three async `finally` blocks with an `await` (`RunAsync`, `TransferAllGroupsAsync`,
  `TransferAllAsync`) left compiler-written rethrow branches no test can take (one is for a thrown
  non-`Exception`). They now go through `CurlCommandRunner.ThenCleanUpAsync`, which waits for the
  work however it ends (`Task.WhenAny`), runs the clean-up, then awaits the work: the same order and
  exception behaviour as `finally`, with no such branches. This also brought `TransferAllGroupsAsync`
  from complexity 14 to under 10.
- `WriteProgressAsync` (complexity 12): the meter-end condition moved to `EndsWithProgressMeter`;
  `eventStandardError` is always open by then, so `?.` became `!`.
- `UrlOutputOf` and the `UrlTransfer` constructor: the parser gives every URL an output entry at its
  own index (`CommandLineOptions.AddUrl`), so the out-of-range and null branches were unreachable
  and are gone.
- `CurlComposition.CreateProtocolHandlers`: the `OperatingSystem.IsWindows()` picks moved to
  `LdapDialectFor` and `SshAlgorithmPreferencesFor`, tested for both platforms.
- `ToHttpVersionPreference` and `OutputFileOpenWarning.ReasonFor`: the enum switch's out-of-range
  branch is covered with an undefined value (and `FileAccessStatus.Ok`).
- `TransferProgressRecorder.ReportUploaded`: a test passes an upload to a `-Z` share.
- `TransferCredentialLookup.CredentialsOf`: measured curl 8.21.0 (Schannel) on 2026-10-03 with
  `Record-CurlExchange.ps1 --netrc-file`: `machine 127.0.0.1 password np` sends `Basic Om5w` (`:np`)
  bare and `Basic dXU6bnA=` (`uu:np`) for `http://uu@host/`; `machine 127.0.0.1 login lo` with
  `http://:up@host/` sends `Basic bG86` (`lo:`) - curl drops the URL's password. Curl sent `lo:up`;
  fixed by never filling an entry's missing password from the URL, which also removed the branch.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. Curl.Console meets every quality gate: 100% line and branch, 0 failing members; netrc entry without a password no longer takes the URL's
