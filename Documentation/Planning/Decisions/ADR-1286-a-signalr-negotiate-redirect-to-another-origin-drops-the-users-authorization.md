# ADR-1286: A SignalR negotiate redirect to another origin drops the user's Authorization

Status: Accepted. Decided by Claude under Stewart's delegation (BL-5311, BL-5377).

## Context
The negotiate reply may redirect to a `url`. The port sent the user's `-u`, `--oauth2-bearer` and `-H Authorization` there, also over cleartext after a `signalrs://` start, and the WebSocket upgrade after it.

## Decision
Like `-L` without `--location-trusted`, a redirect to another scheme, host or port drops every `Authorization` field and every `Cookie` field (from `-b` or `-H "Cookie:"`) from the negotiate request and the upgrade; an `accessToken` still adds `Authorization: Bearer <token>`. A same-origin redirect keeps them. `--location-trusted` (`CURLOPT_UNRESTRICTED_AUTH`) keeps them, cookies included: it reaches the handler as the new `HttpRequestOptions.SendsCredentialsToOtherOrigins`. An https-to-http redirect is not refused; it is sent without credentials or cookies, as `-L` does.

## Consequences
`SignalR:TP-Negotiate-Curl-1` in `Documentation/Conformance/Extended/SignalR.md` records it.
