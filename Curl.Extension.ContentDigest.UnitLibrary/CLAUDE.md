# Curl.Extension.ContentDigest.UnitLibrary

The HTTP digest fields extension (ADR-0999, ADR-1094), tested against RFC 9530. Namespace
`Curl.Extension.ContentDigest`. It references only `Curl.Extension.Abstractions.UnitLibrary` and the shared
layers it brings.

- `ContentDigestExtension` gives the `--content-digest` flag, its description and its hook activator, which
  `Curl.Extension.Composition` registers.
- `ContentDigestTransferHook` adds `Want-Content-Digest` and `Want-Repr-Digest` (§4) to every HTTP request and a
  `-d` body's SHA-256 `Content-Digest` (§2) and `Repr-Digest` (§3), leaving any `-H` gave; it hashes the
  response body through a `DigestingStream` and fails the transfer with exit 56 when a SHA-256 or SHA-512 digest
  the server sent does not match. `Repr-Digest` is not checked on a 206; neither is checked on a 304, under
  `-I`, or when `--compressed` decoded a content coding.
- `DigestField` reads and writes the fields' Structured Fields Dictionary; `DigestAlgorithms` names the two
  "Active" algorithms (§5).

Tested against the specification, not against curl. Held to the four quality gates in the root
`CLAUDE.md`. Its tests are in `Curl.Extension.ContentDigest.UnitTests`.
