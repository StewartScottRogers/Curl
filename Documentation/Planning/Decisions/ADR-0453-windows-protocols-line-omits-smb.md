# ADR-0453 — `curl -V` on Windows does not list `smb` and `smbs`

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (task BL-1830, GF-0037).

## Context

Curl serves `smb://` and `smbs://` URLs and its `Protocols:` line listed both on every platform.
Finding GF-0037 measures `smb` and `smbs` as a difference on Windows: curl 8.21.0's Schannel
reference build does not list them. Measured on this machine, `curl.exe -V` (Schannel) prints

`Protocols: dict file ftp ftps gopher gophers http https imap imaps ipfs ipns ldap ldaps mqtt mqtts pop3 pop3s rtsp scp sftp smtp smtps telnet tftp ws wss`

with no `smb` or `smbs`.

## Decision

1. `CurlVersionText.Lines` writes the new `WindowsProtocolsLine` on Windows: `ProtocolsLine`
   without `smb` and `smbs`. Linux and macOS keep `ProtocolsLine`.
2. `smb://` and `smbs://` URLs keep working on every platform, and `CommandLineProtocolSet.KnownSchemes`
   keeps both (ADR-0189); only the `Protocols:` line changes.

## Why

- Matching the platform's curl is the standing rule: a script on Windows that reads `curl -V` must
  see what the Schannel build prints.
- It follows ADR-0450, ADR-0451 and ADR-0452, which made the same choice for `Features:` entries.

## Consequences

- `protocols:smb` and `protocols:smbs` measure `match` on Windows in the next measurement of the
  protocols area.
- This narrows, for Windows only, ADR-0021 Decision 6's rule that the `Protocols:` line lists every
  scheme Curl serves.
