# ADR-0293 — An interface name binds its device first on Linux, and a plain or `if!` name bound so binds nothing else

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1026.

## Context

ADR-0269 left out the `SO_BINDTODEVICE` step libcurl's `bindlocal` (`lib/cf-socket.c`) takes first
on Linux. curl 8.18.0 (OpenSSL, Ubuntu under WSL, kernel 6.x) was measured on 2026-10-01 as uid 1000
and as root, against `http://127.0.0.1:1/` (BL-1026 Notes). Both answered the same: since Linux 5.7 an
unprivileged process may bind a socket not yet bound to a device, so "privileged only" no longer holds.

## Decision

1. **Which name.** A plain `--interface` name and `if!<name>` (`LocalBinding.InterfaceName`) and the
   interface part of `ifhost!` (`LocalBinding.DeviceName`) are bound as a device first; `host!` never is.
2. **What a success means.** A plain or `if!` name bound to its device is the whole binding: no address
   and no `--local-port` port is bound (measured: `--interface lo --local-port 40000-40010` connects from
   an ephemeral port). `ifhost!` goes on to bind its host's address and ports whether the device bind
   succeeded or not.
3. **A failure** (a name that is no device, or no privilege on an older kernel) carries on exactly as
   ADR-0269 says.
4. **The seam.** `ITcpDialer.DialFromDeviceAsync` takes the device name, whether the address is bound
   after it, and a callback that chooses the local end only when one is needed, so
   `LocalBindingAddressChooser`'s failures still come after the device bind, as libcurl orders them.
   Its default binds no device, as every platform but Linux, so the other dialers need no change.
   `TcpDialer` calls `Socket.SetRawSocketOption(1, 25, name + NUL)`, the bytes libcurl passes, on Linux
   only; `TcpDialer.TryBindToDevice` is excluded from coverage per ADR-0083 and pinned by Linux tests.

## Consequences

- An `--interface eth0` dial to a loopback address now leaves through `eth0` and times out on Linux,
  as curl's does (measured: exit 28).
- Not yet done, each its own task: the `-v` line `socket successfully bound to interface '<name>'`,
  and the same device bind on QUIC's UDP socket (ADR-0292).
