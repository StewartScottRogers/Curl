---
id: BL-157
title: Record the ADR for HTTP request options, proxy connect targets and the auth and cookie seams
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-157 — Record the ADR for HTTP request options, proxy connect targets and the auth and cookie seams

## Goal

An Accepted ADR fixes the public surface of `HttpRequestOptions`, `HttpRequestBody`, `ProxyEndpoint`/`ConnectTarget.Proxy`, `IHttpAuthenticator` and `ICookieStore` in `Curl.Protocol.Abstractions.UnitLibrary`.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item X1. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- The seams the plan fixes (to be recorded by BL-157 and BL-158):
  - `HttpRequestOptions` (CustomMethod, Headers verbatim in order, UserAgent - null means `curl/8.21.0`, empty means omit - Referer, Body, FollowRedirects, Fail None/Fail/FailWithBody, Version Http11/Http10, Compressed, Raw, IgnoreContentLength, RequestTarget, AuthSchemes, BearerToken, ForwardProxy), reached through a new `HttpRequestOptions? Http` on `ITransferContext`/`TransferContext`.
  - abstract `HttpRequestBody` with sealed `BytesBody(ReadOnlyMemory<byte>, string contentType)` and `StreamBody(Stream, long? length, string contentType)`.
  - `ProxyEndpoint(ProxyKind, Host, Port, NetworkCredential?)` and `ConnectTarget.Proxy`.
  - `TransferReport` (response code, proxy CONNECT code, HTTP version, method, response headers, content type, redirect URL, header/request/download/upload sizes, connection count, local/remote endpoints, `TransferTimings`) via `TransferResult.Report`; `ConnectTimings` on `ConnectResult`.
  - `IHttpAuthenticator.CreateAuthorization(HttpAuthRequest, IReadOnlyList<string> challenges)`; `ICookieStore.GetCookieHeader(Uri, bool secure, DateTimeOffset)` and `StoreFromResponse(Uri, IReadOnlyList<string>, DateTimeOffset)`.
  - These live in `Curl.Protocol.Abstractions.UnitLibrary` because `Curl.Protocol.Http.UnitLibrary` may not reference `Curl.Authentication` or `Curl.Cookies`; `Curl.Console` composes them. The multipart body builder lives in `Curl.Core.UnitLibrary` because SMTP and IMAP `-F` need the same MIME later.
- Existing shape to extend: `ITransferContext` and `TransferContext` (ADR-0003, ADR-0006, ADR-0008), `ConnectTarget(string Host, int Port, bool UseTls)` and `IConnector` (ADR-0005).
- BL-159, BL-161, BL-162 and BL-164 implement or depend on this.

## Acceptance criteria

- [x] A new ADR under `Documentation/Planning/Decisions/` with the next free number, status Accepted, titled for HTTP request options and the auth, cookie and proxy seams, states that it was decided by Claude under Stewart's delegation, and records the decision, the reasons and the alternatives rejected.
- [x] `Documentation/Planning/Decisions/README.md` lists the new ADR.
- [x] The ADR lists every type and member named in Context with its C# signature and nullability, the `HttpAuthSchemes` flags, and what each `HttpRequestOptions` member maps from on the command line.
- [x] The ADR states why these live in Abstractions (Http may not reference Authentication or Cookies) and why the multipart builder lives in `Curl.Core.UnitLibrary`.
- [x] The ADR states that `TransferContext.Http` defaults to null and that non-HTTP handlers ignore it.

## Notes

- Plan item: X1 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

- Delivered as ADR-0014 (`Documentation/Planning/Decisions/ADR-0014-http-request-options-and-the-auth-cookie-and-proxy-seams.md`), written by align-and-document; docs only, no `.cs` changed.
- Choices recorded in the ADR (defaults taken, unattended run): `bool ProxyTunnel` (`-p`) added so BL-192/BL-212 have a member to fill; `-T` stays on `ITransferContext.Upload` (PUT) and `Body` carries only the `-d`/`-F` families (POST), because curl sends them with different methods; `Bearer = 16` is a flag but not in `Any`, since `--anyauth` covers only user/password schemes; `ProxyKind` has Http, Http10, Https, Socks4, Socks4a, Socks5, Socks5Hostname because Phase 1 tasks need SOCKS; `IHttpAuthenticator` returns only the header value (null = no header) and is stateless, so NTLM/Negotiate need a later ADR.
- TransferReport, TransferTimings and ConnectTimings are only named here; BL-158 specifies them.
- Number 0014 was the next free one in this lane; if a parallel lane lands another ADR-0014 first, the second to integrate renumbers.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ADR-0014 fixes HttpRequestOptions, HttpRequestBody, ProxyEndpoint/ConnectTarget.Proxy, IHttpAuthenticator and ICookieStore
