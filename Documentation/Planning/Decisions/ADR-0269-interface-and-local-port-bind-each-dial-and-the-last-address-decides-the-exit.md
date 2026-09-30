# ADR-0269 — `--interface` and `--local-port` bind each dial, and the last address decides the exit

- **Status:** Accepted
- **Date:** 2026-09-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-600.

## Context

BL-599 parses `--interface` into `InterfaceBinding` and `--local-port` into `LocalPortRange`.
BL-600 makes the TCP connector bind with them, as libcurl's `bindlocal` (`lib/cf-socket.c`)
does. curl 8.21.0 (Schannel, Windows) was measured with `Record-CurlExchange.ps1` on 2026-09-29
(BL-600 Notes), and curl 8.18.0 (OpenSSL, Ubuntu under WSL) for the interface-name answer.

## Decision

1. **The seam.** `ITcpDialer.DialFromAsync(endPoint, localEndPoint, localPortCount)` binds the
   local end before connecting. `TcpDialer.BindLocalEnd` tries each port of the range in turn,
   never past 65535, and throws `LocalBindException` (`LocalBindFailure.InterfaceFailed`) when
   none binds. `TcpConnector` takes a `LocalBinding` and wraps its dialer in the internal
   `LocalBindingTcpDialer`, which picks the local address for each address dialled. Unix domain
   sockets are never bound, as libcurl binds IP sockets only.
2. **The local address**, for the family of the address dialled: an `ifhost!` interface part
   over 254 characters is `BadArgument`; an interface name that `INetworkInterfaceLookup` finds
   gives its first address of the family, or `AddressFamilyMismatch` when it has none; a name
   that is no interface is `InterfaceFailed` after `if!` and resolved as a host otherwise
   (a plain name, `host!`, `ifhost!`). A host is an address as written, `localhost` as `::1`
   then `127.0.0.1`, or the resolver's answer, whatever `-4`/`-6` says; its first address is
   bound, none is `InterfaceFailed`, one of the other family `AddressFamilyMismatch` (measured:
   `host!localhost` and `::1` to an IPv4 URL are exit 7, `-4` too). With only `--local-port`
   the unspecified address of the family is bound.
3. **Platforms.** The Windows build has no `getifaddrs`, so no interface name is ever found
   there and `if!<any>` is exit 45 (measured with the loopback adapter's own name);
   `SystemNetworkInterfaceLookup` already answers so (ADR-0110). Off Windows the name is looked
   up among the machine's interfaces (measured: `if!lo` binds on Linux, `if!bogus0` is exit 45).
4. **A failed bind is a failed address.** The race moves on to the next address, as libcurl's
   eyeballer does, with the `-v` line `connect to <ip> port <port> from  port 0 failed: No error`
   (errno 0; `Success` off Windows, glibc's words, not yet measured on 8.21.0). When every
   address has failed, the last one decides: `InterfaceFailed` is exit 45 `Failed to connect to
   <host>:<port> after <n> ms: Failed binding local connection end`, `BadArgument` exit 43
   `... A libcurl function was given a bad argument`, anything else the usual exit 7.
5. **setopt refusals.** A value `InterfaceBinding.IsMalformed` flags fails its transfer before
   any connection with exit 43 `setopt 0x274e got bad argument` (also a `-v` line), still writes
   `-w`, and ends the run, as measured.
6. **Pools.** Each option group builds its own connector and pool, so a bound connection is
   never reused by a transfer without the binding.

## Consequences

- The `SO_BINDTODEVICE` step libcurl tries first on Linux (privileged; silently skipped
  otherwise) is not taken, so an `ifhost!` interface part only has its length checked; the
  `-v` lines `Local port: N`, `Bind to local port N failed, trying next`, `bind failed with
  errno ...`, `Could not bind to ...` and `Name '...' family N resolved to ...` are not yet
  written; and QUIC's UDP socket is not bound. Each is its own follow-up task.
- `TcpDialer.DialFromAsync` is excluded from coverage like `DialAsync` (ADR-0083); the bind
  loop it runs, `BindLocalEnd`, is unit tested over real, unconnected sockets.
