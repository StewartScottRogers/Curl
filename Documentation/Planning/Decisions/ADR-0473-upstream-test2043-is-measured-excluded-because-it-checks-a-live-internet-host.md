# ADR-0473: Upstream test2043 is measured `excluded`, because it checks a live internet host

- Status: Accepted
- Date: 2026-10-10
- Task: BL-2020 (gap finding GF-0001, item `behaviour:test2043`)
- Decided by Claude under Stewart's delegation

## Context

Upstream test2043 runs `--ssl-no-revoke -I https://revoked.badssl.com/` on a Schannel build and
expects exit 0. It names no `<server>`: the host is badssl.com's public server, whose certificate
its operator revokes on purpose, and the case checks that Schannel's revocation check is switched
off, so the revoked certificate is accepted. Its `<verify>` holds nothing but the exit code.

ADR-0455 made `UpstreamCaseScreening` skip such a case in the in-process ratchet ("the case reaches
revoked.badssl.com on the internet, which the harness does not"). The gap office, which runs the
same harness, measured exit 52 and left `behaviour:test2043` open. BL-2020 asked for the case to
pass in process or for a recorded reason to measure it `excluded`.

An in-process stand-in cannot reproduce what the case checks:

- The command gives no `--cacert`, `--insecure` or `--ssl-revoke-best-effort`, so the stand-in's
  certificate passes only if the harness makes it trusted by the default store, which would test
  the harness, not curl.
- What `--ssl-no-revoke` changes is Schannel's revocation lookup against the issuer's CRL or OCSP
  responder on the internet. A stand-in certificate whose revocation status is unknown would fail
  or pass on the lookup's network reach, not on the option, and a stand-in would have to serve
  the responder too.
- The answer depends on a third party's server and certificate, which its operator can change at any time, so a measured result would not stay true.

What the case is about is already pinned without the network: `--ssl-no-revoke` maps to
`TlsClientOptions` in `Curl.Console.UnitTests` (`TlsClientOptionsMappingTests`), and
`SslStreamTlsProviderTests.ChainErrors` and `.RevocationListFile` in `Curl.Networking.UnitTests`
pin which revocation errors the option lets through.

## Decision

1. test2043 stays skipped by `UpstreamCaseScreening` in the in-process ratchet (ADR-0455, decision
   2) and is not added to `PassingUpstreamCases.txt`.
2. The gap office measures `behaviour:test2043` as `excluded`, with the reason "reaches
   revoked.badssl.com on the internet; `--ssl-no-revoke` is pinned by unit tests (ADR-0473)", so
   gap finding GF-0001's item can close.
3. The same holds for any other upstream case the screening skips because it reaches a named
   internet host with no `<server>`: it is `excluded` with that reason, never a stand-in that
   answers for the public host.

## Consequences

GF-0001's last item closes on a re-measurement that records `excluded` with a reason, as the gap
office's rules allow. Applying the exclusion in the gap office's own measurement lives under
`Gap/`, which the dark factory never reads or changes, so it is left to an interactive session's next
gap run.
