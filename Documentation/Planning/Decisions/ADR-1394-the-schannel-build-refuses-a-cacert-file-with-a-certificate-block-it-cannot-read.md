# ADR-1394: The Schannel build refuses a `--cacert` file with a CERTIFICATE block it cannot read (BL-5884)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context
Fitness finding STD-203: in the Schannel build a `--cacert` (or `--proxy-cacert`) file holding a
malformed `CERTIFICATE` block was read with `X509Certificate2Collection.ImportFromPem`, whose
failures were swallowed, so the port trusted the blocks before it (exit 0) or dropped the whole file
(exit 60). curl 8.22.0 Schannel (Git for Windows) refuses such a file with exit 77. Measured with
`Record-CurlExchange.ps1 -Tls` and `-sS --ssl-no-revoke --cacert <file>` (B and E are the
`-----BEGIN CERTIFICATE-----` and `-----END CERTIFICATE-----` markers):

- `schannel: CA file '<path>' is not correctly formatted`: a B with no later `\n` + E (a lone B
  line, also after a good block; CR-only line ends).
- `schannel: failed to extract certificate from CA file '<path>': An error occurred during encode or
  decode operation.`: the body between the markers is empty or only white space.
- `schannel: failed to extract certificate from CA file '<path>': Cannot find the requested
  object.`: any other body that yields no certificate (`AAAA`, `AA`, `!` anywhere, `-` inside the
  base64, a `Proc-Type:` header, truncated base64), also before a good block.
- Read: missing `=` padding, white space and blank lines in the body, CRLF, text before B or after E
  on their lines, bytes after the DER, any text after the `=` padding, and an E inside the body's
  last line (the body ends there; scanning resumes after the next `\n` + E).
- An empty file, or one with only `TRUSTED CERTIFICATE` blocks, is not refused (exit 60 later).

## Decision
- `SchannelCaCertificateFile.Read` walks the text as curl's `add_certs_data_to_store`
  (`schannel_verify.c`) does: each B followed by CR or LF in turn (a B followed by anything else, or
  by the end of the text, ends the walk with no error, as curl's loop stops there), requiring
  `\n` + E after it. The body runs to the first E; white space is dropped and the text cut at the
  first `=`; an empty body is the encode or decode message. Otherwise the base64 is decoded with its
  padding supplied and a lone trailing character dropped, the DER cut to its first encoded value
  with `AsnDecoder` (so Linux and macOS read it as Windows' `CryptQueryObject` does), and loaded with
  `X509CertificateLoader`; any failure is "Cannot find the requested object.".
- A refusal is a `SchannelCaCertificateFileException`, a `CryptographicException` carrying curl's
  line (built in `TlsFailureMessages`); `ServerCertificateVerification.TrustAnchorsUnusable` returns
  exit 77 with it, as it picks out `CertificateRevocationListFileException` for exit 82.
- The name in the message is the `--cacert` path as given; for a `CURLOPT_CAINFO_BLOB` it is
  `(memory blob)`, the name `schannel_verify.c` passes for a blob (from curl's source, not measured).
- The OpenSSL build is unchanged (`OpenSslCaFile`).

## Consequences
The Schannel build no longer trusts part of a file curl refuses. The texts are Windows' messages,
pinned on every platform, since the reading is the port's own code.
