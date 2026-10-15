---
id: BL-2283
title: Move the per-transfer setup (URL refusals, proxy and credential choice, SSH options, cookies, HSTS, alt-svc) from CurlCommandRunner into TransferPerformer
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2282]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2283 — Move the per-transfer setup (URL refusals, proxy and credential choice, SSH options, cookies, HSTS, alt-svc) from CurlCommandRunner into TransferPerformer

## Goal

The per-transfer setup that libcurl does before connecting - URL parse and refusals, proxy and credential choice, SSH option resolution, cookie loading, the HSTS upgrade, the alt-svc cache and setup failures - lives in `TransferPerformer`, not `CurlCommandRunner`; behaviour is byte for byte unchanged.

## Context

ADR-0506, plan phase 1 step 2; follows BL-2282. Candidates in `Curl.Console/CurlCommandRunner.cs` (names as of 2026-10-15): `TryParseTransferUrl`, `UrlRejectedFailure`, `ReportUrlRejected`, `ParsedUrlRefusal`, `TrySelectProxyAndCredentials`, `ResolveSshOptionsAsync`, `ReportKnownHostsFileMissingAsync`, `LoadCookieFilesAsync`, `SwitchToHttpsForHstsAsync`, `OpenAltSvcCacheAsync`, `SetupFailureAsync`, `RefuseMalformedSetopt`, `RefuseMalformedInterface`, `RefuseMalformedEchMode`, `CookieLineTooLongFailureAsync`, `TransferWithBodyAsync`'s connect-side part, and the cookie jar, alt-svc and HSTS write-back (`WriteCookieJarAltSvcAndHstsFilesAsync`; libcurl writes `CURLOPT_COOKIEJAR` at cleanup).

Stays in the runner (the curl tool's work): globbing, IPFS rewriting, `--etag-*`, output-file naming and opening, `-J`, `--create-dirs`, `--skip-existing`, upload-file opening, the progress meter, `-w`, `curl: (N)` lines. Where a setup step writes a message to standard error, keep the write where it is for now and pass it through a delegate; BL-2284 removes the remaining direct writes.

## Acceptance criteria

- [x] The methods named in Context (or their renamed successors) are declared in `TransferPerformer` (or classes it owns), not in `CurlCommandRunner`.
- [x] `TransferPerformerTests` cover a rejected URL (exit 3), a proxy chosen from `-x`, a missing known-hosts file, an HSTS upgrade of an `http://` URL and a cookie file loaded before the request, on fakes.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [x] `Curl.Console/CLAUDE.md` says which steps `TransferPerformer` owns.

## Notes

- 2026-10-15: The setup moved into `Curl.Console/TransferPerformer.Setup.cs`, a partial of
  `TransferPerformer` (the class stays constructed on its clock alone, so `CurlComposition` and
  BL-2282's tests are untouched). Its methods take what they need as arguments (the dispatch's
  cookies, the run's `HstsTransferPolicy`, `IFileSystem`, standard input, the diagnostic log, the
  credential lookup) rather than new constructor dependencies: the simplest step that keeps
  behaviour byte for byte, and BL-2284 can bundle them when it removes the remaining writes.
  Standard-error writes go through delegates the runner passes (`WriteErrorLineAsync`, a wrapped
  warning writer); `--libcurl`'s known-hosts record goes through an `Action<string?, bool>`.
  `UrlRejectedFailure` folded into `ReportUrlRejected` (its only caller); `CookieLineTooLongFailureAsync`
  folded into `SetupFailureAsync`; `ReportKnownHostsFileMissingAsync` stays a private helper there.
  The `TransferWithBodyAsync` connect-side part is the proxy/credential and SSH calls, now
  `TransferPerformer`'s. Run-ending failures keep reference identity (`RunEndingFailures` names
  `TransferPerformer.*Failure`).
- Measured: fast tests all green (Curl.Console.UnitTests 2675 passed, Conformance 3112 passed, 0 failed);
  `Measure-CodeQuality.ps1 -Library Curl.Console`: 100% line, 100% branch, 0 failing members,
  worst CRAP 10 (the first run caught complexity 12 in the known-hosts helper, split).
  `PassingUpstreamCases.txt`, `CurlEasyErrorText.cs` and `InProcessCurl.cs` unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. TransferPerformer owns the per-transfer setup (URL refusals, proxy and credentials, SSH, cookies, HSTS, alt-svc, setup failures, jar write-back); behaviour unchanged
