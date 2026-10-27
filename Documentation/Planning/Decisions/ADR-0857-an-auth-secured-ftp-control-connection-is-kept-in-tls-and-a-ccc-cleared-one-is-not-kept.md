# ADR-0857: An AUTH-secured FTP control connection is kept in TLS for the next URL; a CCC-cleared one is not kept

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3694 (CGR-165)
- Decided by Claude under Stewart's delegation.
- Amends: BL-1981's kept FTP control connection (`FtpKeptConnection`)

## Context

`FtpSession.TryKeepControlConnection` kept the control connection for the next URL only when the
session still talked on the pooled connection it was given. Explicit FTPS (`--ssl`, `--ssl-reqd`)
talks after `AUTH` on the TLS connection the TLS provider wraps around the pooled one, so it was
never kept: Curl sent `QUIT` after the first URL and logged in again for the second. Under
`--ftp-ssl-ccc` the TLS connection's `ClearTlsAsync` hands back the pooled connection it wraps, so
the CCC-cleared plaintext connection *was* kept, and the next `--ssl-reqd` URL ran on it with no
`AUTH`, `PBSZ` or `PROT`.

Measured for BL-3694 with `Record-CurlExchange.ps1 -Ftp`:

- curl 8.22.0 (Schannel) `-sS -k --ssl-reqd ftp://localhost:P/a.txt ftp://localhost:P/b.txt`: one
  control connection with `AUTH SSL`, one login, `RETR a.txt`, `EPSV`, `SIZE b.txt`, `RETR b.txt`,
  `QUIT`; exit 0.
- curl 8.18.0 (OpenSSL, WSL) with `--ftp-ssl-ccc` added: after `CCC` and `RETR file.txt` it sends
  nothing more on that connection and opens a new one for `file2.txt`.
- curl 8.22.0 (Schannel) with `--ftp-ssl-ccc`: exit 81 at `CCC`, as before.

## Decision

- A session that secured its control connection with `AUTH` keeps it: `FtpKeptConnection.SecuredControl`
  holds the TLS connection, which the reusing session switches to (`ResumeKeptConnection`) and the
  closing `QUIT` is sent on. The session no longer disposes that TLS connection once it is kept.
- A session whose control connection `CCC` cleared never keeps it: it sends `QUIT` at once, and the
  next URL connects anew with the full `AUTH`, login, `PBSZ`, `PROT` and `CCC`. curl leaves the
  unmatched connection in its cache and sends its `QUIT` at exit instead; the bytes on each
  connection are the same, only the order across the two connections differs, and holding a
  connection no transfer can reuse would only delay its `QUIT`.

## Consequences

Explicit FTPS runs of several URLs log in once, as curl does. Implicit `ftps://` is unchanged (its
pooled connection is TLS itself).
