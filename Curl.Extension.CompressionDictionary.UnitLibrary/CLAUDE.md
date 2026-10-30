# Curl.Extension.CompressionDictionary.UnitLibrary

The Compression Dictionary Transport extension, `--compression-dictionary <dir>` (RFC 9842; ADR-0999, ADR-1100,
ADR-1154). Namespace `Curl.Extension.CompressionDictionary`. It references only
`Curl.Extension.Abstractions.UnitLibrary` and the shared layers it brings, and `Curl.Zstandard.UnitLibrary` for
`dcz` (ADR-1107).

- `CompressionDictionaryExtension` gives the option, the description and the hook to `Curl.Extension.Composition`.
- `CompressionDictionaryTransferHook` stores `Use-As-Dictionary` responses, advertises the best matching dictionary
  (`Available-Dictionary`, `Dictionary-ID`, `Accept-Encoding: dcb, dcz`, after curl's `--compressed` codings when that is
  given, running curl's handler with content decoding off and decoding them through `CompressedResponseBody`) unless `--raw` or `-H` sets the
  codings, and decodes a `dcb` or `dcz` body through `DictionaryCompressedBody` (exit 61 when it cannot).
  `DictionaryTransferRequest` decides which transfers it takes part in.

- `UseAsDictionary` parses the `Use-As-Dictionary` response header (§2.1) through
  `StructuredFieldDictionaryParser`, an RFC 8941 dictionary parser (`StructuredFieldMember`, `StructuredFieldItem`).
- `DictionaryRequestHeaders` builds `Available-Dictionary` (§2.2) and `Dictionary-ID` (§2.3).
- `DictionaryMatchPattern` resolves a `match` pattern against the dictionary's URL and matches request URLs,
  same-origin only (§2.1.1, §2.2.2), in the URLPattern subset ADR-1100 describes.
- `DictionaryStreamHeader` checks the magic and dictionary hash at the start of a `dcb` (§4) or `dcz` (§5) stream.
- `DictionaryStore` keeps `<sha256>.dictionary` and `<sha256>.metadata` files (`StoredDictionary`, encoded by
  `StoredDictionaryEncoding`) in a directory and picks the longest-match dictionary for a URL (§2.2.2).
- `Brotli/BrotliDictionaryDecoder` decodes a `dcb` body's Brotli stream with the dictionary as a prefix (ADR-1114);
  a failure is a `Brotli/BrotliFormatException`.
- `Zstd/DictionaryZstandardDecompressor` decodes a `dcz` body's Zstandard frames against the dictionary as a raw
  content dictionary, with a window of at most 8 MiB or 1.25 times the dictionary (§5); a failure is a
  `Zstd/DictionaryZstandardException` naming the `ZstandardDecodeError`.

Tested against the specification, not against curl. Held to the four quality gates in the root `CLAUDE.md`.
Its tests are in `Curl.Extension.CompressionDictionary.UnitTests`.
