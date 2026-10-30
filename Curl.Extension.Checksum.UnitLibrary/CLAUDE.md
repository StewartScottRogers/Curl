# Curl.Extension.Checksum.UnitLibrary

The `--checksum <alg>:<hex>` extension (ADR-0999, ADR-1092). Namespace `Curl.Extension.Checksum`.
It references only `Curl.Extension.Abstractions.UnitLibrary` and the shared layers it brings.

- `ChecksumExtension` gives the `--checksum` option, its description and its hook activator, which
  `Curl.Extension.Composition` registers in its option, description and transfer hook catalogues.
- `ExpectedChecksum` reads `<alg>:<hex>` (`sha1`, `sha256`, `sha384`, `sha512` of FIPS 180-4, either case,
  exactly the digest's length) and writes a digest back in that form.
- `ChecksumTransferHook` hashes the response body through `HashingStream` on its way to curl's output and fails
  an otherwise successful transfer with exit 61 when the digest differs; `InvalidChecksumTransferHook` refuses
  every transfer with exit 2 when the value cannot be read.

Hashing uses the base class library's `IncrementalHash`. Held to the four quality gates in the root `CLAUDE.md`.
Its tests are in `Curl.Extension.Checksum.UnitTests`.
