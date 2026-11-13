---
id: BL-5098
title: Export one OpenTelemetry span per transfer with -L, in one trace
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.OpenTelemetry.UnitLibrary/OpenTelemetryTransferHook.cs, Curl.Extension.OpenTelemetry.UnitTests/OpenTelemetryRedirectTests.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5098 — Export one OpenTelemetry span per transfer with -L, in one trace

## Goal

`--otel-endpoint` with `-L` exports one span for the whole transfer, with one trace-id carried on every redirect hop's `traceparent`.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding EXT-054 (Low, Confirmed). Part B: judged against the port's own docs and specs, not curl.

**Repro.** With a server that logs requests, where `/redir` answers `302` to `/plain` and `/otel` accepts POSTs: `curl -sS -m 5 --otel-endpoint http://127.0.0.1:28000/otel -L http://127.0.0.1:28000/redir -o NUL`

**Expected.** ADR-1134: one trace-id and span-id per HTTP transfer, the span "timed ... around the whole transfer (redirects and retries the handler follows included)". Both hops carry the same trace-id in `traceparent`; one span with status 200 is exported.

**Actual.** Two `traceparent` headers with different trace-ids; two `POST /otel` exports (one span for `/redir` with 302, one for `/plain` with 200); exit 0.

**Where to start.** The `Curl.Extension.OpenTelemetry.UnitLibrary` transfer hook (`OpenTelemetryTransferHook.cs`) is invoked once per redirect hop; make it run once around the whole transfer, or have later hops reuse the first hop's trace context and suppress their own export. Read ADR-1134 first.

## Acceptance criteria

- [ ] A new test file `Curl.Extension.OpenTelemetry.UnitTests/OpenTelemetryRedirectTests.cs` proves that a transfer with a 302 followed by a 200 sends the same trace-id in `traceparent` on both hops and exports exactly one span, whose `http.response.status_code` is 200.
- [ ] A test proves a transfer with no redirect still exports one span as before (regression), and a test covers a redirect chain of three hops.
- [ ] The tests are platform-neutral and use no real network.
- [ ] If ADR-1134 or the extension's conformance document needs a sentence about redirects, it is added, and `--extended-help` stays right.
- [ ] `dotnet build Curl.Extension.OpenTelemetry.UnitLibrary -warnaserror` is clean, and `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers must be PowerShell, a C# file-based app (`dotnet run tool.cs`) or `Record-CurlExchange.ps1`, never Python.
- Extension isolation holds: the extension references only `Curl.Extension.Abstractions` and shared layers.
- Review acceptance check, as a hand check: exactly one `POST /otel` whose single span has `http.response.status_code` 200, and both hops carry the same trace-id.

## Log

- 2026-11-13: Created.
