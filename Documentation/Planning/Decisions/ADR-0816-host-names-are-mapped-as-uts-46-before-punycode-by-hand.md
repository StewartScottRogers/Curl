# ADR-0816 — A host name that is not ASCII is mapped as UTS #46 does, by hand, before punycode

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3548
- Decided by Claude under Stewart's delegation.

## Context

CGR-019 (third-party gap report, 2026-10-25): Curl sent `xn--bcher-2pa` for `BÜCHER` where curl
8.22.0 sends `xn--bcher-kva`, so a different host was contacted. `Curl.Console` is built with
`InvariantGlobalization`, and in that mode `IdnMapping.GetAscii` only punycodes the name as
written and `string.Normalize` changes nothing: no lower-casing beyond what `char.ToLowerInvariant`
does, no compatibility mapping, no removal of ignored characters, no composition. The unit tests
run with globalization on, where ICU does map, so they never saw it.

Measured with the Windows reference curl 8.22.0 (libidn2 2.3.8) through `Record-CurlExchange.ps1`
and `--connect-to ::127.0.0.1:PORT`: `BÜCHER` is `xn--bcher-kva`, `École` is `xn--cole-9oa`,
`straße` is `xn--strae-oqa` (non-transitional: `ß` kept). Characters outside the ANSI code page
cannot reach Windows curl's argv; the report's Linux results give `ΣΣ` `xn--4xaa`, `İstanbul`
`xn--istanbul-o0e`, `Ａb` `ab`, `ﬁle` `file`, `a`+soft hyphen or ZWJ+`b` `ab` and
`e`+U+0301+`cole` `xn--cole-9oa`.

## Decision

`CurlUrlHostNameMapping.Map` maps a name that is not ASCII before `CurlUrlHost` punycodes it:
it drops the characters UTS #46 maps to nothing, lower-cases with `char.ToLowerInvariant`
(`İ` becomes `i` and U+0307), turns full-width ASCII and the ligatures U+FB00 to U+FB06 into
ASCII, and composes a lower-case letter and a following combining mark from a table of the
canonical compositions in U+00C0 to U+024F, U+0370 to U+04FF and U+1E00 to U+1FFF, generated
once with globalization on. A name that is ASCII once mapped is used as it is. `ß` and `ς`
stay, as non-transitional processing keeps them. The URL API's own host keeps the decoded name.

Turning globalization on was rejected: it needs ICU on Linux and changes behaviour elsewhere.
Calling the platform's IDN library was rejected: libidn2 is not on every machine and the
library must stay testable without native code. The rest of the UTS #46 table is BL-3827.

## Consequences

Every host in CGR-019 maps as curl's does, in the shipped invariant binary and in the tests.
Validity rules (disallowed characters, joiner context) stay as they were; BL-3588 owns them.
