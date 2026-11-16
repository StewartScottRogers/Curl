# ADR-1273: A cookie whose Domain equals the host stays a host cookie in the jar, as curl 8.22.0 writes it

- Status: Accepted
- Date: 2026-11-16
- Decided by Claude under Stewart's delegation (BL-5292)

## Context

BL-5292, filed from Surl's measurement beside curl 8.21.0, asked for `Domain=<host>` to be saved as
`.<host>	TRUE` (cases 798 and 1136). The port's oracle is the platform's curl, and the vendored cases in
`CurlTestCases/Original/` are curl 8.22.0's. Measured 2026-11-16 with `Record-CurlExchange.ps1` and the
installed curl 8.22.0 (Schannel build) on case 798's command line and response, the jar line is
`#HttpOnly_localhost	FALSE	/p4	TRUE	0	flavor	tasty`; the 8.22.0 `<file>` sections of 798 and 1136 say the same
(`z-1.compute-1.amazonaws.com	FALSE	/`). The port writes exactly that, so the cases correctly sit on the ratchet.

## Decision

No change to `SetCookieParser` or `NetscapeCookieFile`. The 8.21.0 jar lines differ from 8.22.0's; the port follows
8.22.0. The ratchet puzzle is answered by the version: the runner compares against 8.22.0's `<file>`, Surl's copy
holds 8.21.0's.
