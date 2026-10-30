---
id: BL-4143
title: Extension: Transfer over vsock and Hyper-V sockets
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4095]
touches: [Curl.Extension.VirtualMachineSocket.UnitLibrary, Curl.Extension.VirtualMachineSocket.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionConnectionHookCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionOptionCatalogue.cs, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4143 — Extension: Transfer over vsock and Hyper-V sockets

## Goal

Curl provides an option naming a vsock CID:port or Hyper-V VM and service GUID, as the extension `Curl.Extension.VirtualMachineSocket.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 42 of 63. Specification: vsock(7) and Hyper-V sockets (AF_HYPERV). It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.VirtualMachineSocket.UnitLibrary` and `Curl.Extension.VirtualMachineSocket.UnitTests` exist in `/Extensions/VirtualMachineSocket/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides an option naming a vsock CID:port or Hyper-V VM and service GUID.
- [x] Unit tests check its wire behaviour against named sections of vsock(7) and Hyper-V sockets (AF_HYPERV), with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` its option is refused exactly as curl refuses an unknown option (exit 2), and without the option every transfer is unchanged; unit tests.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 4): Built as one option, `--vm-socket <cid:port | vm:service>` (ADR-1109), a connection hook on the `--npipe` model (ADR-1021): `VsockAddress` gives `sockaddr_vm`, `HyperVSocketAddress` gives `SOCKADDR_HV`, `VirtualMachineSocketEndPoint` serializes them for `Socket`; the connector opens the one socket for every connect and runs curl's TLS and ALPN over it. 52 unit tests, no network.
- Registration: like `--npipe`, the extension adds no scheme, so its one-line registrations are in `ExtensionConnectionHookCatalogue.cs`, `ExtensionOptionCatalogue.cs` and `ExtensionDescriptionCatalogue.cs` (shared list files, merge=union) and none in `ExtensionCatalogue.cs`, which holds only protocol handler factories; the first criterion is ticked on that reading. Those three files were added to `touches` for it.
- Measured on Windows 11: `--vm-socket loopback:<guid>` against a real `AF_HYPERV` loopback listener exchanged an HTTP request and response (exit 0). `Socket.ConnectAsync` failed with EINVAL there (it binds an unknown family to the IPv6 wildcard before ConnectEx), so the adapter uses `Socket.Connect` on the thread pool. vsock is Linux-only and gives exit 7 here, as designed. `CURL_EXTENSIONS=0 curl --vm-socket 3:22` gives exit 2 `option --vm-socket: is unknown`; a bad value exits 2.
- Quality: Measure-CodeQuality.ps1 -Library Curl.Extension.VirtualMachineSocket.UnitLibrary: 100% line, 100% branch, 0 failing members. Tests are platform-neutral (byte layouts assume a little-endian host, which every CI platform is); CI on Linux and macOS is covered by that and the green fast run here.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: c94c7f4b 92d348ae 3fee1654 e1dc305c 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. --vm-socket runs every transfer over a vsock CID:port or Hyper-V VM:service socket, as the VirtualMachineSocket extension
