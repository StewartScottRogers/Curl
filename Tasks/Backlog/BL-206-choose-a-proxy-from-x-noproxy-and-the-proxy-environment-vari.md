---
id: BL-206
title: Choose a proxy from -x, --noproxy and the proxy environment variables
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-162]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-206 — Choose a proxy from -x, --noproxy and the proxy environment variables

## Goal

A proxy selector returns the `ProxyEndpoint` curl would use for a URL, from `-x`, `--noproxy` and injected environment variables.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item K4. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- curl reads `http_proxy` lowercase only, `HTTPS_PROXY`/`https_proxy`, `ALL_PROXY`, `NO_PROXY` (https://curl.se/docs/manpage.html#ENVIRONMENT).
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] Case rules, `NO_PROXY` wildcards and CIDR are measured on curl 8.21.0 and pinned.
- [ ] The environment is injected; no test reads the real environment.
- [ ] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan item: K4 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
