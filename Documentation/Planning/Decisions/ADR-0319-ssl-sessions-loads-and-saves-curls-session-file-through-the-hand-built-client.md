# ADR-0319 — `--ssl-sessions` loads and saves curl's session file through the hand-built client

- **Status:** Accepted
- **Date:** 2026-10-01
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-710: curl 8.21.0's `--ssl-sessions <file>` imports TLS sessions from a file before the transfers
and exports them after, so a later run resumes. ADR-0151 accepts the option on every platform and
gives it to the hand-built TLS client, because `SslStream` can neither export nor import a session.

The reference Schannel build refuses `--ssl-sessions` with exit 2, and no OpenSSL build with
`SSLS-EXPORT` was available to record, so the format and the messages come from the source at tag
`curl-8_21_0`: `lib/vtls/vtls_spack.c`, `lib/vtls/vtls_scache.c`, `src/tool_ssls.c` and
`src/tool_msgs.c` (BL-710 Notes).

- File: two `#` comment lines, then one `base64(salt32 ‖ HMAC-SHA256(salt, peer key)):base64(pack)`
  line per session. Pack: `01`, then tagged fields - `04` u16-length ticket (OpenSSL's
  `i2d_SSL_SESSION`), `02` u16 IETF protocol id, `03` u64 valid-until, `05` u16-length ALPN,
  `06` u32 max early data, `07` u16-length QUIC transport parameters.
- Warnings: `unrecognized line N in SSL session file F`, `invalid shmax base64 encoding in line N`,
  `invalid sdata base64 encoding in line N: X`, `import of session from line N rejected(26|43)`,
  `Failed to create SSL session file F`; under `-v`, `Note: SSL session file does not exist (yet?): F`.
  None changes the exit code.
- The load runs before the transfers and the save after them, whatever the transfers' result.
  At most 2 sessions are kept per peer, and a TLS 1.3 session is taken out of the cache when offered.

## Decision

- `TlsClientRouting.Choose` sends any connection with `SslSessionsFile` to the hand-built client.
- `Curl.Networking`'s `TlsSessionCache` holds the run's sessions as `vtls_scache.c` does, and
  `TlsSessionPacking` packs and unpacks curl's format. `Curl.Console`'s `TlsSessionFileLines` loads the
  file before the run's transfers and saves it after, with CRLF line ends on Windows, writing the
  warnings above (none under `-s`).
- The peer key is curl's: `host:port`, then `:NO-VRFY-PEER:NO-VRFY-HOST` under `-k`, `:VRFY-STATUS`
  under `--cert-status`, `:CA-<full path>` of a `--cacert` file when the peer is verified, and
  `:IMPL-Curl:G`. curl's own keys name `IMPL-OpenSSL/<version>`, so a line written by curl never
  matches one of Curl's peers; it is kept, unused, and written back, as curl keeps a line whose peer
  it never meets.
- The port is the connection's remote port (`IConnection.RemoteEndPoint`), 443 when unknown; through a
  tunnel this is the proxy's port.
- Only TLS 1.3 sessions are kept. The hand-built TLS 1.2 client resumes nothing, so it records none.
- `--tls-earlydata` (0-RTT on a resumed session) is left to BL-1105: it needs the request bytes before
  the handshake, which the HTTP layer does not hand to the TLS provider today.

## Consequences

- A Curl run and a later Curl run share sessions through the file in curl's exact format; the file
  is readable by curl and curl's is readable by Curl, though neither resumes the other's sessions.
- Each run under `--ssl-sessions` takes the hand-built client, never `SslStream`.

## Alternatives considered

- Name `IMPL-OpenSSL/<version>` in the key so curl's lines match: rejected, since the ticket inside
  is OpenSSL's own and the version string would be a guess that changes with every OpenSSL release.
- Keep TLS 1.2 sessions: rejected until the TLS 1.2 client can resume one.
