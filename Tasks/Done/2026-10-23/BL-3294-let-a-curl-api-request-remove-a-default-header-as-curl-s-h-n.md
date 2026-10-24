---
id: BL-3294
title: Let a Curl.Api request remove a default header as curl's -H 'Name:' does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Api/Messages/CurlRequestOptions.cs, Curl.Api/Messages/CurlRequest.cs, Curl.Api.UnitTests/Messages]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3294 — Let a Curl.Api request remove a default header as curl's -H 'Name:' does

## Goal

A `Curl.Api` request can remove a header libcurl adds by default (`Accept`, `Host`, `Content-Type` of a form post), as curl's `-H 'Accept:'` and a `CURLOPT_HTTPHEADER` line `Accept:` do.

## Context

Found by BL-3242's Curl.Api differential cases (ADR-0790). `CurlRequest.Headers` maps each field to a `CURLOPT_HTTPHEADER` line in `CurlRequestOptions.HeaderLine`: `Name: value`, or `Name;` for an empty value (send the header with no value). No field produces `Name:`, so the curl command line `-s -H 'Accept:' {U}/echo` (wire `GET /echo HTTP/1.1\r\nHost: 127.0.0.1:18080\r\n\r\n` from curl 8.22.0) has no `Curl.Api` equivalent: the closest request sends `Accept:` with an empty value. Decide the API shape (for example a `CurlRequest.RemovedHeaders` list of names) in an ADR, keeping the existing empty-value meaning.

## Acceptance criteria

- [x] A `Curl.Api` request can name a header to remove, and the engine then sends the request without it (a `CURLOPT_HTTPHEADER` line `Name:`).
- [x] Unit tests in `Curl.Api.UnitTests/Messages` pin the header lines for a removed header, an empty-valued header and a valued one.
- [x] Build clean, fast tests green, Measure-CodeQuality.ps1 reports 0 failing members for Curl.Api.

## Notes

- ADR-0791: `CurlRequest.RemovedHeaders` (names), sent as `Name:` after `Headers`. Quality: 0 failing members for Curl.Api.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 0cc07379 f8c89d09 2d023807.
- 2026-10-23: Doing -> Done. CurlRequest.RemovedHeaders sends Name: lines (ADR-0791)
