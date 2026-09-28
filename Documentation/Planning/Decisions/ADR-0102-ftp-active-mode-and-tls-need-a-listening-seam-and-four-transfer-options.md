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
