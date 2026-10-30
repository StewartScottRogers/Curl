# ADR-1137: The Dane extension's TLSA lookup walks DS from the root itself and leaves out what it cannot prove

- Status: Accepted
- Date: 2026-10-29
- Task: BL-4332
- Decided by Claude under Stewart's delegation

## Context

`Curl.Extension.Dane.UnitLibrary/Lookup/` asks a recursive resolver for `_<port>._tcp.<host>` TLSA (RFC 6698
section 3) and must say whether the answer is secure, insecure, bogus or proved absent, validating it itself with
BL-4333's codec and BL-4335's verifiers rather than trusting the resolver's AD bit. Upstream curl has no DANE, so the
choices below follow the RFCs, not curl.

## Decision

- **Transport.** Every query sets DO and CD (RFC 4035 section 3.2.2) and goes over UDP through `IDatagramConnector`;
  a reply with TC set is asked again over TCP through `IConnector` with RFC 1035 section 4.2.2's two-octet length
  prefix (RFC 7766 section 5). A UDP datagram counts only when it comes from the server's address and carries the
  query's random ID. One `TimeProvider` timeout covers UDP and TCP together; when it fires, or the server cannot be
  reached, answers unreadably or with an RCODE other than NOERROR or NXDOMAIN, the lookup is `Failed`.
- **Finding zone cuts.** From the root's DNSKEY RRset, proved by the trust anchors (`RootTrustAnchors` in
  production, a test's own key in tests), the lookup asks for DS at every ancestor of the TLSA name, top down. A
  signed DS RRset makes that name a zone: its DNSKEY RRset must be signed by a key a DS names. A proved absence of DS
  at a name with NS (an NSEC or NSEC3 listing NS without DS or SOA, or an Opt-Out NSEC3 covering it under a proved
  closest encloser) ends the lookup `Insecure`. Any other NODATA keeps the name in its parent's zone without proof:
  hiding a real delegation that way cannot make anything below it secure, because the child's signatures then
  fail against the parent's keys and the lookup is `Bogus`. A DS RRset whose digest types or algorithms are all
  unsupported is `Insecure` (RFC 4035 section 5.2, ADR-1131).
- **The answer.** A TLSA RRset must be signed by the deepest zone's keys; a wildcard expansion (RRSIG labels fewer
  than the owner's) also needs an NSEC covering the name or an NSEC3 covering its next closer name. A CNAME is
  followed when the zone's keys sign it, and the target is looked up from the root again; eight aliases are the
  most, after which the lookup is `Failed`. NODATA needs an NSEC owned by the name or an NSEC3 matching its hash
  that lists neither TLSA nor CNAME; NXDOMAIN needs NSEC covering the name and the wildcard at its closest encloser,
  or NSEC3's closest encloser proof with the next closer name and the wildcard covered. Either gives `Absent`;
  anything unproved gives `Bogus`. An `Insecure` result carries no records: RFC 6698 section 4.1 forbids using them.

## What the lookup leaves out, and why

- **Opt-Out in a name error.** An NSEC3 covering the next closer name proves the name absent whether or not its
  Opt-Out flag is set; RFC 5155 section 8.4 would call an Opt-Out span only insecure. Both outcomes lead DANE to
  the same place, no usable TLSA, so the difference is not worth a fifth status.
- **Wildcard NODATA** (RFC 4035 section 5.4's wildcard case, RFC 5155 section 8.7) is not proved, so a name that
  matches only an empty wildcard is `Bogus`. TLSA owners are specific names in practice.
- **NSEC3 with over 150 iterations or a hash other than SHA-1** is ignored, so a proof that depends on it fails as
  `Bogus`: RFC 9276 section 3.2 lets a validator refuse such records, and SERVFAIL is what a resolver gives.
- **DNAME** (RFC 6672) is not followed; a DNAME-synthesised CNAME is checked like any CNAME, so the unsigned
  synthesised record makes the lookup `Bogus`.
- **Signatures by a CNAME target's own zone at the alias** are not mixed: each name in a chain is walked from the
  root, at the cost of repeating the DS and DNSKEY queries, which keeps the walk one simple loop.
- **Caching** is left to the caller (BL-4151 looks up one name per transfer).

## Consequences

- A lookup costs two queries per zone and one per ancestor that is not a zone, all to one resolver.
- Unit tests replay signed hierarchies they generate (Ed25519 keys, a test trust anchor) through fake connectors,
  with a manual `TimeProvider` for the timeout, so no test needs a network.
