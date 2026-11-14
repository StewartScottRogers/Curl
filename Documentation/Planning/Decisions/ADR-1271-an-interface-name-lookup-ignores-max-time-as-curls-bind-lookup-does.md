# ADR-1271: An --interface name lookup ignores -m, as curl's bind lookup does

- Status: Accepted
- Date: 2026-11-14
- Decided by Claude under Stewart's delegation (BL-5188)

## Context

AF-0743: `-m 1 --interface - URL` is exit 45 `Failed binding local connection end` in curl 8.22.0, after
about 3 seconds, but exit 28 in Curl after 1 second. curl's `bindlocal` resolves the interface host name with a
blocking lookup that never consults the transfer timeout, so `-m` cannot end it; Curl passed the transfer's
cancellation token (which `-m` fires) to the resolver.

## Decision

`LocalBindingAddressChooser` resolves an `--interface` host name with `CancellationToken.None`. The lookup runs
to its end and the bind then fails or succeeds as without `-m`. The token parameter of `ChooseAsync` stays for
its callers and is documented as unused. Measured: the reproduction now exits 45.

## Consequences

A slow name lookup in `--interface` can overrun `-m`, exactly as curl's does. The dial that follows the lookup
is still cancelled by `-m`.
