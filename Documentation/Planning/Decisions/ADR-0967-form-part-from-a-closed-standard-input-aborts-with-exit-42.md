# ADR-0967: A `-F` part read from a closed standard input aborts the transfer with exit 42

Status: Accepted. Decided by Claude under Stewart's delegation (BL-3985, CGR-110).

## Context

`curl -sS -F f=@- URL <&-` with standard input closed: curl 8.22.0 (Schannel, Windows),
measured on 2026-10-28 with `Record-CurlExchange.ps1`, sends the request head with
`Transfer-Encoding: chunked` and `Expect: 100-continue`, one chunk holding the part's head,
and ends with exit 42, `curl: (42) operation aborted by callback`, printing nothing on stdout.
Curl read standard input whole while building the body, found it empty, sent a
`Content-Length` body with an empty part and exited 0.

`MultipartFormBodyBuilder` (in `Curl.Core.UnitLibrary`) could not tell a closed standard
input from an empty one: `ClosedStandardInputStream`, the type that says so (ADR-0949),
lived in `Curl.Cli.UnitLibrary`, which `Curl.Core` does not reference. And
`HttpRequestBodyWriter` turned every `RequestBodyReadFailedException` into exit 26.

## Decision

- `ClosedStandardInputStream` moves to `Curl.Core.UnitLibrary`, namespace `Curl.Core`;
  `Curl.Cli` and `Curl.Console` use it from there. Its tests stay in `Curl.Cli.UnitTests`,
  since they drive the Cli's data reader and parser.
- `RequestBodyReadFailedException` carries the `CurlExitCode` the transfer fails with,
  `ReadError` (26) unless given another, and `HttpRequestBodyWriter` fails with it.
- An `@-` or `<-` part whose standard input is a `ClosedStandardInputStream` is the part's
  headers followed by a callback segment of unknown length whose read throws that exception
  with `AbortedByCallback` (42) and `operation aborted by callback`. The body is chunked, its
  first read gives the part head, and the read after it fails the transfer, as curl's tool
  read callback aborts on a failed `fread`.

## Consequences

The tool's `-F f=@- <&-` matches curl's exit code, stderr and stdout. The SMTP sender still
reports any body read failure as exit 26; no measured case asks otherwise.
