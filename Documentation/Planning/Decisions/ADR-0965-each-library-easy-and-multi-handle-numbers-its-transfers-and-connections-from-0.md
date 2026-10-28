# ADR-0965: Each library easy and multi handle numbers its transfers and connections from 0

- Status: Accepted
- Date: 2026-10-28
- Decided by Claude under Stewart's delegation (CGR-247, BL-3776)

## Context
`CURLINFO_XFER_ID` and `CURLINFO_CONN_ID` of a new libcurl 8.22.0 easy handle's first transfer are 0, as each
`curl_easy_perform` runs over a multi handle of its own and a multi numbers its transfers and connections from 0.
Curl numbered them across the whole engine, so the second handle answered 1, and so on.

## Decision
`ManagedEngineSharedState.WithOwnCaches` (a library easy handle, a duphandle copy, a multi handle) also gives the
state a performer of its own (`TransferPerformer.StartingAtZero`), so the numbering is per handle or multi. The curl
tool's engine-wide state and a share-attached perform keep the engine's performer.

## Consequences
Easy handles in one multi handle still count on together; two easy handles of one engine each start at 0.
