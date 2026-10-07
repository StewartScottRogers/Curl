---
id: BL-865
title: Map --http2 to HttpVersionPreference.Http2 so a cleartext request upgrades to h2c
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-716]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-865 — Map --http2 to HttpVersionPreference.Http2 so a cleartext request upgrades to h2c

## Goal

`curl --http2 http://host/` sends the h2c upgrade request BL-716 built into the HTTP handler, because `Curl.Console` maps `RequestedHttpVersion.Http2` to `HttpVersionPreference.Http2` instead of `Http11`.

## Context

- BL-716 added `HttpVersionPreference.Http2` (`Curl.Protocol.Abstractions.UnitLibrary/HttpVersionPreference.cs`) and the handler's upgrade (`HttpH2cUpgradeConnection`), pinned through a fake connection. It could not change the mapping: `Curl.Console` was held by BL-602 at the time.
- `Curl.Console/HttpVersionMapping.cs`, `ToHttpVersionPreference`: `--http2` falls to the `_ => HttpVersionPreference.Http11` arm today. The ALPN offer (`HttpOverTlsApplicationProtocolsOf`) stays as it is.
- With no version option the preference stays `Http11`: curl's default never upgrades over cleartext, even in the OpenSSL builds that default to HTTP/2 over TLS.

## Acceptance criteria

- [x] `HttpVersionMapping.ToHttpVersionPreference(RequestedHttpVersion.Http2)` returns `HttpVersionPreference.Http2`, pinned in `Curl.Console.UnitTests`, and no version option still returns `Http11`.
- [x] The doc comment on `ToHttpVersionPreference` says what `--http2` now maps to.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and the fast tests pass.

## Notes

- `--http2` now maps to `HttpVersionPreference.Http2`; the handler upgrades only when the transport is not TLS (`HttpProtocolHandler`, BL-716), so `https://` still leaves the choice to ALPN, which `HttpOverTlsApplicationProtocolsOf` keeps offering as `h2,http/1.1`.
- `AltSvcTransferCache.ApplyTo` uses the same mapping; a switched alt-svc route still falls to `Http11`/`Http3Only` as before, and a same-ALPN route keeps `Http2`, which only matters over cleartext.
- Two pins changed in `HttpVersionMappingTests`: the enum mapping row and the `HttpRequestOptionsFromCommandLine` row for `--http2`. Full fast suite green on Windows.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. --http2 maps to HttpVersionPreference.Http2, so a cleartext request sends the h2c upgrade
