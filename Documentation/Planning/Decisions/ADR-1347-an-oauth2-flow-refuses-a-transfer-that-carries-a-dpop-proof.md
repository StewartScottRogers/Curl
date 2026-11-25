# ADR-1347 — An --oauth2-* flow refuses a transfer that carries a DPoP proof

- **Status:** Superseded by [ADR-1367](ADR-1367-an-oauth2-flow-binds-its-token-to-the-dpop-key.md), which binds the token; only a `-H "DPoP: ..."` beside a flow is still refused
- **Date:** 2026-11-24

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-5898.

## Context

Fitness finding EXT-123: with `--dpop` and an `--oauth2-*` flow, the flow's token went as `Authorization: Bearer` beside
a DPoP proof with no `ath`, and the command exited 0. The token was bound to no key, which defeats `--dpop`.
`--dpop`'s hook (ADR-1145) runs before the OAuth2 hook (ADR-1075), so its proof is made before the token exists. Binding
the token needs a proof on the token request (RFC 9449 §5), a `DPoP` token type accepted and cached, `Authorization: DPoP`
and an `ath` proof on the resource request (§4.2, §7.1), and a way for the two extensions, which may not reference each
other, to share the key through `Curl.Extension.Abstractions`.

## Decision

- Until that binding is built, the combination fails closed: an HTTP transfer that already carries a `DPoP` header with a
  value, from `--dpop` or `-H "DPoP: ..."`, makes the OAuth2 hook fail it with exit 2 (`CURLE_FAILED_INIT`) and
  `The --oauth2-* options cannot be combined with --dpop or -H "DPoP: ...": curl cannot yet bind the flow's access token
  to a DPoP proof.` No endpoint is asked and the transfer is not sent.
- A `-H "DPoP: ..."` the user gave is refused too: its proof cannot hold the `ath` of a token the flow has yet to get.
- The OAuth2 hook sees the proof as a request header, so `--dpop`'s hook stays ahead of it in
  `ExtensionTransferHookCatalogue`; a Composition test pins that order.
- A transfer that is not HTTP is unchanged, as neither extension adds anything to it.

This is not a decision to leave the binding out: a task on the board builds it and replaces this refusal.
