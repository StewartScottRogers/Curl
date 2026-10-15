# ADR-0512 — The WebSocket, header and connect-only contracts on `IEasyTransfer`

- **Status:** Accepted
- **Date:** 2026-10-15

**Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"), for BL-2315.

## Context

ADR-0506 and ADR-0509 define one easy handle's contract. BL-2315 adds the three libcurl APIs that
work on an easy handle beside `curl_easy_perform`: the WebSocket API (`include/curl/websockets.h`),
the header API (`include/curl/header.h`) and `curl_easy_recv`/`curl_easy_send`, matching libcurl
8.22.0. A few shapes had more than one reasonable answer.

## Decision

- `IEasyTransfer` reaches them through three properties: `WebSocket` (`IEasyWebSocket`, namespace
  `Curl.Api.EngineContracts.WebSocket`), `Headers` (`ITransferHeaders`, `...Headers`) and
  `Connection` (`IConnectOnlyConnection`, `...ConnectOnly`). The property is `Connection`, not
  `ConnectOnly`, because `Curl.Api.Options.Network.ConnectOnly` already names the option's enum.
- `curl_ws_recv`, `curl_ws_send`, `curl_easy_recv` and `curl_easy_send` return a code-and-count
  record struct (`CurlWebSocketReceiveResult`, `CurlWebSocketSendResult`, `CurlConnectOnlyResult`)
  rather than a tuple or an `out` parameter: they are async, and the receive result also carries
  the frame. `CURLE_AGAIN` (`CurlCode.Again`) means nothing was ready, as in libcurl.
- `struct curl_ws_frame` is the record struct `CurlWebSocketFrame` (age, flags, offset, bytes left,
  length), whose default is every field zero. `curl_ws_meta` is the nullable `CurrentFrame`
  property: `null` outside the write callback, where libcurl returns `NULL`.
- `CurlWebSocketFlags` and `CurlHeaderOrigin` are `[Flags]` enums with upstream's numbers and an
  added `None = 0`, as `[Flags]` enums need a zero member.
- `curl_easy_header` is `CurlHeaderCode TryGet(..., out CurlHeader? header)`: it returns libcurl's
  `CURLHcode` rather than a `bool`, so callers see why a lookup failed. `curl_easy_nextheader` is
  `CurlHeader? Next(...)`, `null` at the end. `struct curl_header` is the record `CurlHeader`
  without libcurl's private `anchor`; `Next` finds its place from the record it is given. The
  `request` argument's -1 is the constant `ITransferHeaders.LastRequest`.

## Consequences

Contracts only: nothing implements them yet. Every `IEasyTransfer` fake must implement the three
new properties.
