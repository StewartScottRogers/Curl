# ADR-1324: az:// -X DELETE deletes one blob and refuses a container URL

- Status: Accepted
- Date: 2026-11-20
- Task: BL-5648
- Decided by Claude under Stewart's delegation.

## Context

ADR-1249 made `az://` refuse every `-X` but GET (and PUT with `-T`). ADR-1322 gave `s3://` DELETE; `az://` had the same gap.

## Decision

`curl -X DELETE az://account/container/blob` sends an authorized `DELETE /container/blob` (Delete Blob; the URL's query, for example `snapshot`, goes along), writes nothing and exits 0 on any status below 300 (Azure answers 202). A 404 or other 3xx-and-above status fails as the other operations do (exit 22). `-X DELETE` with `-T` stays refused (exit 4). On a container URL or a name ending in `/` it is refused with exit 3 before connecting, since deleting a container is far more destructive. `abfs://` and `abfss://` still refuse `-X DELETE` (exit 4); the Data Lake path delete is out of this task.

## Consequences

The `az://` refusal messages read "it sends only GET, DELETE with -X DELETE, or PUT with -T"; `abfs://` keeps the old text.
