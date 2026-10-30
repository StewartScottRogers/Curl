# ADR-1153: The Dane extension checks TLSA records validated from the root with --dane

- Status: Accepted
- Date: 2026-10-30
- Task: BL-4151
- Decided by Claude under Stewart's delegation

## Context

BL-4151 adds DNSSEC validation and DANE TLSA checking as an extension (ADR-0999). Its split built the codec
(ADR-1123, BL-4333), TLSA matching (BL-4334), signature and delegation checks (BL-4335) and the validating lookup
(BL-4332); the certificate check hook gained the port (ADR-1141). This task adds the option, the hook and the
catalogue lines. Upstream curl does no DANE, so the behaviour follows RFC 4033-4035, RFC 6698 and RFC 7671.

## Decision

- One option, `--dane <resolver>`, with the recursive resolver as `host`, `host:port`, a bare IPv6 address or
  `[IPv6]:port` (port 53 by default). A value always names its resolver: the system's resolver configuration is not
  portable to read from the BCL, and a validating stub needs a resolver that passes DNSSEC records through.
- The hook (ADR-1016) runs after curl accepted the chain. It looks up `_<port>._tcp.<host>`'s TLSA RRset (RFC 6698
  section 3) with DO and CD set and validates it itself from the IANA root trust anchors, never trusting AD
  (RFC 4035 section 4.9.3). Then, following RFC 6698 section 4.1 and RFC 7671 section 4:
  - secure records with a usable one matching the chain: the chain stands;
  - secure records none of which is usable: treated as no records, the chain stands (RFC 7671 section 4.1);
  - secure usable records none of which matches: exit 60;
  - bogus: exit 60; no usable answer (timeout, unreachable resolver, error RCODE): exit 60, failing closed since the
    user asked for DANE;
  - a proved-insecure delegation or a proved absence of TLSA records: the chain stands on curl's PKIX check alone.
- A server named by an IP address has no TLSA name, so the hook lets its chain stand without a lookup. A connection
  whose port is unknown (an in-place upgrade, ADR-1141) is refused, as its TLSA name cannot be formed.
- Each DNS query, UDP then TCP, may take five seconds, the common resolver timeout.

## Left out

- DANE-TA(2) and DANE-EE(3) can make RFC 6698 accept a chain that PKIX rejects. A certificate check hook can only
  refuse a chain curl accepted (ADR-1016), and the extension never changes what curl does, so such a server still
  needs `--cacert` or `-k`; this is documented rather than worked around.
- No requirement that DANE apply (a "DANE or fail" mode for insecure zones): RFC 7672's opportunistic mode does not
  require it either, and a later option can add it without changing this one.
- The DNS TLSA lookup for SMTP's MX-based names (RFC 7672) is not done; the name is always the connection's host.

## Consequences

`curl --extensions` and `curl --ai-help extensions` list `dane`; with `CURL_EXTENSIONS=0` `--dane` is an unknown
option (exit 2). Without `--dane` no hook activates and every transfer is unchanged.
