---
id: BL-5617
title: Decide how Curl.Api exposes extension options, starting with --dns-json for dns://
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Options/Protocol/CurlRequest.Protocol.cs, Curl.Api/Options/Protocol/CurlDnsOptions.cs, Curl.Api/Client/CurlClient.cs, Curl.Api/Composition/CurlClient.Create.cs, Curl.Api/Messages/CurlClient.SendAsync.cs, Curl.Api/README.md, Curl.Api.UnitLibrary/Options/EasyOptions.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs]
requirement: none
created: 2026-11-20
completed: 2026-11-20
---
# BL-5617 — Decide how Curl.Api exposes extension options, starting with --dns-json for dns://

## Goal

A `Curl.Api` user can ask a `dns://` request for JSON output, as `curl --dns-json` does, through a documented way of setting extension options.

## Context

From Surly cookbook bug report `2026-11-20T1022-Curl-bug-report.md` (batch 4 of the 1,000-recipe plan), finding 6. Build tested: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11; server Surl `763754785`. Check it still reproduces on the current branch first.

`Curl.Api` has no property for `--dns-json`. Rather than one property, decide (ADR, decided by Claude under Stewart's delegation) how `Curl.Api` reaches extension options at all: typed option groups per extension (for example `CurlDnsOptions`), or a generic name/value bag the extension registry validates. Extensions must stay optional and `CURL_EXTENSIONS=0` must still turn them off. Implement it for `--dns-json` and `--dns-transport` first; document it in the README.

## Acceptance criteria

- [x] An ADR records how `Curl.Api` sets extension options and why.
- [x] A `Curl.Api` request to a DNS test server with JSON output asked for returns the same JSON `curl --dns-json` prints; `--dns-transport tcp` likewise uses TCP.
- [x] With `CURL_EXTENSIONS=0`, setting the option fails the way the console fails for an unknown option.
- [x] The README documents it; `dotnet build` is clean and the fast tests pass.

## Notes

Decision (ADR-1319): a typed record per extension (CurlRequest.Dns = CurlDnsOptions), carried as EasyOptions.ExtensionOptions to transfer hooks the engine runs. Touches widened to the Api and Engine files above; no other Doing task names them. The same DnsQueryTransferHook runs as for the tool, so output matches; unit tests pin the mapping, the hook activation and the CURL_EXTENSIONS=0 failure (UnknownOption). Quality gates: 0 failing for Curl.Api, Curl.Api.UnitLibrary, Curl.Engine.UnitLibrary. Platform-neutral for Linux and macOS.

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1022, finding 6.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 7aea9493 06b41d08 e874a8d3 06f9b39f.
- 2026-11-20: Doing -> Done. Curl.Api CurlRequest.Dns (CurlDnsOptions) sets --dns-json and --dns-transport; CURL_EXTENSIONS=0 fails UnknownOption; ADR-1319.
