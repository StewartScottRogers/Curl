---
id: BL-5617
title: Decide how Curl.Api exposes extension options, starting with --dns-json for dns://
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Options/Protocol/CurlRequest.Protocol.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5617 — Decide how Curl.Api exposes extension options, starting with --dns-json for dns://

## Goal

A `Curl.Api` user can ask a `dns://` request for JSON output, as `curl --dns-json` does, through a documented way of setting extension options.

## Context

From Surly cookbook bug report `2026-11-20T1022-Curl-bug-report.md` (batch 4 of the 1,000-recipe plan), finding 6. Build tested: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11; server Surl `763754785`. Check it still reproduces on the current branch first.

`Curl.Api` has no property for `--dns-json`. Rather than one property, decide (ADR, decided by Claude under Stewart's delegation) how `Curl.Api` reaches extension options at all: typed option groups per extension (for example `CurlDnsOptions`), or a generic name/value bag the extension registry validates. Extensions must stay optional and `CURL_EXTENSIONS=0` must still turn them off. Implement it for `--dns-json` and `--dns-transport` first; document it in the README.

## Acceptance criteria

- [ ] An ADR records how `Curl.Api` sets extension options and why.
- [ ] A `Curl.Api` request to a DNS test server with JSON output asked for returns the same JSON `curl --dns-json` prints; `--dns-transport tcp` likewise uses TCP.
- [ ] With `CURL_EXTENSIONS=0`, setting the option fails the way the console fails for an unknown option.
- [ ] The README documents it; `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1022, finding 6.
