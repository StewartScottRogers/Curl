# ADR-0357 — `-vv` and `--trace-config setup` write the setup filter's lines; `-vv` to `-vvvv` turn on trace components

- **Status:** Accepted
- **Date:** 2026-10-02
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-1103, following ADR-0318 and ADR-0356: curl 8.21.0 (Schannel) writes per-component trace lines for
its connection filters and transfer engine. Measured on 2026-10-02 with `Record-CurlExchange.ps1`
(BL-1103 Notes):

- `-vv` and `--trace-config setup -v` write four `[SETUP]` lines around a direct connect: `added`
  before anything else, `happy eyeballing to origin <host>:<port>` after the resolve lines and before
  the first `Trying`, and `removing connected setup filter` and `destroy` after `Established
  connection` (after the `[DNS]` filter's own removal when both are on). A connect that fails writes
  only the first two.
- `-vvv` adds `[READ]` and `[WRITE]`; `-vvvv` writes every component, as `--trace-config all`.
- Order matters: `--trace-config -setup -vv` keeps `[SETUP]`, `-vv --trace-config -setup` drops it.
  `-vv -v` drops it (a first `-v` resets), while `--trace-config dns -v -v` keeps `[DNS]`.
  `--no-verbose` drops every component, even ones `--trace-config` named; so does `-all`.
- `network` writes `[DNS]`, `[HAPPY-EYEBALLS]`, `[TCP]`, `[MULTI]` and `[TIMER]`, but no `[SETUP]`.
- `[HAPPY-EYEBALLS]`, `[TCP]`, `[MULTI]` and `[TIMER]` lines carry socket descriptors, nanosecond
  progress stamps and poll-loop repetitions whose counts differ from run to run.

## Decision

1. `Curl.Cli` adds the components each verbosity brings to `CommandLineOptions.TraceComponents`
   (`setup` at 2, `read` and `write` at 3, `all` at 4) and remembers which ones it added, so a first
   `-v` takes those out again unless a later `--trace-config` named them; `--no-verbose` and
   `--trace-config -all` empty the set. The console layer then needs only the set.
2. `TcpConnector.TracesSetupFilter` (`CurlComposition.TracesSetup`: `setup` or `all`) writes
   `[SETUP] added` and wraps a direct connect's events in `SetupFilterTraceEvents`, layered over
   `DnsFilterTraceEvents` so the two filters' lines interleave in curl's order.
3. `network` also turns on the `[DNS]` lines (`CurlComposition.TracesDns`).
4. This run delivers `[SETUP]` only. The other components' lines are split into follow-up tasks, each
   to record how its volatile values (descriptors, nanoseconds, poll repetitions) are produced:
   `[HAPPY-EYEBALLS]` and `[TCP]`; `[MULTI]`, `[TIMER]`, `[READ]` and `[WRITE]`; and the proxy filters'
   and `[HTTPS-CONNECT]`'s lines.

## Alternatives considered

- Deriving `-vv`'s components from `Verbosity` in the console layer: loses the measured ordering
  (`--trace-config -setup -vv` against `-vv --trace-config -setup`), which only the parser sees.
- Folding the `[SETUP]` lines into `DnsFilterTraceEvents`: one class for two components, and its name
  would no longer say what it does.

## Consequences

`curl -vv` now prints curl's four `[SETUP]` lines for every direct TCP connect, and `--trace-config`
names that the console does not yet write lines for are already in the set, ready for their tasks.

## Amendment, 2026-10-02 (BL-1159): the `[READ]` lines

Decided by Claude under Stewart's delegation.

BL-1159 split again (its Context allows it): this run delivers the `[READ]` lines of a plain
transfer; `[TIMER]`, `[WRITE]` and `[MULTI]` each go to a follow-up task of their own, because
`[TIMER]` is written inside `Curl.Networking`'s connector and `[WRITE]` and `[MULTI]` from the HTTP
handler's client writer stack and a transfer engine Curl does not have as such.

- Measured (BL-1159 Notes): `[READ] client_reset, clear readers` is written once as the transfer
  starts - after the `--resolve` entries' `Added ... to DNS cache` lines, before anything resolved or
  dialled, `[<xfer>-x]` under `--trace-ids` - and once more as a finished transfer resets, before its
  connection's `left intact` or `shutting down connection #N` line; a failed transfer (a refused
  connect, `-f` on a 404) writes no second line.
- `Curl.Console` writes the first line itself (`CurlCommandRunner.TraceClientReaderReset`) and wraps
  the transfer's events in `ClientReaderResetTraceEvents` for the second, under
  `CurlComposition.TracesRead` (`read` or `all`, which `-vvv` and `-vvvv` put there; `network` does not).
- The `[READ]` lines carry no volatile value. The upload readers' lines (`add buf reader`,
  `cr_buf_read(len=65388)`, `client_read(...)`) of a `-d` body, and the lines of each followed
  redirect's hop, were not delivered here and are a follow-up task.

## Amendment, 2026-10-02 (BL-1160): the `[HAPROXY]` lines

Decided by Claude under Stewart's delegation.

BL-1160 split again: this run delivers the `[HAPROXY]` lines and pins `[SETUP]` through a plain
HTTP proxy; `[HTTP-PROXY]` and `[H1-PROXY]` (a CONNECT tunnel), `[SOCKS]` and `[HTTPS-CONNECT]` each
go to a follow-up task (BL-1193, BL-1191, BL-1192), each a filter of its own with lines to measure.

- Measured (BL-1160 Notes): a plain `-x http://` proxy adds no proxy filter at all; its `[SETUP]`
  lines are a direct connect's, naming the proxy as the origin, which Curl already wrote, since a
  forward-proxy target is a direct connect.
- With `--haproxy-protocol` the setup filter writes `[SETUP] added HAPROXY filter` once the socket
  connected, before `Established connection`, and the `[HAPROXY]` component writes `removing
  connected setup filter` and `destroy` after the `[SETUP]` removal. `haproxy`, `proxy` and `all`
  turn `[HAPROXY]` on (`CurlComposition.TracesHaproxy`, `TcpConnector.TracesHaproxyFilter`);
  `network` does not. A refused connect writes no `[HAPROXY]` line.
- `TcpConnector` writes the added line beside the PROXY line it sends and the removal lines after it
  reports the connection opened, over TLS too, where their place is unmeasured (BL-1192 measures it).
- The `[SETUP]` and `[HAPROXY]` lines carry no volatile value: no descriptor, stamp or repetition.

## Amendment, 2026-10-02 (BL-1161): the `[HAPPY-EYEBALLS]` and `[TCP]` lines

Decided by Claude under Stewart's delegation.

BL-1161 delivers the `[HAPPY-EYEBALLS]` lines and the `[TCP]` lines of the connect attempts; the
`[TCP]` lines of the connection's I/O (`query ALPN`, `send(...)`, `recv(...)`) go to a follow-up task
(BL-1195), since they come from the transfer, not the connect.

- Measured (BL-1161 Notes): `happy-eyeballs`, `network` and `all` (so `-vvvv`) turn the
  `[HAPPY-EYEBALLS]` lines on; `tcp`, `network` and `all` the `[TCP]` lines
  (`CurlComposition.TracesHappyEyeballs`/`TracesTcp`, `TcpConnector.TracesHappyEyeballsFilter`/`TracesTcpFilter`).
- `ConnectAttemptTraceEvents` sits below the `[DNS]` and `[SETUP]` filters' events, so the lines it
  writes as a `Trying` line passes through land where curl's do: the ballers' `want to do more` and
  `check for next ... address` lines before it, the socket's opening and `checked connect attempts`
  after it and before `[DNS] Curl_conn_connect(block=0) -> 0, done=0`. `AddressFamilyRace` tells it
  the rest: the second family's timeout, each failure, the winner, and giving up. `TcpConnector`
  writes `[HAPPY-EYEBALLS] removing connected setup filter` and `destroy` after `[SETUP]`'s and
  `[HAPROXY]`'s removal, as measured.
- Volatile values: curl's `fd=` is its operating system's socket descriptor; Curl numbers each
  connect's sockets from 3 (`ConnectAttemptTraceEvents.FirstSocketDescriptor`), as the dialler does
  not expose the socket. The `local address` line, written as the socket opens, names its family's
  unspecified address and port `0`, as .NET binds the local end only as it connects (as ADR-0100's
  failed-connect line does); curl names the bound end. curl repeats `not connected yet`,
  `checked connect attempts` and `adjust_pollset` once per poll of the system, a number that varies
  run to run; Curl writes one poll round per wake-up of the race (the timeout, a failure, the
  winner). No nanosecond stamp appears in these lines without `--trace-time`.
- Unmeasured and so not written: a `--no-keepalive` connect (Curl always writes `Set TCP_KEEP*`),
  the lines through a proxy or a Unix socket (only a direct connect is traced, as for `[DNS]`), and
  `baller N` for more than one family giving up (Curl writes `baller 0: result=7`).

## Amendment (BL-1187): the `[WRITE]` client writer lines

Decided by Claude under Stewart's delegation, 2026-10-02.

BL-1187 delivers the `[WRITE]` lines of a transfer's response in `Curl.Console`, as
`ClientWriterTraceEvents`, a wrapper over the transfer's events that `CurlCommandRunner.WithTraceLineEvents`
puts outermost under `CurlComposition.TracesWrite` (`write` or `all`, so `-vvv` and `-vvvv`; not
`network`). No change to `Curl.Protocol.Http` was needed: its handler already reports each response
header line and each body block as an event, which is where curl's client writers take them.

- Measured (BL-1187 Notes): after each `< ` header line, four lines of type `c` for the status line
  and, for every later line, `header_collect pushed(type=1, len=N)` and four of type `4`; after each
  body block `[OUT] wrote N body bytes -> N` and three of type `1`, then `xfer_write_resp`; before the
  connection's `left intact` (or `shutting down`) line `[WRITE] [OUT] done`, which comes before
  `[READ] client_reset`, so the wrapper sits outside `ClientReaderResetTraceEvents`.
- `xfer_write_resp(len=N)`: curl's `N` is the bytes of the one socket read it handed on (40 for a
  17 + 19 + 2 byte head and a 2 byte body, 38 for the same head with no body). Curl's handler reports
  lines and blocks rather than reads, so `N` is every header byte reported since the last
  `xfer_write_resp` plus the body block's bytes: curl's number whenever the head and the first body
  block arrive in one read, as they do for a small response. A response with no body writes its
  `xfer_write_resp` for the head before `[OUT] done`. `eos` is always `0`, as measured.
- Not written: `[OUT] done` before a failed transfer's `closing connection #N` (unmeasured).

## Amendment, 2026-10-02 (BL-1191): the `[SOCKS]` lines

Decided by Claude under Stewart's delegation.

- Measured (BL-1191 Notes) through a scripted SOCKS server for SOCKS4, SOCKS4a, SOCKS5, SOCKS5h and
  `--preproxy`: `SOCKS5: connecting to H:P` (SOCKS4: `SOCKS4 connecting to H:P`, no colon), `adjust
  pollset in (7)` after the greeting, `SOCKS5 connect to H:P (remotely resolved)` for SOCKS5h (an
  address literal too) or `A:P (locally resolved)` for SOCKS5, an IPv6 address in brackets
  (SOCKS4: `SOCKS4 connect to IPv4 A (locally resolved)`, none for SOCKS4a), `adjust pollset in (15)`
  (SOCKS4: `(4)`) after the request, and `SOCKS5 request granted.` before `Opened SOCKS connection`.
  The numbers in brackets are curl's handshake states, the same on every run, so they are pinned.
  A refused request ends after the pollset line with the existing exit 97 message. A pre-proxy names
  the HTTP proxy as the destination.
- `socks`, `proxy` and `--trace-config all` turn them on (`CurlComposition.TracesSocks`,
  `TcpConnector.TracesSocksFilter`); `network` does not, and neither does `-vvvv`, although it puts
  `all` among the components: `CommandLineOptions.VerbosityTraceComponents` tells the two apart.
- The setup filter writes `[SETUP] added SOCKS filter to H:P` before the handshake.
- Not written: `[SOCKS] query ALPN`, which curl writes after `Established connection` when the HTTP
  layer asks the filter chain for ALPN, as `[TCP] query ALPN` (BL-1195) is; it follows in its own task.
  The lines of a SOCKS5 user name and password or GSS-API negotiation are unmeasured.

## Amendment, 2026-10-02 (BL-1195): the `[TCP]` I/O lines

Decided by Claude under Stewart's delegation.

curl 8.21.0 (mingw, Schannel) under `--trace-config tcp`, `network`, `all` and `-vvvv` writes, for a
plain `http://` transfer, `[TCP] query ALPN` after the setup filters' removal and before
`using HTTP/1.x`, `[TCP] send(len=<n>) -> 0, <n>` before the request's `>` lines (one send for the
head and a small `-d` body together, `len=160`), and `[TCP] recv(len=102400) -> <result>, <bytes>`
before the response's `<` lines (BL-1195 Notes).

- `TcpConnector` wraps a plain HTTP connection (`ConnectTarget.PoolScheme` `http`) dialled under
  `TracesTcpFilter` in `TcpIoTraceConnection` and writes `TcpConnector.QueryAlpnLine` after the setup
  filters' removal. The wrapper writes the `send` line after each write and the `recv` line after
  each read; the HTTP handler reports its head after writing it, so the order matches curl's.
- **Would-block results.** curl writes `recv(len=102400) -> 81, 0` (`CURLE_AGAIN`) when it reads
  before the server has answered, which depends on timing: measured once for a plain GET, not for a
  150000-byte body nor a `-d` upload. The wrapper writes it when the inner read does not complete at
  once (its `ValueTask` is not completed when returned), the same condition in .NET terms, so it
  follows the same timing rather than being pinned on or off. A live run of Curl against the
  recorder writes it exactly as curl did.
- **Length.** `len=` is always 102400, curl's receive buffer, whatever the reader asked for; curl
  asks for less only for a body's known remainder (`recv(len=47643)` for the tail of a 150000-byte
  body), which Curl's 16384-byte reads do not mirror, so a body over 16384 bytes writes more `recv`
  lines than curl. Not pinned.
- Not written: the lines over TLS (`https://`, where the TCP filter sits below the TLS filter and
  `query ALPN` comes from it), on other protocols (FTP's reads are `len=900`), through a proxy, and the
  `[HAPROXY]` line's `send(len=44)`. The wrapper keeps the events of the transfer that dialled it, so
  a reused connection writes its lines through those events.

## BL-1192 amendment: the [HTTPS-CONNECT] lines of a direct https:// connect

Decided by Claude under Stewart's delegation, 2026-10-02. Measured with curl 8.21.0 (mingw,
Schannel) and `Record-CurlExchange.ps1 -Tls` (BL-1192 Notes).

- `--trace-config https-connect` and `all` (so `-vvvv`) write them; `network` and `proxy` do not.
  `TcpConnector.TracesHttpsConnectFilter` turns them on for a direct connect to an `https://` origin
  (`PoolScheme` `https`, TLS, not a forward proxy), through `HttpsConnectFilterTraceEvents`, which sits
  between the `[SETUP]` and `[DNS]` events: `added` before `[DNS] created`; `connect, init` and
  `1st attempt uses <v> from wanted versions` before `[SETUP] happy eyeballing`; `connect -> 0, done=1`
  before `Established connection`; its removal after `[DNS]`'s and before `[SETUP]`'s. `<v>` is `h1`
  under `--http1.0`/`--http1.1`, `h3` under `--http3`/`--http3-only` (unmeasured: this curl build has
  neither HTTP/2 nor HTTP/3), else `h2`, even where the Schannel build's ALPN offers only `http/1.1`.
- Volatile: curl writes one `connect -> 0, done=0` / `adjust_pollset -> 0, 1 socks` pair per poll
  round. Curl writes one after the `Trying` line, two before a finished handshake's lines and one
  before a failed one's - the loopback TLS 1.2 counts. curl writes the handshake's pairs between
  `ALPN: curl offers` and `ALPN: server ...`; one `TlsHandshakeEvent` carries both lines, so Curl
  writes the pairs before them. Under `dns` too, `[DNS] Curl_conn_connect(...) done=0` comes before
  the first pair, where curl puts it between the pair's two lines.
- A failed dial or handshake writes `connect, all attempts failed` and `connect -> <exit>, done=0`
  (measured 7 and 60) after the failure's own lines; a name that does not resolve writes only `added`.
- An `https://` origin's setup filter is added by the ALPN connect filter, so `[SETUP] added` is not
  written for it, and `[SETUP] added SSL filter for origin` is, before the handshake's lines (after
  any `[SETUP] added HAPROXY filter`, by curl's setup-filter order; unmeasured over HAPROXY).
- Not written: through a tunnelling proxy, over a Unix socket, and over QUIC (BL-1254).

## BL-1193 amendment: the [HTTP-PROXY] and [H1-PROXY] lines of a CONNECT tunnel

Decided by Claude under Stewart's delegation, 2026-10-02. Measured with curl 8.21.0 (mingw,
Schannel) and `Record-CurlExchange.ps1 -Script` playing a plain HTTP proxy (BL-1193 Notes).

- `--trace-config http-proxy` writes `[HTTP-PROXY]`, `h1-proxy` writes `[H1-PROXY]`, and `proxy` and
  a named `all` write both; `network` does not, nor does the `all` that `-vvvv` puts among the
  components (as for `[SOCKS]`). `TcpConnector.TracesHttpProxyFilter` and `TracesH1ProxyFilter` turn
  them on for a tunnel through an `Http` or `Http10` proxy, written by `HttpProxyTunnelTrace`.
- Volatile: curl writes `[HTTP-PROXY] CONNECT`, `[H1-PROXY] connect` and `[H1-PROXY] CONNECT receive`
  once more for each poll round that finds the CONNECT reply not there yet. Curl writes one such
  round, as every loopback measurement had, between `CONNECT receive` and the reply's head.
- `[H1-PROXY] new tunnel state 'failed'` follows `CONNECT tunnel established` too, as curl clears a
  finished tunnel through that state; after a refusal (measured 403) it follows `CONNECT response`.
  The `[HTTP-PROXY]` filter's removal follows the `[SETUP]` filter's, and the HTTP handler's ALPN
  query of a plain tunnel is `[H1-PROXY] query ALPN`, never `[TCP] query ALPN`.
- The setup filter on this path: `[SETUP] added` (not for an `https://` origin) before the `[DNS]`
  lines, `happy eyeballing to proxy H:P` naming the proxy, `added HTTP proxy tunnel filter` before
  `CONNECT: no ALPN negotiated`, `added SSL filter for origin` after the tunnel for an `https://`
  origin, and its removal after `Established connection`. A refused dial writes no tunnel line.
- The CONNECT reply heads go to the header output the target's events were before any trace filter
  wrapped them, so tracing never loses them.
- Not written: through an HTTPS proxy (its TLS filter's lines are unmeasured, BL-1255), and the
  sequence of a second CONNECT after a `407` is unmeasured: each CONNECT writes the same lines.

## Amendment, 2026-10-02 (BL-1246): `[SOCKS] query ALPN`

Decided by Claude under Stewart's delegation. Measured with curl 8.21.0 (mingw, Schannel) and
`Record-CurlExchange.ps1 -Script` playing a SOCKS5h proxy (BL-1246 Notes).

- The ALPN query is answered by the connection's topmost filter, and only that filter writes the
  line. Through a SOCKS proxy (or a `--preproxy` in front of a forward HTTP proxy) to a plain
  `http://` target that is the SOCKS filter: `TcpConnector.SocksQueryAlpnLine`, `[SOCKS] query ALPN`,
  after `Established connection` and before `using HTTP/1.x`, under `TracesSocksFilter` (`socks`,
  `proxy`, a named `all`). It replaces `[TCP] query ALPN`: under `socks` and `tcp` together only the
  `[SOCKS]` line is written, and under `network`, `-vvvv` or plain `-v` no `query ALPN` line at all.
- Written once per new connection; a reused connection writes no second line (measured).
- Not written over TLS: for an `https://` target the TLS filter sits above the SOCKS filter and
  answers. The scripted proxy cannot finish a TLS handshake, so this follows from the filter order
  and from the `network` measurement (only the topmost filter writes the line), not from a recording.
  Through a CONNECT tunnel behind a pre-proxy the `[H1-PROXY]` filter answers, as before.

## Amendment, 2026-10-02 (BL-1253): `[TCP]` I/O through a proxy, for the PROXY line, and what is left

Decided by Claude under Stewart's delegation. Measured with curl 8.21.0 (mingw, Schannel) and
`Record-CurlExchange.ps1` under `-s -v --trace-config tcp` (BL-1253 Notes).

- `--haproxy-protocol`: the TCP filter sits below the HAPROXY filter, so the PROXY line's write is
  traced like any other send, `[TCP] send(len=44) -> 0, 44` for a TCP4 line, after the dial's
  `[TCP]` lines and before `Established connection`. `TcpConnector` writes it after writing the
  line whenever the dial traces the TCP filter, over TLS too, since the line goes out before the
  handshake.
- A forward HTTP proxy (`-x http://...` to an `http://` URL) writes exactly what a direct plain
  connection does (`query ALPN`, `send(len=131)` for the absolute-form request, `recv(len=102400)`);
  its target already carries `PoolScheme` `http`, so the BL-1195 wrapper covered it and only a test
  was added.
- Left to their own tasks, because neither can be written from `Curl.Networking` alone:
  - `https://`: curl's Schannel build traces the TLS records below the TLS filter, the handshake's
    reads as `recv(len=4096)` and the application data's as `recv(len=103424)`, with record sizes
    (`send(len=429)` for the ClientHello) that depend on the TLS stack's own messages and buffer
    sizes; no `query ALPN` line. Matching that needs the record layer's read sizes, not a wrapper.
  - FTP: the control connection reads as `recv(len=900)` and the data connection writes
    `[TCP-1]` lines whose `len=` is the bytes still expected. The FTP control and data targets
    look alike to `TcpConnector` (no `PoolScheme`), so the FTP handler has to say which is which
    and what length to report, a change to `Curl.Protocol.Ftp.UnitLibrary` and `ConnectTarget`.

## Amendment, 2026-10-02 (BL-1254): `[HTTPS-CONNECT]` through a proxy and over a Unix socket

Decided by Claude under Stewart's delegation. Measured with curl 8.21.0 (mingw, Schannel) and
`Record-CurlExchange.ps1` (`-Script` playing an HTTP or SOCKS5 proxy, `-UnixSocket`, `-Tls` playing
an HTTPS proxy) under `-s -k -v --trace-config https-connect,setup` (BL-1254 Notes).

- An `https://` origin reached through an HTTP, HTTPS or SOCKS proxy, or over `--unix-socket`, writes
  the same `[HTTPS-CONNECT]` lines as a direct connect: `added` first, `connect, init` and the
  `1st attempt` line before the setup filter's `happy eyeballing` line (or `Trying`), a poll-round
  pair after `Trying`, the handshake's pairs, and `done=1` and the removal, or `all attempts failed`
  and the exit code. `TcpConnector` builds the filter for each route through one helper
  (`SetupAndDnsFilterEvents`), each route giving its own DNS and setup filters.
- Through a proxy the filter also polls while the proxy works: one pair after the CONNECT request
  head (before the reply's lines), and two before `Opened SOCKS connection`. As in the BL-1192
  amendment the counts follow curl's poll timing; they are fixed to the loopback measurement. A
  SOCKS handshake that fails writes none of its pairs (curl wrote four for a SOCKS5 whose local
  resolve failed); the failure lines still end it.
- The setup filter through a SOCKS proxy (not only its `added SOCKS filter` line) is now traced, for
  every origin: `[SETUP] added` (none for an `https://` origin), `happy eyeballing to origin
  <proxy>:<port>` (the SOCKS proxy is its origin, unlike an HTTP proxy's `to proxy`), and for an
  `https://` origin `added SSL filter for origin` once the tunnel is open. Over a Unix socket it
  eyeballs to `origin <path>:0`, the whole path though `Trying` cuts it to 45 characters, and adds the
  SSL filter for an `https://` origin before the handshake.
- Left to their own tasks: an HTTPS proxy's own `[SETUP]` lines (`added SSL filter for HTTP proxy`,
  `added HTTP proxy tunnel filter`, measured; BL-1283), so through it only the `[HTTPS-CONNECT]`
  lines are written; and `--http3`, which the reference build cannot do, so it could not be measured
  (BL-1284).

## Amendment, 2026-10-02 (BL-1255): the tunnel lines through an HTTPS proxy

Decided by Claude under Stewart's delegation. Measured with curl 8.21.0 (mingw, Schannel) and
`Record-CurlExchange.ps1 -Tls -Script` playing an HTTPS proxy (TLS, then the CONNECT and the
tunnel's bytes), `--trace-config proxy,setup`, `http-proxy`, `ssl` and `all` (BL-1255 Notes).

- Through an `Https` proxy the setup filter writes what it writes through a plain one (`[SETUP]
  added`, `happy eyeballing to proxy H:P`, its removal; a refused dial writes nothing more), and
  after the dial `added SSL filter for HTTP proxy` (`TcpConnector.HttpsProxySslFilterAddedLine`) and
  `added HTTP proxy tunnel filter`, before the proxy's handshake. The tunnel's `[HTTP-PROXY]` and
  `[H1-PROXY]` lines are the plain tunnel's, after `CONNECT: ... negotiated`.
- Volatile: `[HTTP-PROXY] CONNECT` is written once before the proxy's handshake and once more for
  each poll round the handshake waits; the loopback measurement had two. Curl writes both once the
  handshake is done, before `CONNECT: ... negotiated`, so they follow the handshake's `-v` lines
  rather than sitting among them. A failed proxy handshake writes only the first.
- Not written: curl's `[SSL-PROXY]` lines (`cf_connect()`, `adjust_pollset, POLLIN fd=N`, `query
  ALPN`), which `--trace-config proxy` and `ssl` both turn on. No `[SSL]` filter line is written
  anywhere yet; they wait for that filter's own task.

## Amendment, 2026-10-03 (BL-1259): `[TCP]` and `[TCP-1]` I/O of an FTP transfer

Decided by Claude under Stewart's delegation. Measured with curl 8.21.0 (mingw, Schannel) and
`Record-CurlExchange.ps1` under `-s -v --trace-config tcp` for a download, an upload (`-T`) and a
listing (BL-1259 Notes).

- `ConnectTarget.TcpIoTrace` (a `TcpIoTraceLines`: filter name, receive length, whether a read that
  cannot complete at once is first written as `recv(len=<n>) -> 81, 0`) lets a handler say how its
  connection's I/O is traced. `TcpConnector` wraps the connection in `TcpIoTraceConnection` with
  those lines when the dial traces the TCP filter; a target without them keeps the BL-1195 rule
  (`PoolScheme` `http` gets the HTTP lines and the `query ALPN` line, anything else none).
- The control connection, when it stays plain: `[TCP] send(len=<n>) -> 0, <n>` before each `>`
  command and `[TCP] recv(len=900) -> 0, <n>` before each `<` reply. No would-block line: curl's
  `recv(len=900) -> 81, 0` before the final `226` is a race with the server (absent after a
  listing), so it is not written. `QUIT` is sent as curl closes the connection and writes no
  `[TCP]` line; the handler stops the control connection's info lines before it
  (`InfoLineStoppingTransferEvents`).
- The data connection, curl's second filter chain: `[TCP-1] recv(len=<bytes still expected>)`,
  preceded by the would-block line for a read the server has not answered yet. With a known size
  curl reads no end of file and stops at `SIZE`, so the session sizes its reads to the bytes still
  expected and stops there; a listing reads `recv(len=102400)` to `-> 0, 0`; an upload writes
  `[TCP-1] send(len=<n>) -> 0, <n>`.
- `ftps://` and `--ssl`/`--ssl-reqd` transfers write none of these lines, as the reads below TLS
  are records, not FTP's reads; they wait for the TLS record layer's task, like `https://`.

## Amendment, 2026-10-03 (BL-1260): `[TCP]` I/O of an `https://` connection's TLS records

Decided by Claude under Stewart's delegation. Measured with curl 8.21.0 (mingw, Schannel) under
`-s -v -k --trace-config tcp https://127.0.0.1:P/` (BL-1253 Notes): after `ALPN: curl offers
http/1.1`, `send(len=429)`, `recv(len=4096) -> 81, 0`, `recv(len=4096) -> 0, 1175`, `send(len=158)`,
`recv(len=4096) -> 0, 51`; then `send(len=108)` before the `>` lines and `recv(len=103424) -> 81, 0`,
`recv(len=103424) -> 0, 72` before the `<` lines; no `[TCP] query ALPN`.

- `TcpConnector.TlsRecordTraceFor` wraps a direct connection to an `https` origin (`PoolScheme`
  `https`, no `Proxy`, not a forward proxy) dialled under `TracesTcpFilter` in `TcpIoTraceConnection`
  before the TLS handshake, so the provider's record reads and writes are written. Its lines start as
  `HttpsHandshakeLines` (`recv(len=4096)`) and change to `HttpsApplicationDataLines`
  (`recv(len=103424)`) once the handshake succeeds; a read that cannot complete at once is first
  written as `-> 81, 0`, as for plain HTTP. No `[TCP] query ALPN` is written: TLS answers that query.
- What matches curl exactly: the order of the lines beside the TLS info lines and the `>`/`<` lines,
  the `send` lines' form, the receive lengths, and the would-block lines when the server has not
  answered yet. The receive lengths 4096 and 103424 are Schannel's buffers, pinned as constants
  whatever buffer `SslStream` or the hand-built TLS client passes, since they say how curl reads, not
  how many bytes arrive.
- What cannot match byte for byte: the record sizes (`send(len=429)`, `1175`, `158`, `108`, ...) are
  whatever the TLS stack in use writes and the server sends - `SslStream` (Schannel or OpenSSL
  underneath) or `Curl.Tls` - so a different ClientHello, key share or session ticket gives other
  numbers, and a stack that reads a record in two reads writes two `recv` lines. The handshake's
  `adjust_pollset, !active, POLLIN fd=N` lines are poll rounds of curl's event loop with a socket
  number Curl does not have; they are not written.
- The OpenSSL build's lines on Linux and macOS were not reachable from the lane; Curl writes the
  Schannel build's receive lengths on every platform until they are measured.
- Through a proxy or a tunnel the TCP filter sits below the proxy's filter and the target's records
  ride inside it; those connections keep writing no record lines.
