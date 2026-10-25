# CurlTestCases/Extended

Our own conformance cases, beside `../Original/` (upstream curl's `tests/data`, vendored byte for byte).
`Update-UpstreamTestData.ps1` writes only into `Original/` and never touches this folder, so a refresh
from a curl release tag leaves these cases alone.

- **Naming.** `ext<number>.curltest` (for example `ext1.curltest`). The `ext` prefix means a number never
  collides with an upstream `test<number>.curltest`.
- **Format.** The same `tests/data` format upstream uses (`<testcase>`, `<client>`, `<reply>`, `<verify>`),
  so `UpstreamCaseRunner` runs them unchanged.
- **Run environment.** `UpstreamConformanceTests.ExtendedCases_RunThroughCurl_AllPass` runs every case here
  with `Original/`'s `certs/` as `%CERTDIR` and `Original/`'s `data/` as `%SRCDIR/data`.
- **Not upstream.** These cases are not on `PassingUpstreamCases.txt` and never change the upstream pass
  count, the ratchet or the gap office's behaviour score; each one must simply pass.

## Folders and number blocks

Cases live in one subfolder per protocol (decided by Claude under Stewart's delegation, ADR-0810, BL-3462).
Secure variants (HTTPS, FTPS, IMAPS, POP3S, SMTPS, LDAPS, SMBS, GOPHERS, WSS) go in their base protocol's
folder; SCP and SFTP go in `SSH/`. Each folder owns a block of case numbers, so parallel tasks never collide:

| Folder | Numbers | Folder | Numbers |
| --- | --- | --- | --- |
| `DICT/` | `ext1001` to `ext1999` | `SMB/` | `ext11001` to `ext11999` |
| `FILE/` | `ext2001` to `ext2999` | `SMTP/` | `ext12001` to `ext12999` |
| `FTP/` | `ext3001` to `ext3999` | `SSH/` | `ext13001` to `ext13999` |
| `GOPHER/` | `ext4001` to `ext4999` | `TELNET/` | `ext14001` to `ext14999` |
| `HTTP/` | `ext5001` to `ext5999` | `TFTP/` | `ext15001` to `ext15999` |
| `IMAP/` | `ext6001` to `ext6999` | `WS/` | `ext16001` to `ext16999` |
| `LDAP/` | `ext7001` to `ext7999` | `MQTT/` | `ext8001` to `ext8999` |
| `POP3/` | `ext9001` to `ext9999` | `RTSP/` | `ext10001` to `ext10999` |

`ExtendedCases_RunThroughCurl_AllPass` runs every `ext<number>.curltest` here and in the subfolders, names a
failure as `<FOLDER>/ext<number>`, and fails, naming the file, for a number outside its folder's block or a
number used twice. An empty folder holds a `.gitkeep`.
