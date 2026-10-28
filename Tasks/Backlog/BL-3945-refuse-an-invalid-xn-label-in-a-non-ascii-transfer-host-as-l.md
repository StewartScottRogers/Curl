---
id: BL-3945
title: Refuse an invalid xn-- label in a non-ASCII transfer host as libidn2 does
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-3884]
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlPunycode.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlHostXnLabelTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3945 — Refuse an invalid xn-- label in a non-ASCII transfer host as libidn2 does

## Goal

A transfer to a non-ASCII host holding an `xn--` label that is not valid punycode fails as curl 8.22.0 does: `curl -sS "http://xn--zz.%FC:20200/a" --connect-to ::127.0.0.1:20200` exits 3 with `curl: (3) URL using bad/illegal format or missing URL`, while `xn--tda.%FC` still sends `Host: xn--tda.xn--tda:20200`.

## Context

BL-3588 (CGR-059, ADR-0942) converts a non-ASCII host label by label in `CurlUrlHost.ToAsciiLabel` and passes an ASCII `xn--` label as written. libidn2 decodes an `xn--` label and refuses one that does not decode (measured with the reference curl 8.22.0 through `Record-CurlExchange.ps1` on 2026-10-26: `xn--zz.%FC` exit 3, `xn--tda.%FC` exit 0). Needs a punycode decoder beside `CurlUrlPunycode.Encode`.

## Acceptance criteria

- [ ] `xn--zz.%FC` run through real curl and Curl gives exit 3 and the same stderr; `xn--tda.%FC` gives the same Host line in both; recorded in Notes.
- [ ] A focused unit test in `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostXnLabelTests.cs` fails before the change and passes after it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created.
