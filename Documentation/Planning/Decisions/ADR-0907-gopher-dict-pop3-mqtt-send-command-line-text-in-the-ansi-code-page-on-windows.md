# ADR-0907: Gopher, dict, POP3 and MQTT send command-line text in the ANSI code page on Windows

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3842, CGR-015 part 5)
- Relates to: [ADR-0135](ADR-0135-smtp-without-a-message-sends-vrfy-expn-help-or-the-x-command-and-writes-each-reply-line.md)

## Context

Third-party gap report finding CGR-015 found that Curl sends non-ASCII command-line text as UTF-8 on
Windows, where the reference curl (Git for Windows ucrt64 curl 8.22.0, an ANSI-argv build) receives its
arguments, and so sends them, in the system ANSI code page. BL-3544 decided Curl matches the reference on
Windows and split the fix by library; SMTP and IMAP already do it (`SmtpCommandLineText`,
`ImapCommandLineText`).

Measured on 2026-10-26 with `Record-CurlExchange.ps1` (code page 1252):

| Command | Bytes real curl sent |
| --- | --- |
| `curl -s -S gopher://127.0.0.1:P/0/é` | selector `2F E9 0D 0A` |
| `curl -s -S dict://127.0.0.1:P/d:é` | `DEFINE ! \` `E9` CRLF (the word escaped as for any byte at or above 0x7F) |
| `curl -s -S -u u:p -X "XFOO é" pop3://127.0.0.1:P/` | `XFOO E9` CRLF |
| `curl -s -S -u u:p -X "XFOO €" pop3://127.0.0.1:P/` | `XFOO 80` CRLF |
| `curl -s -S mqtt://127.0.0.1:P/é` | SUBSCRIBE topic `00 01 E9` |

## Decision

1. `Curl.Original.Protocol.Gopher`, `.Dict`, `.Pop3` and `.Mqtt` each get a `<Protocol>CommandLineText`, the same
   shape as `SmtpCommandLineText` (protocol libraries never reference each other): the system ANSI code
   page on Windows (Windows-1252 when the host reports none), UTF-8 elsewhere.
2. The gopher selector, the dict path, the POP3 `-X` command and the MQTT topic are turned into bytes with
   it before they are percent-decoded, so a literal non-ASCII character becomes its code-page byte and a
   percent-escape still decodes to the byte it names.
3. Off Windows nothing changes. The POP3 message id read from the URL keeps UTF-8; only `-X` is
   command-line text in the measured cases. MQTT payloads come from `-d` and are BL-3844's.
