---
id: BL-269
title: Let the proxy selector take the -x or --socks option's proxy kind and read socks:// as SOCKS4
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-206, BL-192]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-269 — Let the proxy selector take the -x or --socks option's proxy kind and read socks:// as SOCKS4

## Goal

`ProxySelector` (BL-206) chooses a proxy of the kind a `--socks4`, `--socks4a`, `--socks5` or `--socks5-hostname` option names when the value has no scheme, and reads `socks://` as SOCKS4, so one scheme reader serves both the command line and the environment.

## Context

- Found in BL-192. `Curl.Cli`'s `CommandLineProxy` carries the value and the option's `ProxyKind` (ADR-0026); ADR-0024's `ProxyUrlParser` assumes `http` for a value with no scheme and does not list `socks`.
- Measured on curl 8.21.0 (mingw) on 2026-09-26 with `Record-CurlExchange.ps1`, reading the first bytes the proxy received: `--socks4 127.0.0.1:P` and `-x socks://127.0.0.1:P` send `04 01 ...` (SOCKS4); `--socks5 A -x B` speaks HTTP; `--socks5 http://A` speaks HTTP.
- Once this lands, `CommandLineProxy.TryGetKind` can be retired in favour of the selector's reading (a Curl.Cli change, filed separately if wanted).

## Acceptance criteria

- [x] `ProxySelector` given a proxy value with no scheme and `ProxyKind.Socks5` returns a `ProxyEndpoint` of kind `Socks5` on port 1080; a test pins it.
- [x] `socks://h` parses as `ProxyKind.Socks4` on port 1080; a test pins it.
- [x] `dotnet build Curl.Core.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core`.

## Notes

- Plan: `ProxyUrlParser.TryParse` gains an overload taking the `ProxyKind` for text with no scheme (default port by kind: Http/Http10 80, Https 443, SOCKS 1080); `socks` joins the scheme table as `Socks4`. `ProxySelector.TrySelect` gains an overload taking the proxy option's kind; the old signatures delegate with `ProxyKind.Http`, so no caller changes.
- Behaviour follows the measurements in Context, not a new decision, so no ADR: a scheme in the text wins over the option's kind (`--socks5 http://A` speaks HTTP), and the kind never applies to environment proxy text (the environment has no option kind). Which of `-x` and `--socksN` wins when both are given is `Curl.Cli`'s job (it passes the winning text and kind), per ADR-0026.
- The first cut of the new `TrySelect` measured cyclomatic complexity 12; choosing text and kind in one tuple conditional brought it under 10. `Measure-CodeQuality.ps1 -Library Curl.Core.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ProxySelector reads option text with no scheme as the -x or --socks option's kind, and socks:// as SOCKS4
