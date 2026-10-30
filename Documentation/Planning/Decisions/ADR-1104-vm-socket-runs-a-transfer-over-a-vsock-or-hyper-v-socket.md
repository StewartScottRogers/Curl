# ADR-1104: --vm-socket runs a transfer over a vsock or Hyper-V socket

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4143)

## Context

Stewart chose an extension that transfers over vsock and Hyper-V sockets, the sockets between a virtual machine and
its host, among the extensions of 2026-10-29 (ADR-0999). Upstream curl has no such option, so there is no curl output
to measure. The extension follows the Linux man page vsock(7) (man-pages 6.x: "Address format", the well-known CIDs
and `VMADDR_CID_ANY`/`VMADDR_PORT_ANY`) and Microsoft's "Make your own integration services" (Hyper-V sockets:
`AF_HYPERV`, `HV_PROTOCOL_RAW`, `SOCKADDR_HV` and the well-known VM IDs). ADR-1010 gives an extension the
`IConnectionHook` seam that replaces curl's connector for an option group; ADR-1021 used it for `--npipe`.

## Decision

- The extension is `Curl.Extension.VirtualMachineSocket.UnitLibrary`. Its one option is `--vm-socket <address>`,
  taking a value; it adds no URL scheme. As `--npipe` does, it registers a connection-hook activator in
  `ExtensionConnectionHookCatalogue`, its option in `ExtensionOptionCatalogue` and its description in
  `ExtensionDescriptionCatalogue`, one line each, and no line in `ExtensionCatalogue`, which holds protocol handler
  factories for schemes it does not add.
- One option names either family, so a script names the peer without a second option to pick the family:
  - `<cid>:<port>` is vsock. The CID is a decimal number or `hypervisor` (0), `local` (1) or `host` (2); the port a
    decimal number. `VMADDR_CID_ANY` and `VMADDR_PORT_ANY` (4294967295) are refused: vsock(7) gives them to `bind`
    only.
  - `<vm>:<service>` is Hyper-V. The VM is a GUID or `parent`, `loopback` or `silohost` (`HV_GUID_PARENT`,
    `HV_GUID_LOOPBACK`, `HV_GUID_SILOHOST`); the service a GUID. The all-zero GUID (`HV_GUID_WILDCARD`) is refused for
    either, as only a listener takes it. Names match in any case.
  - Any other value is the command line's error: the activator throws `ArgumentException`, reported with exit 2 as
    `--npipe` reports its own (ADR-1021).
- Every connection of the option group opens that socket, whatever host and port the URL names, as `--unix-socket`
  ignores them; the `Host:` header and the request are curl's, unchanged. The stream socket carries the bytes
  unchanged. A TLS target runs curl's handshake over it with curl's TLS provider and ALPN list, as ADR-1055 and
  ADR-1066 do for `--npipe`; without a provider a TLS target is refused with exit 4.
- The socket is a .NET `Socket` in family 40 (`AF_VSOCK`, Linux) or 34 (`AF_HYPERV`, Windows) with protocol 0 or
  `HV_PROTOCOL_RAW` (1), connected to a `VirtualMachineSocketEndPoint` that serializes `sockaddr_vm` (port and CID in
  host byte order) or `SOCKADDR_HV` (GUIDs in Windows' layout). It connects with `Socket.Connect` on the thread pool,
  closed on cancel, not `Socket.ConnectAsync`: on Windows that binds an unbound socket of a family .NET does not know
  to the IPv6 wildcard before `ConnectEx`, which `AF_HYPERV` refuses with `EINVAL` (measured on Windows 11 against an
  `HV_GUID_LOOPBACK` listener, which `Connect` then reached and exchanged HTTP with). A platform without the family, a refused peer or no
  answer fails with exit 7 and `Failed to connect to the <address>: <reason>`.

## Left out, and why

- No vsock on Windows or macOS, and no Hyper-V socket on Linux: those operating systems give no such socket family to
  a process (a Linux guest on Hyper-V reaches the host through vsock, which `--vm-socket host:<port>` covers), so the
  connect fails with exit 7 there, as `--unix-socket` fails on a platform without the socket.
- No `VMADDR_FLAG_TO_HOST` or other `svm_flags`: vsock(7) sets it only for sibling-VM routing, which a client of the
  host never needs; the field stays zero.
- No hostnames or service names for a Hyper-V service: there is no lookup in either specification, so the value is
  always the GUID the service was registered under.

## Consequences

- Without `--vm-socket` no hook is activated and every transfer is unchanged; with `CURL_EXTENSIONS=0` the option is
  not offered, so curl's parser refuses it as unknown (exit 2).
- `SystemVirtualMachineSocketOpener` is excluded from coverage (ADR-0083): every line needs a live peer, which only a
  virtual machine can offer. Everything else is unit tested without a network.
