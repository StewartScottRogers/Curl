# ADR-1300: Name constraints cover directoryName, rfc822Name and URI names (BL-5436)

Decided by Claude under Stewart's delegation.

## Status

Accepted, 2026-11-18.

## Context

curl's Schannel build refuses a chain whose leaf breaks a CA's permitted or excluded name constraint of any kind
(`schannel: error 0x00004000` and `0x00008000`). `CertificateNameConstraints` (BL-5375) evaluated only dNSName and
iPAddress, so a leaf with a subject outside a permitted directoryName, or an email or URI SAN outside a permitted
rfc822Name or URI subtree, was accepted (fitness finding STD-118).

## Decision

- The leaf's names also include its subject as a directoryName (when not empty), and the directoryName, rfc822Name
  and uniformResourceIdentifier entries of its subject alternative names.
- A directoryName constraint matches a name that begins with the same relative distinguished names (byte-equal DER).
- An rfc822Name constraint with an `@` names one mailbox; otherwise it names a host, or with a leading dot the hosts below it.
- A URI constraint is matched against the URI's host the same way; a URI that does not parse matches nothing.
- A constraint only applies to names of its own kind, as before; other kinds (otherName, registeredID) stay unevaluated.

## Consequences

Matching is byte-exact for directory names (no case folding or string-type normalisation), the conservative reading.
