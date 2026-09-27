---
id: BL-147
title: Run the vendored upstream cases as data-driven MSTest with a passing-case ratchet
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-005, BL-143, BL-145, BL-146]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.Console/Curl.Console.csproj]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-147 — Run the vendored upstream cases as data-driven MSTest with a passing-case ratchet

## Goal

`dotnet test` runs every vendored upstream case in process, fails on a regression of any case on
the committed passing list, and reports every other case as `Inconclusive` with its reason, so
the upstream pass rate is a number anyone can recompute.

## Context

- ADR-0013, decisions 4, 5 and 6.
- The runner is `CurlComposition.CreateRunner(standardOutput, standardError, standardInput,
  connector, datagramConnector)` in `Curl.Console/CurlComposition.cs`; `Curl.Console` needs
  `<InternalsVisibleTo Include="Curl.Conformance.UnitTests" />` beside the existing one.
- `<client><command>` is split into arguments as upstream's `runtests.pl` does (shell-style
  quoting); `%LOGDIR` is a fresh temporary directory per case (BL-145); `<client><file>` is
  written there before the run; `<verify><file>` is compared after.
- Comparison: received bytes against `<verify><protocol>` after `<strip>` / `<strippart>`;
  standard output against `<stdout>`; standard error against `<stderr>` when present; exit code
  against `<errorcode>` (0 when absent).
- Skipped with a reason, not counted as runnable: `<tool>` cases (libcurl, not the tool),
  a `<server>` the harness does not emulate yet, a `<features>` entry Curl lacks, an unknown
  variable, an unsupported `<servercmd>`.
- `http` is not yet registered in `CurlComposition.CreateProtocolHandlers`, so the first passing
  list may be short (`file://` cases, for example); that is expected.

## Acceptance criteria

- [x] One `[DynamicData]` test method in `Curl.Conformance.UnitTests`, in
      `TestCategory("Conformance")`, yields one row per vendored case, named by its test number.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` lists case numbers; a listed case that
      fails fails the test with its first difference; an unlisted failing case is `Inconclusive`
      with its first difference; an unlisted passing case is `Inconclusive` saying it can be listed.
- [x] Every case that passes on the day the task lands is on the list, and the pass rate (listed
      over runnable) is recorded in `Notes` and in `Curl.Conformance.UnitTests/CLAUDE.md`.
- [x] The whole conformance run takes under 60 seconds on the development machine (time recorded
      in `Notes`) and opens no socket.
- [x] 100% line and branch coverage of `Curl.Conformance.UnitLibrary`, complexity at most 10 per
      method, per `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Emulations for FTP, IMAP, POP3, SMTP, TFTP and the other servers are follow-up tasks, filed
  once this runner exists (ADR-0013, decision 7). Filed 2026-09-26 for the four servers that
  skip the most cases: BL-289 (FTP), BL-290 (SMTP), BL-291 (IMAP), BL-292 (POP3).
- Result on 2026-09-26 (Windows, Schannel feature set): 2013 cases; 137 pass and are listed,
  324 fail, 1552 skipped. Pass rate 137 / 461 runnable = 29.7%. The pass set was identical
  across three consecutive runs. Largest skip reasons: an unsupported `<servercmd>` or strip
  form (312), no `%FTPPORT` (238), no `%SMTPPORT` (87), missing features `unittest` and
  `proxy` (71 each), no `%IMAPPORT` (70).
- Timing: the conformance category runs in about 3 seconds (`dotnet test` reports 1-3 s;
  4-8 s wall clock including host start-up), well under the 60-second limit. No socket: HTTP
  goes to `SwsHttpServerConnector`, UDP to `UnreachableDatagramConnector`.
- Choices taken as defaults (all within ADR-0013; no new ADR needed):
  - Per-case time limit of 10 seconds, measured with an injected `TimeProvider`; a case over
    it fails with that as its difference rather than hanging the suite.
  - `%LOGDIR` is a fresh folder under the test output directory (`log/test<N>-<guid>`), not
    the system temporary folder, whose path can contain a space that an unquoted `%LOGDIR`
    in a command would split; upstream's relative `log/` never has one.
  - The proxy environment variables are cleared once per class, as `runtests.pl` does.
  - Platform features follow the platform's curl build (Schannel on Windows, OpenSSL
    elsewhere), per CLAUDE.md, through `UpstreamCurlPlatform`.
  - A listed case that becomes skipped also fails the ratchet, so a screening change cannot
    silently drop a passing case.
- Refactored `UpstreamFirstDifference.Escape` and `UpstreamPerlSubstitution.Parse`, which
  Cobertura measured at complexity 14 and 12, to at most 10.
- Code review after the first commit, all acted on: a timed-out run cannot be cancelled
  (`CurlCommandRunner.RunAsync` takes no token and `Curl.Console` code is outside `touches`),
  so its memory streams are no longer disposed under it; a file part outside the case's log
  directory now skips the case; `$` and a backtick inside double quotes now count as shell
  syntax; `<reply><data base64>` is decoded before comparison; a strip pattern that times out
  is the case's difference instead of an exception; the timeout test uses a fake
  `TimeProvider`; the no-op clearing of proxy variables was removed (the conformance
  composition never reads them). Pass counts unchanged (137 / 324 / 1552).
- Decided: the conformance tests touch the file system but stay out of `Integration`, because
  ADR-0013 and this task put the ratchet in the fast suite and upstream's cases need real
  `%LOGDIR` files; recorded in `Curl.Conformance.UnitTests/CLAUDE.md`.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Ratchet runs 2013 upstream cases in ~3 s; 137 listed, pass rate 29.7%; gates green
