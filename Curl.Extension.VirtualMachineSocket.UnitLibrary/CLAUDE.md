# Curl.Extension.VirtualMachineSocket.UnitLibrary

The `--vm-socket <cid:port | vm:service>` extension (ADR-0999, ADR-1104): every transfer of its option group runs
over a vsock (`AF_VSOCK`) or Hyper-V (`AF_HYPERV`) stream socket, as `--unix-socket` runs it over a Unix domain
socket. Namespace `Curl.Extension.VirtualMachineSocket`. Tested against vsock(7) and Microsoft's "Make your own
integration services" (Hyper-V sockets), not against curl.

- `VirtualMachineSocketExtension` declares the `vm-socket` option, the extension's `ExtensionDescription`, and
  `Activate`, the connection hook activator `Curl.Extension.Composition` registers: `null` without `--vm-socket`, an
  `ArgumentException` (curl's exit 2) for a value that is not an address.
- `VirtualMachineSocketAddress.TryParse` gives a `VsockAddress` (CID a number or `hypervisor`, `local`, `host`; a
  port) or a `HyperVSocketAddress` (VM a GUID or `parent`, `loopback`, `silohost`; a service GUID). Each gives its
  `sockaddr_vm` or `SOCKADDR_HV` bytes, which `VirtualMachineSocketEndPoint` serializes for `Socket.ConnectAsync`.
- `VirtualMachineSocketConnectionHook` gives a `VirtualMachineSocketConnector`, which opens the socket through an
  `IVirtualMachineSocketOpener` for every connect, whatever host and port the URL names, and runs curl's TLS handshake
  over it for a TLS target, offering curl's ALPN list for `https`. `SystemVirtualMachineSocketOpener` is the thin
  `Socket` adapter, excluded from coverage (ADR-0083).

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.VirtualMachineSocket.UnitTests`.
