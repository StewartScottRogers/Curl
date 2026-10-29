# ADR-0809: `--http2` keeps running on Windows; GF-0098 is answered by the measurement, not by a refusal

- Status: Accepted
- Date: 2026-10-23
- Decision: Decided by Claude under Stewart's delegation (BL-3431), within Stewart's own decision ADR-0871

## Context

Gap finding GF-0098 measures upstream case test1704 (`--http2` against an HTTP/2-only reply) as a
difference on Windows: Curl sends the `Upgrade: h2c` request and exits 8 (`Version mismatch (from HTTP/1
to HTTP/2)`, ADR-0788), while the Schannel reference curl 8.22.0, built without nghttp2, sends nothing
and exits 2 (`option --http2: the installed libcurl version does not support this`). Curl's answer is
the one upstream's `<verify>` asks for; the reference has no `h2c` feature, which test1704's
`<features>` requires.

To measure `match`, Curl would have to refuse `--http2` on Windows again. ADR-0397 once did that, and
Stewart reversed it in ADR-0871 ("do it", 2026-10-22). ADR-0808 then matched the reference's `-V` line
but kept the option running.

## Decision

- Curl keeps running `--http2`, `--http2-prior-knowledge` and `--proxy-http2` on Windows (ADR-0871).
  `CommandLineOptionTable.HttpVersionRows` is unchanged.
- Undoing a decision Stewart made himself is not covered by his delegation, so BL-3431 goes to Stewart
  rather than refusing the option.
- The recommended close for GF-0098 is office work: the behaviour measurement
  (`Gap/Tools/ConvertTo-BehaviourMeasurement.ps1`, rule 4) should exclude a case whose `<features>` names
  one the reference's `curl -V` lacks (`h2c` here) even when the harness ran it, not only when it skipped it.

## Consequences

- `behaviour:test1704` stays a Windows difference until Stewart rejects GF-0098 or the gap office
  changes its measurement; Linux and macOS (nghttp2-built references) are unaffected.
- A Windows script relying on the refusal still sees a transfer, the trade-off ADR-0871 accepted.
