# ADR-1116 — The virtual machine socket end point writes its own family bytes

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4322.

## Context

`VirtualMachineSocketEndPoint.Serialize` built its `SocketAddress` with `new SocketAddress(address.Family, size)`.
On Linux and macOS .NET maps the family to the platform's through a fixed table, and `AF_VSOCK` (40) and `AF_HYPERV`
(34) are not in it, so the constructor threw `PlatformNotSupportedException` (CI run 41002071588), for the test and
for any real `--vm-socket` connect there.

## Decision

`Serialize` builds the address as `AddressFamily.Unspecified`, which every platform maps, and then copies every byte
of `sockaddr_vm` or `SOCKADDR_HV`, its two-byte family included, over it. Both structures start with a 16-bit family
in host byte order, as Linux's and Windows's `sockaddr` do, so the bytes are the ones the kernel expects. The test
compares all the bytes, not `SocketAddress.Family`, which on Linux and macOS cannot name an unknown family either.

## Consequences

- No platform branch, so the method keeps full coverage from the Windows run.
- macOS lays `sockaddr` out as a length byte then a family byte; vsock is Linux's and Hyper-V sockets Windows's, so no
  macOS connect relies on this layout.
