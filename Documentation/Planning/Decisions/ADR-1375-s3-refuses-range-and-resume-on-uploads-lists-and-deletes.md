# ADR-1375: s3:// refuses -r and -C on uploads, lists and deletes with exit 4 (BL-5914)

Status: Accepted. Decided by Claude under Stewart's delegation.

## Context
An `s3://` download sends a `Range` header for `-r` and `-C` (BL-5873, BL-5897), but uploads, lists and `-X DELETE`
ignored both and exited 0. A script then believed part of a transfer had been done when all of it had. ADR-1249 says a
storage scheme never sends a request other than the one it reports, and ADR-1348 refuses the same options for `az://`.

## Decision
With `-T`, a prefix URL (an empty key or one ending in `/`) or `-X DELETE`, `-r`, or `-C` other than offset 0 (`-C -`
on an upload included), fails with exit 4 and `s3:// does not support <option> on an upload, list or delete; only a
download sends a range` before any connection (`S3ByteRangeRefusal`). `-C 0` asks for the whole transfer and is let
through. Downloads are unchanged.

Refusing was chosen over honouring them: S3 has no ranged PutObject or DeleteObject, a resumed upload would need the
state of an earlier multipart upload, and a range of a listing has no meaning the S3 API gives it.

## Consequences
A script that passed `-r` or `-C` to these operations now gets exit 4 and nothing is sent.
