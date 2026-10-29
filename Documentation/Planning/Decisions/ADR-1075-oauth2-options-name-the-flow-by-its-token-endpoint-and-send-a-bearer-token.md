# ADR-1075: The OAuth2 options name the flow by its token endpoint and send a Bearer token

- Status: Accepted
- Date: 2026-10-29
- Decision: Decided by Claude under Stewart's delegation (BL-4161)

## Context

The OAuth2 extension (ADR-1025) has its wire format, HTTP client (ADR-1030), token cache with refresh
(ADR-1034) and three flows (ADR-1045). It still needs command-line options, a transfer hook that gets
the token before the transfer (ADR-1039 gives a hook curl's connector and a loopback listener), and its
description in `curl --extensions` and `curl --ai-help extensions`. Upstream curl has no OAuth 2.0 grant
support (only `--oauth2-bearer`, which sends a token the user already has), so there is no curl output to
match; the extension follows RFC 6749, RFC 7636 and RFC 8628 as published (with RFC 6750 §2.1 for the
header and RFC 8252 §7.3 for the loopback redirect) and never changes what curl's own options do.

## Decision

- One flow option turns the extension on and names the token endpoint (RFC 6749 §3.2):
  `--oauth2-client-credentials <URL>` (§4.4), `--oauth2-device <URL>` (RFC 8628) or
  `--oauth2-pkce <URL>` (§4.1 with RFC 7636 `S256`). Giving two is an error.
- Companion options complete it: `--oauth2-client-id` (required), `--oauth2-client-secret` (sent by
  HTTP Basic, §2.3.1, which every server must support; without it the `client_id` goes in the body),
  `--oauth2-scope`, `--oauth2-device-url` (required with `--oauth2-device`), `--oauth2-authorize-url`
  (required with `--oauth2-pkce`), `--oauth2-redirect-port` (0, the default, takes any free port, as
  RFC 8252 §7.3 lets a native client do) and `--oauth2-token-cache <directory>`. Without the last, tokens
  are cached in the default directory ADR-1034 finds; an empty value turns the cache off. Companions
  without a flow option do nothing, as curl ignores an option nothing uses.
- Every name starts `oauth2-` and none is a curl option name; `--oauth2-bearer` stays curl's own.
- A missing or wrong option fails the transfer with exit 2 (`CURLE_FAILED_INIT`, curl's code for a
  bad option argument) and a message naming the option. The flow's failure fails the transfer with
  the flow's code (ADR-1045). In both cases the transfer's request is never sent.
- The token is sent as `Authorization: Bearer <token>` added after curl's own request headers
  (ADR-1039's `WithRequestHeaders`). A token whose `token_type` is not `Bearer` (case-insensitive) fails
  with exit 8, since RFC 6749 §7.1 forbids a client to use a token type it does not understand.
- A host that gives the hook no `TransferHookServices` gets exit 2: the flows cannot connect without
  curl's connector.
- With `CURL_EXTENSIONS=0` none of the options is registered, so each is refused as an unknown option
  (exit 2); without a flow option no hook is activated and every transfer is unchanged.

## Left out

- `client_secret_jwt`, `private_key_jwt` and mutual-TLS client authentication: they need key options
  beyond the three grants this task asks for; Basic and body `client_id` cover RFC 6749 §2.3.1.
- The implicit and password grants: RFC 9700 (OAuth 2.0 Security BCP) says not to use them.
- Opening a browser: the PKCE flow prints the authorization URL to standard error (ADR-1045), as curl
  never starts other programs.

## Consequences

The extension registers ten lines in `ExtensionOptionCatalogue.cs`, one in
`ExtensionDescriptionCatalogue.cs` and one in `ExtensionTransferHookCatalogue.cs`; it adds no URL scheme,
so `ExtensionCatalogue.cs` gets no line.
