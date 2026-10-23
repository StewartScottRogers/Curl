---
id: BL-3256
title: Carry offered auth schemes across the HTTP auth retry onto TransferReport and check the retry's redirect count
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs, Curl.Protocol.Abstractions.UnitLibrary/TransferReport.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3256 — Carry offered auth schemes across the HTTP auth retry onto TransferReport and check the retry's redirect count

## Goal

The HTTP handler's `TransferReport` carries every auth scheme the server (and proxy) offered across its own auth retry, and counts the retry in the redirect count as upstream does.

## Context

First part of BL-3193. Upstream ORs every challenge seen into `data->info.httpauthavail` (`Curl_http_input_auth`). Today `TransferInfoFromReport.WithCounts` reads the offered bits only from the final response's `WWW-Authenticate`/`Proxy-Authenticate` when it is a 401/407, so after a retry ending 200 they are lost. Add new `TransferReport` fields (e.g. `AuthSchemesOffered`, `ProxyAuthSchemesOffered`) filled by `HttpProtocolHandler`. The handler counts an auth retry in `RedirectsFollowed` (~3541, report `RedirectCount` ~1465): check the count against upstream 8.22.0 (measure with `Record-CurlExchange.ps1 -Connections 2` under a timeout) before changing it. Tests use fakes or loopback only; put them in `Curl.Protocol.Http.UnitTests` in a new file.

## Acceptance criteria

- [ ] A test shows the report's offered-scheme field holds Basic and Digest after a 401 offering both followed by a retry that ends 200, and the proxy field the same after a 407.
- [ ] A test shows the report's redirect count after a digest auth retry equals upstream 8.22.0's value.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- Split from BL-3193 (over its cost cap). No stash was left.

## Log

- 2026-10-23: Created.
