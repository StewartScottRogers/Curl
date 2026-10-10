# ADR-0468: FTP keeps the control connection for the next URL and sends QUIT at exit

- Status: Accepted
- Date: 2026-10-10
- Decided by Claude under Stewart's delegation (BL-1981, GF-0051)

## Context

curl 8.21.0 keeps an FTP control connection in its connection cache after a transfer its
`ftp_done` leaves valid, reuses it for the next URL to the same host, port and login, and
sends `QUIT` only from `ftp_disconnect` when the cache closes it. Curl sent `QUIT` after
every URL, so upstream tests 146, 215, 216, 1010, 1096, 1149, 1217, 1225, 149, 698, 2002 and
2003 failed.

## Decision

- The control connect target carries `PoolScheme` (`ftp` or `ftps`), so the run's
  `ConnectionCache` (ADR-0050) keys and serves it. A transfer that ends where curl prints
  "Connection #n to host ... left intact" hands the connection back with an
  `FtpKeptConnection` held as its `IConnectionSession` and `MarkReusable` instead of `QUIT`.
  Ends that curl closes (the "shutting down connection" ends after `ABOR`, a refused
  post-transfer quote, any failure with no `QUIT`) keep today's behaviour.
- `FtpKeptConnection.ShutDownAsync` sends `QUIT` and waits up to 120 seconds (curl's
  response timeout) for one reply line, reporting nothing to `-v`, `--trace` or `-D`.
- A reused connection skips the greeting, login, `PBSZ`, `PROT` and `PWD`; it sends no
  `TYPE` already in force (`ftp_nb_type`), no `CWD` when the URL's directory equals the
  remembered `prevpath` or the path is an absolute `nocwd` one, and otherwise `CWD <entry
  path>` first unless the path is absolute (`ftp_state_cwd`), then the path's own `CWD`s.
- The login key is user, password, account, alternative-to-user, TLS level and CCC mode,
  as curl's `ConnectionExists` compares them; a kept connection for another login is closed
  (with its `QUIT`) and a new one connected. `ConnectTarget` has no login field, so the
  pool still offers it first; the extra `Re-using` `-v` line in that rare case is accepted.
- A control connection that `AUTH` upgraded or `CCC` cleared is not kept: the TLS stream
  the session owns would be disposed with it. Such sessions still `QUIT` per URL.
- Reply bytes read past the last reply travel with the kept connection, so the next
  transfer reads them first.

## Consequences

Multi-URL FTP command lines send the same command sequence as curl 8.21.0, and `QUIT`
comes at exit, after other protocols' transfers, as test 2002 expects. Keeping
`AUTH`-upgraded connections is left for later.
