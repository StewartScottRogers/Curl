---
id: BL-2606
title: Honour CURLOPT_PORT over the URL's port in the request line, Host header and connect (cases 521, 522, 523)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-17
completed: 2026-10-17
---
# BL-2606 — Honour CURLOPT_PORT over the URL's port in the request line, Host header and connect (cases 521, 522, 523)

## Goal

`CURLOPT_PORT` set through `Libcurl` overrides the URL's port (or its scheme default) for the connect, the request line and the `Host` header, as libcurl does, so upstream cases 521, 522 and 523 pass.

## Context

Found by BL-2529, whose ports lib521 and lib523 (`Curl.Conformance.LibTest.UnitLibrary`) set `CURLOPT_PORT`. The setter is `Libcurl/Options/Network/EasyNetworkOptionSetters.cs` (`SetPort`); the engine does not apply it. Measured on Windows with `CURL_UPSTREAM_CASE`:

- test522 (lib521, `http://127.0.0.1/522`, `CURLOPT_PORT` 8990): `<protocol>` line 2 expected `Host: 127.0.0.1:8990`, got `Host: 127.0.0.1`.
- test523 (lib523, URL `http://www.example.com:999/523` through a proxy, `CURLOPT_PORT` 19999): line 1 expected `GET http://www.example.com:19999/523 HTTP/1.1`, got `...:999/523`; line 2 expected `Host: www.example.com:19999`, got `Host: www.example.com:999`.
- test521 (lib521, `ftp://127.0.0.1/521/`, `CURLOPT_PORT` = FTP port): curl did not finish within 20 seconds (connects to the wrong port).

## Acceptance criteria

- [x] `CURLOPT_PORT` overrides the URL's port for the connect, the request target and the `Host` header, with focused unit tests in `Libcurl.UnitTests`.
- [x] Cases 521, 522 and 523 pass through the runner on Windows and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] `dotnet build` is clean, the fast tests pass and `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members.

## Notes

- Touches widened to `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests`: `Libcurl` only stores `CURLOPT_PORT`; the engine runs the perform, and applying it there covers the multi handle too. No task in Doing on `origin/work/dark-factory` named either project.
- Fix: `UrlPortOverride.Apply` writes a non-zero `Port` into the URL through a `ManagedCurlUrlHandle` before the perform, as libcurl's `parse_remote_port` sets `CURLUPART_PORT` (ADR-0672).
- Tests: `UrlPortOverrideTests` and `ManagedEasyTransferTests.Port.cs` (connect port, `Host`, proxy request target, effective URL) in `Curl.Engine.UnitTests`; `CurlEasyTests.Perform_PortOptionOnTheManagedEngine_TransfersTheUrlWithThatPort` in `Libcurl.UnitTests` runs a real `CurlGlobal.Init` engine with an unusable proxy, so it fails before dialling and the effective URL shows the port (no socket).
- Measured: cases 521, 522, 523 pass with `CURL_UPSTREAM_CASE` on Windows (FTP and HTTP over the in-memory servers, no paths, so nothing platform-specific); fast tests all green; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Libcurl`: 0 failing members.

## Log

- 2026-10-17: Created.
- 2026-10-17: Backlog -> Doing. Criteria at claim: 19e2562b 4e614c37 58a689e2.
- 2026-10-17: Doing -> Done. CURLOPT_PORT now overrides the URL's port for the connect, request target, Host header and effective URL; cases 521-523 hold the ratchet
