# ADR-1322: s3:// -X DELETE deletes one object and refuses a bucket URL

- Status: Accepted
- Date: 2026-11-20
- Task: BL-5647
- Decided by Claude under Stewart's delegation.

## Context

ADR-1249 made `s3://` refuse every `-X` but GET (and PUT with `-T`), so deleting needed `https://` with `--aws-sigv4`. The cookbook asked for DeleteObject.

## Decision

`curl -X DELETE s3://bucket/key` sends a SigV4-signed `DELETE /bucket/key` (the URL's query goes along, for example `versionId`), writes nothing and exits 0 on any status below 300 (S3 answers 204). A 404 or other 3xx-and-above status fails as the other s3 operations do (exit 22). `-X DELETE` with `-T` stays refused (exit 4). `-X DELETE` on a bucket URL or one ending in `/` is refused with exit 3 before connecting: deleting a bucket (DeleteBucket) is far more destructive than deleting an object and `-X DELETE` on a prefix has no S3 meaning. Any other `-X` is still refused with exit 4.

## Consequences

The refusal messages now read "it sends only GET, DELETE with -X DELETE, or PUT with -T". `az://` has the same gap and is filed as its own task.
