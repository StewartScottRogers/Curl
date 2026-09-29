# ADR-0180 — `TcpConnector` resolves for QUIC and `QuicDialer` tries each address in turn

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-728.

## Context

ADR-0144 section 3 puts the UDP socket in `Curl.Networking.UnitLibrary`: a datagram
adapter behind a seam like `ITcpDialer`, and a `ConnectMultiplexedAsync` that resolves as
`TcpConnector` does, tries each address, runs `Curl.Quic`'s handshake with the hand-built
TLS client and reports `%{time_connect}` and `%{time_appconnect}` when it completes.
ADR-0165 leaves two things to this task: the `Failed to connect to <host> port <port>
after <n> ms: <text>` line, and turning a receive error on the channel into exit 56. Some
choices were still open: where the QUIC code sits beside `TcpConnector`, when the next
address is tried, and what the `-v` lines are for failures ADR-0144 did not measure.

## Decision

1. **`TcpConnector.ConnectMultiplexedAsync` resolves; `QuicDialer` dials.** The connector
   takes an optional `QuicDialer`. Without one it keeps the interface's answer (exit 7,
   `QUIC is not available on this connector`). With one it loads the `--resolve` entries,
   applies `--connect-to` (exit 49 for a bad entry) and resolves through the same DNS
   cache, `-4`/`-6` filter and `-v` lines as `ConnectAsync` (exit 6), so a QUIC attempt and
   a TCP fallback share one cache and one connection-number sequence, as in curl. A proxy
   on the target is ignored: curl's ngtcp2 build connects QUIC directly.
2. **The seam is `IUdpChannelOpener`.** `UdpChannelOpener`, the production one, opens a
   `UdpDatagramChannel` bound to the local address and port `--interface` and
   `--local-port` give (any address and an ephemeral port by default). Parsing those two
   options into an address is not done anywhere yet, for TCP either; the composition
   passes what it has.
3. **Each address in turn.** `QuicDialer` reports `  Trying <address>:<port>...` and the
   trust event, opens the channel, and runs `QuicClientConnector` with curl's ClientHello
   (`QuicClientSettings.CreateCurlTlsSettings`, `server_name` left out for an IP address)
   and a `HandBuiltCertificateVerifier` over the shared `ServerCertificateVerification`.
   A failure moves on to the next address unless it is a timeout (exit 28 or 55) or an
   unusable `--cacert` (exit 77); when every address fails the last failure is the result.
   curl races addresses with happy eyeballs; trying them in order gives the same outcome
   for a server that answers and is simpler to test.
4. **Timeouts.** A `--connect-timeout` greater than zero is a limit on all the attempts
   together, counted from the connect's start; an attempt gets what is left, and the
   exit 28 message counts from the start (ADR-0117). Without one each handshake has QUIC's
   own 10 seconds and exit 55 (ADR-0144 section 5).
5. **The lines.** A success reports the TLS handshake event with no offered ALPN
   protocols (curl's ngtcp2 build prints no ALPN line) and `ReportConnectionOpened` with
   the channel's local endpoint, then returns a `QuicConnection` with the handshake's end
   as both `Connected` and `TlsHandshakeCompleted`. A failure reports its message, then
   `QUIC connect to <address> port <port> failed: <text>` (not for the handshake
   timeout, as measured), then `Failed to connect to <host> port <port> after <n> ms:
   <text>`, where `<text>` is `curl_easy_strerror`'s for the exit and `<n>` counts from the
   resolve. Exit 28 reports only `Connection timed out after <n> milliseconds`. A
   certificate the verifier rejected keeps the exit and message the TCP path gives it.
6. **Socket errors.** A `SocketException` from the channel during the handshake is
   exit 56 `QUIC: recvfrom() unexpectedly returned -1 (errno=<code>; <text>)`: the
   Schannel build's words for Winsock errors (`Connection was reset` for WSAECONNRESET,
   as measured), the system's message in the OpenSSL build. A send error is reported the
   same way; the channel does not say which call failed. A socket that cannot be opened
   or bound fails that address as exit 7 with the reason.
7. **Pooling.** `PoolingConnector.ConnectMultiplexedAsync` asks its inner connector every
   time; keeping one QUIC connection per origin is BL-735's.

## Consequences

- `Curl.Networking.UnitLibrary` references `Curl.Quic.UnitLibrary`, and
  `Curl.Quic.UnitLibrary` lets `Curl.Networking.UnitTests` see its internals, so the
  tests run a real handshake against a copy of `Curl.Quic.UnitTests`' in-memory server.
- The ClientHello is curl.se's LibreSSL profile on every platform until the OpenSSL
  profile exists for QUIC, and `--cert`, `--ciphers` and `--tls13-ciphers` do not reach
  a QUIC handshake yet; both are follow-up work.
- Nothing calls `ConnectMultiplexedAsync` until BL-732 composes a `QuicDialer` into the
  console's connector.

## Alternatives considered

- **A separate QUIC connector with its own resolver.** It would duplicate the DNS cache,
  the `Added ... to DNS cache` and resolve lines, and connection numbering, and a
  `--http3` race would resolve twice. Rejected.
- **Happy eyeballs across addresses.** Closer to curl's timing on multi-homed hosts, but
  the outcome for a host that answers is the same and the tests would have to race.
  Rejected for now.
