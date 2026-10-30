# ADR-1085 — `--segments` downloads a plain HTTP GET as parallel byte ranges, in a transfer hook

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4128.

## Context

BL-4128 adds the extension `Curl.Extension.SegmentedDownload.UnitLibrary` (ADR-0999): `--segments <n>` downloads
one file as several byte ranges fetched at the same time. Upstream curl has no such option, so there is no curl
output to match; RFC 9110 (HTTP Semantics, June 2022) §14 "Range Requests" decides the wire behaviour. Transfer
hooks can run curl's handler more than once and capture what it received (`ExecuteAsync`, ADR-1019), which is
all the extension needs.

## Decision

- **Names.** One option, `--segments <n>`, and the extension name `segments` in `curl --extensions` and
  `curl --ai-help extensions`. No scheme: the ranges ride on curl's own `http` and `https`.
- **Registration.** Like `--cache-dir` (ADR-1069), the extension adds no protocol handler, so
  `ExtensionCatalogue.cs` gets no line: it holds protocol handler factories. Its option, description and hook
  activator are one line each in `ExtensionOptionCatalogue`, `ExtensionDescriptionCatalogue` and
  `ExtensionTransferHookCatalogue`. With `CURL_EXTENSIONS=0` the option is not registered, so curl refuses it as an
  unknown option (exit 2).
- **The value.** A whole number from 1 to 64. Anything else refuses the transfer with exit 2 (`FailedInit`, as curl
  refuses an option whose number it cannot read) and the message `--segments expects a whole number from 1 to 64`,
  without contacting the server. `--segments 1` runs the transfer exactly as without the option.
- **Which requests.** Only a plain GET of the whole resource: an HTTP transfer with no custom method (`-X`), body
  (`-d`, `-F`), upload (`-T`), `HEAD` (`-I`), range (`-r`), resume (`-C`) or time condition (`-z`); range requests
  are defined for GET (§14.2). Every other transfer runs exactly as without the option.
- **Flow.** First `Range: bytes=0-0` (§14.2) to learn the complete length from the 206's `Content-Range` (§14.4).
  Then the length is split into `n` consecutive ranges of nearly equal length (fewer when the file has fewer bytes),
  each asked for in parallel with `Range: bytes=first-last`, and, when the first response had a strong entity tag,
  `If-Range` with it (§13.1.5). Once every segment is a 206 whose `Content-Range` is exactly its range of the same
  complete length, whose body is that long and whose `ETag` and `Last-Modified` equal the first response's, the
  bodies are written in order. `-i` and `-D` show the first segment's 206 headers, the transfer's response code is
  206 and its size the complete length.
- **When it cannot be split.** A 200 to the first request (a server that ignores `Range`, §14.2) is written as
  received, from that one request. A first response that is not a 206 with a satisfied `Content-Range` of known
  length (a 416 for an empty file, `bytes 0-0/*`, a failed connection) runs the transfer again as curl runs it.
  A failed segment fails the transfer with its exit code and nothing written. A 200 to a segment (the
  representation changed, so `If-Range` gave the whole of it) is written in place of the parts. Any other segment
  that does not fit runs the transfer again as curl runs it, so the output is never a mix of two representations.

## Left out, and why

- **Weak validators and dates in `If-Range`.** §13.1.5 forbids a weak entity tag there and allows a date only when
  it is strong, which a client cannot know in general; without a strong tag the segments are instead compared on
  `ETag` and `Last-Modified` before they are joined.
- **Multipart ranges** (`multipart/byteranges`, §14.6): one request per range keeps every response a plain 206 the
  handler already reads, and lets the ranges run in parallel.
- **Streaming to the output as segments arrive.** The segments are held in memory and written once all are checked,
  so a failure never leaves half a file; a file larger than memory is better fetched without `--segments`.
- **Resuming a segmented download** (`-C` with `--segments`): resume already asks for a range, so it runs as curl
  runs it.

## Consequences

- Without `--segments`, and for every transfer it does not split, curl's behaviour is unchanged.
- A split transfer makes `n + 1` requests; the first is one byte.
