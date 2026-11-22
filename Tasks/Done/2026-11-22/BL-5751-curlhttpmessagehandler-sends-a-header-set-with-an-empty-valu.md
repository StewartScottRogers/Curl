---
id: BL-5751
title: CurlHttpMessageHandler sends a header set with an empty value as Name; again after ADR-1335
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Api/HttpMessageHandler/Core/HttpRequestMapping.cs, Curl.Api.UnitTests/HttpMessageHandler/Core]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5751 — CurlHttpMessageHandler sends a header set with an empty value as Name; again after ADR-1335

## Goal

An `HttpRequestMessage` header set to an empty value (`TryAddWithoutValidation("X-Foo", "")`) is sent through `CurlHttpMessageHandler` as the empty header `X-Foo;` again, as `SocketsHttpHandler` sends it, instead of being removed.

## Context

Found by the shift-end review of 2026-11-22 (ADR-0710), reported by both the code-reviewer and the type-design-analyzer.

BL-5749 / ADR-1335 changed `Curl.Api/Messages/CurlRequestOptions.cs:74-75` so an empty value in `CurlRequest.Headers` becomes the line `Name:` (remove the header, as `curl -H "Name:"`) and added `CurlRequest.EmptyHeaders` for `Name;`. `Curl.Api/HttpMessageHandler/Core/HttpRequestMapping.cs:29` and `FieldsOf` (`:73-76`) still copy every header, empty values included, into `CurlRequest.Headers`, so since that change the adapter silently drops a header the caller set empty, and for a default header (`Accept`, `User-Agent`) removes libcurl's default instead of sending it empty. The adapter's contract is to behave like `HttpClient` (ADR-0647); ADR-1335 only considered direct `CurlRequest` callers.

## Acceptance criteria

- [x] `HttpRequestMapping.ToCurlRequestAsync` puts every request or content header whose joined value is empty into `CurlRequest.EmptyHeaders`, and only non-empty ones into `CurlRequest.Headers`.
- [x] A test in `Curl.Api.UnitTests/HttpMessageHandler/Core` sends an `HttpRequestMessage` with `X-Foo` set to `""` through `CurlHttpMessageHandler` and asserts the engine receives the `CURLOPT_HTTPHEADER` line `X-Foo;` and no `X-Foo:` line.
- [x] A test asserts an empty `User-Agent` is sent as `User-Agent;`, not removed.
- [x] ADR-1335's Consequences note the adapter's mapping; `Curl.Api` stays at 100% line and branch coverage.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

HttpRequestMapping splits empty-valued fields into EmptyHeaders via SplitEmpty (keeps ToCurlRequestAsync under complexity 10). Curl.Api quality run: 100% line and branch, 0 failing. ADR-1335 Consequences updated.

## Log

- 2026-11-22: Created by the shift-end review (ADR-0710).
- 2026-11-22: Backlog -> Doing. Criteria at claim: 90ddf0c5 ff7c0920 0a2c860d 7692c245 3d836d5e.
- 2026-11-22: Doing -> Done. Empty request headers map to EmptyHeaders; tests, ADR-1335 note, Curl.Api quality 100%
