# Curl.Extension.NamedPipe.UnitLibrary

The `--npipe <\\.\pipe\name>` extension (ADR-0999, ADR-1021): every transfer of its option group runs
over a Windows named pipe, as `--unix-socket` runs it over a Unix domain socket. Namespace
`Curl.Extension.NamedPipe`. Tested against Microsoft's "Pipe Names" and "Named Pipes" documentation, not
against curl.

- `NamedPipeExtension` declares the `npipe` option, the extension's `ExtensionDescription`, and `Activate`,
  the connection hook activator `Curl.Extension.Composition` registers: `null` without `--npipe`, an
  `ArgumentException` (curl's exit 2) for a value that is not a pipe name.
- `NamedPipeName` parses `\\<server>\pipe\<name>` (`.` for this machine), as "Pipe Names" defines it.
- `NamedPipeConnectionHook` gives a `NamedPipeConnector`, which opens the pipe through an `INamedPipeOpener`
  for every connect, whatever host and port the URL names, and wraps it in a `NamedPipeConnection`. For a TLS
  target it runs the handshake over the pipe with the TLS provider the host hands the hook (curl's, built from the
  group's `-k`, `--cacert` and the rest); without one a TLS target is refused with exit 4 (ADR-1021, ADR-1055). An `https` target offers its own `ConnectTarget.ApplicationProtocols` through ALPN, or else the `IConnector.HttpOverTlsApplicationProtocols` of the connector curl would have used, so the HTTP version options and `--no-alpn` hold over the pipe and HTTP/2 can be negotiated (ADR-1066). `SystemNamedPipeOpener` is the thin `NamedPipeClientStream`
  adapter, excluded from coverage (ADR-0083).

Held to the four quality gates in the root `CLAUDE.md`. Its tests are in `Curl.Extension.NamedPipe.UnitTests`.
