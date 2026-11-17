# ADR-1290: oci:// hops match the no-proxy list with their own matcher (BL-5345)

Status: Accepted. Date: 2026-11-17. Decided by Claude under Stewart's delegation.

## Context

An `oci://` pull opens several connections (registry, token endpoint, blob redirect target), each through the transfer's proxy (ADR-0056). A hop whose host `--noproxy` or `NO_PROXY` exempts must go direct, as curl decides the proxy per host. ADR-1288 put the list on `ITransferContext.NoProxy` as text, but an extension cannot reference `Curl.Core`, where `NoProxyMatcher` lives.

## Decision

`Curl.Extended.Protocol.OciRegistry.UnitLibrary` reads the list with its own internal `HopNoProxyMatcher`, following the same curl 8.21.0 `Curl_check_noproxy` rules as `NoProxyMatcher` (`*` alone, comma and blank separators, domain-suffix names, address entries with `/bits`). `TransferEventsConnector` opens a matching hop with no proxy and every other hop with the transfer's proxy. Moving `NoProxyMatcher` into a shared layer was rejected for now: it would touch `Curl.Core` and every caller, which another task can do if a second extension needs it.

## Consequences

Two copies of the matching rules exist; a change to curl's rules must be made in both. A hop that the first URL's exemption left without a proxy stays direct, since the context carries only the first URL's proxy.
