# ADR-1219: The AMQP 1.0 transfer surface pins a refused link's exit 78 under `--fail`

- Status: Accepted
- Date: 2026-11-02
- Task: BL-4629

Decided by Claude under Stewart's delegation.

## Context

BL-4629 checks curl's generic transfer options for `amqp1` and `amqp1s`
(`Documentation/Conformance/Extended/CurlSurface-Transfer-Brokers-Amqp1.md`). Its criteria ask for exit 22 under
`--fail` and `--fail-with-body` "where the library maps a server error to it". curl's manual says both options act on
HTTP response codes of 400 or more. The AMQP 1.0 client (ADR-1062, `Amqp1ClientMessages`) maps no AMQP error to
exit 22: a detach, end or close error is exit 67 for `amqp:unauthorized-access`, 78 for `amqp:not-found` and 8
otherwise, and a message the receiver does not accept is exit 55.

## Decision

The `--fail` and `--fail-with-body` rows run a receive against a link `Amqp1ReferenceContainer` refuses with
`amqp:not-found`, and pin that either option leaves the extension's answer unchanged: exit 78,
`curl: (78) AMQP 1.0 detach error: amqp:not-found: ...` on standard error and the same code and message from `-w`.
The extension does not start mapping AMQP errors to exit 22 under `--fail`.

## Why

- curl's `--fail` concerns HTTP response codes; AMQP has none, and real curl, given an option that does not apply to
  the protocol, does not change the transfer's outcome.
- ADR-1062's mapping already gives scripts the distinct exit codes curl uses for the same causes (login denied,
  remote file not found), which a blanket 22 would hide.

## Consequences

- An AMQP broker error under `--fail` ends with the same exit code as without it.
- Should the extension ever map an AMQP error to exit 22, these rows change with it.
