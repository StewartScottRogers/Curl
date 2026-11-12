# ADR-1249 — s3, gs, az and oci refuse -I and a -X they cannot send, with exit 4 before any request

- Status: Accepted
- Date: 2026-11-11
- Task: BL-4904
- Decided by Claude under Stewart's delegation.

## Context

The outside fitness review of 2026-11-11 (EXT-004) found that `curl -X DELETE s3://bucket/key` and
`curl -I s3://bucket/key` sent `GET`, wrote the object and exited 0; `gs://`, `az://` and `oci://` did
the same. Each handler picked its operation only from `-T` and the URL's shape and ignored
`HttpRequestOptions.CustomMethod` and `ITransferContext.NoBody`. A script asking to delete an object
was told it had succeeded while nothing was deleted.

These schemes are Curl's extensions (ADR-0999); upstream curl has no answer to match. Two fixes were
open: implement DeleteObject and HeadObject (and their Cloud Storage, Blob and registry equivalents),
or refuse what is not implemented.

## Decision

Each handler first asks its `<Scheme>RequestMethod.RefusalOf`. `-I`/`--head`, and any `-X` other than
`GET` without `-T` or `PUT` with it, fail with exit 4 (`CURLE_NOT_BUILT_IN`, "a requested feature,
protocol or option was not found built-in") and the message
`<scheme>:// does not support -X <METHOD>; it sends only GET, or PUT with -T` (or `-I/--head`), before
any connection is opened. `-X GET` and `-X PUT -T` stay accepted because they name what is sent.

Refusing is the simplest change that makes a destructive request never report success unsent, which
is the task's goal; delete and head operations, when wanted, are new operations each extension can
add later and then accept their method here. Exit 4 says the option is not available for this
scheme, rather than blaming the URL (3) or the server (8). Each extension carries its own copy of
the check because extensions never reference each other (ADR-0999).

## Consequences

- No storage scheme sends a request other than the one it reports.
- Scripts that pass `-X DELETE` or `-I` to these schemes now fail fast with exit 4.
- Each scheme's conformance document lists the methods it sends.
