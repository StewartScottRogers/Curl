# ADR-1367 — An --oauth2-* flow binds its token to the --dpop key

- **Status:** Accepted
- **Date:** 2026-11-24

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-5911. Supersedes ADR-1347.

## Context

ADR-1347 made `--dpop` with an `--oauth2-*` flow fail closed until the token could be bound to the key (RFC 9449). The
two extensions may not reference each other, and `--dpop`'s hook ran first, so its proof was made before the token
existed.

## Decision

- **Order.** The OAuth2 hook runs before `--dpop`'s in `ExtensionTransferHookCatalogue`, so it adds
  `Authorization: DPoP <token>` and `--dpop`'s hook, inside it, signs the request carrying that header with its `ath`
  (§4.2, §7.1). The resource nonce retry and followed hops need nothing new, and a nonce retry asks for no second token.
  It also puts the token on every request the hooks between them (resumable upload, WebDAV tools, delta download,
  checksum, Sigstore) send through curl, and fetches it once for all of them.
- **Seam.** `Curl.Extension.Abstractions.Transfer` gains `IDpopKeySource` (a hook that holds a key) and `IDpopKey` (its
  RFC 7638 thumbprint and a proof for a `POST`). The hooking handler lends the first source among the hooks to the others
  as `TransferHookServices.DpopKeySource`. The private key stays in the DPoP extension.
- **Token endpoint (§5, §8).** Every token and refresh request carries a proof (`htm` `POST`, `htu` the endpoint without
  query or fragment, no `ath`). The endpoint's last `DPoP-Nonce` goes into the next proof, and a 400 `use_dpop_nonce`
  answer is retried once. The device authorization request carries none: it asks for no token.
- **Token type.** With a key only `token_type` `DPoP` is accepted; anything else, `Bearer` included, is exit 8. §5 lets
  a client keep an unbound token, but the user asked for a bound one, so it fails closed as ADR-1347 did. Without a key
  only `Bearer` is accepted, as before.
- **Cache.** The cache key holds the thumbprint (`dpop_jkt` in the file, absent for an unbound token), so a token is
  reused and refreshed only with the key it was bound to, and never replaces a Bearer one (ADR-1034).
- **Unreadable key.** No endpoint is asked; `--dpop`'s hook reports it (exit 26, ADR-1145).
- **`-H "DPoP: ..."`** beside a flow stays exit 2: curl cannot give the user's proof the token's `ath`.
- **Header token.** `--dpop` binds an `Authorization: DPoP` header's token in preference to `--oauth2-bearer`'s, which
  that header replaces in curl's request, and then adds no header of its own.
- The authorization request's `dpop_jkt` parameter (§10) is not sent: RFC 9449 makes it optional.
