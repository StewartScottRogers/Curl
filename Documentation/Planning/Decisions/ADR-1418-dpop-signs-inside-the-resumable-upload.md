# ADR-1418: `--dpop` signs inside `--resumable-upload`, one proof per request

- Status: Accepted. Decided by Claude under Stewart's delegation.
- Date: 2026-11-26
- Task: BL-6029 (fitness finding EXT-116)

## Context

BL-5866 put the `--dpop` hook outside the `--resumable-upload` hook, because the upload then wrote its requests
itself and only an outer hook's headers reached them. Since ADR-1379 the upload sends each of its requests through
the rest of curl's transfer. An outer `--dpop` hook therefore made one proof, for `PUT` to the original URL, and
`OPTIONS`, `POST` and `PATCH` all carried it: a wrong `htm`, a wrong `htu` and a replayed `jti` (RFC 9449 §4.2).

## Decision

- `ExtensionTransferHookCatalogue` lists the ResumableUpload hook before the DPoP hook, so the DPoP hook runs for
  each of the upload's requests and signs that request's method and URL with its own `jti`.
- The OAuth2 hook stays before both (ADR-1367): its `Authorization: DPoP` header is copied onto each upload request
  to the same origin, and the DPoP hook binds each proof to it by `ath`.
- A `DPoP-Nonce` retry (RFC 9449 §8, §9) now repeats one upload request, so the DPoP hook rewinds that request's
  upload first. When the upload cannot be rewound, it does not retry and the refusal is the answer, rather than
  sending a truncated body.

## Consequences

A nonce refusal in the middle of an upload retries only the refused request, not the whole upload. The order is
pinned by `DpopWithResumableUploadOrderTests` in `Curl.Extension.Composition.UnitTests`.
