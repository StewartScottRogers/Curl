---
id: BL-158
title: Record the ADR for TransferReport on TransferResult and ConnectTimings on ConnectResult
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-158 — Record the ADR for TransferReport on TransferResult and ConnectTimings on ConnectResult

## Goal

An Accepted ADR fixes `TransferReport`, `TransferTimings` and `ConnectTimings` and names the `-w` variable or `-L` decision that reads each field.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item X2. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- The seams the plan fixes (to be recorded by BL-157 and BL-158):
  - `HttpRequestOptions` (CustomMethod, Headers verbatim in order, UserAgent - null means `curl/8.21.0`, empty means omit - Referer, Body, FollowRedirects, Fail None/Fail/FailWithBody, Version Http11/Http10, Compressed, Raw, IgnoreContentLength, RequestTarget, AuthSchemes, BearerToken, ForwardProxy), reached through a new `HttpRequestOptions? Http` on `ITransferContext`/`TransferContext`.
  - abstract `HttpRequestBody` with sealed `BytesBody(ReadOnlyMemory<byte>, string contentType)` and `StreamBody(Stream, long? length, string contentType)`.
  - `ProxyEndpoint(ProxyKind, Host, Port, NetworkCredential?)` and `ConnectTarget.Proxy`.
  - `TransferReport` (response code, proxy CONNECT code, HTTP version, method, response headers, content type, redirect URL, header/request/download/upload sizes, connection count, local/remote endpoints, `TransferTimings`) via `TransferResult.Report`; `ConnectTimings` on `ConnectResult`.
  - `IHttpAuthenticator.CreateAuthorization(HttpAuthRequest, IReadOnlyList<string> challenges)`; `ICookieStore.GetCookieHeader(Uri, bool secure, DateTimeOffset)` and `StoreFromResponse(Uri, IReadOnlyList<string>, DateTimeOffset)`.
  - These live in `Curl.Protocol.Abstractions.UnitLibrary` because `Curl.Protocol.Http.UnitLibrary` may not reference `Curl.Authentication` or `Curl.Cookies`; `Curl.Console` composes them. The multipart body builder lives in `Curl.Core.UnitLibrary` because SMTP and IMAP `-F` need the same MIME later.
- `TransferResult` (`Curl.Protocol.Abstractions.UnitLibrary/TransferResult.cs`) and `ConnectResult` (`ConnectResult.cs`) exist; neither carries timings or response metadata today.
- The `-w` variables are listed at https://curl.se/docs/manpage.html#-w (curl 8.21.0).
- BL-160, BL-211, BL-203 and BL-225 depend on this.

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for TransferReport and ConnectTimings, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [x] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [x] Every field is listed with its type and the `-w` variable(s) (for example `%{response_code}`, `%{http_connect}`, `%{size_header}`, `%{num_connects}`, `%{time_connect}`) or the `-L` decision that reads it.
- [x] The ADR states that timings are `TimeProvider` timestamps taken by the connector and handler, never wall-clock reads, and that `TransferResult.Report` and the `ConnectResult` timings default to null so existing handlers compile unchanged.

## Notes

- Plan item: X2 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Recorded as ADR-0015 (`Documentation/Planning/Decisions/ADR-0015-transfer-report-on-transfer-result-and-connect-timings-on-connect-result.md`); written directly in the session rather than through align-and-document, since the whole deliverable is one new ADR and one index row.
- Choice: timings are `TimeProvider.GetTimestamp()` longs (monotonic, fake-able), not `TimeSpan` or `DateTimeOffset`; readers convert with `GetElapsedTime`. `null` means the event did not happen.
- Choice: `ConnectTimings` holds points in time only. `LocalEndPoint` and `ProxyConnectResponseCode` go on `ConnectResult` as separate defaulted members (for `%{local_ip}`/`%{local_port}` and `%{http_connect}`), not on `IConnection`, so no connection or fake changes. BL-160 and BL-211 implement them as the ADR states.
- Choice: `TransferReport` also carries `EffectiveUrl` and `RedirectCount` (filled by the BL-203 follower) because BL-203's merged report needs them; `RedirectUrl` is a `string` pending ADR-0010.
- Left to measure: how `%{time_*}` combine across redirects (BL-203, BL-225); whether a failed CONNECT's code reaches `%{http_connect}` (BL-212).

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ADR-0015 fixes TransferReport, TransferTimings and ConnectTimings and maps every field to its -w variable or -L decision
