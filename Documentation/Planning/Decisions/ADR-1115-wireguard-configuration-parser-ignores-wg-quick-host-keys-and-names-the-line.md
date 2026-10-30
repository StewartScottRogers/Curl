# ADR-1115 — The WireGuard configuration parser ignores wg-quick's host-side keys and names the line of every error

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4326.

## Context

BL-4326 scaffolds `Curl.Extension.WireGuard.UnitLibrary` and asks it to parse a wg-quick configuration file
(wg(8) "CONFIGURATION FILE FORMAT", wg-quick(8)) into a typed configuration, rejecting bad input with a message that
names the line. Real files also carry keys that configure the host rather than the tunnel, and the two manual pages
leave some value forms open.

## Decision

- **Host-side keys are ignored.** In `[Interface]`, `Table`, `FwMark`, `SaveConfig`, `PreUp`, `PostUp`, `PreDown`
  and `PostDown` route traffic through the operating system or run shell commands. Curl's tunnel runs in user space
  for one transfer, so it has no use for them; accepting and ignoring them lets an existing file work unchanged,
  while running its hooks would surprise a user. Any other unknown key, in either section, is an error.
- **Addresses keep their host bits.** `Address = 10.0.0.2/24` names the interface's own address, so a
  `WireGuardCidr` is an address and a prefix length, not `System.Net.IPNetwork` (which rejects host bits). A missing
  prefix covers the whole address (`/32` or `/128`), as wg does. Lists are comma separated and repeated lines add up.
- **Endpoints.** `host:port` or `[IPv6]:port`, port 1 to 65535; an unbracketed IPv6 address is an error, since its
  port cannot be told apart.
- **Numbers.** `ListenPort` 0 to 65535, `MTU` 1 to 65535, `PersistentKeepalive` 0 to 65535 or `off`, where 0 and
  `off` both mean no keepalive (`null`), as in wg.
- **Errors.** Every failure throws `WireGuardConfigurationException : FormatException`, whose message starts
  `line N:`. A section without its key names the section's header line; a file with no `[Interface]` or no `[Peer]`
  names its last line.

## Consequences

BL-4147 can hand the parsed configuration to the handshake without re-reading text. A file that relies on a
host-side key behaves as if the key were absent, which is what a user-space tunnel can offer.
