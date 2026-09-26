# ADR-0031 — Corrupt `--compressed` bodies report curl's generic exit 61 text where zlib's own text is unavailable

- **Status:** Accepted
- **Date:** 2026-09-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions").

## Context

BL-177 decodes `--compressed` response bodies with the BCL's `GZipStream`, `ZLibStream`,
`DeflateStream` and `BrotliStream` (ADR-0020). When a body is corrupt, the reference build
(ADR-0018: curl 8.21.0, mingw) exits 61 and prints a message that depends on the library
that found the fault. Measured against a loopback server (BL-177 Notes):

| Body | curl 8.21.0 prints |
| --- | --- |
| `gzip`, first bytes `00 01 02 …` | `curl: (61) Error while processing content unencoding: incorrect header check` |
| `gzip`, `1F 8B 07 …` | `curl: (61) Error while processing content unencoding: unknown compression method` |
| `deflate`, `FF FF FF FF` | `curl: (61) Error while processing content unencoding: invalid block type` |
| `gzip` data labelled `deflate` | `curl: (61) Error while processing content unencoding: invalid block type` |
| `br`, `FF FF FF FF` | `curl: (61) Unrecognized or bad HTTP Content or Transfer-Encoding` |
| `Content-Encoding: compress` | `curl: (61) Unrecognized content encoding type` |

The `Error while processing content unencoding:` messages carry zlib's own `msg` string.
The BCL's decompression streams do not expose it: every zlib data error surfaces as an
`InvalidDataException` with the BCL's own wording, and a Brotli error as an
`InvalidOperationException`. Curl is base class library only, so there is no zlib binding
to read the text from.

## Decision

- The two header faults are detected by Curl's own code before a BCL stream is created,
  exactly as zlib detects them, and report zlib's measured text:
  `incorrect header check` (first two bytes neither gzip's `1F 8B` nor a zlib header, for
  `gzip`) and `unknown compression method` (a gzip or zlib header naming a method other
  than deflate).
- Every other corrupt body, whatever the coding, is exit 61
  `Unrecognized or bad HTTP Content or Transfer-Encoding`: the measured text for Brotli,
  and the text `curl_easy_strerror` gives exit 61.
- An unrecognized coding is exit 61 `Unrecognized content encoding type`, as measured.

## Consequences

- The exit code is always right, and so is the message for every measured Brotli fault,
  every header fault and every unknown coding.
- A corrupt deflate stream past a good header prints the generic exit 61 text where the
  reference prints zlib's detail (`invalid block type`, `invalid distance too far back`,
  `incorrect data check` and the like). A script that matches that detail sees the
  difference; one that checks the exit code does not.

## Alternatives considered

- **Map BCL exception messages to zlib's text.** The BCL gives one message for every zlib
  data error, so there is nothing to map from.
- **Always print `invalid block type`.** Right for the one measured case, false for every
  other zlib fault; a message that is sometimes a lie is worse than a generic one.
- **Hand-write an inflater to report zlib's exact errors.** A large piece of work for the
  wording of an error message; revisit only if a conformance case needs it.
