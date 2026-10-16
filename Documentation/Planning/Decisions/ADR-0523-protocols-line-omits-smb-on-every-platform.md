# ADR-0523 — `curl -V` lists `smb` and `smbs` on no platform

- **Status:** Accepted
- **Date:** 2026-10-15

Decided by Claude under Stewart's delegation (task BL-2334, GF-0091).

## Context

ADR-0453 dropped `smb` and `smbs` from the Windows `Protocols:` line only, because the Schannel
reference build does not list them, and kept them on Linux and macOS on the evidence of Ubuntu's distro
curl 8.18.0 (ADR-0200). Gap finding GF-0091 measures the reference ADR-0505 names for Linux - curl 8.22.0
built from the release tarball with OpenSSL - and its `Protocols:` line omits `smb` and `smbs` too.
macOS is measured against the same tarball built the same way, so its line agrees.

## Decision

1. `CurlVersionText.ProtocolsLine` drops `smb` and `smbs` and is the `Protocols:` line on every
   platform; `WindowsProtocolsLine`, now identical, is removed.
2. `smb://` and `smbs://` URLs keep working, and `CommandLineProtocolSet.KnownSchemes` keeps both
   (ADR-0189, ADR-0200); only the `-V` line changes.
3. This supersedes ADR-0453's "Linux and macOS keep `ProtocolsLine`".

## Why

- Matching the platform's curl is the standing rule, and the reference builds on all three platforms
  leave the two schemes off the line.
- One line for every platform is simpler than a split no reference needs.

## Consequences

- `protocols:smb` and `protocols:smbs` measure `match` on Linux and macOS in the next gap analysis.
- A script that probes `curl -V` for `smb` before an SMB transfer now skips it, as it would with the
  reference curl.
