---
id: BL-5423
title: Report a SOCKS handshake send failure as a send failure, not Recv failure
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/SocksProxyTunnel.cs]
requirement: none
created: 2026-11-18
completed:
---
# BL-5423 — Report a SOCKS handshake send failure as a send failure, not Recv failure

## Goal

A socket error while sending a SOCKS4 or SOCKS5 request is reported as curl reports a send failure, not as `Recv failure` with `CURLPX_RECV_CONNECT`.

## Context

Found by the shift-end review (ADR-0710) of the 2026-11-17 shift (silent-failure-hunter agent), in BL-5382's change.
`Curl.Networking.UnitLibrary/SocksProxyTunnel.cs:61`: the new `catch (IOException failure) when (CurlSocketErrorText.ReceiveFailure(failure) is { } receiveFailure)` wraps the whole handshake, including the writes in `SocksProxyTunnel.SendAsync` (`:121`) and the GSSAPI exchange. A reset during a write becomes exit 97 `Recv failure: ...` with `%{proxy_error}` `CURLPX_RECV_CONNECT`. curl 8.22.0's `lib/socks.c` reports a failed send with its own message and code (for example `Failed to send SOCKS5 connect request.`, `CURLPX_SEND_CONNECT`). Measure real curl before pinning the text.

## Acceptance criteria

- [ ] Only a failure while reading a reply maps to `Recv failure` and `ReceiveConnect`; a failure while writing maps to curl's send message and `CURLPX_SEND_*` code for that step.
- [ ] Unit tests in `Curl.Networking.UnitTests` cover a reset during a SOCKS5 greeting write and a reset during a reply read, pinning message, exit code 97 and proxy code.
- [ ] BL-5382's tests still pass; quality gates hold.

## Notes

## Log

- 2026-11-18: Created.
