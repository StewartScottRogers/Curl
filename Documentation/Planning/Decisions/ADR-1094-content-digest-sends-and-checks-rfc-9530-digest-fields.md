# ADR-1094 — --content-digest sends and checks RFC 9530 digest fields

- **Status:** Accepted
- **Date:** 2026-10-29

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4134.

## Context

BL-4134 adds HTTP digest fields as an extension (ADR-0999) in `Curl.Extension.ContentDigest.UnitLibrary`: an option
that checks the server's `Content-Digest` and `Repr-Digest` against the body and adds them to uploads. curl has no
such option, so the extension is tested against RFC 9530 (February 2024), the version followed, and never changes
what curl does without the option.

## Decision

- **Option.** One flag, `--content-digest`, with no argument; curl has no option of that name (`--digest` is HTTP
  Digest authentication, a different thing). It is a transfer hook (ADR-1007), registered in the option, description
  and transfer hook catalogues; it has no URL scheme, so it adds no line to `ExtensionCatalogue`.
- **Request (§2, §3, §4).** Every HTTP request asks for the server's digests with `Want-Content-Digest` and
  `Want-Repr-Digest`, both `sha-256=10, sha-512=5`. A `-d`/`--data` body (a `BytesBody`) gets its SHA-256
  `Content-Digest` and `Repr-Digest`: curl sends that body whole and with no content coding, so the content and the
  representation data are the same bytes and the two fields carry the same value. A field `-H` already gave, in
  any case, is left to it.
- **Algorithms (§5, §6).** SHA-256 is sent; SHA-256 and SHA-512, the registry's two "Active" algorithms, are checked.
  Members for any other algorithm (the deprecated md5, sha, unixsum, unixcksum, adler, crc32c, or an unknown one)
  are ignored, as §2 lets a recipient ignore an algorithm it does not support.
- **Parsing.** Each field is a Structured Fields Dictionary (RFC 8941 §3.2) of Byte Sequences; every field line of
  one name is joined with commas, parameters are ignored, a repeated key's last member wins, and a value that does
  not parse is ignored whole (RFC 8941 §4.2).
- **Response.** The body is hashed as curl's handler writes it, without buffering it. `Content-Digest` is checked
  on every response with content; `Repr-Digest` on every one but a 206, whose body is only part of the
  representation. Neither is checked for a 304, under `-I` (no content), or when `--compressed` was given and the
  response named a content coding other than `identity`, since curl then writes the decoded bytes, which neither
  digest covers. Trailer fields count, as `TransferReport.ResponseHeaders` holds them.
- **Mismatch: exit 56.** curl has no exit code for a body that fails an integrity check; 56 (`CURLE_RECV_ERROR`,
  "failure in receiving network data") says the data received was wrong, which is the nearest. The message is
  `<field> <algorithm> does not match the response body`, and the body has already been written, as with any
  receive error after the first byte. The transfer's report is kept, so `-w` still sees the response.

## Left out, and why

- **`-T` and `-F` uploads.** A `-T` file or stdin and a `-F` multipart body reach the hook as streams, which it
  cannot hash before they are sent without reading them twice; HTTP trailers, where a digest of streamed content
  belongs, are not something curl's requests send. Their requests still carry the `Want-` fields.
- **Choosing the algorithm, honouring the server's `Want-` fields.** One flag keeps the option a drop-in addition;
  SHA-256 is what every implementation must be able to verify in practice, and the server's preferences only
  matter to a client sending many requests.
- **Checking digests of decoded content.** See above: RFC 9530 defines no digest over decoded bytes.

## Consequences

- `curl --content-digest URL` fails with exit 56 when the server's digest does not match; without the option, or
  with `CURL_EXTENSIONS=0` (when the option is refused as unknown, exit 2), every transfer is unchanged.
- `curl --extensions` and `curl --ai-help extensions` list it.
