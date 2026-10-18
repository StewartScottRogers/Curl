# ADR-0632 — `Curl.Api`'s `CurlRequest` and `CurlResponse` records and how `SendAsync` maps them

- **Status:** Accepted
- **Date:** 2026-10-16

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2499.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md` gives `Curl.Api` request and response
records and `CurlClient.SendAsync`, built on the seam (`IEasyTransfer`, `EasyOptions`,
`TransferInfo`, the header callback; ADR-0506). The task left the shapes open: how the URL, method,
headers and body are typed, how they map to easy options, and where the response's parts come from.

## Decision

- `CurlRequest(HttpMethod method, string url)` with `Headers` (`IReadOnlyList<KeyValuePair<string,
  string>>`) and `Body` (`ReadOnlyMemory<byte>?`). The URL is a `string`, not a `Uri`: it goes to
  `CURLOPT_URL` unparsed, so every URL curl accepts (any scheme, unnormalised paths) is accepted.
  `HttpMethod` is the BCL type .NET callers already use.
- Method mapping, as the curl tool maps `-I`, `-d` and `-X`: `GET` sets nothing, `HEAD` sets
  `CURLOPT_NOBODY`, `POST` (or any request with a body) sets `CURLOPT_POST` with the body as
  `CURLOPT_POSTFIELDS` and its length as `CURLOPT_POSTFIELDSIZE` (an empty body for a body-less
  `POST`); any method libcurl would not send by itself goes in `CURLOPT_CUSTOMREQUEST`.
- Headers become `CURLOPT_HTTPHEADER` lines `Name: Value` in the given order and casing; an empty
  value becomes `Name;`, libcurl's way to send a header with no value (`Name:` would remove one).
- `CurlResponse` is a record of `StatusCode` (`CURLINFO_RESPONSE_CODE`), `ReasonPhrase` (from the
  last status line), `Version` (`CURLINFO_HTTP_VERSION` mapped to `System.Net.HttpVersion`'s 1.0,
  1.1, 2.0, 3.0, else `Unknown`), `Headers` and `Trailers` (as received, casing and duplicates kept),
  `Info` (the whole `TransferInfo`) and `Body` (`Stream.Null` until BL-2501).
- Header lines are split by `ResponseHeaderCollector`: a status line starts a new response and drops
  the previous one's fields (so a `1xx` or followed redirect leaves only the final response); a field
  after the blank line that ends the headers is a trailer. Lines are read as Latin-1 so no byte is lost.
- A transfer result other than `CURLE_OK` throws `CurlException` (ADR-0630); the easy handle is
  disposed whether the transfer succeeded or not.

## Consequences

- BL-2500 widens `Body` to streams through the read and seek callbacks; BL-2501 replaces the
  discarding write callback and `Stream.Null` with a streamed body.
- `CurlHttpMessageHandler` (BL-2510) maps `HttpRequestMessage` onto these records without parsing
  headers again.
