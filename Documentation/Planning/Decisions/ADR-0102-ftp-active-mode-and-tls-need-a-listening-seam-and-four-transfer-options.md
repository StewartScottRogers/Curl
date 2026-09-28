# ADR-0102 — FTP active mode and TLS need a listening seam and four transfer options

- **Status:** Accepted
- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions"; task BL-437).

## Context

ADR-0093 scoped `FtpProtocolHandler` to passive mode over plaintext `ftp://`. BL-437 adds
active mode (`-P`/`--ftp-port`) and TLS (`ftps://` and `AUTH` on `ftp://`). Checked on
2026-09-27, the handler cannot do either with the contracts it has:

- `IConnector` only connects out. Active mode needs a port the server connects back to,
  and a handler never constructs a `Socket` (root `CLAUDE.md`).
- `-P` needs the control connection's own address for `-P -`; `IConnection` exposes only
  `RemoteEndPoint`.
- `-P`, `--disable-eprt`, `--ssl`, `--ssl-reqd`, `--ftp-ssl`, `--ftp-ssl-reqd` and
  `--ftp-ssl-control` are names in `CurlOptionAliasTable` only: no `CommandLineOptions`
  property, no `ITransferContext` property.
- `ITlsProvider.AuthenticateAsClientAsync` already upgrades a plaintext `IConnection`,
  which is all that `AUTH` and a TLS data connection need.

## Decision

**Scope.** The handler gains:

- Active mode for `-P` values `-` (the control connection's local address), an IPv4 or
  IPv6 literal, each with an optional `:port` or `:low-high` range. `EPRT` is sent first,
  then `PORT` when `EPRT` is refused on IPv4; `--disable-eprt` sends `PORT` only. After the
  `RETR`/`LIST`/`STOR` reply the handler accepts the server's connection. The waiting time,
  the exit code and message when nothing connects, and the exact command bytes are
  measured with curl 8.21.0 before they are pinned.
- `ftps://` (implicit TLS, default port 990): the control connection is made with
  `ConnectTarget.UseTls`, and every data connection is upgraded with `ITlsProvider`.
- `ftp://` with `--ssl` (try) or `--ssl-reqd` (required): the `AUTH` command(s) curl
  sends before `USER`, the upgrade of the control connection, then `PBSZ 0` and `PROT P`
  (`PROT C` under `--ftp-ssl-control`) after login, all as measured. A refused `AUTH` goes
  on in plaintext under `--ssl` and fails under `--ssl-reqd` with curl's measured exit
  code (expected 64, `CURLE_USE_SSL_FAILED`).
- Not in scope, left to their own tasks: an interface name or host name as the `-P`
  address, `--ftp-ssl-ccc`, `--ftp-pret`, `--ftp-account`, `--ftp-alternative-to-user`.

**Contract additions**, each owned by a prerequisite or follow-up task:

1. `Curl.Protocol.Abstractions` (prerequisite of BL-437):
   - `IConnectionListener.ListenAsync(ListenTarget, CancellationToken)` returning a
     `ListenResult`: either a listening port (`IPendingConnection`, with its bound
     `EndPoint LocalEndPoint` and `AcceptAsync(CancellationToken)` returning a
     `ConnectResult`, disposed to stop listening) or a failure with curl's exit code and
     message. `ListenTarget` holds the address to bind and the port range (0 for any).
   - `IConnection.LocalEndPoint`, a default interface member returning `null`, so no
     existing implementation or test fake changes; the TCP connection overrides it.
   - `ITransferContext` / `TransferContext`: `FtpPort` (`string?`, the `-P` value, `null`
     for passive), `FtpUseEprt` (`bool`, `true` unless `--disable-eprt`), `SslLevel` (new
     enum `TransportSecurityLevel`: `None`, `Try`, `Required`, for `--ssl`/`--ftp-ssl`
     and `--ssl-reqd`/`--ftp-ssl-reqd`) and `FtpSslControlOnly` (`--ftp-ssl-control`).
2. `Curl.Networking`: `TcpConnectionListener`, the production `IConnectionListener`, and
   `LocalEndPoint` on its connections.
3. `Curl.Cli`: the seven options parsed into `CommandLineOptions`.
4. `Curl.Console` (after BL-437): `TransferContextFactory` maps the options,
   `CurlComposition` gives `FtpProtocolHandler` the listener and TLS provider, and
   `ftps` is routed and listed as curl lists it.

`FtpProtocolHandler` keeps its one-argument constructor for passive plaintext use and
gains one taking `IConnector`, `IConnectionListener` and `ITlsProvider`, so BL-437 depends
only on the abstractions task; the networking, CLI and console tasks can run in parallel
with it.

`Record-CurlExchange.ps1 -Ftp` is extended by BL-437 itself (it connects back to the
`EPRT`/`PORT` address, answers `AUTH` and wraps its streams in `SslStream` with a test
certificate) to measure the cases before they are pinned.

## Consequences

- Active mode and TLS stay testable without a network: tests pass fake listeners and a
  fake `ITlsProvider`.
- `IConnection` grows a member without breaking any implementer.
- `ftps://` is not usable from the command line until the console task lands, although the
  handler supports it once BL-437 is done.

## Alternatives considered

- **A listen method on `IConnector`.** Every connector and fake would have to implement it,
  though only FTP listens.
- **Bind the listener to the control connection by passing the `IConnection`.** The
  listener would have to know the connection's concrete type to find its address.
- **One task doing everything.** It would touch five projects, three of them in use by
  other lanes, and serialise the whole shift behind it.

## BL-437 addendum — what curl 8.21.0 was measured to do, and what the handler does

- **Date:** 2026-09-27
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

Measured against curl 8.21.0 (the Schannel build) with `Record-CurlExchange.ps1 -Ftp`,
which BL-437 extended to dial back to the `EPRT`/`PORT` address, answer `AUTH` and serve
TLS on the control and data connections (`-Tls` with `-Ftp` for `ftps://`), and hold the
control connection open for `-FtpIdleMilliseconds`. `FtpProtocolHandler` now does the same:

**Active mode.**

- `EPRT |1|127.0.0.1|56703|` (`|2|::1|…|` for IPv6) goes where `EPSV` would, after the
  `CWD`s and before `TYPE`. A refused `EPRT` (any reply but 2xx) is followed by a fresh
  bind and `PORT 127,0,0,1,221,142`: curl binds a new port for `PORT`, and so does the
  handler. `--disable-eprt` sends `PORT` only. `PORT` refused is exit 30,
  `Failed to do PORT`, after `QUIT`.
- A bind that fails (the one port given already in use) is exit 30,
  `bind() failed, ran out of ports`, after `QUIT` and before `EPRT`; the handler reports the
  listener's code and message after `QUIT`.
- The server's connection is accepted after the transfer command is answered `125`/`150`.
  If none arrives, curl waits 60 seconds whatever `--connect-timeout` says, then exits 12,
  `Accept timeout occurred while waiting server connect`, after `QUIT`; the handler waits
  60 seconds on `ITransferContext.TimeProvider`. A failed accept is its exit code after
  `QUIT`. curl also watches the control connection during the wait; the handler only waits
  for the accept.
- `-P` values: `-` or nothing before the colon is the control connection's own address (an
  IPv4-mapped one announced as IPv4); `[v6]` and a bare IPv6 literal; `:port` and
  `:low-high` read as `atoi` reads them, and a range whose low end is above its high end
  (measured: `40000-39000`) or above 65535 means any port.
- **Divergences, decided here.** (Superseded for names by ADR-0108: the handler now resolves
  a `-P` name through an injected `IDnsResolver`.) A host or interface name is not resolved (no DNS seam
  reaches the handler): it ends with exit 6, `Could not resolve host: <name>`, and no
  `QUIT`, which is what curl was measured to do for a name that does not resolve
  (`nosuch.invalid`). curl 8.21.0 reads a bare `::1` oddly (it announced the control
  address); the handler takes it as the IPv6 literal. On IPv6, a refused `EPRT`, or
  `--disable-eprt`, leaves curl sending nothing and waiting until the server hangs up
  (exit 56); the handler sends `QUIT` and ends with exit 30, `Failed to do PORT`. With
  `-P -` and a control connection that reports no local address the handler ends with exit
  30 after `QUIT`.

**TLS.**

- `ftps://` (port 990 unless given): the control connection is TLS from the start, no
  `AUTH`; after `PASS`, `PBSZ 0` and `PROT P`, then `PWD`.
- `ftp://` under `--ssl`, `--ftp-ssl-control` or `--ssl-reqd`: `AUTH SSL` right after the
  `220` greeting, then `AUTH TLS` when that is refused; `234` or `334` starts the handshake.
  Both refused: exit 64, `Requested SSL level failed`, with no `QUIT`, under
  `--ssl-reqd` and `--ftp-ssl-control`; plaintext and no `PBSZ` under `--ssl`. A `230`
  greeting skips `AUTH`.
- After login over TLS: `PBSZ 0`, its reply not checked, then `PROT P`, or `PROT C` under
  `--ftp-ssl-control`. Ranking, measured: `--ssl-reqd` over `--ftp-ssl-control` over
  `--ssl`. A refused `PROT` is exit 64 with no `QUIT` under `--ssl-reqd` only; otherwise,
  `ftps://` included, the data goes in plaintext.
- After an accepted `PROT P` every data connection, passive or active, is secured with
  `ITlsProvider` once the transfer command is answered. A failed data handshake is its exit
  code with no `QUIT` (not measured: it is the same ending as a failed passive connect); a
  failed control handshake is its exit code, measured as exit 60 for an untrusted
  certificate.

**Construction.** `FtpProtocolHandler(IConnector)` stays and serves `ftp` only: `-P` there
ends with exit 30, `Failed to do PORT`, and an accepted `AUTH` with exit 64,
`Requested SSL level failed`. `FtpProtocolHandler(IConnector, IConnectionListener,
ITlsProvider)` serves `ftp` and `ftps`; BL-458 wires it in `Curl.Console`. `-P -` reads
`IConnection.LocalEndPoint` from the connection the connector returned, so under `ftps://`
the TLS connection must report it too.
