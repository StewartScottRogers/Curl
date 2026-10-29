# ADR-1021: --npipe runs a transfer over a Windows named pipe

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4142)

## Context

Stewart chose a named pipe extension, the Windows counterpart of `--unix-socket`, among the extensions of
2026-10-29 (ADR-0999). Upstream curl has no such option, so there is no curl output to measure; the extension is
tested against Microsoft's Win32 documentation, "Pipe Names"
(<https://learn.microsoft.com/windows/win32/ipc/pipe-names>) and "Named Pipes" (byte-mode client I/O), as
`System.IO.Pipes.NamedPipeClientStream` implements it in .NET 10. ADR-1010 gives an extension the
`IConnectionHook` seam that replaces curl's connector for an option group.

## Decision

- The extension is `Curl.Extension.NamedPipe.UnitLibrary`. Its one option is `--npipe <pipe>`, taking a value; it
  adds no URL scheme. It registers a connection-hook activator in `ExtensionConnectionHookCatalogue`, its option in
  `ExtensionOptionCatalogue` and its description in `ExtensionDescriptionCatalogue`, one line each. It adds no line to
  `ExtensionCatalogue`, which holds protocol handler factories, and the extension handles no scheme.
- The value must be a pipe name as "Pipe Names" writes it: `\\<server>\pipe\<name>`, `.` for this machine, `pipe`
  in any case, a pipe part of any characters but a backslash, at most 256 characters in all. Any other value is the
  command line's error: the activator throws `ArgumentException`, which `Curl.Console` reports with exit 2 as it
  reports a connection-hook conflict (ADR-1014). Forward slashes and a bare pipe name are not accepted, so the value
  says exactly the pipe Windows would open.
- Every connection of the option group opens that pipe, whatever host and port the URL names, as `--unix-socket`
  ignores them; the `Host:` header and the request are curl's, unchanged. The bytes pass through unchanged (byte mode).
- The pipe is opened with a zero wait (`ConnectAsync(0)`): a pipe that is not there, or has no free instance, fails
  at once with exit 7 and `Failed to connect to the named pipe <pipe>: <reason>`, as `--unix-socket` fails at once on
  a missing socket. Access denied and a timeout are exit 7 as well.
- `SystemNamedPipeOpener`, the `NamedPipeClientStream` adapter, is excluded from coverage under ADR-0083: every line
  needs a live pipe. Everything above it is tested through a fake opener.

## Left out, and why

- TLS over the pipe (`https://` with `--npipe`) is refused with exit 4, `TLS over the named pipe <pipe> is not built
  in`: the connection hook replaces curl's whole connector, and the TLS handshake lives inside curl's connector,
  which an extension cannot reach (it references neither `Curl.Networking` nor a protocol library). A follow-up task
  adds a TLS seam to `Curl.Extension.Abstractions` and layers TLS over the pipe.
- HTTP/3 over the pipe is not offered: a pipe is a byte stream, not datagrams, and `--unix-socket` has no HTTP/3 either.
- Off Windows, .NET maps a local pipe name onto a Unix domain socket in the temporary directory; the option is left
  available there, as that is what `System.IO.Pipes` does, but it is specified and tested for Windows' pipes.

## Consequences

- `curl --npipe \\.\pipe\docker_engine http://localhost/_ping` talks to Docker Desktop's engine as
  `curl --unix-socket /var/run/docker.sock http://localhost/_ping` does on Linux.
- With `CURL_EXTENSIONS=0` the option is not registered, so the parser refuses it as an unknown option (exit 2).
