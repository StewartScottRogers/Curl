# ADR-1425: An upload keeps an s3 or gs bucket name as typed

- Status: Accepted
- Date: 2026-11-26
- Decided by Claude under Stewart's delegation (BL-6031; fitness finding EXT-120)

## Context

`UploadTransferUrl` writes a `-T` URL back out from its parsed parts, as curl does, and
`CurlUrl.Host` holds an all-digit or hex host as a dotted quad. The S3 and GCS handlers read
the bucket from the URL's original text (BL-5870), which for an upload was already rewritten,
so `-T f s3://12345678/k` uploaded to bucket `0.188.97.78`. The handlers cannot recover the
name: they are given only the rewritten URL.

## Decision

`UploadTransferUrl` writes the host of an `s3://` or `gs://` URL as typed, because it is a
bucket name, not a server. The rest of the URL is normalised as before, and so is the host of
every other scheme, including the schemes curl does not support. A bracketed or empty
authority is written as `CurlUrl.Host` holds it. `%{url_effective}` is worked out separately
and is unchanged, the same for an upload as for a download.

## Alternatives

- Keep every host curl does not know as typed: changes what curl prints for an unsupported scheme.
- Pass the typed URL to handlers beside the transfer URL: a shared contract change for two schemes.
- `az://` is left alone: its mapping treats a numeric host as an IP address on purpose.
