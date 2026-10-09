# ADR-0439 — The Windows Features line lists SSPI

- **Status:** Accepted
- **Date:** 2026-10-08

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-1796.

## Context

Gap finding GF-0003 measured sixteen upstream `tests/data` NTLM cases (test67, test68,
test81, test89, test91, test150, test162, test169, test170, test176, test239, test243,
test267, test775, test776, test1215) differing on Windows. ADR-0142 routes NTLM,
Negotiate and Kerberos through SSPI on Windows, so the type-1 message is SSPI's (flags
`0xa2088207` plus a VERSION field). But `curl -V` did not list `SSPI`, so the upstream
harness took Curl for a non-SSPI build and ran the `!SSPI` cases, which pin the
hand-built type-1 bytes.

The finding offered two fixes: list `SSPI` on Windows, or route Windows NTLM through the
hand-built `Curl.Ntlm.UnitLibrary` context.

## Decision

1. On Windows, `CurlVersionText.Lines` writes `WindowsFeaturesLine`: the shared
   `FeaturesLine` with `SSPI` after `SSL`, in curl's case-insensitive alphabetical order,
   as the Schannel reference build (the platform's curl) lists it.
2. Linux and macOS keep `FeaturesLine` unchanged: they have no SSPI.
3. Windows NTLM stays on SSPI (ADR-0142 stands).

## Why

- Matching the platform's curl is the standing rule, and the Windows reference build is
  an SSPI build: its NTLM type-1 message is SSPI's too. Listing `SSPI` makes the
  advertised features match the behaviour, so the `!SSPI` cases are excluded there exactly
  as they are for real curl on Windows.
- Moving Windows NTLM off SSPI would make Curl differ from the Windows reference in
  single sign-on (`--ntlm -u :`) and in the bytes on the wire.

## Consequences

- A later gap run measures the `!SSPI` cases as excluded on Windows; on Linux and macOS
  they run against the hand-built NTLM context as before.
- test775 (an over-long user name) requires `!SSPI` as well, so it is excluded on Windows
  too; whether the hand-built path refuses it as curl does is measured on Linux.
