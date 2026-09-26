# ADR-0027 — Multipart form bodies are built as libcurl 8.21.0 builds them

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-205 turns the parts `-F`/`--form` and `--form-string` describe (BL-189) into the
`multipart/form-data` `StreamBody` (ADR-0014) the HTTP handler sends. `Curl.Cli` already
references `Curl.Core`, so the builder in `Curl.Core` cannot take BL-189's
`FormPartSpecification`. Five questions were open as well: how each part's headers are
chosen, how the length is known before sending, how files are read, what an unreadable file
does, and which bytes text becomes.

Every answer below was measured with the reference build (curl 8.21.0, mingw, Schannel,
`/mingw64/bin/curl`) on 2026-09-26 with
`Record-CurlExchange.ps1 -Port 18205 -CurlArgs --no-progress-meter,-F,<spec>,...,http://127.0.0.1:18205/`,
and read against `lib/mime.c` at tag `curl-8_21_0`. The recorded bodies are pinned in
`MultipartFormBodyBuilderTests` in `Curl.Core.UnitTests`.

## Decision

- **Its own part model.** `Curl.Core.Multipart.MultipartFormPart` (name, kind, content,
  type, file name, headers, nested parts) mirrors `FormPartSpecification`; whoever wires
  `-F` into a transfer maps one onto the other. `;encoder=` and standard input (`@-`, `<-`)
  are not in it yet.
- **Headers are `Curl_mime_prepare_headers`.** A part's own `Content-Type:` header counts as
  its type and is written in curl's place, never twice; its own `Content-Disposition:`
  replaces curl's. With no type, a file part takes one from the file name's extension,
  then the path's, then `application/octet-stream` when it has a file name; a text part
  only from its file name's extension; a nested multipart `multipart/mixed`. A `text/plain`
  curl chose is dropped from a part with no file name. Parts of a `multipart/form-data` are
  `form-data`, others `attachment`, and an `attachment` with neither name nor file name has
  no disposition. `"`, CR and LF in a name or file name are `%22`, `%0D`, `%0A`. The file
  name of `@path` is the text after the last `/` or `\`.
- **Files are opened while building, through `IFileSystem`,** so `Content-Length` is known
  before a connection is made, then streamed rather than read into memory. A stream that
  cannot seek makes the length unknown and the body goes chunked, as curl does for a
  non-regular file.
- **An unopenable file is exit 26 before connecting,** with
  `Failed to open/read local data from file/application`, as measured for a missing
  `@file` and `<file`. A directory is exit 26 with `read error getting mime data`, curl's
  message; curl sends a chunked request head first and fails mid-body, which is not
  reproduced: nothing is sent.
- **Text bytes follow the platform curl.** The builder takes an `Encoding`: measured on
  Windows, `-F "né=vé"` sent `é` as the single byte `E9`, the ANSI code page, as ADR-0022
  found for credentials; UTF-8 on Linux and macOS.
- **Boundaries are injected.** Production passes `MultipartBoundary.CreateRandom`
  (24 dashes and 22 random alphanumerics); tests pass the boundaries curl chose, the
  outermost first.

## Alternatives considered

- **Read every file into memory.** Simpler, but a large upload would cost its size in
  memory, which curl never does.
- **Move `FormPartSpecification` down into `Curl.Protocol.Abstractions`.** It would change
  a shared contract and `Curl.Cli` for one consumer; mapping in the wiring task is cheaper.
- **Send the directory case as curl does.** It needs a body stream that fails part-way and
  a handler that reports the failure mid-send, for a case nobody relies on.

## Consequences

- `Curl.Console` (or the transfer wiring) maps `CommandLineOptions.FormParts` onto
  `MultipartFormPart`, passes `PhysicalFileSystem`, the platform encoding and
  `MultipartBoundary.CreateRandom`, and disposes the body's stream.
- `;encoder=`, `@-`/`<-` and the `Expect: 100-continue` curl adds to a chunked form post are
  left to follow-up tasks.
