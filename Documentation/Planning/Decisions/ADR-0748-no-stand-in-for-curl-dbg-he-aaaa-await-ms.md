# ADR-0748 — No stand-in for `CURL_DBG_HE_AAAA_AWAIT_MS`

- Status: Accepted
- Date: 2026-10-21
- Task: BL-2837
- Decided by Claude under Stewart's delegation

## Context

Upstream case 2100 sets `CURL_DBG_HE_AAAA_AWAIT_MS=60000`. In curl's debug build that holds back
the DoH AAAA answer so the A answer wins the happy-eyeballs race, which makes the order of curl's
own trace and connect deterministic. BL-2837 planned a harness stand-in that delays the DoH AAAA
reply on the harness clock.

Measured on 2026-10-21, after BL-2919, BL-2920 and BL-2928: with `PlatformFor` reporting `Debug`
to a `DoH` case that runs `--doh-url` (`RunsCurlWithDohUrl`), cases 2100, 2101 and 2117 each end
`Passed` through `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` with no delay at all.

## Decision

No stand-in for `CURL_DBG_HE_AAAA_AWAIT_MS`. The variable only makes debug curl's order
deterministic; Curl's order is deterministic already: `DohDnsResolver` writes its trace lines in a
fixed order (ADR-0380), and case 2100's `<strippart>` removes the family byte so the order of the
two DoH POSTs in `<protocol>` does not matter. A delay would add harness code that changes no
outcome. The three cases go on `PassingUpstreamCases.txt`.

## Consequences

- If a later case depends on the A answer arriving first in a way Curl's fixed order does not
  give, that case's task adds the stand-in then, on the injected `TimeProvider`.
