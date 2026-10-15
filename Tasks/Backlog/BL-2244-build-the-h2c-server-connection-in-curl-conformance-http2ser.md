---
id: BL-2244
title: Build the h2c server connection in Curl.Conformance.Http2Server.UnitLibrary that relays each HTTP/2 stream to an HTTP/1.1 connector, as nghttpx does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Http2Server.UnitLibrary, Curl.Conformance.Http2Server.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-14
completed:
---
# BL-2244 — Build the h2c server connection in Curl.Conformance.Http2Server.UnitLibrary that relays each HTTP/2 stream to an HTTP/1.1 connector, as nghttpx does

## Goal

A new `Curl.Conformance.Http2Server.UnitLibrary` (+ `.UnitTests`, both in `Curl.slnx`) holds an `IConnector` that serves h2c - prior knowledge and `Upgrade: h2c` - and relays each HTTP/2 stream as an HTTP/1.1 request to an inner `IConnector` (the sws emulation), turning the reply back into HTTP/2 frames, as upstream's nghttpx does in front of `sws`.

## Context

Split out of BL-2230 (2026-10-14), which wires this server onto `%HTTP2PORT` once it exists. Upstream's `http/2` test server is nghttpx terminating HTTP/2 and speaking HTTP/1.1 to `sws` (`tests/servers.pm` at curl-8_22_0; the nghttpx command line there shows its options). Precedent: the SSH stand-in, `Curl.Conformance.SshServer.UnitLibrary` (ADR-0456) - an `IConnector` whose `ConnectAsync` returns the client end of an in-memory duplex pair and runs the server end (`SshServerConnector`, `SshServerDuplexConnection`), and a factory the runner is handed through a record (`UpstreamSshServerFactory`, `UpstreamSshServer`).

Reuse, do not rewrite: `Curl.Http2.UnitLibrary` is a reference-free library already holding HPACK (`HpackEncoder`, `HpackDecoder`, Huffman, tables), the frame codec (`Http2FrameCodec`, `Http2FrameFactory`, `Http2FramePayloadParser`), settings and flow-control windows; reference it (it is not a `Curl.Protocol.*` library). The sws emulation is `Curl.Conformance.UnitLibrary`'s `SwsHttpServerConnector`; the new library may reference `Curl.Conformance.UnitLibrary` and `Curl.Protocol.Abstractions.UnitLibrary` as the SSH stand-in does. Write an ADR "Decided by Claude under Stewart's delegation" for the design (in-memory relay, which nghttpx headers are added - `Via: 2 nghttpx`, `X-Forwarded-For`/`X-Forwarded-Proto` per servers.pm options; measure from the cases' `<protocol>` sections, e.g. upstream tests 1700, 1702, 2400).

## Acceptance criteria

- [ ] The library and its `.UnitTests` exist, listed in `Curl.slnx` beside each other in sort order; AOT-compatible; `dotnet build` clean; no `Curl.Protocol.*` reference beyond `Curl.Protocol.Abstractions.UnitLibrary`.
- [ ] Unit tests pin: the client preface and server SETTINGS exchange, a GET relayed to the inner connector with the HTTP/1.1 request bytes nghttpx sends (including `Via` / `X-Forwarded-*` as the cases expect), a POST with a DATA body, `Upgrade: h2c` answered with `101 Switching Protocols` then the reply on stream 1, `SETTINGS_ENABLE_PUSH` honoured (no PUSH_PROMISE sent), and GOAWAY when the client closes.
- [ ] Flow control: the server sends WINDOW_UPDATE so a body larger than 65535 bytes is received, and never sends DATA past the client's window.
- [ ] A factory the runner can call in one line (like `UpstreamSshServerFactory`) builds the server over a given inner connector.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Conformance.Http2Server.UnitLibrary` shows 100% line and branch coverage, complexity at most 10 and CRAP at most 30; the fast tests are green; the library has a `CLAUDE.md`.

## Notes

## Log

- 2026-10-14: Created.
