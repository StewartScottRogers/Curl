# ADR-0968: TFTP under `-4` or `-6` filters an IP address literal as the TCP connect does

Status: Accepted. Decided by Claude under Stewart's delegation (BL-3994, CGR-186).

## Context

CGR-186 (BL-3715) made `AddressFamilyFilter` filter an IP address literal of the other family
off Windows, so `curl -6 http://127.0.0.1/` has nothing to dial and fails with exit 7, as
curl 8.18.0 on Linux does. `TcpConnector` chose the platform through its `FiltersAddressLiterals`
property and reported the empty answer as exit 7. `UdpDatagramConnector`, which opens TFTP's
channel, reached the filter through an overload that asked `OperatingSystem.IsWindows()` itself:
off Windows `-6 tftp://127.0.0.1/` became exit 6 `Could not resolve host: 127.0.0.1`, and its
unit test, which pins the Windows build's answer (the literal opened, BL-500), failed on Linux
and macOS in CI.

## Decision

- `UdpDatagramConnector` takes a `FiltersAddressLiterals` init property, defaulting to the
  platform build's choice (`!OperatingSystem.IsWindows()`), as `TcpConnector` does, and hands it
  to `AddressFamilyFilter.Dialable`. The overload that read the platform itself is removed.
- A literal left with nothing to open is exit 7,
  `Failed to connect to <host>:<port> after <n> ms: Could not connect to server`, the
  connector's own exit 7 text, since curl's UDP connect runs through the same socket filter
  that refuses the TCP one.
- Tests pin each build through the property, so both answers are checked on every platform.

## Consequences

The Windows answer is unchanged. Off Windows a TFTP transfer to a literal of the other family
fails with exit 7 rather than exit 6, matching the HTTP path. The TFTP text was not measured
on a Linux curl; it follows the TCP case CGR-186 measured.
