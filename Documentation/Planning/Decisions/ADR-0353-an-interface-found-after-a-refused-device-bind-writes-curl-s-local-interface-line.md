# ADR-0353 — An interface found after a refused device bind writes curl's `Local Interface` line

- **Status:** Accepted
- **Date:** 2026-10-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1079.
Completes ADR-0295's Decision 4, which left libcurl's `Local Interface ... is ip ...` line unwritten until measured.

## Context

libcurl's `bindlocal` (`lib/cf-socket.c`) first tries `SO_BINDTODEVICE` for a plain or `if!` name. When that is
refused and `Curl_if2ip` finds the interface's address of the family dialled, it writes
`Local Interface <name> is ip <address> using address family <af>`. On Linux 5.7 and later an unprivileged device
bind succeeds, so the line shows only where the bind is refused; on Windows `Curl_if2ip` finds nothing
(ADR-0110), so it never shows there.

Measured on 2026-10-02 with curl 8.22.0 (OpenSSL, `curlimages/curl`) in Docker, with a seccomp profile that answers
`setsockopt(SOL_SOCKET, SO_BINDTODEVICE)` with `EPERM`, each to a closed port (exit 7):

| `--interface` | URL | Lines after `Trying` |
| --- | --- | --- |
| `lo` | `http://127.0.0.1:1/` | `Local Interface lo is ip 127.0.0.1 using address family 2`, `Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2`, `Local port: 0` |
| `lo` (`--local-port 40000-40002`) | `http://[::1]:1/` | `Local Interface lo is ip ::1 using address family 10`, `Name '::1' family 10 resolved to '::1' family 10`, `Local port: 40000` |
| `if!lo` | `http://127.0.0.1:1/` | `Local Interface lo is ip 127.0.0.1 using address family 2`, `Local port: 0` |
| `if!lo` | `http://[::1]:1/` | `Local Interface lo is ip ::1 using address family 10`, `Local port: 0` |
| `ifhost!lo!127.0.0.1` | `http://127.0.0.1:1/` | `Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2`, `Local port: 0` |

Without the seccomp profile the same image writes `socket successfully bound to interface 'lo'` instead (BL-1076).

## Decision

1. `LocalBindingAddressChooser.ChooseAsync`, when the interface lookup finds the address, writes
   `LocalBindLines.LocalInterface` with the platform's `AF_*` number (2; for IPv6 10 on Linux, 30 on macOS).
2. A plain name, which is a host name too, then writes the `Name '<address>' family N resolved to '<address>' family N`
   line, as libcurl resolves the found address as the host; `if!` writes the first line alone. `ifhost!` never reaches
   the interface lookup, so writes neither, as measured.
3. The Linux numbers are pinned in `Curl.Networking.UnitTests` (`TcpConnectorTests.LocalInterfaceLine.cs`), the IPv6
   case under `[OSCondition(OperatingSystems.Linux)]`. macOS, which has no `SO_BINDTODEVICE`, takes the same path;
   its IPv6 family number 30 comes from its `sys/socket.h`, as in ADR-0295, unmeasured.

## Consequences

- The TCP and QUIC binds both write the line, since both choose through the same chooser with the dial's events
  (ADR-0352).
- An IPv6 link-local interface address is written as .NET formats it, with its `%scope`; libcurl's form for it was
  not measured.

## Alternatives considered

- Writing only the `Local Interface` line, as the task's goal names: rejected, real curl writes the `Name` line after
  it for a plain name, and output bytes must match.
- Measuring on an old kernel or a container without the capability: no such kernel was at hand, and since Linux 5.7
  the capability is not needed; a seccomp filter refuses the exact call, which is the condition the line depends on.
