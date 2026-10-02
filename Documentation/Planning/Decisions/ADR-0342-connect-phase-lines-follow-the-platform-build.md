# ADR-0342 — A proxy tunnel's CONNECT-phase lines follow the platform build, with curl 8.21.0's text

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-964.

## Context

Measured with `-v -p -x http://<proxy> http://example.test/` (BL-964 Notes), and through an
HTTPS proxy in BL-872:

- **Schannel**, curl 8.21.0 (Windows): `Establishing HTTP proxy tunnel to example.test:80`,
  the CONNECT, the `200` reply, `CONNECT phase completed for HTTP proxy`,
  `CONNECT tunnel established, response 200`.
- **OpenSSL**, curl 8.18.0 (Ubuntu under WSL; no 8.21.0 OpenSSL build was at hand):
  `allocate connect buffer`, `Establish HTTP proxy tunnel to example.test:80`, the CONNECT,
  the reply, `CONNECT phase completed`, `CONNECT tunnel established, response 200`.

curl 8.21.0's own source (`lib/cf-h1-proxy.c`, `lib/http_proxy.c` at tag `curl-8_21_0`) has
`Establishing %s proxy tunnel to %s` and `CONNECT%s phase completed for HTTP proxy`, so those two
differences are the version, not the TLS build. The same source still writes
`allocate connect buffer` in `tunnel_init`, once per tunnel filter (a redial after
`Connect me again please` builds a new one; a second CONNECT on the same connection goes through
`tunnel_reinit`, which writes nothing), yet the measured Schannel 8.21.0 binary does not print it.

## Decision

1. Every build prints `Establishing HTTP proxy tunnel to <authority>` (already so, BL-863),
   `CONNECT phase completed for HTTP proxy` and `CONNECT tunnel established, response <code>`,
   curl 8.21.0's text, after the `2xx` reply head of the CONNECT that opens the tunnel.
2. `allocate connect buffer` is the OpenSSL build's alone: written once per proxy connection,
   before its first CONNECT's lines (before `Proxy auth using`), never by the Schannel build, as
   measured on each.
3. `HttpProxyTunnelOptions.MatchesSchannelBuild` picks the build, `OperatingSystem.IsWindows()`
   by default as the solution's other per-build switches do (ADR-0009), so tests pin both builds
   on every platform.

## Alternatives considered

- **The OpenSSL 8.18.0 text on Linux and macOS** (`Establish`, `CONNECT phase completed`): rejected,
  because Curl is curl 8.21.0 on every platform (its `User-Agent` and `--version` say so) and
  8.21.0's source shows the newer text on every build.
- **No `allocate connect buffer` anywhere**, since 8.21.0's Schannel binary prints none: rejected,
  because the OpenSSL build printed it as measured and 8.21.0's source still writes it; only the
  Schannel binary is known not to.
