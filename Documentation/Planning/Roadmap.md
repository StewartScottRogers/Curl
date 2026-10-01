# Roadmap

Milestones 0 to 6 follow the phasing table in `Documentation/Product/Product-Overview.md`:
Milestone 2 is Phase 2, Milestone 3 is Phase 3, Milestone 4 is the WebSocket part of
Phase 4 (its other protocols shipped in Milestone 1), Milestone 5 is Phase 5 and
Milestone 6 is Phase 6. Dates are deliberately absent rather than guessed.

Ordered by dependency, not by calendar. A milestone is done when its exit
criteria are met, and each milestone cites the requirement IDs it delivers.

## Milestone 0 — Foundations

Repository, solution and documentation structure.

- **Status:** In progress
- **Delivers:** `Curl.slnx`, `Documentation` shared project, bootstrap scripts
- **Exit criteria:** solution opens cleanly in Visual Studio and builds from the
  `dotnet` CLI; documentation structure agreed
- **Done:** `.slnx` solution and `Documentation.shproj` created and verified
  (2026-09-25) — see `Decisions/ADR-0001-adopt-slnx-solution-format.md`
- **Done:** all sixteen Phase 1 projects exist and are listed in `Curl.slnx`;
  `dotnet build Curl.slnx -warnaserror` is clean with no `NU1503` (2026-09-26, BL-003)
- **Outstanding:** nothing.

## Milestone 1 — Phase 1, with the small Phase 4 protocols beside it

Phase 1 of `Documentation/Product/Product-Overview.md` (`Abstractions`, `Networking`,
`Core`, `Cli`, `Output`, `Console`, `File` and `Http`), with `Curl.Authentication` and
`Curl.Cookies` moved in from Phase 2 (ADR-0016), and DICT, Gopher, Telnet, TFTP and MQTT
built alongside it.

- **Status:** In progress
- **Decision (Stewart, 2026-09-26):** DICT, Gopher, Telnet, TFTP and MQTT are built in
  parallel with Phase 1, for throughput, because the dark factory runs parallel lanes.
  WS stays in Phase 4.
- **Decisions that stand:** ADR-0005 (protocol handlers acquire transports through
  connectors) and ADR-0006 (the transfer context carries the Phase 4 protocol options)
  remain Accepted, and `dict://` sends curl's exact `CLIENT libcurl <version>` line
  (`CLIENT libcurl 8.21.0`, FR-020).
- **Delivers:**
  - `Curl.Console` composition: the runner, composition root and `file://` (BL-068),
    the TCP and UDP connectors composed with the TLS provider (BL-069), the dict,
    gopher, telnet, tftp and mqtt handlers registered (BL-070), and the TLS options
    mapped onto the TLS provider (BL-071, BL-072).
  - The production TLS transport, in `Curl.Networking.UnitLibrary` behind
    `ITlsProvider` (ADR-0005; the Product Overview's "TLS lives once" finding): TLS
    failures as a `ConnectResult` (BL-061), `SslStreamTlsProvider` with `-k`, TLS
    versions and exits 35 and 60 (BL-062), `--cacert` (BL-063), failure messages and
    `--capath` (BL-064), `--cert`/`--key` (BL-065) and `--ciphers` (BL-066); their
    command-line parsing in `Curl.Cli.UnitLibrary` (BL-067); and Stewart's decisions on
    which curl TLS build to match (BL-059) and on `--ciphers` with the base class
    library (BL-060).
  - The protocols: `file://` (FR-001 to FR-019) and `dict`, `gopher`, `gophers`,
    `telnet`, `tftp`, `mqtt` and `mqtts` (FR-020 to FR-045).
  - `Curl.Authentication` and `Curl.Cookies`, moved from Phase 2 by ADR-0016 so that
    HTTP's `-u`, `--digest`, `-b` and `-c` work in Phase 1: Basic and Bearer, Digest and
    scheme choice (BL-216 to BL-218), and cookies (BL-219 to BL-223).
  - HTTP, planned on 2026-09-26 (the Phase 1 HTTP plan), on the decisions and contracts
    it rests on: the ADRs (BL-151 to BL-158, BL-163, BL-164), the request, report,
    authentication, cookie and proxy contracts in `Curl.Protocol.Abstractions` (BL-159 to
    BL-162), the exchange recording script (BL-165) and requirements FR-052 to FR-106
    (BL-166 to BL-168).
  - The HTTP handler, `Curl.Protocol.Http`: the response head reader (BL-169), body
    framing by length, close and chunks (BL-170, BL-171), the request writer (BL-172),
    GET and HTTPS over `IConnector` (BL-173), timeouts and send and receive failures
    (BL-174), request bodies (BL-175), `-I` and `-f` (BL-176), `--compressed` (BL-177),
    ranges, resume, `-z` and `--max-filesize` (BL-178), the redirect target (BL-179),
    `-0`, `--raw` and the other transfer-encoding options (BL-180), authentication
    (BL-181), cookies (BL-182), forward proxies (BL-183), `-T` uploads (BL-184),
    progress (BL-185) and `--request-target` and `--path-as-is` (BL-186).
  - The command-line option groups, `Curl.Cli`: `-X`, `-H`, `-A`, `-e` (BL-187), the
    `--data` options, `--json`, `-G` and `--url-query` (BL-188), `-F` (BL-189), `-L`
    and the fail options (BL-190), HTTP versions and transfer encodings (BL-191), auth
    and proxy options (BL-192), `-b`, `-c`, `-j` (BL-193), `-w` and output naming
    (BL-194), `-v` and `--trace` (BL-195), retries and rate limits (BL-196), `-K`
    (BL-197), `.curlrc` and `-q` (BL-198), `--variable` (BL-199), `-V` (BL-200), `-h`
    and `-M` (BL-201), and `--resolve` and `--connect-to` (BL-202).
  - Core, `Curl.Core`: redirects (BL-203), scheme guessing (BL-204), multipart bodies
    (BL-205), proxy selection (BL-206), URL globs (BL-207), retries (BL-208), rate
    limiting (BL-209) and the IPFS rewrite (BL-210).
  - Networking, `Curl.Networking`: connect timings (BL-211), HTTP proxy tunnels
    (BL-212), SOCKS proxies (BL-213), `--resolve` and `--connect-to` (BL-214) and
    connection reuse (BL-215).
  - Output, `Curl.Output`: `-w` templates, variables, times and JSON (BL-224 to
    BL-227), `-v` lines (BL-228) and `--trace` dumps (BL-229).
  - `Curl.Console` wiring: the transfer-context factory (BL-230), the HTTP handler and
    its request options (BL-231), `-i`, `-I` and the fail options (BL-232), `-F`
    (BL-233), `-L` (BL-234), `-w` (BL-235), transfer encodings (BL-236), the
    authenticator and cookie store (BL-237), proxies (BL-238), output file naming
    (BL-239), scheme guessing, globs and IPFS (BL-240), retry and rate limiting
    (BL-241), `-v` and `--trace` (BL-242), `-K` and `.curlrc` (BL-243), `--resolve`
    and `--connect-to` (BL-244), and `--request-target` and `--path-as-is` (BL-245).
- **Exit criteria:** `curl <url>` runs end to end through `Curl.Console` for `file`,
  `dict`, `gopher`, `gophers`, `telnet`, `tftp`, `mqtt` and `mqtts`, and returns curl's
  exit codes; and `curl http(s)://...` runs end to end through `Curl.Console`, sending
  curl's request bytes and writing curl's output and exit codes (FR-052 to FR-106).
- **Done:** ADR-0005 and ADR-0006 (BL-032); requirements FR-020 to FR-045 (BL-033);
  contracts (BL-034, BL-035); scheme dispatch (BL-036); option parser (BL-037, BL-038);
  connectors (BL-039, BL-040); handlers for dict (BL-041), gopher (BL-042), telnet
  (BL-043, BL-044), tftp (BL-045, BL-046, BL-047) and mqtt (BL-048, BL-049); TLS
  (BL-061, BL-062) and its option parsing (BL-067); `curl <url>` end to end for
  `file://` (BL-068). All as of 2026-09-26.
- **Outstanding:** every task under "Delivers" that is not yet in `Tasks/Done`; the task
  board, not this page, holds each one's status.

## Milestone 2 — Phase 2: FTP and SSH

`Curl.Protocol.Ftp` and `Curl.Protocol.Ssh`: a second transport shape, a control
channel beside data channels, without distorting the design.

- **Status:** In progress. FTP is built and registered; SCP and SFTP are being built.
- **Decisions that stand:** ADR-0122 (SCP and SFTP offer each platform curl's libssh2
  algorithms), ADR-0206 (key exchange failures), ADR-0212 (packet protection), ADR-0213
  (host key checks) and ADR-0215 (user authentication).
- **Delivers:**
  - FTP, `Curl.Protocol.Ftp`: download with login, passive mode and RETR (BL-431),
    registration in `Curl.Console` (BL-434), the control options (BL-435, BL-436),
    active mode and FTPS (BL-437, BL-456 to BL-459, BL-463 to BL-466, BL-474), `-r`,
    `-C` and `-I` (BL-438), `-T` uploads (BL-439), `ftp://` through an HTTP proxy
    (BL-343, BL-344), the reply code (BL-392), `--max-time` and `--connect-timeout`
    (BL-512), `%{ftp_entry_path}` (BL-514), MDTM and SIZE (BL-637, BL-638) and exits
    11 and 15 (BL-662).
  - FTP follow-ups: ASCII mode, `--crlf` and `-a` (BL-632, BL-633), ACCT, the
    alternative USER and PRET (BL-634, BL-635), CCC (BL-636), `-R` into the transfer
    context (BL-854), the passive data connect under `--connect-timeout` (BL-797), EPSV
    over IPv6 (BL-903) and the data connection's failure message (BL-904).
  - SSH, `Curl.Protocol.Ssh`: the build decision (BL-560), the transfer options and
    their parsing (BL-561, BL-562), the transport, key exchange and packet protection
    (BL-563 to BL-565, BL-678), host key checks (BL-566), password, keyboard-interactive
    and public key authentication (BL-567, BL-568, BL-681), SFTP download, listing,
    upload, quote commands and ranges (BL-569 to BL-573), SCP download and upload
    (BL-574, BL-577), compression (BL-575), registration for `scp` and `sftp` in
    `Curl.Console` (BL-576) and `-v` and `--trace` lines (BL-578).
  - The rest of the libssh2 and libssh algorithm set: ChaCha20-Poly1305 (BL-679), the
    older ciphers and MACs (BL-680), the post-quantum hybrid key exchanges (BL-748),
    host-key certificates (BL-749) and the ETM MACs (BL-750), on the hand-built
    primitives in `Curl.Cryptography` (BL-669 to BL-677).
- **Exit criteria:** `curl ftp://`, `ftps://`, `scp://` and `sftp://` run end to end
  through `Curl.Console`, sending curl's commands and writing curl's output and exit
  codes, measured against the platform's curl.
- **Outstanding:** every task under "Delivers" that is not yet in `Tasks/Done`; the
  task board, not this page, holds each one's status. `--krb` for FTP (BL-693) is
  deferred.

## Milestone 3 — Phase 3: SMTP, IMAP and POP3

`Curl.Protocol.Smtp`, `Curl.Protocol.Imap` and `Curl.Protocol.Pop3`: line-oriented
protocols sharing one SASL authenticator and one mail options record.

- **Status:** In progress. All three handlers are built and registered; follow-ups
  remain.
- **Decisions that stand:** ADR-0121 (the shared SASL authenticator and mail options),
  ADR-0123, ADR-0139, ADR-0183, ADR-0184 and ADR-0203 (SASL), ADR-0134 (POP3 login
  order) and ADR-0136 (SMTP `MAIL FROM`).
- **Delivers:**
  - The exchange recording modes (BL-529 to BL-531), the SASL decision (BL-533), the
    mail options and authenticator contract (BL-534), their parsing (BL-535), the SASL
    mechanisms (BL-536 to BL-538) and carrying them into each handler (BL-539).
  - SMTP: the session, authentication, upload, commands, options, registration and
    `-v` lines (BL-540 to BL-546).
  - POP3: the session, authentication, list and retrieve, commands, registration and
    `-v` lines (BL-547 to BL-552).
  - IMAP: the session, authentication, fetch, list and search, append, registration
    and `-v` lines (BL-553 to BL-559).
  - Follow-ups: the OAUTHBEARER port (BL-751, BL-876), SASL cancellation and exit 94
    (BL-774, BL-781, BL-856), UTF-8 and IDNA addresses (BL-776), awaited SASL
    exchanges and the security context factory (BL-851, BL-852), the `-v` SASL line
    when no mechanism is found (BL-810) and `--delegation` for SASL GSSAPI (BL-874).
- **Exit criteria:** `curl smtp://`, `smtps://`, `imap://`, `imaps://`, `pop3://` and
  `pop3s://` run end to end through `Curl.Console`, sending curl's commands and writing
  curl's output and exit codes, measured against the platform's curl.
- **Outstanding:** every task under "Delivers" that is not yet in `Tasks/Done`.

## Milestone 4 — Phase 4: WebSocket

`Curl.Protocol.Ws`. The rest of Phase 4 (`Mqtt`, `Tftp`, `Dict`, `Gopher`, `Telnet`)
shipped in Milestone 1.

- **Status:** In progress. The handler is built and registered for `ws` and `wss`;
  follow-ups remain.
- **Decisions that stand:** ADR-0128 (the upgrade request and 101 head) and ADR-0131
  (frame reading).
- **Delivers:** the upgrade decision (BL-579), the upgrade request (BL-580), frames
  (BL-581), writing messages (BL-582), registration (BL-583), `-v` and `--trace` lines
  (BL-584, BL-813), `%{size_delivered}` (BL-777), `-I` on a WebSocket URL (BL-788) and
  Negotiate continuation over `ws` (BL-842).
- **Exit criteria:** `curl ws://` and `wss://` run end to end through `Curl.Console`,
  writing curl's output and exit codes, measured against the platform's curl.
- **Outstanding:** every task under "Delivers" that is not yet in `Tasks/Done`.

## Milestone 5 — Phase 5: LDAP, SMB and RTSP

`Curl.Protocol.Ldap`, `Curl.Protocol.Smb` and `Curl.Protocol.Rtsp`: the awkward
remainder.

- **Status:** In progress. LDAP and RTSP are built and registered; SMB is built but
  not yet registered in `Curl.Console`.
- **Decisions that stand:** ADR-0166 (LDAP on `IConnection`, as each platform's WinLDAP
  or OpenLDAP build answers), ADR-0169 (RTSP requests and replies) and ADR-0200 (SMB
  and SMBS speak curl's SMBv1 on every platform).
- **Delivers:**
  - LDAP: the build decision (BL-585), BER and bind (BL-586), the search (BL-587),
    output (BL-588), registration (BL-589), the WinLDAP bind, signing and sealing
    (BL-830, BL-853) and I/O failure exits (BL-845).
  - RTSP: the message decision (BL-590), CSeq (BL-591), the session ID (BL-592),
    registration with its options (BL-593) and reply header edge cases (BL-840).
  - SMB: the platform decision (BL-594), negotiation and NTLM (BL-595), download
    (BL-596), upload (BL-597) and registration for `smb` and `smbs` (BL-598).
- **Exit criteria:** `curl ldap://`, `ldaps://`, `smb://`, `smbs://` and `rtsp://` run
  end to end through `Curl.Console`, writing curl's output and exit codes, measured
  against the platform's curl.
- **Outstanding:** every task under "Delivers" that is not yet in `Tasks/Done`.

## Milestone 6 — Phase 6: the conformance push, HTTP/2 and HTTP/3

The options, `-w` variables and protocol versions the 2026-09-28 conformance audit found
missing, so the drop-in claim becomes defensible.

- **Status:** In progress.
- **Decisions that stand:** ADR-0141 (HTTP/2 is hand-built), ADR-0144 and ADR-0172
  (HTTP/3 over a hand-built QUIC), which supersede
  [ADR-0017](Decisions/ADR-0017-no-http-2-or-http-3-in-milestone-1.md).
- **Delivers:**
  - The options and `-w` fixes filed on 2026-09-28: BL-488 to BL-528 and BL-599 to
    BL-654 (with BL-661), among them the no-function and unimplemented options,
    `--no-clobber` and the other output options, `-4` and `-6`, the TLS version
    options, `.netrc`, Unix sockets, `-:`, timeouts for every scheme, the `-w` fixes,
    `-Z`, `--proto`, NTLM and Negotiate, the proxy, TLS, HSTS, alt-svc, AWS, FTP, DoH,
    socket and `--libcurl` options.
  - HTTP/2: the decision (BL-655), HPACK (BL-656), frames (BL-657), requests (BL-658),
    ALPN (BL-659), `-v`, `-i` and `%{http_version}` (BL-660), h2c (BL-716), `-Z`
    multiplexing (BL-717) and pooling (BL-817).
  - HTTP/3: the decision (BL-718), the QUIC and HTTP/3 libraries and contracts (BL-719
    to BL-721), QUIC (BL-722 to BL-728), QPACK, framing and requests (BL-729 to
    BL-731), `--http3` (BL-732), alt-svc (BL-733), output (BL-734) and multiplexing
    (BL-735).
- **Exit criteria:** every option in `curl --help all` and every `-w` variable is
  accepted and behaves as the platform's curl does, and `--http2` and `--http3`
  transfers write curl's output and exit codes.
- **Outstanding:** every task under "Delivers" that is not yet in `Tasks/Done`.

## Later / unscheduled

Work that is agreed in principle but not yet sequenced.

Nothing at present. The last item, `--compressed` advertising `zstd`, is done: the
hand-built Zstandard decoder in `Curl.Zstandard` (BL-857 to BL-860) decodes
`Content-Encoding: zstd`, and `--compressed` sends `Accept-Encoding: deflate, gzip, br, zstd`
(BL-861); see
[ADR-0287](Decisions/ADR-0287-compressed-advertises-and-decodes-zstd.md).
