# ADR-0295 — The `--interface` and `--local-port` bind writes curl's `-v` lines through the dial's events

- **Status:** Accepted
- **Date:** 2026-10-01

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1027.

## Context

ADR-0269 and ADR-0293 bind each TCP dial's local end but wrote none of the `-v` lines libcurl's
`bindlocal` (`lib/cf-socket.c`) writes between `Trying` and the connect outcome. The dialer had no
`ITransferEvents`. Measured on 2026-10-01 with `Record-CurlExchange.ps1` against curl 8.21.0 Schannel on
Windows, and with curl 8.18.0 OpenSSL under WSL Ubuntu against a closed port (BL-1027 Notes):

| Case | Lines |
| --- | --- |
| a host or address bound | `Name '<host>' family <dialled AF> resolved to '<first address>' family <its AF>` |
| ...of the dialled family, bound | `Local port: <port asked for>` (`0` without `--local-port`) |
| a port of the range busy, another left | `Bind to local port <port> failed, trying next` |
| the last port busy | `bind failed with errno <errno>: <reason>` (Windows 10048, Linux 98) |
| a host that does not resolve | `Could not resolve host: <host>`, `Could not bind to '<host>' with errno <n>: <reason>` |
| an `if!` name that is no interface | `Could not bind to interface '<name>' with errno <n>: <reason>` |
| a name bound as a device alone (Linux) | `socket successfully bound to interface '<name>'` |

A family mismatch writes the `Name` line and nothing more. `AF_INET6` is 23 on Windows and 10 on Linux.

## Decision

1. **The seam.** `LocalBindingTcpDialer` is created per race with the target's `Events`, and hands them
   to `LocalBindingAddressChooser.ChooseAsync` (the name and `Could not` lines) and to the inner dialer:
   a new `ITcpDialer.DialFromAsync` overload taking `ITransferEvents`, and an `events` parameter on
   `DialFromDeviceAsync`. The overload's default dials through the old one and writes nothing, so the
   console's dialers need no change; `TcpDialer` implements it and `TcpDialer.BindLocalEnd` writes the
   port lines as it walks the range. The lines are written as they happen, so they land between the
   race's `Trying` and its outcome line, as curl's do. `LocalBindLines` holds every text.
2. **Errno.** `bind failed` gives `SocketException.NativeErrorCode` (the platform's errno) and
   `ConnectFailureReason`'s words, which now word `WSAEADDRINUSE` as curl's table does. The `Could not
   bind` lines give errno 0 `No error` on Windows; off Windows the measured Linux answers, 22
   `Invalid argument` after a failed resolve and 19 `No such device` for an `if!` name. macOS is not
   measurable here; it gets the Linux text, and `AF_INET6` 30 from its `sys/socket.h`.
3. **The resolve line names the bind host** (`Could not resolve host: bogus0`), as 8.21.0 does on
   Windows; 8.18.0 on Linux names the URL's host there, taken as that older version's quirk.
4. **Not written:** `Local Interface <name> is ip <address> using address family <n>`, libcurl's line for
   an interface address found after a refused device bind, which could not be produced to measure; and
   every bind line on QUIC's UDP socket (ADR-0292), whose chooser and `BindLocalEnd` get
   `NoTransferEvents` until those lines are measured over QUIC.

## Consequences

- `curl -v --interface ... / --local-port ...` now shows curl's bind lines for TCP dials, the proxy's
  included; failed binds still end with ADR-0269's `connect to ... from  port 0 failed:` line.
- The two lines left out are follow-up work, filed from BL-1027.
