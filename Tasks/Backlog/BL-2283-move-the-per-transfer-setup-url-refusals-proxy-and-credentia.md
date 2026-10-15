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
completed:
---
# BL-2283 — Move the per-transfer setup (URL refusals, proxy and credential choice, SSH options, cookies, HSTS, alt-svc) from CurlCommandRunner into TransferPerformer

## Goal

The per-transfer setup that libcurl does before connecting - URL parse and refusals, proxy and credential choice, SSH option resolution, cookie loading, the HSTS upgrade, the alt-svc cache and setup failures - lives in `TransferPerformer`, not `CurlCommandRunner`; behaviour is byte for byte unchanged.

## Context

ADR-0506, plan phase 1 step 2; follows BL-2282. Candidates in `Curl.Console/CurlCommandRunner.cs` (names as of 2026-10-15): `TryParseTransferUrl`, `UrlRejectedFailure`, `ReportUrlRejected`, `ParsedUrlRefusal`, `TrySelectProxyAndCredentials`, `ResolveSshOptionsAsync`, `ReportKnownHostsFileMissingAsync`, `LoadCookieFilesAsync`, `SwitchToHttpsForHstsAsync`, `OpenAltSvcCacheAsync`, `SetupFailureAsync`, `RefuseMalformedSetopt`, `RefuseMalformedInterface`, `RefuseMalformedEchMode`, `CookieLineTooLongFailureAsync`, `TransferWithBodyAsync`'s connect-side part, and the cookie jar, alt-svc and HSTS write-back (`WriteCookieJarAltSvcAndHstsFilesAsync`; libcurl writes `CURLOPT_COOKIEJAR` at cleanup).

Stays in the runner (the curl tool's work): globbing, IPFS rewriting, `--etag-*`, output-file naming and opening, `-J`, `--create-dirs`, `--skip-existing`, upload-file opening, the progress meter, `-w`, `curl: (N)` lines. Where a setup step writes a message to standard error, keep the write where it is for now and pass it through a delegate; BL-2284 removes the remaining direct writes.

## Acceptance criteria

- [ ] The methods named in Context (or their renamed successors) are declared in `TransferPerformer` (or classes it owns), not in `CurlCommandRunner`.
- [ ] `TransferPerformerTests` cover a rejected URL (exit 3), a proxy chosen from `-x`, a missing known-hosts file, an HSTS upgrade of an `http://` URL and a cookie file loaded before the request, on fakes.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [ ] `Curl.Console/CLAUDE.md` says which steps `TransferPerformer` owns.

## Notes

## Log

- 2026-10-15: Created.
