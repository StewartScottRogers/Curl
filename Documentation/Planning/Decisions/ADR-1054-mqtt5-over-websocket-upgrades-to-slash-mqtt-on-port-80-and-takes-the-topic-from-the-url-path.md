# ADR-1054 — MQTT 5 over WebSocket upgrades to /mqtt on port 80 and takes the topic from the URL path

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4244.

## Context

BL-4244 adds `mqtt5+ws://host[:port]/topic` to `Curl.Extension.Mqtt5.UnitLibrary` (ADR-0999): ADR-1049's publish or
subscribe flow carried over a WebSocket connection, as OASIS MQTT Version 5.0 section 6 describes. Upstream curl has
no such scheme to match. The WebSocket request and the MQTT topic both want the URL path, so the path, the default
port and how the topic is carried need deciding.

## Decision

- **Port 80** when the URL names none, the HTTP port RFC 6455 gives `ws://`. No TLS: a secure variant is a scheme of
  its own if one is wanted.
- **The request path is always `/mqtt`.** It is the path the common brokers serve MQTT over WebSocket on by default
  (EMQX, HiveMQ, and Mosquitto, which accepts any path), so the URL path stays free for the topic.
- **The topic is the URL path**, exactly as `mqtt5://` takes it (after the first '/', URL-decoded; no path is exit 3),
  so the same topic works over either transport.
- **Handshake.** The request offers `Sec-WebSocket-Protocol: mqtt`; the reply must be `101`, carry the
  `Sec-WebSocket-Accept` for the key sent and select `mqtt` (section 6). A non-101 status is exit 22
  `Refused WebSocket upgrade: <status>`, as curl's own `ws://`; a wrong accept, no selected `mqtt`, no status line or a
  head over 64 KiB is exit 8; the server closing before the head ends is exit 52 `Empty reply from server`.
- **Frames.** Each MQTT packet goes out as one masked binary frame. Received binary payload is one byte stream, so a
  packet may span frames and a frame may hold several packets. A ping is answered with a pong; the server's close
  frame reads as the end of the stream (exit 56 `Connection disconnected` when a packet was still awaited). A text
  frame, or a frame that breaks RFC 6455, fails the read with exit 56 and the network connection is closed, as
  section 6 requires. No close frame is sent at the end: MQTT's DISCONNECT ends the session, and the connection is
  closed after it.
- The shared codec `Curl.WebSocket.UnitLibrary` frames the bytes; the library references it beside
  `Curl.Extension.Abstractions`.

## Consequences

- A broker that serves MQTT on another WebSocket path cannot be reached until an option names the path; BL-4115,
  which adds the scheme's registration and options, may add one.
- `Mqtt5WebSocketConnection` is an `IConnection`, so `Mqtt5ClientFlow` runs unchanged over TCP, TLS and WebSocket.
