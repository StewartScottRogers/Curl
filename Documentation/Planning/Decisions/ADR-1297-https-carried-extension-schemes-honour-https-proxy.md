# ADR-1297: The HTTPS-carried extension schemes honour HTTPS_PROXY (BL-5405)

Decided by Claude under Stewart's delegation.

## Status

Accepted

## Context

`ProxySelector` reads `<scheme>_proxy`, its upper-case form, `all_proxy` and `ALL_PROXY` for a URL, and for `wss` also `https_proxy` then `HTTPS_PROXY` (ADR-0024). The extension schemes `oci`, `s3`, `gs`, `az` and `signalrs` always speak HTTPS, yet a user who set only `HTTPS_PROXY` went direct (finding EXT-076).

## Decision

For `oci`, `s3`, `gs`, `az` and `signalrs`, the variables are `<scheme>_proxy`, `<SCHEME>_PROXY`, `https_proxy`, `HTTPS_PROXY`, `all_proxy`, `ALL_PROXY`, in that order, as for `wss`. `http_proxy` is never read for them. The rule is limited to these extension schemes: curl's own schemes are unchanged. `--noproxy` and `NO_PROXY` apply as before. The rule is in `Documentation/Protocols.md` and each scheme's `--extended-help` text.

## Consequences

The one selector serves every extension, so no extension reads a variable itself.
