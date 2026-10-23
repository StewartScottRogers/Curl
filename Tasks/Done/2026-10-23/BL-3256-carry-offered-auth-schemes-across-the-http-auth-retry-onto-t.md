---
id: BL-3256
title: Carry offered auth schemes across the HTTP auth retry onto TransferReport and check the retry's redirect count
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.AuthSchemesOffered.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3256 — Carry offered auth schemes across the HTTP auth retry onto TransferReport and check the retry's redirect count

## Goal

The HTTP handler's `TransferReport` carries every auth scheme the server (and proxy) offered across its own auth retry, and counts the retry in the redirect count as upstream does.

## Context

First part of BL-3193. Upstream ORs every challenge seen into `data->info.httpauthavail` (`Curl_http_input_auth`). Today `TransferInfoFromReport.WithCounts` reads the offered bits only from the final response's `WWW-Authenticate`/`Proxy-Authenticate` when it is a 401/407, so after a retry ending 200 they are lost. Add new `TransferReport` fields (e.g. `AuthSchemesOffered`, `ProxyAuthSchemesOffered`) filled by `HttpProtocolHandler`. The handler counts an auth retry in `RedirectsFollowed` (~3541, report `RedirectCount` ~1465): check the count against upstream 8.22.0 (measure with `Record-CurlExchange.ps1 -Connections 2` under a timeout) before changing it. Tests use fakes or loopback only; put them in `Curl.Protocol.Http.UnitTests` in a new file.

## Acceptance criteria

- [x] A test shows the report's offered-scheme field holds Basic and Digest after a 401 offering both followed by a retry that ends 200, and the proxy field the same after a 407.
- [x] A test shows the report's redirect count after a digest auth retry equals upstream 8.22.0's value.
- [x] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- Split from BL-3193 (over its cost cap). No stash was left.
- Measured (Record-CurlExchange.ps1 -Connections 2): `--digest -u a:b -w "%{num_redirects} %{http_code}"` against a 401 offering Basic and Digest, then 200, prints `1 200` on curl 8.22.0 (Git ucrt64) and 8.21.0 (System32); with `-L --max-redirs 0` 8.22.0 prints `0 401` and exits 47. Curl reported 0, so `WithAuthorization` and `WithProxyAuthorization` now count the retry (`RedirectsFollowed + 1`) and `RetryOfAsync` checks `ThrowIfRedirectLimitReached` before an auth retry, as for the 417 resend. No existing test pinned the old count.
- New `TransferReport.AuthSchemesOffered` / `ProxyAuthSchemesOffered`: the plan ORs the schemes of a 401's `WWW-Authenticate` / a 407's `Proxy-Authenticate` (`HttpRequestPlan.RecordSchemesOffered`, copied into every retry plan) and each exchange's report carries them. Only Basic, Digest, NTLM and Negotiate are recognised (the handler's `SchemeOfAuthorization`, first token, case-sensitive); reading them in `TransferInfoFromReport` stays with BL-3193. No ADR: the behaviour is measured curl, not a judgment call.
- Added the new test file to `touches` (the task asked for a new file in Curl.Protocol.Http.UnitTests).
- Build -warnaserror clean, fast tests green (Http 2251 incl. 3 new), Measure-CodeQuality.ps1 for Http and Abstractions: 0 failing members. Platform-neutral (fakes only), so CI on Linux and macOS is expected green.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 30d14345 fba1d735 3639a5e6.
- 2026-10-23: Doing -> Done. TransferReport carries every auth scheme a 401/407 offered across the auth retry, and the retry counts as a redirect (held to --max-redirs) as curl 8.22.0 does
