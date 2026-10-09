# ADR-0455: The upstream case harness answers NTLM with the hand-built context and screens cases that need the internet

- Status: Accepted
- Date: 2026-10-08
- Decided by Claude under Stewart's delegation (BL-1858, GF-0001)

## Context

Curl's `curl -V` lists no `SSPI`, so `UpstreamCurlPlatform` leaves it off and upstream's `!SSPI`
NTLM cases (test67, 68, 69, 81, 209, 213, 265, 1008, 1021) run on Windows too. They expect curl's
hand-built type-1 message, but on Windows ADR-0142's router sends NTLM to SSPI, whose type-1
differs. test2043 (`--ssl-no-revoke -I https://revoked.badssl.com/`) names no server and needs
the internet, which the in-process harness never reaches.

## Decision

1. The dialing `CurlComposition.CreateRunner` takes `usesHandBuiltNtlm`; when set, NTLM to the
   origin and to a CONNECT proxy is answered by the hand-built context on every platform
   (`HandBuiltNtlmSecurityContextFactory`), Negotiate and Kerberos still go through the router.
   The harness sets it, so it runs Curl as the non-SSPI build its feature list describes. The
   executable is unchanged: on Windows it still answers NTLM with SSPI, as the Schannel build does.
2. `UpstreamCaseScreening` skips a case that names no `<server>`, expects exit 0 and whose command
   holds an `http://` or `https://` URL to a dotted host name, with the reason
   "the case reaches <host> on the internet, which the harness does not". Cases expecting a failure
   (test467's `http://example.com`) still run, since they fail before any connection.

## Consequences

The nine NTLM cases are listed in `PassingUpstreamCases.txt` on every platform; test2043 is
skipped with a stated reason rather than failing with exit 6.
