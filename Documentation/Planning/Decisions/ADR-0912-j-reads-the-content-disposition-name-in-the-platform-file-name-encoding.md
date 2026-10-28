# ADR-0912: `-J` reads the Content-Disposition name in the platform's file-name encoding

- Status: Accepted
- Date: 2026-10-26
- Decision: Decided by Claude under Stewart's delegation (BL-3580, CGR-051)

## Context

curl takes the bytes of a `Content-Disposition` `filename=` value as the file name and hands them to
`open`. The Windows build opens through the ANSI file API, so the bytes are read in the system ANSI code
page; the OpenSSL build on Linux writes the raw bytes. `ContentDispositionFileName` read them as UTF-8 on
every platform, so invalid bytes became U+FFFD. Measured on 2026-10-26 with `Record-CurlExchange.ps1`,
`curl -s -S -OJ http://127.0.0.1:20581/j`, code page 1252:

| `filename=` bytes | curl 8.22.0 Schannel (Windows) | Curl before | Curl after |
| --- | --- | --- | --- |
| `caf E9 .txt` | `café.txt`, exit 0 | `caf\uFFFD.txt` | `café.txt`, exit 0 |
| `caf C3 A9 .txt` | `cafÃ©.txt`, exit 0 | `café.txt` | `cafÃ©.txt`, exit 0 |

Standard output and standard error were empty and the request bytes equal in both runs.

## Decision

`ContentDispositionFileName.Find` takes the encoding to read the name in. `RemoteHeaderNameStream` passes
`CredentialEncoding.ForPlatform(OperatingSystem.IsWindows())` unless given one, the encoding the runner uses for command-line text sent on
the wire: the system ANSI code page on Windows, UTF-8 elsewhere. On Windows .NET then opens the file under
the same Unicode name the ANSI API would, so both measured names match.

Off Windows the name stays UTF-8: a valid UTF-8 name is already the raw bytes curl writes. A name that is
not valid UTF-8 cannot be created through `System.IO`, whose paths are UTF-8 strings, so it keeps U+FFFD
until BL-3858 carries the name as bytes down to the open.

## Consequences

- `-J` on Windows writes the file the Schannel build writes for any byte in the name.
- The Linux raw-byte case is BL-3858's.
