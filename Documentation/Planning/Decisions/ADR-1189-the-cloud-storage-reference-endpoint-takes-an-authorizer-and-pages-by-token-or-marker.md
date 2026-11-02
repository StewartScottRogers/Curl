# ADR-1189: The Cloud Storage reference endpoint takes an authorizer and pages by token or marker

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4475
- Decided by Claude under Stewart's delegation.

## Context

BL-4475 measures the `gs://` client against the Cloud Storage XML API's object operations with a reference endpoint
written from the specification. BL-4476 must add V4 HMAC signature checking without editing BL-4475's files, and the
client follows either `NextContinuationToken` or `NextMarker` to the next listing page.

## Decision

- `GcsReferenceServer` puts every request to an injected `IGcsReferenceAuthorizer` before anything else; BL-4475
  ships the anonymous and the OAuth 2.0 bearer authorizers, and BL-4476 adds its signature authorizer as a new file
  in `GoogleCloudStorage/Signing/`.
- `GcsListPagination` chooses whether a truncated page names the next by an opaque `NextContinuationToken` (Base64 of
  the page's last entry, as Cloud Storage answers `list-type=2`) or by `NextMarker` (the last entry itself, as a
  listing without `list-type=2` and some emulators answer). In both modes the client sends the value back as
  `continuation-token`, and the endpoint resumes after it.
- The endpoint's `Date` header requirement is listed `n/a` here: only signed requests need it, so it belongs to part 2.
- Curl's error line ends in the platform's line end on standard error, so the cases pin it with `Environment.NewLine`.

## Consequences

The signing part adds a file and matrix rows only. A defect the cases find in the extension, such as
`STORAGE_EMULATOR_HOST` read from the process rather than the run's environment (BL-4514), is listed in
`KnownFailures.txt` with its fix task.
