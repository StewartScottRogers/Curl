# ADR-0144 — HTTP/3 is hand-built over a hand-built QUIC, and `--http3` races TCP as curl's ngtcp2 build does

- **Status:** Accepted
- **Date:** 2026-09-28
- **Supersedes:** ADR-0017, for HTTP/3 (its HTTP/2 half is superseded by ADR-0141)

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-718.

## Context

ADR-0017 refused `--http3` and `--http3-only` with exit 2 on every platform, as the
Windows reference build (Schannel, no QUIC) refuses them. Stewart's standing rule (root
`CLAUDE.md`, 2026-09-28) is that Curl is a complete reimplementation: QUIC and HTTP/3
are built by hand, never refused. `System.Net.Quic` is not used: it needs `msquic`,
which is not present on every platform, it cannot be driven through the solution's
seams, and it would not reproduce curl's bytes, `-v` lines or timings. ADR-0120 has
already placed `Curl.Quic.UnitLibrary` and `Curl.Http3.UnitLibrary` in the reference
graph, and ADR-0140 gives QUIC the hand-built TLS 1.3 client in `Curl.Tls.UnitLibrary`.
This ADR decides how the pieces fit, what curl's build does on the wire, and how
failures map to exit codes.

curl's manual (https://curl.se/docs/manpage.html, curl 8.23.0, checked 2026-09-28):
`--http3` "Use HTTP/3. For HTTPS, this negotiates HTTP/3 in the QUIC handshake.";
`--http3-only` "Use HTTP/3 only, and do not fall back to earlier HTTP versions if the
server does not support HTTP/3."

### The builds

| Build | `-V` | HTTP/3 |
| --- | --- | --- |
| Git for Windows' `mingw64\bin\curl.exe` 8.21.0, Schannel (the Windows reference, ADR-0018) | no `HTTP3` | refuses `--http3` and `--http3-only`, exit 2 (ADR-0017) |
| curl.se's official Windows build, installed by WinGet (`cURL.cURL`): `curl 8.18.0 (x86_64-w64-mingw32) libcurl/8.18.0 LibreSSL/4.2.1 … nghttp2/1.68.0 ngtcp2/1.21.0 nghttp3/1.15.0 WinLDAP` (8.22.0 with ngtcp2 1.25.0 and nghttp3 1.18.0 is the current release, https://curl.se/windows/, checked 2026-09-28) | `Features:` lists `HTTP2 HTTP3` | yes |
| Ubuntu (WSL) `curl 8.18.0 … OpenSSL/3.5.5 … nghttp2/1.68.0 …` | no `HTTP3` | `curl: option --http3: the installed libcurl version does not support this`, then `curl: try 'curl --help' or 'curl --manual' for more information`, exit 2 |

So curl.se's ngtcp2 build is the only curl at hand that speaks HTTP/3, and it is the
reference for everything below on every platform. No local HTTP/3 server was available;
success was measured against `https://www.google.com/` (public, HTTP/3 on UDP 443), and
every failure and fallback against the loopback recorder.

### Measurements (2026-09-28, curl.se's build unless stated)

Taken with `Record-CurlExchange.ps1`. This task added its `-UdpSink` switch: it binds UDP
on the recorder's port and takes every datagram without answering, writing them to
`datagrams.txt`, so QUIC meets a silent peer instead of an ICMP port unreachable, and
curl's Initial packets can be decrypted offline (their keys derive from the destination
connection ID, RFC 9001 section 5.2).

**Success**, `--http3 -v https://www.google.com/` (`--http3-only` prints the same lines),
exit 0, `%{http_version}` `3`:

```text
* Host www.google.com:443 was resolved.
* IPv6: (none)
* IPv4: 142.251.154.119, 142.251.157.119, …
*   Trying 142.251.154.119:443...
* SSL Trust Anchors:
*   CAfile: <the build's curl-ca-bundle.crt>
* SSL connection using TLSv1.3 / TLS_AES_256_GCM_SHA384 / [blank] / UNDEF
* Server certificate:
*   subject: CN=*.google.com
*   start date: Sep 10 19:22:01 2026 GMT
*   expire date: Dec  3 19:22:00 2026 GMT
*   issuer: C=US; O=Google Trust Services; CN=WE2
*   Certificate level 0: Public key type ? (256/128 Bits/secBits), signed using ecdsa-with-SHA256
*   Certificate level 1: Public key type ? (256/128 Bits/secBits), signed using ecdsa-with-SHA384
*   Certificate level 2: Public key type ? (384/192 Bits/secBits), signed using ecdsa-with-SHA384
*   subjectAltName: "www.google.com" matches cert's "*.google.com"
* SSL certificate verified via OpenSSL.
* Established connection to www.google.com (142.251.154.119 port 443) from 192.168.1.174 port 55635 
* using HTTP/3
* [HTTP/3] [0] OPENED stream for https://www.google.com/
* [HTTP/3] [0] [:method: GET]
* [HTTP/3] [0] [:scheme: https]
* [HTTP/3] [0] [:authority: www.google.com]
* [HTTP/3] [0] [:path: /]
* [HTTP/3] [0] [user-agent: curl/8.18.0]
* [HTTP/3] [0] [accept: */*]
> GET / HTTP/3
> Host: www.google.com
> User-Agent: curl/8.18.0
> Accept: */*
> 
* Request completely sent off
< HTTP/3 200 
< content-type: text/html; charset=ISO-8859-1
…
< 
* Connection #0 to host www.google.com:443 left intact
```

There is no `ALPN:` line and no `TLSv1.3 (OUT), TLS handshake, …` line: the TLS messages
travel in QUIC CRYPTO frames, and the ngtcp2 path logs none of them. The build's own
`Note: Using embedded CA bundle …` line comes first; it belongs to that build's CA
bundle, not to HTTP/3.

**Failures and fallback** (loopback; `-k` where a TLS server answered):

| Invocation | Peer | Exit | Lines after `*   Trying 127.0.0.1:<port>...` |
| --- | --- | --- | --- |
| `--http3-only https://` | nothing on UDP (ICMP port unreachable) | 56 | `* QUIC: recvfrom() unexpectedly returned -1 (errno=10054; Connection was reset)`, `* QUIC connect to 127.0.0.1 port <p> failed: Failure when receiving data from the peer`, `* Failed to connect to 127.0.0.1 port <p> after 5 ms: Failure when receiving data from the peer`, `* closing connection #0`, then `curl: (56) QUIC: recvfrom() unexpectedly returned -1 (errno=10054; Connection was reset)` |
| `--http3-only https://` | silent UDP (`-UdpSink`) | 55 after 10 037 ms | `* ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT`, `* Failed to connect to 127.0.0.1 port <p> after 10007 ms: Failed sending data to the peer`, `* closing connection #0`, `curl: (55) ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT` |
| `--http3-only --connect-timeout 1 https://` | silent UDP | 28 | `curl: (28) Connection timed out after 1008 milliseconds` |
| `--http3-only --connect-timeout 3 https://example.com:9/` | public host, nothing on UDP 9 | 28 | a second `*   Trying 172.66.147.243:9...` for the next address, then `* Connection timed out after 3013 milliseconds` |
| `--http3-only http://` | – | 3 | `* HTTP/3 requested for non-HTTPS URL`, `* closing connection #-1`, `curl: (3) HTTP/3 requested for non-HTTPS URL` (no `Trying` line) |
| `--http3 http://` | HTTP/1.1 on TCP | 0 | plain HTTP/1.1, exactly as without `--http3`; no datagram is sent |
| `--http3 https://` | nothing on UDP, TLS on TCP | 0 | the three QUIC failure lines of the first row, then `*   Trying 127.0.0.1:<p>...`, `* ALPN: curl offers h2,http/1.1` and the TCP TLS handshake; the transfer runs over HTTP/1.1 |
| `--http3 https://` | silent UDP, TLS on TCP | 0 | no QUIC line; a second `*   Trying 127.0.0.1:<p>...` then `* ALPN: curl offers h2,http/1.1`; `%{time_connect}` 0.212 (0.207 on a rerun) |
| `--http3 --happy-eyeballs-timeout-ms 1000 https://` | silent UDP, TLS on TCP | 0 | as above; `%{time_connect}` 1.007 and 1.006 |
| `--http3 https://` | nothing on UDP or TCP | 56 | the QUIC failure lines, `*   Trying …`, `* connect to 127.0.0.1 port <p> from 0.0.0.0 port <q> failed: Connection refused`, `* Failed to connect to 127.0.0.1 port <p> after 2015 ms: Could not connect to server`, `* closing connection #0`, `curl: (56) QUIC: recvfrom() …` - the QUIC attempt's error |

Option interplay (last one wins, as for every version option): `--http1.1 --http3` sends
QUIC datagrams and races; `--http3 --http1.1`, `--http3 --http2` and
`--http3-only --http1.1` send no datagram and speak HTTP/1.1 over TCP; `--http2
--http3-only` sends QUIC only (exit 55 against a silent peer).

**The client Initial**, decrypted from `datagrams.txt` (`--http3-only`, silent peer):

- Every datagram is 1200 bytes: one Initial packet, version `0x00000001`, a 20-byte
  random destination and a 20-byte random source connection ID, no token, a one-byte
  packet number starting at 0, padded with PADDING frames.
- ngtcp2 1.21 splits the ClientHello into 10 to 15 CRYPTO frames of a few bytes each, in
  shuffled offset order with PING and PADDING between them, differently in each packet.
- Against a silent peer, packet numbers 1 to 3 re-send the ClientHello on the probe
  timeout, and packet 4, at the 10-second handshake timeout, carries
  `CONNECTION_CLOSE` (type `0x1c`) with transport error `0x1` (`INTERNAL_ERROR`), frame
  type 0 and an empty reason. When `--connect-timeout 1` fires first, packet 1 carries
  `CONNECTION_CLOSE` with error `0x0` (`NO_ERROR`).
- The ClientHello (LibreSSL 4.2.1, 251 bytes with SNI `localhost`): legacy version
  `0x0303`, an empty legacy session ID, cipher suites `1302 1303 1301 00ff`, the null
  compression method, and these extensions (LibreSSL shuffles their order on every
  connection; this is one run's):
  `quic_transport_parameters` (57), `server_name` (0), `ec_point_formats` (11:
  uncompressed), `supported_groups` (10: `001d 0017 0018 0019`), `key_share` (51:
  x25519 only), ALPN (16: `h3`, `h3-29`), `supported_versions` (43: `0304` only),
  `signature_algorithms` (13: `0806 0601 0603 0805 0501 0503 0804 0401 0403`).
  With an IP-literal URL `server_name` is absent.
- The transport parameters, in the order sent:

  | ID | Parameter | Value |
  | --- | --- | --- |
  | `0x0f` | `initial_source_connection_id` | the 20-byte source connection ID |
  | `0x05` | `initial_max_stream_data_bidi_local` | 32 768 |
  | `0x06` | `initial_max_stream_data_bidi_remote` | 32 768 |
  | `0x07` | `initial_max_stream_data_uni` | 1 048 576 000 |
  | `0x04` | `initial_max_data` | 1 048 576 000 |
  | `0x08` | `initial_max_streams_bidi` | 262 144 |
  | `0x09` | `initial_max_streams_uni` | 262 144 |
  | `0x11` | `version_information` (RFC 9368) | chosen `0x00000001`, available `0x00000001` |

  Not sent, so the RFC 9000 defaults apply: `max_idle_timeout` (curl sets 0, "no idle
  timeout from our side"), `max_udp_payload_size`, `ack_delay_exponent`,
  `max_ack_delay`, `disable_active_migration`, `active_connection_id_limit`.

These values agree with curl's source at tag `curl-8_18_0`
(`lib/vquic/curl_ngtcp2.c`, `quic_settings`): `H3_STREAM_WINDOW_SIZE_INITIAL` 32 KiB,
`H3_CONN_WINDOW_SIZE_MAX` 100 times the 10 MiB stream maximum, `QUIC_MAX_STREAMS`
256 × 1024, `QUIC_HANDSHAKE_TIMEOUT` 10 s unless `--connect-timeout` is set,
`max_stream_window` 0 (no window auto-tuning), path MTU discovery on
(`MAX_UDP_PAYLOAD_SIZE` 1452 in `lib/vquic/vquic_int.h`). Neither the congestion
controller nor the HTTP/3 `SETTINGS` can be seen without a server: curl calls
`ngtcp2_settings_default`, whose `cc_algo` is CUBIC, and never changes it; it calls
`nghttp3_settings_default` and changes nothing, so its QPACK dynamic table capacity and
blocked-streams limit are 0. The racing rule is in `lib/cf-https-connect.c`: the h3
attempt starts first; the next starts at once when it fails, at the soft timeout
(a quarter of `--happy-eyeballs-timeout-ms`) if it "has not seen any data", and at the
hard timeout (`--happy-eyeballs-timeout-ms`, default 200 ms) otherwise. The measured
fallback starts at the hard timeout against a silent peer, so the soft rule never fires
for ngtcp2 (its attempt reports no reply time before the handshake completes); Curl
follows what was measured.

## Decision

### 1. The libraries

| Library | Holds | References (ADR-0120) |
| --- | --- | --- |
| `Curl.Quic.UnitLibrary` (BL-719) | QUIC v1 client: variable-length integers, packet headers and frames (RFC 9000), Initial secrets, packet and header protection (RFC 9001), the handshake driving `Curl.Tls`'s I/O-free `Tls13ClientHandshake` at each encryption level, loss detection and congestion control (RFC 9002, RFC 9438), streams and flow control, connection close, idle timeout and stateless reset | `Curl.Tls.UnitLibrary`, `Curl.Cryptography.UnitLibrary`, `Curl.Protocol.Abstractions.UnitLibrary` |
| `Curl.Http3.UnitLibrary` (BL-720) | HTTP/3 frames, control and QPACK streams, `SETTINGS`, `GOAWAY` (RFC 9114), QPACK (RFC 9204) | `Curl.Http2.UnitLibrary` (HPACK's Huffman code, which QPACK reuses), `Curl.Protocol.Abstractions.UnitLibrary` |

`Curl.Http3` never references `Curl.Quic`: it reads and writes streams through the
Abstractions contracts below, so HTTP/3 is tested over in-memory streams with no QUIC.
Neither constructs a `Socket`; time comes through `TimeProvider`, randomness (connection
IDs, packet-number start, TLS randoms) through injected sources.

### 2. The contracts in `Curl.Protocol.Abstractions.UnitLibrary` (BL-721)

- **The UDP seam is the existing `IDatagramChannel`.** It gains one default member,
  `EndPoint? LocalEndPoint => null`, for the `Established connection … from <ip> port
  <port>` line and `%{local_ip}`/`%{local_port}`; TFTP's fakes are unaffected. A receive
  that fails (an ICMP port unreachable) throws the `SocketException` the BCL raised; the
  QUIC layer turns it into curl's line.
- **`IMultiplexedConnection : IAsyncDisposable`**: one QUIC connection. Members:
  `EndPoint? RemoteEndPoint`, `EndPoint? LocalEndPoint`, `string ApplicationProtocol`
  (the ALPN the server chose), `ValueTask<IMultiplexedStream> OpenBidirectionalStreamAsync(CancellationToken)`,
  `ValueTask<IMultiplexedStream> OpenUnidirectionalStreamAsync(CancellationToken)` (HTTP/3's
  control and QPACK streams), `ValueTask<IMultiplexedStream> AcceptUnidirectionalStreamAsync(CancellationToken)`
  (the server's), and `ValueTask CloseAsync(long applicationErrorCode, CancellationToken)`.
- **`IMultiplexedStream : IAsyncDisposable`**: `long StreamId`;
  `ValueTask<int> ReadAsync(Memory<byte>, CancellationToken)` returning 0 at the peer's
  FIN; `ValueTask WriteAsync(ReadOnlyMemory<byte>, bool endStream, CancellationToken)`;
  `void Abort(long applicationErrorCode)` (RESET_STREAM and STOP_SENDING). A stream the
  peer resets makes `ReadAsync` throw `MultiplexedStreamResetException`, carrying the
  application error code; a lost connection throws `MultiplexedConnectionFailedException`
  carrying the `CurlExitCode` and curl's message from the table in section 7.
- **`IConnector.ConnectMultiplexedAsync(ConnectTarget target, CancellationToken)`**,
  returning `MultiplexedConnectResult` (`Connected(IMultiplexedConnection, ConnectTimings)`
  or `Failed(CurlExitCode, string)`, built like `ConnectResult`). Its default
  implementation returns `Failed(CurlExitCode.CouldntConnect, "QUIC is not available on
  this connector")`, so every existing connector and fake compiles unchanged.
- **`HttpVersionPreference`** gains `Http3` (`--http3`) and `Http3Only`
  (`--http3-only`), appended after the values BL-659 adds for HTTP/2.

Nothing else is added; later tasks that need more amend this ADR.

### 3. Where the UDP socket lives

`Curl.Networking.UnitLibrary` (BL-728) owns it: a UDP datagram adapter behind a dialer
seam like `ITcpDialer` (ADR-0083) implements `IDatagramChannel`, and its
`ConnectMultiplexedAsync` resolves the host as `TcpConnector` does (`-4`/`-6`,
`--resolve`, `--connect-to`, `--interface`, `--local-port`), tries each address in turn,
runs `Curl.Quic`'s handshake with the hand-built TLS client (ADR-0140, verified by the
shared `IServerCertificateVerifier`), and reports `%{time_connect}` and
`%{time_appconnect}` when the handshake completes. `PoolingConnector` keeps a QUIC
connection per origin for reuse by later transfers and by `-Z` streams (BL-735).

### 4. Racing and fallback

The HTTP handler (`Curl.Protocol.Http.UnitLibrary`, BL-731 and BL-732) chooses, per
transfer, from `HttpVersionPreference` and the URL:

- **`Http3Only`** on `https://`: QUIC only, through `ConnectMultiplexedAsync`. Its
  failure is the transfer's failure. On `http://` it fails before connecting with exit 3
  and `HTTP/3 requested for non-HTTPS URL`.
- **`Http3`** on `https://`: start the QUIC attempt; start the TCP attempt (TLS with
  ALPN `h2,http/1.1` on every platform, as measured) as soon as the QUIC attempt fails,
  or when `--happy-eyeballs-timeout-ms` (default 200 ms, BL-644) has elapsed on the
  injected `TimeProvider` without the QUIC handshake completing. The first attempt to
  succeed carries the transfer and the other is cancelled and disposed. When both fail,
  the transfer fails with the QUIC attempt's exit code and message, as measured.
- **`Http3`** on `http://`: ignored; plain HTTP/1.1, as measured.
- **Any other preference**: no QUIC attempt. A plain `https://` without `--http3` never
  sends a datagram (Alt-Svc upgrades are BL-733's).
- The version options are one setting and the last one on the command line wins
  (BL-732).

### 5. QUIC on the wire

- **Version** 1 only, with `version_information` as measured. A Version Negotiation
  packet that lists no version 1 fails the connect (section 7).
- **ClientHello**: ADR-0140's rule stands: on Windows the LibreSSL profile's TLS 1.3
  parts (measured above: suites `1302 1303 1301 00ff`, groups `001d 0017 0018 0019`, an
  x25519 key share, the nine signature algorithms, `ec_point_formats` uncompressed, no
  legacy session ID); on Linux and macOS the OpenSSL profile's TLS 1.3 parts. The
  extension order is fixed at the order measured above, not shuffled per connection, so
  the first Initial can be pinned for fixed inputs (a server cannot tell the
  difference). ALPN offers `h3`, then `h3-29`; either answer runs RFC 9114 HTTP/3.
- **CRYPTO frames**: the ClientHello goes in one CRYPTO frame at offset 0, then PADDING
  to a 1200-byte datagram. ngtcp2's shuffled small frames are anti-ossification
  randomness that changes every packet and that no server can depend on; one frame keeps
  the pinned Initial deterministic.
- **Connection IDs**: 20 random bytes each for the first destination and the source.
- **Transport parameters**: exactly the table above, in that order, encoded as the
  minimal variable-length integers.
- **Congestion control**: CUBIC (RFC 9438) over RFC 9002's loss detection and probe
  timeout, as ngtcp2's default that curl keeps. RFC 9002's NewReno is built too, because
  CUBIC's recovery reuses its structure, but it is not selectable from the command line
  (curl has no option for it). Pacing follows RFC 9002 section 7.7.
- **Maximum UDP payload**: 1200 bytes until path MTU discovery (RFC 9000 section 14,
  DPLPMTUD) confirms more, up to 1452, as curl's `MAX_UDP_PAYLOAD_SIZE`.
- **Timeouts**: the handshake must complete within `--connect-timeout`, or 10 s when it
  is not given. After the handshake the client advertises no idle timeout and honours the
  server's, sending a PING at half the server's `max_idle_timeout` while a transfer waits
  (`curl_ngtcp2.c` lines 185 to 211).
- **Close**: `CONNECTION_CLOSE` with `NO_ERROR` when the transfer ends or a timeout
  cancels it, and with `INTERNAL_ERROR` after a handshake timeout, as measured; the
  application close after HTTP/3 is `H3_NO_ERROR` (`0x100`).
- **HTTP/3 `SETTINGS`**: nghttp3's defaults as curl leaves them: QPACK dynamic table
  capacity 0 and blocked streams 0, so requests are encoded with the static table and
  literals only; BL-730 checks the exact `SETTINGS` bytes against nghttp3 1.15's
  `nghttp3_conn.c`, since they cannot be captured without a server.

### 6. Output

- `-v` for a QUIC transfer prints the lines measured above, on every platform: the
  resolve and `Trying` lines as for TCP; the TLS lines of the build the profile belongs
  to (on Windows curl.se's LibreSSL lines, `SSL Trust Anchors:` naming the anchors Curl
  actually used; on Linux and macOS the OpenSSL lines the TCP path prints, without the
  handshake-message lines, which the QUIC path never prints); `Established connection
  to …` with its trailing space; `using HTTP/3`; `[HTTP/3] [<stream>] OPENED stream for
  <url>` and one `[HTTP/3] [<stream>] [<name>: <value>]` line per pseudo-header and
  header; `> GET / HTTP/3`; `< HTTP/3 200 ` with its trailing space. The build-specific
  `Note: Using embedded CA bundle` line is not printed: Curl embeds no bundle.
- `-i` and `-D` write `HTTP/3 <code> ` status lines; `%{http_version}` is `3` (BL-734).
- `curl -V` lists `HTTP3` under `Features:` on every platform once BL-732 makes the
  options work, as ADR-0141 does for `HTTP2`. The version line gains no `ngtcp2/` or
  `nghttp3/` token, because Curl links neither.
- **Until BL-732 lands**, `--http3` and `--http3-only` keep ADR-0017's refusal (exit 2,
  `the installed libcurl version does not support this`), as ADR-0137 does for every
  real option not yet implemented; the refusal goes in the change that makes them work.

### 7. Exit codes

Each QUIC and HTTP/3 failure maps to one exit code and curl's message. "Measured" rows
are pinned from the runs above; "source" rows from `curl_ngtcp2.c` at `curl-8_18_0`,
whose text the task that implements them copies from that file.

| Failure | Exit | curl's message | From |
| --- | --- | --- | --- |
| `--http3-only` with an `http://` URL | 3 `UrlMalformat` | `HTTP/3 requested for non-HTTPS URL` | measured |
| A UDP receive fails during the handshake (ICMP port unreachable) | 56 `RecvError` | `QUIC: recvfrom() unexpectedly returned -1 (errno=<n>; <text>)`, with the platform's error number and curl's text for it (Windows: `10054`, `Connection was reset`) | measured |
| The handshake does not complete in 10 s and `--connect-timeout` is not set | 55 `SendError` | `ngtcp2_conn_handle_expiry returned error: ERR_HANDSHAKE_TIMEOUT` | measured |
| `--connect-timeout` or `-m` elapses | 28 `OperationTimedOut` | `Connection timed out after <n> milliseconds` | measured |
| The peer closes the connection during the handshake with a transport error, a TLS alert (`CRYPTO_ERROR`), or Version Negotiation without version 1 | 7 `CouldntConnect` | `Failed to connect to <host> port <port> after <n> ms: Could not connect to server` | source |
| The peer closes during the handshake with `CONNECTION_REFUSED` (`0x2`) | 8 `WeirdServerReply` | as above, with `Weird server reply` | source |
| TLS fails inside QUIC, including certificate verification | 60 `PeerFailedVerification` | the verifier's message (ADR-0140) | source |
| The server allows fewer than three unidirectional streams, or the control or QPACK streams cannot be opened | 96 `QuicConnectError` | `QUIC connection lacks 3 uni streams to run HTTP/3`, `error creating HTTP/3 control stream: <reason>` and the like | source |
| The server resets the request stream | 95 `Http3`, or 18 `PartialFile` once body bytes have arrived | `HTTP/3 stream <id> reset by server` | source |
| The request stream ends before the response head is complete | 95 `Http3` | `HTTP/3 stream <id> was closed cleanly, but before getting all response header fields, treated as error` | source |
| A write fails after the handshake | 55 `SendError` | `ngtcp2_conn_writev_stream returned error: <reason>` | source |
| Under `--http3`, both the QUIC and the TCP attempt fail | the QUIC attempt's exit and message | | measured |

## Consequences

Good:

- `--http3` and `--http3-only` work on every platform, where today they exit 2; scripts
  written for curl.se's Windows build or a Linux curl with ngtcp2 run unchanged.
- Everything is testable off the network: QUIC runs over a fake `IDatagramChannel` with
  a fake `TimeProvider`, HTTP/3 over in-memory `IMultiplexedStream`s, and the race over
  fake connectors.
- The measured Initial, transport parameters and failure lines give each task bytes to
  pin rather than guesses.

Costs:

- A very large, security-sensitive hand-written stack (QUIC's packet protection, loss
  recovery, CUBIC, flow control, QPACK). It is held to the quality gates and to the RFCs'
  test vectors, and runs only when `--http3`, `--http3-only` or an Alt-Svc `h3` entry
  asks for it.
- On Windows `curl -V` lists `HTTP3` where the reference does not, and on Linux where
  the distribution's curl does not; both match curl.se's build.
- Some failure texts come from curl's source rather than a run, because no local HTTP/3
  server could be made to fail on demand; the tasks that implement them keep the source
  as their reference.

The work, each its own task:

- BL-719 — create `Curl.Quic.UnitLibrary` and `Curl.Quic.UnitTests`.
- BL-720 — create `Curl.Http3.UnitLibrary` and `Curl.Http3.UnitTests`.
- BL-721 — add the contracts in section 2 to `Curl.Protocol.Abstractions.UnitLibrary`.
- BL-722 — encode and decode QUIC variable-length integers, packet headers and frames.
- BL-723 — protect QUIC packets with the RFC 9001 Initial secrets, AEAD and header
  protection.
- BL-724 — complete the QUIC handshake over the hand-built TLS 1.3 client, with the
  ClientHello, ALPN, transport parameters and first Initial of section 5.
- BL-725 — detect loss and control congestion to RFC 9002, CUBIC by default.
- BL-726 — carry QUIC streams with stream and connection flow control.
- BL-727 — close connections, time out idle ones, recognise stateless resets and map
  failures to the exit codes of section 7.
- BL-728 — dial QUIC over UDP in `Curl.Networking.UnitLibrary` (section 3).
- BL-729 — encode and decode HTTP/3 header blocks with QPACK.
- BL-730 — read and write HTTP/3 frames, control streams and QPACK streams.
- BL-731 — send an HTTP/3 request and read its response on a QUIC stream.
- BL-732 — parse `--http3` and `--http3-only` and run transfers over HTTP/3, racing and
  falling back as section 4 says.
- BL-733 — use `h2` and `h3` alternatives from Alt-Svc.
- BL-734 — write curl's `-v`, `-i`, `%{http_version}` and `-V` output for HTTP/3.
- BL-735 — multiplex `-Z` parallel transfers to one origin over one HTTP/3 connection.

## Alternatives considered

- **`System.Net.Quic`.** Needs `msquic`, missing on some platforms; cannot be driven
  through `IDatagramChannel` so cannot be tested off the network; would not produce
  curl's Initial, transport parameters or failure lines. Rejected.
- **Keep refusing `--http3`, as the Windows reference and Ubuntu's curl do.** Contrary to
  the standing rule that what any official curl build does, Curl does everywhere.
  Rejected.
- **Put the race in `Curl.Networking.UnitLibrary`, returning either kind of connection.**
  Networking would have to know HTTP versions and ALPN fallback rules, which are the HTTP
  handler's business; the handler already chooses the version per transfer. Rejected for
  the race in the handler over two connector methods.
- **Reproduce ngtcp2's shuffled CRYPTO frames and LibreSSL's shuffled extension order.**
  Both are per-connection randomness no server depends on, and they would make the first
  Initial impossible to pin. Rejected for a fixed, measured order.
- **Only NewReno, RFC 9002's own controller.** Simpler, but not what curl's build runs;
  on lossy or long paths the transfer times would differ. Rejected for CUBIC by default.
- **A new UDP seam instead of `IDatagramChannel`.** TFTP's channel already sends and
  receives whole datagrams to a named endpoint, which is all QUIC needs, plus the local
  endpoint added here. Rejected as a second name for one concept.
