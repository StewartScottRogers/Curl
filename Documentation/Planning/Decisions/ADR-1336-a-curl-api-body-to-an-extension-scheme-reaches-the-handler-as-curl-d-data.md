# ADR-1336: A Curl.Api body to an extension scheme reaches the handler as `-d` data

- Status: Accepted (2026-11-22, BL-5758)
- Decided by Claude under Stewart's delegation.

## Context

Surly reported five times that a `Curl.Api` body did not reach an extension as the console's `-d`
data does (BL-5611, BL-5681, BL-5745, BL-5754, BL-5755), each fixed per scheme. The console's `-d`
lands on the transfer context's `PostData`; extensions read `PostData` (some also `Upload`).
`Curl.Api` made a body into `CURLOPT_POSTFIELDS`, or an upload for `UploadSchemes`, but the managed
engine copied `PostFields` into `PostData` only for MQTT URLs, and a `PUT` body became an upload for
every URL, whatever its scheme.

## Decision

One rule, supersedes the per-scheme parts of ADR-1316 and the BL-5594, BL-5611 and BL-5681 notes:

1. A body for a scheme `UploadSchemes` lists is an upload, as `-T` (unchanged).
2. A body for any other scheme an extension serves (`ExtensionListing.Schemes`) is `-d` data
   whatever the method: post fields, with `PUT` kept as the custom request, never an upload
   (`UploadSchemes.ServedByExtension`).
3. The engine gives the handler `PostFields` as `PostData` for every URL whose scheme curl itself
   does not serve (`ManagedEasyTransfer.PostDataOf`), as well as `mqtt` and `mqtts`, so (2) reaches
   `PostData` exactly as the console's `-d` does. curl's own schemes are unchanged.

`ExtensionBodyParityTests` walks the extension registry, so an extension added later is covered
without editing it. A new extension that reads only `Upload` must be added to `UploadSchemes`.

## Consequences

mcp, mcps, mcp+stdio, webtransport, coap, dns, socketio and every later extension scheme receive a
`Curl.Api` body with no per-scheme change.
