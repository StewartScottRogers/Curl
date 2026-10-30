# ADR-1097: OCI pushes blobs through a monolithic upload session and manifests with their media type

- Status: Accepted
- Date: 2026-10-29
- Decided by Claude under Stewart's delegation (BL-4293)

## Context

BL-4293 extends the `oci://` extension (ADR-0999, ADR-1093) so that `curl -T` pushes what the URL names, tested
against the OCI Distribution Specification 1.1.0, "Pushing blobs" and "Pushing manifests", and the Docker/Distribution
token authentication specification. Real curl has no such scheme, so there is no curl output to match; the choices
are which upload form to use, how to authenticate, and how a push fails.

## Decision

- **What `-T` pushes.** The URL keeps ADR-1093's grammar: `oci://registry/name@digest` pushes the upload as a blob,
  `oci://registry/name[:tag]` as a manifest under that tag (`latest` when none is given). The upload is read whole.
- **Blobs.** The SHA-256 digest of the upload is computed with the BCL and must equal the URL's digest exactly;
  otherwise, including a URL digest of another algorithm, the push fails before connecting with exit 25
  (`CURLE_UPLOAD_FAILED`), "The upload's digest X does not match the URL's Y.". Then `HEAD /v2/<name>/blobs/<digest>`:
  a 200 means the registry has the blob and nothing is uploaded; a 404 starts a monolithic upload, `POST
  /v2/<name>/blobs/uploads/` (empty body), which must answer 202 with a `Location`, then `PUT <Location>&digest=` or
  `?digest=` with the blob as `application/octet-stream`. The single-POST form and chunked `PATCH` uploads are not used:
  the monolithic session is the form every registry must support, and the upload is already in memory.
- **Manifests.** One `PUT /v2/<name>/manifests/<reference>` whose `Content-Type` is the manifest's top-level
  `mediaType` string, or `application/vnd.oci.image.manifest.v1+json` when it has none or is not a JSON object.
- **Authentication.** The first 401 of a push is answered as in ADR-1093, but the token is asked for the scope
  `repository:<name>:push,pull` in place of the challenge's, since a registry often challenges a `HEAD` with a pull-only
  scope. The token goes on every later request to the registry's own scheme, host and port, never to a `Location` on
  another host. A second 401 with the token fails as the registry's error (exit 67).
- **Failures.** Any other status fails as ADR-1093's registry error (exit 22, or 67 for denied); a 202 without a usable
  `Location` fails with exit 8. The final response's body, usually empty, is written to the output.
- The exception both flows throw is renamed `OciTransferException`, and the shared send-and-token code moves to
  `OciRegistrySession`, so the pull and push flows authenticate the same way.

## Consequences

Pushing a whole image is one `-T` per blob and then one for the manifest, in that order, as the specification requires.
Chunked and cross-repository mount uploads are not offered.
