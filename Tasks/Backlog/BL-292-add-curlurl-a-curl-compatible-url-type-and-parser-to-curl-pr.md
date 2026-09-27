---
id: BL-292
title: Add CurlUrl, a curl-compatible URL type and parser, to Curl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-292 — Add CurlUrl, a curl-compatible URL type and parser, to Curl.Protocol.Abstractions

## Goal

`Curl.Protocol.Abstractions.UnitLibrary` has a `CurlUrl` type whose parser accepts and
rejects URLs as curl 8.21.0's URL API does, keeps the text as typed, and exposes each
part as written. Nothing uses it yet.

## Context

- ADR-0010 (Accepted, option 1 "Replace") is the design. This is its first step. The
  type is additive, so no other project changes here. BL-294 switches the contract to it.
- Model the parts on libcurl's URL API (<https://curl.se/libcurl/c/curl_url_get.html>)
  and `lib/urlapi.c` at curl 8.21.0: scheme, user, password, options, host, zone id,
  port, path, query, fragment, and the original text.
- To make BL-294 mechanical, `CurlUrl` also offers the members handlers read from
  `System.Uri` today, with the same meaning where curl agrees: `Scheme`, `Host`,
  `IdnHost` (punycode through `System.Globalization.IdnMapping`), `Port` (the scheme's
  default when none is written), `IsDefaultPort`, `AbsolutePath` (the path as written,
  dot segments removed unless parsed with path-as-is), `Query`, `Fragment` and
  `OriginalString`.
- A parse switch equivalent to `CURLU_PATH_AS_IS` keeps dot segments.
- `file:` follows curl's rules: `file://C:`, `file:///C:`, `file:///C:%2FWindows/win.ini`,
  `file:///Q:dir/../x` and `file:////server/share` parse; `file://user:pass@localhost/x`
  and `file://ab:/x` are rejected (exit 3). ADR-0010's case table has each one.
- Base class library only. Measure curl 8.21.0 (the mingw build, `/mingw64/bin/curl`,
  ADR-0018) before pinning any accept or reject outcome, and record the command and
  result under Notes.

## Acceptance criteria

- [ ] `CurlUrl.TryParse(string text, bool pathAsIs, out CurlUrl? url)` exists, and a URL it rejects is reported as `CurlExitCode.UrlMalformat` (3) by the caller's contract documented on the method.
- [ ] Tests in `Curl.Protocol.Abstractions.UnitTests` cover every row of ADR-0010's case table, `http://example.com/a/../b` with and without path-as-is, and `http://example.com/a%2Fb` (path stays `/a%2Fb`), each outcome measured against curl 8.21.0 and recorded under Notes.
- [ ] `IdnHost`, `Port`, `IsDefaultPort`, `AbsolutePath` and `OriginalString` are tested for `http`, `https`, `dict`, `gopher`, `mqtt`, `telnet`, `tftp` and `file` URLs.
- [ ] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions`.

## Notes

- Filed by BL-010 when ADR-0010 was accepted.

## Log

- 2026-09-26: Created.
