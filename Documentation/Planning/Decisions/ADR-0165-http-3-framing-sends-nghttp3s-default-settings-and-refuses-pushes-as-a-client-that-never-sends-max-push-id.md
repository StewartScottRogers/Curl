# ADR-0165 — HTTP/3 framing sends nghttp3's default SETTINGS and refuses pushes, as a client that never sends MAX_PUSH_ID

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-730.

## Context

RFC 9114 fixes the HTTP/3 frame and stream layouts, but leaves the client to choose which
settings it sends, in what order, whether it allows server push, and how much of a frame it
will buffer. ADR-0144 records that curl's ngtcp2 build calls `nghttp3_settings_default` and
changes nothing, and that the `SETTINGS` bytes cannot be captured without a QUIC server, so
BL-730 was to check them against nghttp3's source. The Windows `curl.exe` (the Schannel
build) has no HTTP/3 at all, so there is no curl exchange to record with
`Record-CurlExchange.ps1`.

## Decision

In `Curl.Http3.UnitLibrary`:

1. **The client's control stream** starts `00 04 0d 06 ffffffffffffffff 01 00 07 00`:
   stream type 0, then one `SETTINGS` frame with `SETTINGS_MAX_FIELD_SECTION_SIZE`
   2^62 - 1, `SETTINGS_QPACK_MAX_TABLE_CAPACITY` 0 and `SETTINGS_QPACK_BLOCKED_STREAMS` 0,
   in that order. That is what nghttp3's `nghttp3_stream_write_settings` writes for its
   defaults: those three always, with `H3_DATAGRAM` and `ENABLE_CONNECT_PROTOCOL` only when
   switched on, which curl does not do. No grease setting, frame or stream is sent, as
   nghttp3 sends none. `Http3LocalUnidirectionalStreams.CurlSettings` holds the list; the
   QPACK encoder and decoder streams start with their types, `02` and `03`.
2. **No server push.** curl never sends `MAX_PUSH_ID`, so the client allows no push ID:
   a push stream is `H3_ID_ERROR` (RFC 9114 section 4.6), and so is a `CANCEL_PUSH`
   (section 7.2.3, a push ID above the maximum allowed). A `MAX_PUSH_ID` from the server
   is `H3_FRAME_UNEXPECTED` (section 7.2.7).
3. **Frames are read whole.** `Http3FrameReader` reads a known frame's whole payload before
   returning it, up to a limit its caller sets; over it is `H3_EXCESSIVE_LOAD`. The server's
   control stream allows 64 KiB (`Http3ControlStreamReader.MaximumFramePayloadLength`),
   far above any real `SETTINGS` or `GOAWAY`. Frames of unknown and grease types are
   skipped in 4 KiB chunks whatever their length. The request-stream reader in BL-731 sets
   its own limit.
4. **Grease is dropped on read.** Setting identifiers of the reserved form are left out of
   a parsed `Http3SettingsFrame`; other unknown identifiers are kept, since they are
   harmless and useful to a caller that knows them.
5. **The control stream closing** at any point, inside a frame included, is
   `H3_CLOSED_CRITICAL_STREAM` (section 6.2.1), not `H3_FRAME_ERROR`; so is either QPACK
   stream closing.
6. **Unknown unidirectional stream types**, grease types and streams that end before their
   type are reported as `null` by `Http3PeerUnidirectionalStreams.AcceptAsync`; the caller
   stops reading them, as section 6.2 allows.

## Consequences

The control stream's first bytes are pinned by a test and change only with this ADR. A
server push is always refused with a connection error, which is what nghttp3 does for a
client that never raised its maximum push ID. A server that sends a `DATA` frame larger
than a reader's limit fails with `H3_EXCESSIVE_LOAD`, so BL-731 must choose a limit that
real response bodies fit, or read `DATA` payloads in pieces.

## Alternatives considered

- **Sending only non-default settings** (an empty `SETTINGS`): smaller, but not what
  nghttp3 writes, so a server-side capture would differ from curl's.
- **Allowing pushes** by sending `MAX_PUSH_ID`: curl does not, and a drop-in replacement
  must not change what the server may send.
- **Streaming every payload**: needed only for `DATA`, and left to BL-731 where the
  request stream is read.
