# ADR-1471: A proxied WinLDAP transfer runs on WinLDAP's own direct connection

- Status: Accepted
- Date: 2026-11-28
- Decided by Claude under Stewart's delegation (BL-6091)

## Context

curl's own connection of an `ldap` or `ldaps` transfer is tunnelled through the proxy, but
WinLDAP knows nothing of curl's proxy and opens a connection of its own straight to the host.
Measured on curl 8.22.0 (WinLDAP, Schannel) with `Record-CurlExchange.ps1` and a proxy that
answers the CONNECT `200`:

- `ldap://example.test/`, with and without `-u`: the `LDAP local:` vendor, URL and
  `trying to establish cleartext connection` lines, then
  `LDAP local: bind via ldap_win_bind Unavailable`, `shutting down connection #0`, exit 38
  (BL-6087).
- `ldap://127.0.0.1:<closed port>/`: the same ending `Timeout`, after about 480 s (BL-6087).
- `ldaps://example.test/ -k`, with and without `-u`, the proxy handshaking TLS inside the tunnel
  with an untrusted certificate: curl's own handshake completes, then
  `LDAP local: trying to establish encrypted connection`,
  `LDAP local: bind via ldap_win_bind Server Down`, exit 38, and no byte inside the tunnel.

## Decision

With a proxy the WinLDAP dialect binds and searches on a second, direct connection for every
transfer, `ldaps` with `-u` included, and curl's own handshake obeys `-k`. When that connection
cannot be opened the transfer writes the lines before the bind (a URL or `--ssl-reqd` refusal
among them ends it) and exits 38: `Server Down` over `ldaps`; otherwise `Unavailable` when the
host does not resolve and `Timeout` for any other connect failure.

Without a proxy nothing changes: `ldaps` with `-u` stays on curl's own connection with the
Windows-store certificate check (ADR-0994, BL-5461), and a plaintext failure of the second
connection stays the connector's.

Curl ends as soon as its connector fails; it does not wait the 480 s WinLDAP takes on a
refused port.

## Why

Matching the platform's curl: a script behind a proxy sees the same exit code, `-v` lines and
bytes on the wire. In particular Curl no longer sends a `-u` password down the proxy tunnel of
an `ldaps` transfer, where curl sends nothing. The wait is not copied because no output depends
on it.
