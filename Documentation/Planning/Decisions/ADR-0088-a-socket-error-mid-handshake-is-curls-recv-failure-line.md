# ADR-0088 — A socket error mid-handshake is curl's `Recv failure` line

- **Status:** Accepted
- **Date:** 2026-09-27

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-369.

## Context

ADR-0009 makes `SslStreamTlsProvider` behave like curl's Schannel build on Windows and its
OpenSSL build elsewhere. BL-150 pinned a server that closes cleanly after the ClientHello.
BL-369 measured a server that resets the connection instead (a zero linger time, so the
close is a TCP RST), with curl 8.21.0 and `-sS https://127.0.0.1:<port>/`:

- Schannel build (`x86_64-w64-mingw32`, Git for Windows, and System32 `curl.exe`), on
  Windows 11 against `Record-CurlExchange.ps1 -Reset`: exit 35
  `Recv failure: Connection was aborted`, and in one run of six
  `Recv failure: Connection was reset` - Windows reports WSAECONNABORTED or WSAECONNRESET
  depending on timing.
- OpenSSL build (`curlimages/curl:8.21.0`, `x86_64-pc-linux-musl`, OpenSSL 3.5.7), client
  and a busybox `nc -l -e` server that exits without reading in one Linux container, so
  Linux sends the RST itself: exit 35 `Recv failure: Connection reset by peer`, three of
  three.

Until BL-369 the Schannel build reported the `SocketException` (a `Win32Exception`) as a
security status - `schannel: next InitializeSecurityContext failed: Unknown error
(0x00002746) - ...` - which no curl prints, and the OpenSSL build printed .NET's message.

## Decision

- **Any `SocketException` in the handshake failure is `Recv failure: <text>`**, checked
  before the security status (Schannel) and before the OpenSSL error string.
- The text is curl's for the error: in the Schannel build curl's own Winsock table,
  `Connection was reset` (WSAECONNRESET) and `Connection was aborted` (WSAECONNABORTED);
  in the OpenSSL build strerror's `Connection reset by peer` (ECONNRESET). The table is
  keyed by `SocketError`, so tests pin both builds on any platform.
- Any other socket error's own .NET message stands in, as ADR-0009 already does for an
  unmeasured OpenSSL failure. On Linux that message is strerror's text, which is what curl
  prints.
- Always `Recv`, never `Send failure`: curl sends the ClientHello before the reset can
  arrive, so every measured run failed on receive.

## Consequences

A reset is reported as curl reports it in both builds. Socket errors other than the three
measured ones print .NET's wording, which matches curl on Linux but may not on Windows.

## Alternatives considered

- Skip socket errors and fall back to `schannel: failed to receive handshake, ...`: not
  what curl prints for a reset.
- Map every Winsock error curl names: unmeasured text, and no handshake path produces them
  in practice.
