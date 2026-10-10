# ADR-0462: The upstream harness serves FTPS over implicit TLS with plain data connections and emulates no AUTH TLS

- Status: Accepted
- Date: 2026-10-10
- Task: BL-1913
- Decided by Claude under Stewart's delegation.

## Context

BL-1913 asked for upstream's FTPS test server (`%FTPSPORT`) to be emulated in
`Curl.Conformance.UnitLibrary`, explicit and implicit, so the 8 cases naming the port are
measured. Upstream starts that server as `tests/ftpserver.pl` behind stunnel: stunnel wraps the
control port in TLS from the first byte, and nothing else. ftpserver.pl answers `PBSZ` and `PROT`
with `500 ... not implemented` and has no `AUTH` handler, so the data connections stay plain and
no plain control connection is ever upgraded.

All 8 vendored cases (400, 401, 403, 404, 406, 407, 408, 1112) use `ftps://` with
`--ftp-ssl-control` and verify `PROT C`: implicit FTPS with clear data. No case at `curl-8_21_0`
reaches a server that answers `AUTH TLS`.

## Decision

1. `FtpsServerConnector` stands in for stunnel in front of the FTP stand-in: `%FTPSPORT` 9007
   reaches `FtpServerConnector.FtpPort` through `TlsRelayConnection` (as `MailTlsServerConnector`
   does for mail, ADR-0459), with `test-localhost.pem`, no ALPN and no client certificate
   request. Every other connection, the passive and active data connections included, passes
   on as it is, so data stays plain.
2. `%FTPSPORT` has a value only when the caller names a certificate directory, as `%HTTPSPORT`
   does; the runner wraps the connector chain with it only for a case naming `ftps`.
3. Screening lets `ftps` cases run, reads their `<servercmd>` with ftpserver.pl's command set and
   records their `<verify><upload>`.
4. Explicit FTPS (`AUTH TLS` and a later upgrade of the control or data connections) is not
   emulated, since ftpserver.pl offers none: a case using `--ssl` on a plain `ftp` server measures
   curl against a server that refuses `AUTH`, as upstream's does.

## Consequences

- 400, 401, 403, 406 and 408 are measured; they differ on a Curl difference (Curl sends
  `PROT P` where curl at `--ftp-ssl-control` sends `PROT C`), which the next gap run files.
- 404 (exit code `77,60`), 407 (`<client><stdout>`) and 1112 (`SLOWDOWNDATA`) skip for another
  reason, never for `%FTPSPORT`.
