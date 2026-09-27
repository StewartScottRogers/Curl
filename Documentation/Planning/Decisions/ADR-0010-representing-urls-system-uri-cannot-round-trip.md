# ADR-0010 — Representing URLs `System.Uri` cannot round-trip: replace, wrap or pre-parse

- **Status:** Proposed
- **Date:** 2026-09-26

## Context

`ITransferContext.Url` (`Curl.Protocol.Abstractions.UnitLibrary/ITransferContext.cs`) is
a `System.Uri`, and so is `TransferContext.Url`, its one production implementation.
ADR-0003's "Known limitation" section records, against curl 8.21.0, the URLs `Uri`
alters or refuses, and task BL-010 adds two more. This ADR compares three ways to
represent them, so that the decision in BL-010 can be made. It decides nothing itself.

### How a URL reaches a handler today

Read from the source on 2026-09-26:

1. `CommandLineParser` (`Curl.Cli.UnitLibrary`) appends each URL to
   `CommandLineOptions.Urls` as a `string`, unchanged and unvalidated.
2. `CurlCommandRunner.TransferAsync` (`Curl.Console`) calls
   `Uri.TryCreate(url, UriKind.Absolute, ...)`. When that fails, the transfer ends with
   exit 3 (`CurlExitCode.UrlMalformat`) and the message
   `URL rejected: Malformed input to a URL function`, and no handler runs. No scheme is
   guessed and nothing is rewritten before this call.
3. `CreateContext` puts the `Uri` on a `TransferContext`, and
   `ProtocolDispatcher.DispatchAsync` (`Curl.Core.UnitLibrary`) picks a handler by
   `context.Url.Scheme`.
4. The handler reads the parts it needs from the `Uri`.

The `Uri` members read in production code, by project:

| Project | File | Reads |
| --- | --- | --- |
| `Curl.Console` | `CurlCommandRunner.cs` | `Uri.TryCreate`, `Scheme` |
| `Curl.Core.UnitLibrary` | `ProtocolDispatcher.cs` | `Scheme` |
| `Curl.Protocol.Dict.UnitLibrary` | `DictProtocolHandler.cs` | `IdnHost`, `IsDefaultPort`, `Port`, `AbsolutePath` |
| `Curl.Protocol.File.UnitLibrary` | `FileUrlPath.cs` | `Scheme`, `OriginalString` |
| `Curl.Protocol.Gopher.UnitLibrary` | `GopherProtocolHandler.cs`, `GopherSelector.cs` | `IdnHost`, `IsDefaultPort`, `Port`, `Scheme`, `OriginalString` |
| `Curl.Protocol.Mqtt.UnitLibrary` | `MqttProtocolHandler.cs`, `MqttSession.cs`, `MqttTopic.cs` | `IdnHost`, `IsDefaultPort`, `Port`, `Scheme`, `AbsolutePath` |
| `Curl.Protocol.Telnet.UnitLibrary` | `TelnetProtocolHandler.cs` | `IdnHost`, `IsDefaultPort`, `Port` |
| `Curl.Protocol.Tftp.UnitLibrary` | `TftpProtocolHandler.cs` | `AbsolutePath`, `Port`, `IdnHost` |

`Curl.Cli.UnitLibrary` calls only `Uri.EscapeDataString` (ADR-0004) and never holds a
`Uri`. The other ten protocol libraries (`Ftp`, `Http`, `Imap`, `Ldap`, `Pop3`, `Rtsp`,
`Smb`, `Smtp`, `Ssh` and `Ws`) contain no source file yet, so no HTTP handler reads
`Url` today.

In the tests, `new Uri(` appears 77 times across eight projects, 55 of them in
`Curl.Protocol.File.UnitTests`, and 175 lines assign a `Url`. The only implementations
of `ITransferContext` are `TransferContext` and the test fake
`Curl.Protocol.File.UnitTests/Fakes/FakeTransferContext.cs`.

### What the `file` handler already works around

`FileUrlPath.TryParse` reads `Uri.OriginalString`, never `AbsolutePath` or `LocalPath`,
and applies curl's `file://` rules to the text itself: backslashes, the three accepted
authorities, the two-character drive-letter authority, the `////server/share` form, dot
segments with a `pathAsIs` switch, and percent-decoding. So the spellings `Uri` accepts
but alters already come out as curl's for `file://`. The spellings `Uri` refuses never
reach it, because step 2 above ends the transfer first.

`--path-as-is` is not in `CommandLineOptionTable`, so `CommandLineParser` refuses it as
an unknown option (exit 2), and `FileProtocolHandler.ExecuteAsync` calls
`FileUrlPath.TryParse(context.Url, out path)`, the overload that always removes dot
segments. Every option below needs the same extra step for that case: recognise the
option and carry it to handlers as a new `ITransferContext` member.

### What `System.Uri` does with each case

Run on .NET 10.0.12 on Windows on 2026-09-26, with `new Uri(text)` and
`Uri.TryCreate(text, UriKind.Absolute, ...)`:

| Text | `System.Uri` on .NET 10.0.12 |
| --- | --- |
| `file:///C:%2FWindows/win.ini` | Throws `UriFormatException`, "A Dos path must be rooted" |
| `file://user:pass@localhost/x` | Throws `UriFormatException`, "The hostname could not be parsed" |
| `file:///c\|/x` | Accepts. `AbsolutePath` `c:/x`, `LocalPath` `c:\x`, `ToString()` `file:///c:/x`; `OriginalString` unchanged |
| `file:////server/share` | Accepts. `Host` `server`, `IsUnc` true, `AbsolutePath` `/share`, `LocalPath` `\\server\share`, `ToString()` `file://server/share` |
| `file:///C:/dir/../x` | Accepts. `AbsolutePath` `C:/x`; the same for `%2e%2e` |
| `http://example.com/a/../b` | Accepts. `AbsolutePath` `/b` |
| `http://example.com/a%2Fb` | Accepts. `AbsolutePath` `/a%2Fb`, `LocalPath` `/a/b` |
| `file://C:` | Throws, "A Dos path must be rooted" |
| `file://ab:/x` | Throws, "The hostname could not be parsed" |
| `file:///C:` and `file:///Q:dir/../x` | Throw, "A Dos path must be rooted" |

BL-010 records "A Dos path must be rooted" for `file://ab:/x` as well; on .NET 10.0.12
the message for that spelling is "The hostname could not be parsed", as the comment in
`Curl.Protocol.File.UnitTests/FileUrlPathTests.cs` also says. Both are
`UriFormatException`, so the conclusion is the same.

## Decision

Not made. This ADR sets out the three options; Stewart chooses between them under
BL-010, and the accepted choice will be recorded then, by accepting this ADR with its
Decision section rewritten or by a new ADR that supersedes it.

### Option 1 — Replace: a curl-style URL type

`ITransferContext.Url` would become a hand-rolled, base-class-library-only type, for
example `CurlUrl`, modelled on libcurl's URL API
([`curl_url_get`](https://curl.se/libcurl/c/curl_url_get.html)): scheme, user,
password, options, host, zone id, port, path, query and fragment, each available as
written and decoded, plus the text exactly as typed. It would parse the way curl's
`lib/urlapi.c` does, with a switch for `CURLU_PATH_AS_IS`, so a URL curl accepts always
produces a value and one curl rejects produces exit 3 at the same point. `System.Uri`
would not appear on the contract; `IdnMapping` would give `IdnHost`'s punycode form.

Projects that would change: `Curl.Protocol.Abstractions.UnitLibrary` (the type, the
contract, `TransferContext`), `Curl.Console` (`CurlCommandRunner`), `Curl.Core.UnitLibrary`
(`ProtocolDispatcher`), the six protocol libraries in the table above, and their eight
test projects plus `Curl.Protocol.Abstractions.UnitTests`. `FileUrlPath` would keep its
`file://` path rules but read them from the new type.

Rough effort: six to eight tasks, about five to eight days. Most of it is the parser
itself, which has to meet the 100% line and branch coverage and complexity-10 gates,
and the 77 test URLs, which are mechanical.

### Option 2 — Wrap: keep a `Uri` where it parses, carry the text beside it

`ITransferContext.Url` cannot stay a non-null `Uri`, because the refused spellings have
no `Uri` at all. It would become a small wrapper, for example `TransferUrl`, holding the
text exactly as typed and a `Uri?` that is `null` when `Uri.TryCreate` fails (or,
equivalently, `Url` would become `Uri?` with a new `string UrlText` beside it).
`CurlCommandRunner` would stop failing on a refused spelling and pass it on. Each
handler would then decide per scheme: `file` would parse the text (as `FileUrlPath`
nearly does already, from `OriginalString`), and a network handler would keep reading
the `Uri` and answer exit 3 when it is `null`.

Projects that would change: `Curl.Protocol.Abstractions.UnitLibrary`, `Curl.Console`,
`Curl.Core.UnitLibrary` (the scheme has to come from the text when there is no `Uri`),
`Curl.Protocol.File.UnitLibrary`, and, lightly, the other five handler libraries (a
`null` check each), with their test projects.

Rough effort: three to four tasks, about two to three days.

### Option 3 — Pre-parse: normalise or reject ahead of `System.Uri`

`ITransferContext.Url` would stay `Uri`. A curl-compatible pre-parser in
`Curl.Console` or `Curl.Core.UnitLibrary` would run before `Uri.TryCreate`, reject what
curl rejects with curl's exit code, and rewrite what curl accepts but `Uri` refuses into
a spelling `Uri` does accept. Handlers would be unchanged, and would still have to read
`OriginalString` wherever `Uri`'s normalisation differs from curl's.

Projects that would change: `Curl.Console` (or `Curl.Core.UnitLibrary`, if the
pre-parser lives there) and its test project. No protocol library changes.

Rough effort: two to three tasks, about two days. But the pre-parser has to understand
curl's URL syntax well enough to decide, which is most of Option 1's parser, and its
output is still subject to `Uri`.

### Each case under each option

"curl 8.21.0" is what this repository records: ADR-0003's "Known limitation" section,
BL-010, and the measurements in the remarks of `FileUrlPath`. "Today" is what the code
does on 2026-09-26. An exit code that the repository does not record is marked
*not recorded*; none was measured for this ADR.

| Case | curl 8.21.0 | Today | Replace | Wrap | Pre-parse |
| --- | --- | --- | --- | --- | --- |
| `file:///C:%2FWindows/win.ini` | Exit 0. | Exit 3: `Uri` refuses it. **No.** | Path `C:%2FWindows/win.ini` reaches `file`, decoded to `C:\Windows\win.ini`; exit 0. **Matches.** | `Uri` is `null`; `file` parses the text; exit 0. **Matches.** | Must rewrite it, e.g. to `file:///C:/Windows/win.ini`, which opens the same file; exit 0. **Matches the exit code**, but the handler no longer sees what was typed, so the path quoted by a failing variant would differ; what curl quotes then is *not recorded*. |
| `file://user:pass@localhost/x` | Exit 3; message *not recorded*. | Exit 3, `URL rejected: Malformed input to a URL function`. **Exit code matches**; message unverified. | User and password parse; the `file` rules reject a userinfo; exit 3. **Matches the exit code.** | `Uri` is `null`; `FileUrlPath`'s authority rule already rejects `user:pass@localhost`; exit 3. **Matches the exit code.** | Rejected before `Uri`; exit 3. **Matches the exit code.** |
| `c\|` drive letter, `file:///c\|/x` | `\|` kept, not turned into `:` (ADR-0003). Measured: `file://D\|/nope.txt` exits 37 quoting `D\|/nope.txt`; `file:///c\|/x` itself *not recorded*. | `Uri` accepts and would say `c:/x`, but `FileUrlPath` reads `OriginalString` and keeps `c\|/x`. **Matches.** | Path kept as written. **Matches.** | Text kept as written. **Matches.** | Passed through; `file` still depends on reading `OriginalString`, and any handler reading `AbsolutePath` would see `c:/x`. **Matches for `file`.** |
| `file:////server/share` | The one UNC form curl accepts (ADR-0003); `file:////server/../x` is quoted as `//x`. The outcome of opening a real share is *not recorded*. | `Uri` folds it to host `server`, path `/share`, but `FileUrlPath` keeps `//server/share`. **Matches.** | Empty host, path `//server/share`. **Matches.** | Text kept. **Matches.** | Passed through; same `OriginalString` dependency. **Matches for `file`.** |
| A literal `..` with `--path-as-is`, `file:///C:/dir\..\x` | Quoted as `C:/dir/../x`: `\` becomes `/`, dot segments kept (ADR-0003). | Exit 2: `--path-as-is` is an unknown option. Without it, dot segments are removed, as curl removes them. **No.** | With the option carried to handlers, the parser keeps dot segments for every scheme, as `CURLU_PATH_AS_IS` does. **Matches.** | With the option carried, `file` keeps them from the text; a network handler reading the `Uri` would not, because `Uri` removes them always. **Matches for `file` only.** | With the option carried, `file` keeps them from `OriginalString`; every handler that reads `AbsolutePath` loses them, and a pre-parser cannot stop that. **Matches for `file` only.** |
| `file://C:` | Exit 37, `Could not open file C:` (BL-010). | Exit 3: `Uri` refuses it. **No.** | `C:` is a drive-letter authority, so the path is `C:`; the open fails; exit 37 quoting `C:`. **Matches**, provided the file system refuses to open `C:` as curl's did. | `Uri` is `null`; `file` parses the text; same as Replace. **Matches**, with the same proviso. | No spelling `Uri` accepts yields the path `C:` (`file:///C:` is refused too, and `file:///C:/` yields `C:/`), so the pre-parser would have to answer exit 37 without opening anything, or rewrite to `C:/` and quote the wrong path. **No.** |
| `file://ab:/x` | Exit 3 (BL-010); message *not recorded*. | Exit 3, `URL rejected: ...`. **Exit code matches**; message unverified. | `ab:` is neither a drive letter nor an accepted host; exit 3. **Matches the exit code.** | `Uri` is `null`; `FileUrlPath`'s two-character drive rule rejects it; exit 3. **Matches the exit code.** | Rejected before `Uri`; exit 3. **Matches the exit code.** |
| `file:///C:` and `file:///Q:dir/../x` (ADR-0003) | Both exit 37, quoting `C:` and `/x`. | Exit 3: `Uri` refuses both. **No.** | Paths reach `file`; exit 37 with those quotes. **Matches.** | Same, from the text. **Matches.** | Same problem as `file://C:`: no accepted spelling carries the path `C:`, and `Q:dir` is refused as an unrooted drive. **No.** |

The rows show the split: the spellings `Uri` accepts but alters are already right for
`file://` today, because of `FileUrlPath`, and stay right under every option. The
difference between the options is in the spellings `Uri` refuses, and in `--path-as-is`
and escaped slashes for schemes other than `file`, where only Option 1 removes `Uri`
from the path a handler reads.

## Consequences

These depend on the option chosen, and are recorded here per option so the choice can
be made with them in view.

Replace:

- Good: one parser, one model of a URL, shaped like curl's own, for every protocol
  library. Scheme guessing, `--path-as-is` and URL quoting in error messages would all
  have one place to live. Ten protocol libraries are still empty, so no handler
  written later has to be migrated.
- Hard: the largest change, and a parser that has to be proved against curl case by
  case. Every `Uri` member a handler reads today needs an equivalent.

Wrap:

- Good: small, and unblocks the refused `file://` spellings quickly.
- Hard: two representations of one URL on the contract. Each handler decides which to
  trust, and a handler that reads the `Uri` silently inherits its normalisation, which
  is how the `%2F` and `--path-as-is` differences would reach HTTP.

Pre-parse:

- Good: no contract change and no handler change.
- Hard: it cannot represent `file://C:`, `file:///C:` or `file:///Q:dir/../x`, which
  curl accepts; it rewrites what the user typed before any handler sees it; and it
  still leaves every handler depending on `OriginalString` to undo `Uri`.

## Alternatives considered

The three options above are the alternatives. Two variants were set aside while
drafting:

- **Construct `Uri` with `UriCreationOptions.DangerousDisablePathAndQueryCanonicalization`.**
  It stops path canonicalisation only, and only for `http` and `https`; it does not
  make `Uri` accept any of the refused `file://` spellings. This is stated from the
  .NET documentation as known for .NET 10 and was not run for this ADR.
- **Register a custom `UriParser` for `file`.** `UriParser.Register` cannot replace the
  built-in `file` scheme parser, and a custom parser still runs inside `Uri`'s
  validation. Stated as known for .NET 10, not run for this ADR.

## Recommendation

Option 1, replace, for these reasons:

- It is the only option under which every case in the table matches curl 8.21.0 for
  every scheme, not only for `file`.
- Pre-parse fails three recorded cases outright, and a pre-parser good enough to decide
  the rest is most of a curl URL parser anyway, without the benefit of using it.
- Wrap is cheaper now but puts two representations on the contract that every future
  handler must choose between, and the `Uri` half would carry `Uri`'s normalisation
  into HTTP, where `--path-as-is` and `%2F` matter.
- The change is at its cheapest now: six handlers read `Url`, and the protocol
  libraries still to be written would start on the new type.

If the cost of Option 1 is too high for the current phase, Option 2 is the fallback,
with the wrapper's text as the source of truth and the `Uri` read only for host and
port. The decision is Stewart's, under BL-010.

Sources: <https://curl.se/docs/url-syntax.html>, <https://curl.se/docs/manpage.html>
(`--path-as-is`) and <https://curl.se/libcurl/c/curl_url_get.html>. The curl behaviour
quoted is what ADR-0003 and BL-010 record against curl 8.21.0; the libcurl URL API parts
named under Option 1 were not re-read against the 8.21.0 documentation for this draft.
