# ADR-1354: gs:// downloads send a Range header for -r and -C; uploads and listings refuse them (BL-5912)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context

The Google Cloud Storage handler ignored `-r` and `-C`, so a resumed download (`-C -`) appended the whole object to the
partial file and exited 0. `s3://` sends a `Range` header (BL-5873); `az://` refuses both options (ADR-1348). Cloud
Storage's "GET Object" takes a `Range` header, and ADR-1249 says a storage scheme never sends a request other than the
one it reports.

## Decision

- An object download sends `Range: bytes=<offset>-` for `-C` with an offset above 0, else `Range: bytes=<text>` for
  `-r <text>`, as typed. With an HMAC key the header is signed.
- Replies are treated as `s3://` and `https://` treat them: a 206 body is written; a resume answered 416, or 200 with a
  `Content-Length` equal to the offset, succeeds with nothing written; a resume answered 200 otherwise fails with exit
  33 and writes nothing; `-r` answered 200 writes the whole body, and answered 416 fails with exit 22.
- An upload or a listing with `-r`, or with `-C` other than offset 0, fails with exit 4 before any connection
  (`GcsByteRangeRefusal`): a resumed listing would append the whole listing to the partial file.

Honouring was chosen over refusing every transfer, as `az://` does, because the XML API documents the header, the
`s3://` behaviour already answers every status, and a resumed download is what `-C` is for.

## Consequences

`-C -` leaves a partial download byte-identical to the object or fails. A server that ignores `-r` hands over the whole
object, as it does over `https://`.
