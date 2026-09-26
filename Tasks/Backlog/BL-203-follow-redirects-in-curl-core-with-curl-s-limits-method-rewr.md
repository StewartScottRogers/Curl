---
id: BL-203
title: Follow redirects in Curl.Core with curl's limits, method rewriting and credential rules
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-159, BL-160]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-203 — Follow redirects in Curl.Core with curl's limits, method rewriting and credential rules

## Goal

A redirect follower in `Curl.Core.UnitLibrary` wraps `ProtocolDispatcher`, follows `TransferReport.RedirectUrl` with curl's limit, method rewriting and credential rules, and returns a merged report.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Measured: `-L --max-redirs 0` exits 47 `curl: (47) Maximum (0) redirects followed`. Default limit 50.
- `ProtocolDispatcher.DispatchAsync(ITransferContext)` is in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs`.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Over the limit returns `CurlExitCode.TooManyRedirects` (47) `Maximum (N) redirects followed`.
- [ ] POST becomes GET on 301/302/303 unless the matching `--post30x` is set; tests per code.
- [ ] Credentials are dropped on a host change unless `--location-trusted`; a redirect to a disallowed protocol exits as measured.
- [ ] The merged report carries the redirect count, effective URL and redirect time.
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
