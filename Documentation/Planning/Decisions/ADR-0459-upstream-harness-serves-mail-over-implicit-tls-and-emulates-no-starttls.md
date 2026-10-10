# ADR-0459: The upstream harness serves mail over implicit TLS and emulates no STARTTLS

- Status: Accepted
- Date: 2026-10-09
- Task: BL-1914
- Decided by Claude under Stewart's delegation.

## Context

BL-1914 asked for the SMTP, IMAP and POP3 stand-ins of `Curl.Conformance.UnitLibrary` to run
behind TLS, both implicit (`smtps`, `imaps`, `pop3s`) and STARTTLS, so that at least ten mail
cases needing TLS are measured.

The vendored curl 8.21.0 `tests/data` holds only three mail cases on an implicit-TLS server:
test987 (`smtps`), test988 (`imaps`) and test989 (`pop3s`), each on `%SMTPSPORT`, `%IMAPSPORT`
or `%POP3SPORT`. The other mail cases that ask for TLS - test980, 981, 982, 984 and 985 (`--ssl`
or `--ssl-reqd` on a plain `smtp`, `imap` or `pop3` server) - check what curl does when the
server does not offer STARTTLS: upstream's `tests/ftpserver.pl` never offers or answers it, so
no case at `curl-8_21_0` reaches a mail server that upgrades. Eight mail cases ask for TLS in
all, so the task's "at least 10" cannot be met by any harness.

## Decision

1. `MailTlsServerConnector` stands in for stunnel in front of the three plain mail stand-ins:
   `%SMTPSPORT` 9000, `%IMAPSPORT` 9001 and `%POP3SPORT` 9002 reach the SMTP, IMAP and POP3
   stand-ins on their plain ports through `TlsRelayConnection`, which runs `TlsServerStream`
   with `test-localhost.pem` (stunnel's default certificate), no ALPN and no client
   certificate request, and relays both ways at once, since a mail server speaks first.
2. The three ports have a value only when the caller names a certificate directory, as
   `%HTTPSPORT` does (BL-1912).
3. No STARTTLS upgrade is emulated: ftpserver.pl has none, so emulating one would make the
   STARTTLS-refused cases differ from upstream. The eight mail cases that ask for TLS are
   measured (`UpstreamConformanceTests.MailTlsCase_RunThroughCurl_IsMeasuredNotSkipped`).

## Consequences

test981, 987, 988 and 989 pass and are on `PassingUpstreamCases.txt`; 984 and 985 already
passed; 980 and 982 fail on Curl differences the next gap run files. If a later curl release
adds a STARTTLS-capable mail server to ftpserver.pl, the upgrade is a new task.
