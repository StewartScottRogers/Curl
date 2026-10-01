# ADR-0316 — `--ip-tos` and `--vlan-priority` set every socket option the operating system has

- **Status:** Accepted
- **Date:** 2026-10-01
- **Decided by Claude under Stewart's delegation** (root `CLAUDE.md`, "Decisions").

## Context

BL-646: curl 8.21.0 reads `--ip-tos` as one of 30 upper-case names (`tos_entries` in
`src/tool_getparam.c`: the DSCP code points `CS0`-`CS7`, `AF11`-`AF43`, `EF`, `LE`, the ECN code
points `CE`, `ECT0`, `ECT1`, and the RFC 1349 bits `LOWDELAY`, `THROUGHPUT`, `RELIABILITY`,
`LOWCOST`, `MINCOST`) or a number up to 255, and `--vlan-priority` as a number up to 7, both through
`str2unummax`. It passes libcurl only a value above 0. libcurl's `cf-socket.c` sets `IP_TOS` on an
IPv4 socket and `IPV6_TCLASS` on an IPv6 one where the system headers define them, and `SO_PRIORITY`
where that is defined, which of the systems Curl runs on is Linux only. A refused `setsockopt` is
logged and the connection goes ahead.

Measured on 2026-10-01 with curl 8.21.0 (Schannel, Windows) and 8.18.0 (OpenSSL, Linux): both
builds accept and refuse the same values with the same exit codes (BL-646 Notes).

## Decision

- The parser takes the names case-sensitively and numbers with `ParseNonNegative`, then refuses a
  number past the option's maximum as "too large number"; all refusals are exit 2 with curl's text.
- `TcpSocketOptions` carries `TypeOfService` and `VlanPriority`; `TcpDialer.ApplySocketOptions` sets
  what `QualityOfServiceSocketOptions.For` lists, raw (`Socket.SetRawSocketOption`) in each operating
  system's own numbers, since the BCL names none of them portably: `IP_TOS` (Linux 1; Windows, Darwin,
  FreeBSD 3), `IPV6_TCLASS` (Windows 39, Linux 67, Darwin 36, FreeBSD 61), and `SO_PRIORITY` (Linux
  `SOL_SOCKET` 12). A `SocketException` from any of them is swallowed, as libcurl carries on.
- On Windows both are set, whatever Windows then does with them, because the operating system has
  `IP_TOS` and `IPV6_TCLASS`; only `SO_PRIORITY`, which Windows, macOS and FreeBSD do not have, is
  skipped there.
- Neither option writes a `-v` line on success, as curl writes none.

## Consequences

- The choice of numbers is pure and tested for every platform on any platform; only the read-back
  tests that need a real kernel are marked `OSCondition`.
- An operating system Curl does not recognise sets none of them.

## Alternatives considered

- `SocketOptionName.TypeOfService` through `SetSocketOption`: it covers only IPv4 and leaves the
  IPv6 Traffic Class unset, so the raw numbers serve both families the same way.
- Skipping the options on Windows, as a Schannel curl may effectively do: refused by the standing
  rule that only what the operating system lacks limits Curl.
