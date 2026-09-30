<#
.SYNOPSIS
    Records one real curl exchange against a canned loopback server, as test fixtures:
    the request bytes curl sent, its stdout, its stderr and its exit code.

.DESCRIPTION
    Expected bytes in Curl's HTTP tests come from real curl, measured first. This script
    is how they are measured. It binds 127.0.0.1:<Port> with
    System.Net.Sockets.TcpListener, runs curl with the given arguments, and for each of
    the first <Connections> connections curl opens it:

      1. reads the request until the header block ends (CRLF CRLF) and, when the
         headers carry Content-Length or Transfer-Encoding: chunked, until the body has
         arrived - each read waits at most one second, so a body that never comes
         ends the read instead of hanging it;
      2. records the raw request bytes;
      3. sends that connection's canned response and closes the connection.

    It then writes five files to OutDirectory:

      request.bin   the raw request bytes, every connection's in the order accepted
      stdout.bin    curl's standard output, byte for byte
      stderr.txt    curl's standard error, byte for byte
      exitcode.txt  curl's exit code, as decimal digits with no line ending
      timing.json   one UTF-8 JSON object: "executable" (the full path of the curl
                    run), "elapsedMilliseconds" (wall clock from start to exit),
                    "peakWorkingSetBytes" and "samples", so the same exchange can be
                    timed for real curl and for Curl

    The peak working set is sampled every 10 ms while curl runs, because it cannot be
    read after the process exits on every platform; the largest reading is kept. Very
    short runs can under-report it, and a run that ends before the first sample
    reports 0 bytes and 0 samples.

    A connection curl never opens is not waited for: once curl exits, the listener is
    stopped. The script exits 0 when the fixtures were written, whatever curl's own
    exit code was; recording a failing curl is as useful as recording a passing one.

.PARAMETER Port
    The loopback TCP port to listen on. The URL in CurlArgs must use the same port.

.PARAMETER Response
    The canned response, as a string with backslash escapes: \r \n \t \0 \\ \" \' and
    \xHH (two hex digits). After decoding, each character becomes one byte (Latin-1),
    so a character above U+00FF is an error. The response is sent exactly as given;
    the script adds nothing. Defaults to an empty 200 with Content-Length: 0.
    Give several, one per connection, to answer a followed redirect's hops
    differently: connection N gets the Nth, and every connection past the last gets
    the last.

.PARAMETER CurlArgs
    The arguments passed to curl, one per element. Each is quoted for the Windows
    command line as needed, so an argument with spaces or quotes reaches curl intact.
    powershell -File hands a comma-separated list to the script as one string, so under
    -File a single CurlArgs string is split at its commas back into the list (BL-528).
    To pass an argument that itself holds a comma, run the script from PowerShell
    (& or .\) instead. An empty element reaches curl as an empty argument (""), as
    when measuring how an option treats a blank value.

.PARAMETER OutDirectory
    Where the four fixture files are written. Created if missing; existing fixture
    files there are overwritten.

.PARAMETER Connections
    How many connections to serve. Default 1. Use more for a run that makes several
    requests, such as several URLs or a followed redirect.

.PARAMETER ResponseDelayMilliseconds
    How long to wait after reading each request before sending the response. Default 0.
    Use it to make a hop take a known time, as when measuring how -m counts across a
    followed redirect.

.PARAMETER Reset
    Instead of reading a request and answering it, reset each connection as soon as it
    is accepted: the socket closes with a zero linger time, so Windows sends a TCP RST
    rather than a FIN. Use it to measure what curl prints when the server resets the
    connection, as during a TLS handshake (BL-369). request.bin is then empty.

.PARAMETER HoldOpenMilliseconds
    After sending each response, keep the connection open instead of closing it, until
    curl closes its end or this many milliseconds pass without a byte from curl, and
    record anything curl sends meanwhile. Default 0: close at once. Use it to measure
    what curl does with a response that stops without the peer closing, such as a head
    that never ends (BL-480). The script always reports how long curl ran.

.PARAMETER RespondAfterBodyBytes
    Instead of reading the whole request, send the response as soon as the header block
    and this many body bytes have arrived, then go on reading (and recording) whatever
    curl still sends until it stops for two seconds or closes its end. Default -1: off.
    Each read then waits up to five seconds, so curl's one-second wait for
    100 Continue does not end the read early. Use it to measure a response that arrives
    while the body is being sent (BL-319, BL-395).

.PARAMETER StandardInput
    What curl reads from standard input, with the same backslash escapes as Response.
    It is written in full and then standard input is closed. Default empty: standard
    input is closed at once. Use it to measure an option that reads '-', such as
    -b - (BL-316).

.PARAMETER Ftp
    Serve one FTP session instead of HTTP responses: send a greeting, then read curl's
    commands one line at a time and answer each from a table of replies, opening a
    passive data connection for EPSV, PASV, RETR, LIST, STOR and APPE. request.bin then holds every
    command line curl sent on the control connection, and transcript.txt holds both
    directions, each line prefixed "> " (curl) or "< " (server). Response,
    Connections, ResponseDelayMilliseconds and Reset are ignored.

    The default replies are: greeting 220, USER 331, PASS 230, PWD 257 "/", EPSV 229
    with the data port, PASV 227 with 127.0.0.1 and the data port, TYPE 200, SIZE 213
    with FtpData's length, MDTM 213 20260927123456, CWD 250, REST 350 (remembering the
    offset), RETR 150 then FtpData from the last REST offset over the data connection
    then 226 (LIST and NLST the same), STOR 150 then every byte curl sends over the data
    connection until it closes it then 226 (APPE the same), QUIT 221 (and the session
    ends), and 502 for any other command. A data connection curl closes early (a range
    read) is not an error. The bytes received on STOR's and APPE's data connections are
    written to upload.bin, and each upload adds one "= <n> bytes received on the data
    connection: <bytes>" line to transcript.txt (BL-439).

    Active mode and TLS (BL-437): EPRT and PORT are answered 200 and remember the address
    curl announced, and every later data connection is then dialled to that address instead
    of accepted on the passive listener. AUTH is answered 234 and the control connection is
    then served over TLS with the same throwaway certificate as -Tls (curl needs -k), with a
    "= TLS handshake completed on the control connection" line in transcript.txt; an
    overridden AUTH whose reply starts 234 is upgraded the same way. PBSZ is answered 200,
    PROT 200, and after PROT P (or with -Tls, until a PROT C or a refused PROT) every data
    connection is TLS too, ended with close_notify when the server sends.

    CCC (BL-636) is answered 200, and the server then clears TLS from the control
    connection: it sends close_notify, reads curl's close_notify if that is what curl sends
    next, and reads the rest of the session in plain text, with a "= TLS cleared from the
    control connection" line in transcript.txt (or "= TLS cleared from the control
    connection without curl's close_notify" when curl went straight on in plain text, as
    --ftp-ssl-ccc-mode passive does, or hung up). An overridden CCC whose reply is below
    500 is cleared the same way, as curl clears it; a 5xx leaves the control connection
    in TLS.

.PARAMETER FtpReply
    Overrides for the FTP reply table, each 'VERB=reply' with the same backslash escapes
    as Response, e.g. 'PASS=430 Access denied'. The reply is sent as given with CRLF
    appended. VERB is a command name in capitals, GREETING for the greeting, or RETRDONE
    for the reply sent after RETR's or LIST's data, or STORDONE for the reply sent after
    STOR's or APPE's data. An overridden EPSV, PASV, RETR, LIST, NLST, STOR or APPE sends
    only the reply: no data connection is offered. The reply CLOSE closes the control
    connection instead of answering, e.g. 'PWD=CLOSE', and the reply STALL sends nothing and
    waits for curl's next command, e.g. 'USER=STALL' or 'GREETING=STALL' (BL-512). Several overrides for one VERB are
    answered in the order given, one per command, the last repeating for the rest, e.g.
    'CWD=550 No such directory' 'CWD=250 OK' (BL-436). In a reply, {DATAPORT} stands for
    the data listener's port and {DATAPORT_HI} and {DATAPORT_LO} for its two PASV numbers,
    e.g. 'PASV=227 Entering Passive Mode (127,0,0,2,{DATAPORT_HI},{DATAPORT_LO})'.

.PARAMETER FtpData
    The file served by RETR, and the listing served by LIST, in -Ftp mode, with the
    same backslash escapes as Response.
    Default empty.

.PARAMETER FtpDataHoldMilliseconds
    How long, in -Ftp mode, the server keeps a RETR, LIST or NLST data connection open after
    sending FtpData, before it closes it and sends the 226. Default 0. With a SIZE override
    larger than FtpData it stalls a download midway, to measure -m mid-RETR (BL-512).

.PARAMETER Smtp
    Serve one SMTP session instead of HTTP responses (BL-529): send a greeting, then read
    curl's command lines one at a time and answer each from a table of replies, as -Ftp
    does. request.bin then holds every line curl sent, the message body after DATA
    included, and transcript.txt holds both directions, each line prefixed "> " (curl)
    or "< " (server). Response, Connections, ResponseDelayMilliseconds and Reset are
    ignored. With -Tls it serves implicit TLS from the first byte, as smtps:// expects.

    The default replies (RFC 5321) are: greeting 220 localhost ESMTP; EHLO a multiline
    250 advertising AUTH PLAIN LOGIN CRAM-MD5, STARTTLS (left out once the session is
    TLS), SIZE 1000000, 8BITMIME and SMTPUTF8; HELO 250; AUTH 235, after the 334
    continuations the mechanism needs (PLAIN without an initial response one, LOGIN one
    per credential it still lacks, CRAM-MD5 one challenge, DIGEST-MD5 a fixed RFC 2831
    challenge for realm "localhost" and nonce "OA6MG9tEQGm2hh" and then an rspauth line
    that is not checked), each continuation line curl
    sends recorded like a command; MAIL 250; RCPT 250; DATA 354, then every line up to
    and including the lone "." that ends the body, then 250; VRFY 250; EXPN 250; HELP
    214; NOOP 250; RSET 250; QUIT 221 (and the session ends); and 502 for any other
    command. STARTTLS is answered 220 and the session is then served over TLS with the
    same throwaway certificate as -Tls (curl needs -k), with a "= TLS handshake completed
    on the control connection" line in transcript.txt.

.PARAMETER SmtpReply
    Overrides for the SMTP reply table, each 'VERB=reply' with the same backslash escapes
    as Response, e.g. 'RCPT=550 no such user'. The reply is sent as given with CRLF
    appended; a multiline reply is written with \r\n between its lines. VERB is a command
    name in capitals, GREETING for the greeting, or DATADONE for the reply sent after the
    message body. An overridden AUTH sends only the reply, with no continuations. An
    overridden DATA whose reply starts 354 still reads the body, and an overridden
    STARTTLS whose reply starts 220 still switches to TLS. The reply CLOSE and the use of
    several overrides for one VERB work as in FtpReply.

.PARAMETER SmtpIdleMilliseconds
    How long, in -Smtp mode, the server waits for curl's next line before it hangs up.
    Default 5000.

.PARAMETER Imap
    Serve one IMAP4rev1 session instead of HTTP responses (BL-531): send an untagged
    greeting, then read curl's tagged commands one at a time and answer each with its
    untagged data and a tagged completion that echoes curl's tag (A001 OK ...), from a
    table of replies. A command line ending in a literal {n} is answered "+ Ready for
    literal data" (nothing for a non-synchronizing {n+}) and its n bytes and the rest of
    the command are read before the reply, so an APPEND upload is recorded whole.
    request.bin then holds every byte curl sent, literals included, and transcript.txt
    holds both directions, each line prefixed "> " (curl) or "< " (server). Response,
    Connections, ResponseDelayMilliseconds and Reset are ignored. With -Tls it serves
    implicit TLS from the first byte, as imaps:// expects.

    The default replies (RFC 3501) are: greeting
    * OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready; CAPABILITY the same
    list (STARTTLS left out once the session is TLS); LOGIN OK; AUTHENTICATE OK, after
    the "+" continuations the mechanism needs (PLAIN without an initial response one,
    LOGIN one per credential it still lacks), each continuation line curl sends recorded
    like a command; SELECT and EXAMINE FLAGS, * 2 EXISTS, * 0 RECENT,
    * OK [UIDVALIDITY 1] and * OK [UIDNEXT 3], then OK [READ-WRITE] or [READ-ONLY];
    FETCH and UID FETCH * <n> FETCH (<item> {length}) carrying ImapMessage as a literal,
    for the first message number curl named and echoing the item it asked for; LIST two
    mailboxes, INBOX and Sent; SEARCH and UID SEARCH * SEARCH 1 2; APPEND OK once its
    literal has arrived; NOOP OK; LOGOUT * BYE then OK (and the session ends); and BAD
    for any other command. STARTTLS is answered OK and the session is then served over
    TLS with the same throwaway certificate as -Tls (curl needs -k), with a
    "= TLS handshake completed on the control connection" line in transcript.txt.

.PARAMETER ImapReply
    Overrides for the IMAP reply table, each 'COMMAND=reply' with the same backslash
    escapes as Response, e.g. 'LOGIN=NO denied'. COMMAND is the command name in capitals,
    'UID FETCH' or 'UID SEARCH' for a UID command, or GREETING for the greeting. The reply
    is written without the tag: its last line is sent with curl's tag in front and any
    lines before it, separated by \r\n, are sent as given, so
    'SELECT=* 0 EXISTS\r\nOK [READ-WRITE] done' serves an empty mailbox. The greeting is
    sent exactly as given. An overridden AUTHENTICATE sends only the reply, with no
    continuations; an overridden STARTTLS whose last line starts OK still switches to
    TLS; a literal is always read after its "+" continuation, whatever the reply. The
    reply CLOSE and the use of several overrides for one COMMAND work as in FtpReply.

.PARAMETER ImapMessage
    The message FETCH and UID FETCH serve as a literal in -Imap mode, with the same
    backslash escapes as Response. Default a four-line message ending in CRLF:
    From: sender@example.com, To: recipient@example.com, Subject: Recorded, a blank line
    and "Hello from the recorder.".

.PARAMETER ImapIdleMilliseconds
    How long, in -Imap mode, the server waits for curl's next line before it hangs up.
    Default 5000.

.PARAMETER Pop3
    Serve one POP3 session instead of HTTP responses (BL-530): send a greeting, then read
    curl's command lines one at a time and answer each from a table of replies, as -Smtp
    does. A multi-line reply ends with a lone "." line and every line of it that starts
    with "." is dot-stuffed (RFC 1939 3). request.bin then holds every line curl sent and
    transcript.txt holds both directions, each line prefixed "> " (curl) or "< " (server),
    multi-line replies as sent, stuffing included. Response, Connections,
    ResponseDelayMilliseconds and Reset are ignored. With -Tls it serves implicit TLS from
    the first byte, as pop3s:// expects.

    The default replies (RFC 1939, RFC 2449, RFC 5034) are: greeting
    +OK POP3 ready <1896.697170952@localhost>, the timestamp APOP digests; CAPA a
    multi-line list of USER, SASL PLAIN LOGIN, STLS (left out once the session is TLS),
    TOP and UIDL; USER +OK; PASS +OK; APOP +OK; AUTH +OK, after the "+" continuations the
    mechanism needs (PLAIN without an initial response one, LOGIN one per credential it
    still lacks), each continuation line curl sends recorded like a command, and AUTH
    without a mechanism a multi-line list of PLAIN and LOGIN; a maildrop of two copies of
    Pop3Message, so STAT +OK 2 <both sizes>, LIST a multi-line listing of both and
    LIST n the one line +OK n <size>; RETR n Pop3Message multi-line; TOP n k its headers,
    the blank line and its first k body lines, multi-line; UIDL a multi-line listing of
    uid-1 and uid-2 and UIDL n one line; DELE +OK; RSET +OK; NOOP +OK; QUIT +OK (and the
    session ends); and -ERR for any other command. STLS is answered +OK and the session is
    then served over TLS with the same throwaway certificate as -Tls (curl needs -k), with
    a "= TLS handshake completed on the control connection" line in transcript.txt.

.PARAMETER Pop3Reply
    Overrides for the POP3 reply table, each 'VERB=reply' with the same backslash escapes
    as Response, e.g. 'PASS=-ERR denied'. VERB is a command name in capitals or GREETING
    for the greeting. The reply is sent exactly as given with CRLF appended, so a
    multi-line reply is written with \r\n between its lines and ends in its own "."
    line, e.g. 'LIST=+OK\r\n.' for an empty maildrop. An overridden AUTH sends only the
    reply, with no continuations, and an overridden STLS whose reply starts +OK still
    switches to TLS. The reply CLOSE and the use of several overrides for one VERB work
    as in FtpReply.

.PARAMETER Pop3Message
    The message RETR and TOP serve, and whose length LIST and STAT report, in -Pop3 mode,
    with the same backslash escapes as Response. It is dot-stuffed as it is sent. Default
    a six-line message ending in CRLF: From: sender@example.com, To:
    recipient@example.com, Subject: Recorded, a blank line, "Hello from the recorder."
    and ".A line that starts with a dot.", which exercises the stuffing.

.PARAMETER Pop3IdleMilliseconds
    How long, in -Pop3 mode, the server waits for curl's next line before it hangs up.
    Default 5000.

.PARAMETER Tftp
    Serve one TFTP transfer on UDP instead of HTTP responses (BL-932): bind UDP on
    ListenAddress and Port, take curl's first request there, and answer it from a fresh
    ephemeral port, as RFC 1350 requires. A read request (RRQ) is answered with TftpData
    in DATA blocks of the agreed block size, each sent once the previous one is
    acknowledged; a write request (WRQ) is answered with ACK 0 and every DATA block curl
    sends is acknowledged, its bytes written to upload.bin. When the request carries
    blksize, tsize or timeout (RFC 2347, 2348, 2349), the first answer is an OACK echoing
    them - tsize filled in with TftpData's length for an RRQ - unless -TftpNoOack is given,
    and the transfer then uses the requested block size. A repeated ACK (or DATA) is
    answered by sending the last packet again, and a repeated request by sending the first
    answer again, so curl's retransmissions are served. The transfer ends when the last
    block is acknowledged, when curl sends an ERROR, or after TftpIdleMilliseconds without
    a datagram.

    datagrams.txt then holds one line per datagram in both directions, in order: "> " for
    curl's, "< " for the server's, the bytes as lowercase hex, a space and a decoded
    summary, e.g. "> 000166696c65006f6374657400... RRQ file octet blksize=512 tsize=0",
    "< 00030001... DATA 1 6 bytes", "> 00040001 ACK 1", "< 0005... ERROR 1 File not found"
    or "< 0006... OACK tsize=6". There is no request.bin. Combining it with -Ftp, -Smtp,
    -Imap, -Pop3, -Script, -Tls, -NoServer, -UnixSocket or -UdpSink is refused.

.PARAMETER TftpData
    The file an RRQ is answered with, in -Tftp mode, with the same backslash escapes as
    Response. Default 'hello\n'.

.PARAMETER TftpReply
    Overrides for the -Tftp transfer, each 'STEP=reply'. 'RRQ=ERROR <code> <text>' (or
    WRQ) answers the request with an ERROR packet of that code and text instead of a
    transfer, e.g. 'RRQ=ERROR 1 File not found'. '<packet><n>=DROP', where packet is ACK
    or DATA, drops the n-th packet of that kind once, whichever side sends it: one curl
    sends is ignored the first time it arrives, one the server sends is not sent the first
    time, so curl times out and retransmits. E.g. 'ACK1=DROP' on a download ignores curl's
    ACK 1, and 'DATA1=DROP' does not send block 1 until curl asks again.

.PARAMETER TftpNoOack
    In -Tftp mode, ignore the options a request carries: answer with no OACK and transfer
    in RFC 1350's 512-byte blocks.

.PARAMETER TftpIdleMilliseconds
    How long, in -Tftp mode, the server waits for curl's next datagram before it ends the
    transfer. Default 15000, longer than curl's retransmission interval.

.PARAMETER Script
    Serve one connection from a script of steps instead of HTTP responses, for binary
    request-reply protocols that no line-at-a-time mode speaks, such as LDAP's BER
    messages or SMB's frames (BL-532). The value is the path of a text file with one step
    per line, run in order; blank lines and lines starting with # are skipped:

      read        wait up to ScriptIdleMilliseconds for curl's first byte, then keep
                  reading until curl is silent for ScriptGapMilliseconds
      read <N>    read until N bytes have arrived
      read ber    read one whole BER element (X.690 8.1), such as one LDAP message,
                  from its identifier and definite length octets
      send <b>    send the bytes <b>, with the same backslash escapes as Response
      close       close the connection and end the session
      reset       reset the connection (RST, not FIN) and end the session, so curl's
                  next receive or send fails with an I/O error (BL-845)

    A read takes only the bytes it asked for; any curl sent beyond them wait for the next
    read. A read that gets no byte for ScriptIdleMilliseconds, or during which curl closes
    its end, ends the session with a line saying so in transcript.txt, instead of
    hanging. When the steps run out, the connection is closed. When the first step is a
    read, every connection curl opens is accepted and the session is served on the first
    one curl writes to: the Windows build's WinLDAP opens a connection of its own beside
    the one curl opened, and speaks only on its own. The others are closed when the
    session ends, and transcript.txt notes how many there were. Once the session ends the
    listener stops, so a connection curl opens afterwards is refused at once. request.bin then holds
    every byte curl sent, and transcript.txt one line per step: "> " and the bytes a read
    took, or "< " and the bytes sent, as lowercase hex pairs separated by spaces, and "= "
    lines for how the session ended. Response, Connections, ResponseDelayMilliseconds and
    Reset are ignored. With -Tls it serves TLS from the first byte, as ldaps:// expects.

    For example, this script answers curl's LDAP simple bind with success and records
    the search request that follows it:

      # BindRequest from curl
      read ber
      # BindResponse, message ID 1, resultCode success (RFC 4511 4.2.2)
      send \x30\x0c\x02\x01\x01\x61\x07\x0a\x01\x00\x04\x00\x04\x00
      # SearchRequest from curl
      read ber
      close

.PARAMETER ScriptIdleMilliseconds
    How long, in -Script mode, a read waits for a byte from curl before it ends the
    session. Default 5000.

.PARAMETER ScriptGapMilliseconds
    How long, in -Script mode, a bare read waits for more bytes after the last that
    arrived before it takes what it has. Default 250.

.PARAMETER Tls
    Answer each connection over TLS 1.2 instead of plain TCP, so the recorder can stand
    in for an HTTPS server or an HTTPS proxy (BL-398, BL-442). The certificate served
    is a throwaway self-signed RSA 2048 certificate for CN=127.0.0.1 (subject
    alternative name IP 127.0.0.1), made for this run and valid for one day; curl needs
    -k (or --proxy-insecure for a proxy) to accept it. request.bin, and the Response
    matching, hold the decrypted bytes, not the TLS records. The certificate is never
    added to a certificate store, and its key container is deleted when the run ends.
    A connection whose handshake fails is recorded as empty. -Reset resets the connection
    before any handshake. With -Ftp it serves implicit FTPS: the control connection is TLS
    from its first byte, as ftps:// expects (BL-437); with -Smtp, implicit SMTPS, as
    smtps:// expects (BL-529); with -Imap, implicit IMAPS, as imaps:// expects (BL-531);
    with -Pop3, implicit POP3S, as pop3s:// expects (BL-530); with -Script, TLS from the
    first byte, as ldaps:// expects (BL-532).

.PARAMETER TlsCloseNotify
    With -Tls, send TLS close_notify after each response before closing the connection.
    Without it the connection closes with no close_notify, a bare end of the transport
    (BL-819). HTTP mode only.

.PARAMETER TlsRootCertificateFile
    With -Tls, serve a certificate issued by a throwaway private root CA in place of the
    self-signed one, and write that root's PEM to this path so curl can be given
    --cacert <path> (BL-490); the root may sign certificates and CRLs. Neither certificate
    names a revocation endpoint, so the Schannel build's revocation check of the leaf ends
    "status unknown", as ADR-0086 describes. The root's key is never written; the file is left for the caller to delete.

.PARAMETER TlsEmptyCrlFile
    With -TlsRootCertificateFile, also write a PEM "X509 CRL" signed by the throwaway root
    that revokes nothing, so curl can be given --crlfile <path> (BL-609). It is a version 1
    list (no extensions), valid from five minutes ago for one day. Left for the caller to
    delete.

.PARAMETER TlsRevokingCrlFile
    With -TlsRootCertificateFile, also write a PEM "X509 CRL" signed by the throwaway root
    that revokes the served certificate by its serial number, otherwise as TlsEmptyCrlFile
    (BL-609). Left for the caller to delete.

.PARAMETER TlsPublicKeyFile
    With -Tls, write the served certificate's public key (its SubjectPublicKeyInfo) to this
    path as a PEM "PUBLIC KEY" block, and the same key as DER to this path with ".der"
    appended, before curl runs, so curl can be given --pinnedpubkey <path> (BL-608). With
    -Tls, whether or not this is given, every "{TlsPublicKeySha256}" in CurlArgs is replaced
    by the base64 SHA-256 of that SubjectPublicKeyInfo, the hash --pinnedpubkey sha256//
    names. The files are left for the caller to delete.

.PARAMETER FtpIdleMilliseconds
    How long, in -Ftp mode, the server waits for curl's next command before it hangs up.
    Default 5000. Raise it to measure a wait longer than five seconds, such as curl's
    60-second accept timeout when an active-mode data connection never arrives (BL-437).

.PARAMETER Curl
    The curl executable to run. Defaults to the reference build ADR-0009 and ADR-0018
    name, curl 8.21.0 from Git for Windows' mingw64 directory, found beside git.exe.

.PARAMETER ListenAddress
    The address the server (and, with -Ftp, its passive data listener, named in PASV's
    227 reply) binds. Default 127.0.0.1. Give the Windows host's address on the WSL
    network, with -Curl wsl.exe and CurlArgs '-d','Ubuntu','--','curl',..., to measure
    the Linux (OpenSSL) build of curl, which cannot reach Windows' loopback (BL-474).

.PARAMETER NoServer
    Bind no port and serve nothing: run curl against a server the caller started, such
    as a local OpenSSH sshd, an LDAP server or an SMB share, which a PowerShell loopback
    server cannot speak (BL-528). Only stdout.bin, stderr.txt, exitcode.txt and timing.json
    are written; there is no request.bin, since the script sees none of the traffic. Port,
    Response, Connections, ResponseDelayMilliseconds, Reset, HoldOpenMilliseconds,
    RespondAfterBodyBytes, FtpReply, FtpData, FtpIdleMilliseconds, SmtpReply,
    SmtpIdleMilliseconds, ImapReply, ImapMessage, ImapIdleMilliseconds, Pop3Reply,
    Pop3Message, Pop3IdleMilliseconds, ScriptIdleMilliseconds, ScriptGapMilliseconds and
    ListenAddress are ignored, and Port need not be given.
    Combining it with a server mode, -Ftp, -Smtp, -Imap, -Pop3, -Script or -Tls, is refused. StandardInput and Curl work as in every other mode.

.PARAMETER UdpSink
    Also bind UDP on ListenAddress and Port, and take every datagram curl sends there
    without ever answering, so a QUIC attempt (--http3, --http3-only) meets a silent
    peer rather than an ICMP port unreachable (BL-718). After curl exits, datagrams.txt
    holds one line per datagram in the order received, its bytes as lowercase hex. A
    client's first QUIC Initial can be decrypted from it offline (RFC 9001 section 5.2),
    which is how curl's transport parameters are read. Works in every mode, -NoServer
    included; Port must then be given.

.PARAMETER DnsPort
    Also run a DNS responder (BL-694) on ListenAddress, UDP and TCP, on each of these ports,
    for measuring curl's c-ares resolver with --dns-servers. Each query is answered with
    the query's ID and question, flags 0x8180 (a response, RD and RA set, NOERROR), and one
    answer record per DnsAnswerAddress of the asked family (A or AAAA; any other type gets
    none), TTL 60. After curl exits, dns.txt holds one line per query, in the order
    received: the milliseconds since the responder started (just before curl), the transport (udp or tcp), the port it
    arrived on, and the query bytes as lowercase hex (a TCP query without its two-byte
    length prefix). Works in every mode, -NoServer included.

.PARAMETER DnsSilentPort
    DnsPort ports that record each UDP query and never answer, so a server that does not
    reply can be measured beside one that does (BL-694). Give each port in DnsPort too.

.PARAMETER DnsAnswerAddress
    The addresses the DNS responder answers with. Default 127.0.0.1.

.PARAMETER DnsTruncate
    Answer every UDP query with the TC (truncated) flag set and no answer records, so the
    resolver has to ask again over TCP, which is answered in full (BL-694).

.PARAMETER DnsResponseCode
    The RCODE every DNS answer carries, e.g. 3 (NXDOMAIN) or 2 (SERVFAIL); any value but 0
    sends no answer records. Default 0 (NOERROR).

.PARAMETER UnixSocket
    Listen on a Unix domain socket at this path instead of TCP, for --unix-socket (BL-507).
    Any file already at the path is deleted first, and the socket file is deleted at the
    end. Only the HTTP mode serves over it: combining it with -Ftp, -Smtp, -Imap, -Pop3, -Script,
    -Tls, -NoServer or -UdpSink is refused. Port need not be given. Windows PowerShell 5.1
    runs on .NET Framework, which has no UnixDomainSocketEndPoint, so the socket is bound
    through a small C# EndPoint compiled with Add-Type; Windows 10 1803 or later has AF_UNIX.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18081 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -CurlArgs 'http://127.0.0.1:18081/a?b' -OutDirectory fixtures\default-get

    request.bin then holds
    GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.21.0\r\nAccept: */*\r\n\r\n
    and stdout.bin holds hello.

.EXAMPLE
    .\Record-CurlExchange.ps1 -NoServer -CurlArgs '-sS','-k','sftp://tester:secret@127.0.0.1:2222/home/tester/a.txt' -OutDirectory fixtures\sftp-get

    Runs curl against an sshd the caller already started on port 2222 and records only
    what curl printed and its exit code.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18025 -Smtp -SmtpReply 'RCPT=550 no such user' -CurlArgs '-sS','--mail-from','a@b','--mail-rcpt','c@d','-T','mail.txt','smtp://127.0.0.1:18025/' -OutDirectory fixtures\smtp-rcpt-refused

    Serves one SMTP session that refuses the recipient; transcript.txt shows EHLO,
    MAIL FROM, RCPT TO and the 550, and request.bin the lines curl sent.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18143 -Imap -CurlArgs '-sS','-u','u:p','imap://127.0.0.1:18143/INBOX;UID=1' -OutDirectory fixtures\imap-fetch

    Serves one IMAP session; transcript.txt shows CAPABILITY, AUTHENTICATE PLAIN, SELECT
    INBOX, UID FETCH 1 BODY[] and LOGOUT with their tagged replies, and stdout.bin the
    message curl printed.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18110 -Pop3 -CurlArgs '-sS','-u','u:p','pop3://127.0.0.1:18110/1' -OutDirectory fixtures\pop3-retr

    Serves one POP3 session; transcript.txt shows CAPA, AUTH PLAIN, RETR 1 with the
    dot-stuffed message and QUIT, and stdout.bin the message curl printed.

.EXAMPLE
    .\Record-CurlExchange.ps1 -Port 18389 -Script ldap-bind-search.txt -CurlArgs 'ldap://127.0.0.1:18389/dc=example' -OutDirectory fixtures\ldap-search

    Runs the steps in ldap-bind-search.txt (the example under .PARAMETER Script);
    request.bin then holds curl's BindRequest and SearchRequest, and transcript.txt both
    directions as hex.
#>
[CmdletBinding()]
param(
    [ValidateRange(0, 65535)] [int] $Port = 0,
    [string[]] $Response = @('HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n'),
    [Parameter(Mandatory = $true)] [AllowEmptyString()] [string[]] $CurlArgs,
    [Parameter(Mandatory = $true)] [string] $OutDirectory,
    [ValidateRange(1, 1000)] [int] $Connections = 1,
    [ValidateRange(0, 600000)] [int] $ResponseDelayMilliseconds = 0,
    [switch] $Reset,
    [ValidateRange(0, 600000)] [int] $HoldOpenMilliseconds = 0,
    [ValidateRange(-1, [int]::MaxValue)] [int] $RespondAfterBodyBytes = -1,
    [string] $StandardInput = '',
    [switch] $Ftp,
    [string[]] $FtpReply = @(),
    [string] $FtpData = '',
    [ValidateRange(1, 600000)] [int] $FtpIdleMilliseconds = 5000,
    [ValidateRange(0, 600000)] [int] $FtpDataHoldMilliseconds = 0,
    [switch] $Smtp,
    [string[]] $SmtpReply = @(),
    [ValidateRange(1, 600000)] [int] $SmtpIdleMilliseconds = 5000,
    [switch] $Imap,
    [string[]] $ImapReply = @(),
    [string] $ImapMessage = 'From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n',
    [ValidateRange(1, 600000)] [int] $ImapIdleMilliseconds = 5000,
    [switch] $Pop3,
    [string[]] $Pop3Reply = @(),
    [string] $Pop3Message = 'From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n.A line that starts with a dot.\r\n',
    [ValidateRange(1, 600000)] [int] $Pop3IdleMilliseconds = 5000,
    [switch] $Tftp,
    [string] $TftpData = 'hello\n',
    [string[]] $TftpReply = @(),
    [switch] $TftpNoOack,
    [ValidateRange(1, 600000)] [int] $TftpIdleMilliseconds = 15000,
    [string] $Script,
    [ValidateRange(1, 600000)] [int] $ScriptIdleMilliseconds = 5000,
    [ValidateRange(1, 600000)] [int] $ScriptGapMilliseconds = 250,
    [switch] $Tls,
    [switch] $TlsCloseNotify,
    [string] $TlsRootCertificateFile,
    [string] $TlsEmptyCrlFile,
    [string] $TlsRevokingCrlFile,
    [string] $TlsPublicKeyFile,
    [string] $Curl,
    [System.Net.IPAddress] $ListenAddress = [System.Net.IPAddress]::Loopback,
    [switch] $NoServer,
    [switch] $UdpSink,
    [int[]] $DnsPort = @(),
    [int[]] $DnsSilentPort = @(),
    [System.Net.IPAddress[]] $DnsAnswerAddress = @([System.Net.IPAddress]::Loopback),
    [switch] $DnsTruncate,
    [ValidateRange(0, 15)] [int] $DnsResponseCode = 0,
    [string] $UnixSocket
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# powershell -File binds '-sS','http://...' as the one string "-sS,http://..."; only then
# is the invocation line empty, so only then is that string split back into its elements.
if ([string]::IsNullOrEmpty($MyInvocation.Line) -and $CurlArgs.Count -eq 1) { $CurlArgs = $CurlArgs[0].Split(',') }
if ($NoServer -and ($Ftp -or $Smtp -or $Imap -or $Pop3 -or $Script -or $Tls)) { throw '-NoServer runs no server, so it cannot be combined with -Ftp, -Smtp, -Imap, -Pop3, -Script or -Tls.' }
if (@($Ftp, $Smtp, $Imap, $Pop3, [bool] $Script | Where-Object { $_ }).Count -gt 1) { throw '-Ftp, -Smtp, -Imap, -Pop3 and -Script each serve a whole session; give one of them.' }
if ($UnixSocket -and ($NoServer -or $Ftp -or $Smtp -or $Imap -or $Pop3 -or $Script -or $Tls -or $UdpSink)) { throw '-UnixSocket serves HTTP only, so it cannot be combined with -NoServer, -Ftp, -Smtp, -Imap, -Pop3, -Script, -Tls or -UdpSink.' }
if (-not $NoServer -and -not $UnixSocket -and $Port -eq 0) { throw '-Port is required unless -NoServer is given: the URL in CurlArgs must name the port the server listens on.' }
if ($UdpSink -and $Port -eq 0) { throw '-UdpSink binds UDP on -Port, so -Port is required with it.' }
if ($Tftp -and ($Ftp -or $Smtp -or $Imap -or $Pop3 -or $Script -or $Tls -or $NoServer -or $UnixSocket -or $UdpSink)) { throw '-Tftp serves one TFTP transfer on UDP -Port, so it cannot be combined with -Ftp, -Smtp, -Imap, -Pop3, -Script, -Tls, -NoServer, -UnixSocket or -UdpSink.' }
if (@($DnsSilentPort | Where-Object { $DnsPort -notcontains $_ }).Count -gt 0) { throw '-DnsSilentPort names a port -DnsPort does not; give each silent port in -DnsPort too.' }

function Get-ReferenceCurlPath {
    $git = Get-Command git.exe -ErrorAction SilentlyContinue
    if ($null -ne $git) {
        # git.exe lives in <Git>\cmd or <Git>\bin; the reference curl in <Git>\mingw64\bin.
        $gitRoot = Split-Path (Split-Path $git.Source -Parent) -Parent
        $candidate = Join-Path $gitRoot 'mingw64\bin\curl.exe'
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    $candidate = Join-Path $env:ProgramFiles 'Git\mingw64\bin\curl.exe'
    if (Test-Path -LiteralPath $candidate) { return $candidate }
    throw 'The reference curl (Git for Windows mingw64\bin\curl.exe) was not found; pass -Curl.'
}

function ConvertFrom-EscapedResponse {
    param([string] $Text)
    $bytes = New-Object System.Collections.Generic.List[byte]
    $index = 0
    while ($index -lt $Text.Length) {
        $character = $Text[$index]
        if ($character -ne '\') {
            if ([int] $character -gt 255) { throw "Response character U+$(([int] $character).ToString('X4')) is not Latin-1." }
            $bytes.Add([byte] [int] $character)
            $index++
            continue
        }
        if ($index + 1 -ge $Text.Length) { throw 'Response ends with a lone backslash.' }
        $escape = $Text[$index + 1]
        switch -CaseSensitive ([string] $escape) {
            'r' { $bytes.Add(13); $index += 2 }
            'n' { $bytes.Add(10); $index += 2 }
            't' { $bytes.Add(9); $index += 2 }
            '0' { $bytes.Add(0); $index += 2 }
            '\' { $bytes.Add(92); $index += 2 }
            '"' { $bytes.Add(34); $index += 2 }
            "'" { $bytes.Add(39); $index += 2 }
            'x' {
                $hex = if ($index + 4 -le $Text.Length) { $Text.Substring($index + 2, 2) } else { '' }
                if ($hex -notmatch '^[0-9A-Fa-f]{2}$') { throw "Response has a malformed \x escape at index $index." }
                $bytes.Add([Convert]::ToByte($hex, 16))
                $index += 4
            }
            default { throw "Response has an unknown escape \$escape at index $index." }
        }
    }
    return , $bytes.ToArray()
}

function ConvertTo-CommandLineArgument {
    # Quotes one argument so CommandLineToArgvW (and the C runtime) reads it back unchanged.
    param([string] $Argument)
    if ($Argument.Length -gt 0 -and $Argument -notmatch '[\s"]') { return $Argument }
    $builder = New-Object System.Text.StringBuilder
    [void] $builder.Append('"')
    $backslashes = 0
    foreach ($character in $Argument.ToCharArray()) {
        if ($character -eq '\') { $backslashes++; continue }
        if ($character -eq '"') {
            [void] $builder.Append('\', 2 * $backslashes + 1)
        } elseif ($backslashes -gt 0) {
            [void] $builder.Append('\', $backslashes)
        }
        $backslashes = 0
        [void] $builder.Append($character)
    }
    if ($backslashes -gt 0) { [void] $builder.Append('\', 2 * $backslashes) }
    [void] $builder.Append('"')
    return $builder.ToString()
}

# The server runs in its own runspace so curl can run in this one. It returns one
# byte array per connection served.
$serveConnections = {
    param($Listener, $ResponseBytes, [int] $ConnectionCount, [int] $DelayMilliseconds, [bool] $ResetConnections, [int] $EarlyResponseBodyBytes, $TlsCertificate, [int] $HoldOpen, [bool] $CloseNotify)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)

    function Test-RequestComplete {
        param([byte[]] $Received, [int] $Length)
        $text = $latin1.GetString($Received, 0, $Length)
        $headerEnd = $text.IndexOf("`r`n`r`n")
        if ($headerEnd -lt 0) { return $false }
        $bodyStart = $headerEnd + 4
        if ($EarlyResponseBodyBytes -ge 0) { return ($Length - $bodyStart) -ge $EarlyResponseBodyBytes }
        $headers = $text.Substring(0, $headerEnd)
        $contentLength = [regex]::Match($headers, '(?im)^Content-Length:[ \t]*(\d+)[ \t]*$')
        if ($contentLength.Success) {
            return ($Length - $bodyStart) -ge [long] $contentLength.Groups[1].Value
        }
        if ($headers -match '(?im)^Transfer-Encoding:.*\bchunked\b') {
            $body = $text.Substring($bodyStart)
            return $body.StartsWith("0`r`n`r`n") -or $body.EndsWith("`r`n0`r`n`r`n")
        }
        return $true
    }

    $requests = New-Object System.Collections.Generic.List[object]
    for ($served = 0; $served -lt $ConnectionCount; $served++) {
        try {
            $client = $Listener.AcceptTcpClient()
        } catch {
            break  # The listener was stopped: curl exited without opening this connection.
        }
        if ($ResetConnections) {
            # A zero linger time makes Close send RST instead of FIN.
            $client.LingerState = New-Object System.Net.Sockets.LingerOption($true, 0)
            $client.Close()
            $requests.Add([byte[]] @())
            continue
        }
        try {
            $stream = $client.GetStream()
            if ($null -ne $TlsCertificate) {
                $stream = New-Object System.Net.Security.SslStream($stream, $false)
                $stream.ReadTimeout = 5000
                try {
                    $stream.AuthenticateAsServer($TlsCertificate, $false, [System.Security.Authentication.SslProtocols]::Tls12, $false)
                } catch {
                    # curl refused the certificate or hung up mid-handshake: nothing was sent.
                    $requests.Add([byte[]] @())
                    continue
                }
            }
            $stream.ReadTimeout = if ($EarlyResponseBodyBytes -ge 0) { 5000 } else { 1000 }
            $buffer = New-Object byte[] 65536
            $received = New-Object System.IO.MemoryStream
            while (-not (Test-RequestComplete -Received $received.GetBuffer() -Length ([int] $received.Length))) {
                try {
                    $count = $stream.Read($buffer, 0, $buffer.Length)
                } catch [System.IO.IOException] {
                    break  # One second without a byte: take what arrived.
                }
                if ($count -le 0) { break }
                $received.Write($buffer, 0, $count)
            }
            if ($DelayMilliseconds -gt 0) { [System.Threading.Thread]::Sleep($DelayMilliseconds) }
            [byte[]] $response = $ResponseBytes[[Math]::Min($served, $ResponseBytes.Count - 1)]
            try {
                $stream.Write($response, 0, $response.Length)
                $stream.Flush()
            } catch [System.IO.IOException] {
                # curl already closed its end; the request is still worth recording.
            }
            if ($EarlyResponseBodyBytes -ge 0) {
                # Answered mid-body: record whatever curl goes on sending until it stops.
                $stream.ReadTimeout = 2000
                while ($true) {
                    try {
                        $count = $stream.Read($buffer, 0, $buffer.Length)
                    } catch [System.IO.IOException] {
                        break
                    }
                    if ($count -le 0) { break }
                    $received.Write($buffer, 0, $count)
                }
            }
            if ($HoldOpen -gt 0) {
                # Held open: wait for curl to hang up, recording what it still sends.
                $stream.ReadTimeout = $HoldOpen
                while ($true) {
                    try {
                        $count = $stream.Read($buffer, 0, $buffer.Length)
                    } catch [System.IO.IOException] {
                        break
                    }
                    if ($count -le 0) { break }
                    $received.Write($buffer, 0, $count)
                }
            }
            $requests.Add($received.ToArray())
            if ($CloseNotify -and $stream -is [System.Net.Security.SslStream]) {
                try { $stream.ShutdownAsync().Wait() } catch { }  # curl may have hung up already.
            }
        } finally {
            $client.Close()
        }
    }
    return , $requests.ToArray()
}

# Helpers the line-at-a-time sessions (-Ftp, -Smtp, -Imap and -Pop3) dot-source into their runspace.
# They pass as text, since a script block invoked in another runspace would run back in
# this one, which is busy waiting for curl. They read $TlsCertificate and $Overrides
# from the session that dot-sources them.
$sessionHelpers = {
    # Serves TLS 1.2 over $Stream with the throwaway certificate, as the server side.
    function Wrap-Tls {
        param($Stream)
        $secure = New-Object System.Net.Security.SslStream($Stream, $false)
        $secure.AuthenticateAsServer($TlsCertificate, $false, [System.Security.Authentication.SslProtocols]::Tls12, $false)
        return $secure
    }

    # Several overrides for one verb are used in turn; the last one repeats.
    function Get-Override {
        param([string] $Verb)
        $replies = $Overrides[$Verb]
        if ($replies.Count -gt 1) {
            $reply = $replies[0]
            $replies.RemoveAt(0)
            return $reply
        }
        return $replies[0]
    }
}

# The -Ftp server: one control connection, answered a line at a time, with a passive
# data listener for RETR, LIST, STOR and APPE. It returns the control bytes curl sent, as
# one array, writes the two-way transcript into $Transcript, and the bytes uploaded on
# STOR and APPE data connections into $UploadedData.
$serveFtpSession = {
    param($Listener, [hashtable] $Overrides, [byte[]] $DataBytes, [System.Text.StringBuilder] $Transcript, [System.IO.MemoryStream] $UploadedData, $TlsCertificate, [bool] $ImplicitTls, [int] $ControlIdleMilliseconds, [System.Net.IPAddress] $ListenAddress, [string] $SessionHelpers, [int] $DataHoldMilliseconds)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    . ([scriptblock]::Create($SessionHelpers))
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $received = New-Object System.IO.MemoryStream
    $dataListener = New-Object System.Net.Sockets.TcpListener($ListenAddress, 0)
    $dataListener.Start()
    $dataPort = ([System.Net.IPEndPoint] $dataListener.LocalEndpoint).Port
    $restOffset = [long] 0
    # Set by EPRT or PORT: the address curl listens on, which the data connection then
    # dials instead of waiting on the passive listener (BL-437).
    $activeEndPoint = $null
    # Set by PROT P, and by implicit TLS until a PROT C: data connections are TLS (BL-437).
    $protectData = $ImplicitTls

    # Answers CCC's acceptance: sends close_notify and hands back the plain network stream
    # the TLS layer ran over, with the first byte curl sends next (BL-636). A TLS alert
    # record there (0x15) is curl's close_notify, read and dropped; anything else is the
    # first byte of a plain command, handed back to be read as one. -1 when curl sent
    # nothing within the idle time or hung up.
    function Clear-ControlTls {
        param($Secure, $Plain)
        $Secure.ShutdownAsync().Wait()
        $Plain.ReadTimeout = $ControlIdleMilliseconds
        $first = -1
        try { $first = $Plain.ReadByte() } catch [System.IO.IOException] { }
        $suffix = " without curl's close_notify"
        if ($first -eq 0x15) {
            $header = New-Object byte[] 4
            $read = 0
            while ($read -lt 4) { $read += $Plain.Read($header, $read, 4 - $read) }
            $length = $header[2] * 256 + $header[3]
            $alert = New-Object byte[] $length
            $read = 0
            while ($read -lt $length) { $read += $Plain.Read($alert, $read, $length - $read) }
            $suffix = ''
            $first = -1
        }
        [void] $Transcript.Append("= TLS cleared from the control connection$suffix`r`n")
        return @($Plain, $first)
    }

    # Opens the data connection: dials curl's EPRT/PORT address in active mode, otherwise
    # accepts on the passive listener; either way wrapped in TLS once PROT P was accepted.
    function Open-DataConnection {
        param([string] $Verb, $ActiveEndPoint, [bool] $Protect)
        if ($null -ne $ActiveEndPoint) {
            $dataClient = New-Object System.Net.Sockets.TcpClient($ActiveEndPoint.AddressFamily)
            $dataClient.Connect($ActiveEndPoint)
        } else {
            $accept = $dataListener.AcceptTcpClientAsync()
            if (-not $accept.Wait(5000)) { throw "curl sent $Verb but opened no data connection within five seconds." }
            $dataClient = $accept.Result
        }
        $dataStream = $dataClient.GetStream()
        if ($Protect) { $dataStream = Wrap-Tls -Stream $dataStream }
        return @($dataClient, $dataStream)
    }

    # EPRT |1|127.0.0.1|5000| or PORT 127,0,0,1,19,136, as an IPEndPoint.
    function ConvertFrom-ActiveCommand {
        param([string] $Verb, [string] $Argument)
        if ($Verb -eq 'EPRT') {
            $fields = $Argument.Split($Argument[0])
            return New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Parse($fields[2]), [int] $fields[3])
        }
        $numbers = $Argument.Split(',')
        $activePort = [int] $numbers[4] * 256 + [int] $numbers[5]
        return New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Parse(($numbers[0..3] -join '.')), $activePort)
    }

    function Send-Reply {
        param($Stream, [string] $Reply)
        $Reply = $Reply.Replace('{DATAPORT_HI}', [string] [Math]::Floor($dataPort / 256)).Replace('{DATAPORT_LO}', [string] ($dataPort % 256)).Replace('{DATAPORT}', [string] $dataPort)
        $bytes = $latin1.GetBytes($Reply + "`r`n")
        $Stream.Write($bytes, 0, $bytes.Length)
        $Stream.Flush()
        foreach ($line in ($Reply -split "`r`n")) { [void] $Transcript.Append("< $line`r`n") }
    }

    try {
        try {
            $client = $Listener.AcceptTcpClient()
        } catch {
            return , @(, $received.ToArray())
        }
        try {
            $stream = $client.GetStream()
            $plainStream = $stream
            if ($ImplicitTls) { $stream = Wrap-Tls -Stream $stream }
            $stream.ReadTimeout = $ControlIdleMilliseconds
            $greeting = if ($Overrides.ContainsKey('GREETING')) { Get-Override -Verb 'GREETING' } else { '220 Recorder ready' }
            if ($greeting -cne 'STALL') { Send-Reply -Stream $stream -Reply $greeting }
            $line = New-Object System.IO.MemoryStream
            # The first plain byte Clear-ControlTls read after CCC, or -1.
            $pendingByte = -1
            while ($true) {
                try {
                    $next = if ($pendingByte -ge 0) { $pendingByte } else { $stream.ReadByte() }
                    $pendingByte = -1
                } catch [System.IO.IOException] {
                    break  # FtpIdleMilliseconds without a byte: curl is done with us.
                }
                if ($next -lt 0) { break }
                $received.WriteByte([byte] $next)
                $line.WriteByte([byte] $next)
                if ($next -ne 10) { continue }
                $command = $latin1.GetString($line.ToArray()).TrimEnd("`r", "`n")
                $line.SetLength(0)
                [void] $Transcript.Append("> $command`r`n")
                $verb = ($command -split ' ', 2)[0].ToUpperInvariant()
                $argument = if ($command.Contains(' ')) { ($command -split ' ', 2)[1] } else { '' }
                if ($Overrides.ContainsKey($verb)) {
                    $override = Get-Override -Verb $verb
                    if ($override -ceq 'CLOSE') { break }  # Hang up instead of replying.
                    if ($override -ceq 'STALL') { continue }  # Answer nothing; curl waits (BL-512).
                    Send-Reply -Stream $stream -Reply $override
                    if ($verb -eq 'QUIT') { break }
                    # A refused PROT leaves the data connections in plaintext.
                    if ($verb -eq 'PROT') { $protectData = $override.StartsWith('2') -and $argument -ceq 'P' }
                    if ($verb -eq 'AUTH' -and $override.StartsWith('234')) {
                        $stream = Wrap-Tls -Stream $stream
                        $stream.ReadTimeout = $ControlIdleMilliseconds
                        [void] $Transcript.Append("= TLS handshake completed on the control connection`r`n")
                    }
                    # curl clears TLS after any CCC reply below 500.
                    if ($verb -eq 'CCC' -and $override -match '^[1-4]' -and $stream -is [System.Net.Security.SslStream]) {
                        $stream, $pendingByte = Clear-ControlTls -Secure $stream -Plain $plainStream
                    }
                    continue
                }
                switch ($verb) {
                    'AUTH' {
                        Send-Reply -Stream $stream -Reply '234 AUTH accepted'
                        $stream = Wrap-Tls -Stream $stream
                        $stream.ReadTimeout = $ControlIdleMilliseconds
                        [void] $Transcript.Append("= TLS handshake completed on the control connection`r`n")
                    }
                    'PBSZ' { Send-Reply -Stream $stream -Reply '200 PBSZ=0' }
                    'CCC' {
                        Send-Reply -Stream $stream -Reply '200 CCC command successful'
                        if ($stream -is [System.Net.Security.SslStream]) { $stream, $pendingByte = Clear-ControlTls -Secure $stream -Plain $plainStream }
                    }
                    'PROT' {
                        $protectData = $argument -ceq 'P'
                        Send-Reply -Stream $stream -Reply "200 Protection level set to $argument"
                    }
                    { $_ -eq 'EPRT' -or $_ -eq 'PORT' } {
                        $activeEndPoint = ConvertFrom-ActiveCommand -Verb $verb -Argument $argument
                        Send-Reply -Stream $stream -Reply "200 $verb command successful"
                    }
                    'USER' { Send-Reply -Stream $stream -Reply '331 Password required' }
                    'PASS' { Send-Reply -Stream $stream -Reply '230 Logged in' }
                    'PWD' { Send-Reply -Stream $stream -Reply '257 "/" is current directory' }
                    'CWD' { Send-Reply -Stream $stream -Reply '250 OK' }
                    'TYPE' { Send-Reply -Stream $stream -Reply '200 Type set' }
                    'SIZE' { Send-Reply -Stream $stream -Reply "213 $($DataBytes.Length)" }
                    'MDTM' { Send-Reply -Stream $stream -Reply '213 20260927123456' }
                    'REST' {
                        $restOffset = [long] ($command -split ' ', 2)[1]
                        Send-Reply -Stream $stream -Reply "350 Restarting at $restOffset"
                    }
                    'EPSV' { Send-Reply -Stream $stream -Reply "229 Entering Extended Passive Mode (|||$dataPort|)" }
                    'PASV' { Send-Reply -Stream $stream -Reply "227 Entering Passive Mode ($($ListenAddress.ToString().Replace('.', ',')),$([Math]::Floor($dataPort / 256)),$($dataPort % 256))" }
                    { $_ -eq 'RETR' -or $_ -eq 'LIST' -or $_ -eq 'NLST' } {
                        Send-Reply -Stream $stream -Reply '150 Opening BINARY mode data connection'
                        $dataClient, $dataStream = Open-DataConnection -Verb $verb -ActiveEndPoint $activeEndPoint -Protect $protectData
                        try {
                            $start = [int] [Math]::Max(0, [Math]::Min($restOffset, $DataBytes.Length))
                            $dataStream.Write($DataBytes, $start, $DataBytes.Length - $start)
                            $dataStream.Flush()
                            # Stall the download midway, as a server that stops sending does (BL-512).
                            if ($DataHoldMilliseconds -gt 0) { [System.Threading.Thread]::Sleep($DataHoldMilliseconds) }
                            # A TLS data connection ends with close_notify, so curl reads a clean end.
                            if ($dataStream -is [System.Net.Security.SslStream]) { $dataStream.ShutdownAsync().Wait() }
                        } catch [System.IO.IOException] {
                            # curl closed the data connection early, as it does once a range is read.
                        } finally {
                            $dataClient.Close()
                        }
                        $done = if ($Overrides.ContainsKey('RETRDONE')) { Get-Override -Verb 'RETRDONE' } else { '226 Transfer complete' }
                        Send-Reply -Stream $stream -Reply $done
                    }
                    { $_ -eq 'STOR' -or $_ -eq 'APPE' } {
                        Send-Reply -Stream $stream -Reply '150 Opening BINARY mode data connection'
                        $dataClient, $dataStream = Open-DataConnection -Verb $verb -ActiveEndPoint $activeEndPoint -Protect $protectData
                        $uploaded = New-Object System.IO.MemoryStream
                        try {
                            $dataStream.ReadTimeout = 5000
                            $dataStream.CopyTo($uploaded)
                        } catch [System.IO.IOException] {
                            # Five seconds without a byte, or a reset: keep what arrived.
                        } finally {
                            $dataClient.Close()
                        }
                        [byte[]] $uploadedBytes = $uploaded.ToArray()
                        $UploadedData.Write($uploadedBytes, 0, $uploadedBytes.Length)
                        [void] $Transcript.Append("= $($uploadedBytes.Length) bytes received on the data connection: $($latin1.GetString($uploadedBytes))`r`n")
                        $done = if ($Overrides.ContainsKey('STORDONE')) { Get-Override -Verb 'STORDONE' } else { '226 Transfer complete' }
                        Send-Reply -Stream $stream -Reply $done
                    }
                    'QUIT' { Send-Reply -Stream $stream -Reply '221 Bye'; break }
                    default { Send-Reply -Stream $stream -Reply '502 Command not implemented' }
                }
                if ($verb -eq 'QUIT') { break }
            }
        } catch [System.IO.IOException] {
            # curl closed the control connection mid-reply; what arrived is still recorded.
        } finally {
            $client.Close()
        }
    } finally {
        $dataListener.Stop()
    }
    return , @(, $received.ToArray())
}

# The -Smtp server: one connection, answered a line at a time, reading the message body
# after DATA up to its lone "." line. It returns every byte curl sent, as one array, and
# writes the two-way transcript into $Transcript (BL-529).
$serveSmtpSession = {
    param($Listener, [hashtable] $Overrides, [System.Text.StringBuilder] $Transcript, $TlsCertificate, [bool] $ImplicitTls, [int] $IdleMilliseconds, [string] $SessionHelpers)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    . ([scriptblock]::Create($SessionHelpers))
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $received = New-Object System.IO.MemoryStream

    function Send-Reply {
        param($Stream, [string] $Reply)
        $bytes = $latin1.GetBytes($Reply + "`r`n")
        $Stream.Write($bytes, 0, $bytes.Length)
        $Stream.Flush()
        foreach ($line in ($Reply -split "`r`n")) { [void] $Transcript.Append("< $line`r`n") }
    }

    # One line from curl, recorded, without its line ending; $null once curl hangs up or
    # stays silent for IdleMilliseconds.
    function Read-Line {
        param($Stream)
        $line = New-Object System.IO.MemoryStream
        while ($true) {
            try {
                $next = $Stream.ReadByte()
            } catch [System.IO.IOException] {
                return $null
            }
            if ($next -lt 0) { return $null }
            $received.WriteByte([byte] $next)
            $line.WriteByte([byte] $next)
            if ($next -eq 10) { break }
        }
        $text = $latin1.GetString($line.ToArray()).TrimEnd("`r", "`n")
        [void] $Transcript.Append("> $text`r`n")
        return $text
    }

    function Get-EhloReply {
        param([bool] $Secure)
        $capabilities = @('250-localhost', '250-AUTH PLAIN LOGIN CRAM-MD5')
        # RFC 3207: STARTTLS is not offered again once the session is TLS.
        if (-not $Secure) { $capabilities += '250-STARTTLS' }
        $capabilities += @('250-SIZE 1000000', '250-8BITMIME', '250 SMTPUTF8')
        return $capabilities -join "`r`n"
    }

    # The 334 continuations each mechanism needs before 235; $false once curl hangs up.
    function Complete-Authentication {
        param($Stream, [string] $Argument)
        $mechanism = ($Argument -split ' ', 2)[0].ToUpperInvariant()
        $hasInitialResponse = $Argument.Contains(' ')
        $challenges = switch ($mechanism) {
            'PLAIN' { if ($hasInitialResponse) { @() } else { @('334 ') } }
            'LOGIN' { if ($hasInitialResponse) { @('334 UGFzc3dvcmQ6') } else { @('334 VXNlcm5hbWU6', '334 UGFzc3dvcmQ6') } }
            'CRAM-MD5' { @('334 ' + [Convert]::ToBase64String($latin1.GetBytes('<1896.697170952@localhost>'))) }
            'DIGEST-MD5' {
                @(
                    ('334 ' + [Convert]::ToBase64String($latin1.GetBytes('realm="localhost",nonce="OA6MG9tEQGm2hh",qop="auth",algorithm=md5-sess,charset=utf-8'))),
                    ('334 ' + [Convert]::ToBase64String($latin1.GetBytes('rspauth=ea40f60335c427b5527b84dbabcdfffd'))))
            }
            default { @() }
        }
        foreach ($challenge in $challenges) {
            Send-Reply -Stream $Stream -Reply $challenge
            if ($null -eq (Read-Line -Stream $Stream)) { return $false }
        }
        Send-Reply -Stream $Stream -Reply '235 Authentication successful'
        return $true
    }

    # The body after DATA's 354, up to and including the lone "." line, then the reply.
    function Receive-MessageBody {
        param($Stream)
        while ($true) {
            $bodyLine = Read-Line -Stream $Stream
            if ($null -eq $bodyLine) { return $false }
            if ($bodyLine -ceq '.') { break }
        }
        $done = if ($Overrides.ContainsKey('DATADONE')) { Get-Override -Verb 'DATADONE' } else { '250 OK message accepted' }
        Send-Reply -Stream $Stream -Reply $done
        return $true
    }

    function Start-Tls {
        param($Stream)
        $secure = Wrap-Tls -Stream $Stream
        $secure.ReadTimeout = $IdleMilliseconds
        [void] $Transcript.Append("= TLS handshake completed on the control connection`r`n")
        return $secure
    }

    try {
        $client = $Listener.AcceptTcpClient()
    } catch {
        return , @(, $received.ToArray())
    }
    try {
        $stream = $client.GetStream()
        $secure = $ImplicitTls
        if ($ImplicitTls) { $stream = Wrap-Tls -Stream $stream }
        $stream.ReadTimeout = $IdleMilliseconds
        $greeting = if ($Overrides.ContainsKey('GREETING')) { Get-Override -Verb 'GREETING' } else { '220 localhost ESMTP' }
        Send-Reply -Stream $stream -Reply $greeting
        while ($true) {
            $command = Read-Line -Stream $stream
            if ($null -eq $command) { break }  # SmtpIdleMilliseconds without a byte, or curl hung up.
            $verb = ($command -split ' ', 2)[0].ToUpperInvariant()
            $argument = if ($command.Contains(' ')) { ($command -split ' ', 2)[1] } else { '' }
            if ($Overrides.ContainsKey($verb)) {
                $override = Get-Override -Verb $verb
                if ($override -ceq 'CLOSE') { break }  # Hang up instead of replying.
                Send-Reply -Stream $stream -Reply $override
                if ($verb -eq 'QUIT') { break }
                if ($verb -eq 'DATA' -and $override.StartsWith('354') -and -not (Receive-MessageBody -Stream $stream)) { break }
                if ($verb -eq 'STARTTLS' -and $override.StartsWith('220')) {
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                continue
            }
            $continue = $true
            switch ($verb) {
                'EHLO' { Send-Reply -Stream $stream -Reply (Get-EhloReply -Secure $secure) }
                'HELO' { Send-Reply -Stream $stream -Reply '250 localhost' }
                'STARTTLS' {
                    Send-Reply -Stream $stream -Reply '220 Ready to start TLS'
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                'AUTH' { $continue = Complete-Authentication -Stream $stream -Argument $argument }
                'MAIL' { Send-Reply -Stream $stream -Reply '250 OK' }
                'RCPT' { Send-Reply -Stream $stream -Reply '250 OK' }
                'DATA' {
                    Send-Reply -Stream $stream -Reply '354 End data with <CR><LF>.<CR><LF>'
                    $continue = Receive-MessageBody -Stream $stream
                }
                'VRFY' { Send-Reply -Stream $stream -Reply '250 Recorder <recorder@localhost>' }
                'EXPN' { Send-Reply -Stream $stream -Reply '250 Recorder <recorder@localhost>' }
                'HELP' { Send-Reply -Stream $stream -Reply '214 EHLO HELO STARTTLS AUTH MAIL RCPT DATA VRFY EXPN HELP NOOP RSET QUIT' }
                'NOOP' { Send-Reply -Stream $stream -Reply '250 OK' }
                'RSET' { Send-Reply -Stream $stream -Reply '250 OK' }
                'QUIT' { Send-Reply -Stream $stream -Reply '221 Bye'; $continue = $false }
                default { Send-Reply -Stream $stream -Reply '502 Command not implemented' }
            }
            if (-not $continue) { break }
        }
    } catch [System.IO.IOException] {
        # curl closed the connection mid-reply; what arrived is still recorded.
    } finally {
        $client.Close()
    }
    return , @(, $received.ToArray())
}

# The -Imap server: one connection, answered a tagged command at a time, with literals
# ({n}) read from curl after a "+" continuation and FETCH bodies sent as literals. It
# returns every byte curl sent, as one array, and writes the two-way transcript into
# $Transcript (BL-531).
$serveImapSession = {
    param($Listener, [hashtable] $Overrides, [byte[]] $MessageBytes, [System.Text.StringBuilder] $Transcript, $TlsCertificate, [bool] $ImplicitTls, [int] $IdleMilliseconds, [string] $SessionHelpers)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    . ([scriptblock]::Create($SessionHelpers))
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $received = New-Object System.IO.MemoryStream

    function Send-Reply {
        param($Stream, [string] $Reply)
        $bytes = $latin1.GetBytes($Reply + "`r`n")
        $Stream.Write($bytes, 0, $bytes.Length)
        $Stream.Flush()
        foreach ($line in ($Reply -split "`r`n")) { [void] $Transcript.Append("< $line`r`n") }
    }

    # One line from curl, recorded, without its line ending; $null once curl hangs up or
    # stays silent for IdleMilliseconds.
    function Read-Line {
        param($Stream)
        $line = New-Object System.IO.MemoryStream
        while ($true) {
            try {
                $next = $Stream.ReadByte()
            } catch [System.IO.IOException] {
                return $null
            }
            if ($next -lt 0) { return $null }
            $received.WriteByte([byte] $next)
            $line.WriteByte([byte] $next)
            if ($next -eq 10) { break }
        }
        $text = $latin1.GetString($line.ToArray()).TrimEnd("`r", "`n")
        [void] $Transcript.Append("> $text`r`n")
        return $text
    }

    # One command from curl: a line and, while it ends in a literal {n} (RFC 3501 4.3),
    # the "+" continuation (none for a non-synchronizing {n+}), the n literal bytes and
    # the line that follows them. Returns the first line; $null once curl hangs up.
    function Read-Command {
        param($Stream)
        $first = Read-Line -Stream $Stream
        $line = $first
        while ($null -ne $line -and $line -match '\{(\d+)(\+?)\}$') {
            $length = [int] $Matches[1]
            if ($Matches[2] -ne '+') { Send-Reply -Stream $Stream -Reply '+ Ready for literal data' }
            $literal = New-Object byte[] $length
            $read = 0
            while ($read -lt $length) {
                try {
                    $count = $Stream.Read($literal, $read, $length - $read)
                } catch [System.IO.IOException] {
                    return $null
                }
                if ($count -le 0) { return $null }
                $read += $count
            }
            $received.Write($literal, 0, $length)
            $literalText = $latin1.GetString($literal)
            # A literal ending in CRLF adds no empty line of its own to the transcript.
            if ($literalText.EndsWith("`r`n")) { $literalText = $literalText.Substring(0, $literalText.Length - 2) }
            foreach ($literalLine in ($literalText -split "`r`n")) { [void] $Transcript.Append("> $literalLine`r`n") }
            $line = Read-Line -Stream $Stream
        }
        if ($null -eq $line) { return $null }
        return $first
    }

    # A reply for the command tagged $Tag: every line but the last sent as given, the
    # last with the tag in front.
    function Send-Tagged {
        param($Stream, [string] $Tag, [string] $Reply)
        $lines = @($Reply -split "`r`n")
        $lines[$lines.Count - 1] = "$Tag " + $lines[$lines.Count - 1]
        Send-Reply -Stream $Stream -Reply ($lines -join "`r`n")
    }

    function Get-CapabilityLine {
        param([bool] $Secure)
        # RFC 3501 6.2.1: STARTTLS is not offered again once the session is TLS.
        if ($Secure) { return '* CAPABILITY IMAP4rev1 AUTH=PLAIN AUTH=LOGIN' }
        return '* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN'
    }

    # The "+" continuations each mechanism needs before OK; $false once curl hangs up.
    function Complete-Authentication {
        param($Stream, [string] $Tag, [string] $Argument)
        $words = @($Argument -split ' ')
        $mechanism = $words[0].ToUpperInvariant()
        $hasInitialResponse = $words.Count -gt 1
        $challenges = switch ($mechanism) {
            'PLAIN' { if ($hasInitialResponse) { @() } else { @('+ ') } }
            'LOGIN' { if ($hasInitialResponse) { @('+ UGFzc3dvcmQ6') } else { @('+ VXNlcm5hbWU6', '+ UGFzc3dvcmQ6') } }
            default { @() }
        }
        foreach ($challenge in $challenges) {
            Send-Reply -Stream $Stream -Reply $challenge
            if ($null -eq (Read-Line -Stream $Stream)) { return $false }
        }
        Send-Tagged -Stream $Stream -Tag $Tag -Reply 'OK Authenticated'
        return $true
    }

    # The untagged FETCH response carrying the message as a literal, then the OK.
    function Send-FetchReply {
        param($Stream, [string] $Tag, [string] $Verb, [string] $Argument)
        $fields = @($Argument -split ' ', 2)
        $sequence = ($fields[0] -split '[:,]')[0]
        if ($sequence -notmatch '^\d+$') { $sequence = '1' }
        $item = if ($fields.Count -gt 1) { $fields[1].Trim('(', ')') } else { 'BODY[]' }
        $uid = if ($Verb -eq 'UID FETCH') { "UID $sequence " } else { '' }
        $message = $latin1.GetString($MessageBytes)
        Send-Reply -Stream $Stream -Reply "* $sequence FETCH ($uid$item {$($MessageBytes.Length)}`r`n$message)"
        Send-Tagged -Stream $Stream -Tag $Tag -Reply 'OK FETCH completed'
    }

    function Start-Tls {
        param($Stream)
        $secure = Wrap-Tls -Stream $Stream
        $secure.ReadTimeout = $IdleMilliseconds
        [void] $Transcript.Append("= TLS handshake completed on the control connection`r`n")
        return $secure
    }

    try {
        $client = $Listener.AcceptTcpClient()
    } catch {
        return , @(, $received.ToArray())
    }
    try {
        $stream = $client.GetStream()
        $secure = $ImplicitTls
        if ($ImplicitTls) { $stream = Wrap-Tls -Stream $stream }
        $stream.ReadTimeout = $IdleMilliseconds
        $greeting = if ($Overrides.ContainsKey('GREETING')) { Get-Override -Verb 'GREETING' } else { '* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready' }
        Send-Reply -Stream $stream -Reply $greeting
        while ($true) {
            $command = Read-Command -Stream $stream
            if ($null -eq $command) { break }  # ImapIdleMilliseconds without a byte, or curl hung up.
            $words = @($command -split ' ', 3)
            $tag = $words[0]
            $verb = if ($words.Count -gt 1) { $words[1].ToUpperInvariant() } else { '' }
            $argument = if ($words.Count -gt 2) { $words[2] } else { '' }
            if ($verb -eq 'UID' -and $argument -ne '') {
                $uidWords = @($argument -split ' ', 2)
                $verb = 'UID ' + $uidWords[0].ToUpperInvariant()
                $argument = if ($uidWords.Count -gt 1) { $uidWords[1] } else { '' }
            }
            if ($Overrides.ContainsKey($verb)) {
                $override = Get-Override -Verb $verb
                if ($override -ceq 'CLOSE') { break }  # Hang up instead of replying.
                Send-Tagged -Stream $stream -Tag $tag -Reply $override
                if ($verb -eq 'LOGOUT') { break }
                if ($verb -eq 'STARTTLS' -and ($override -split "`r`n")[-1].StartsWith('OK')) {
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                continue
            }
            $continue = $true
            switch ($verb) {
                'CAPABILITY' { Send-Tagged -Stream $stream -Tag $tag -Reply ((Get-CapabilityLine -Secure $secure) + "`r`nOK CAPABILITY completed") }
                'STARTTLS' {
                    Send-Tagged -Stream $stream -Tag $tag -Reply 'OK Begin TLS negotiation now'
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                'LOGIN' { Send-Tagged -Stream $stream -Tag $tag -Reply 'OK LOGIN completed' }
                'AUTHENTICATE' { $continue = Complete-Authentication -Stream $stream -Tag $tag -Argument $argument }
                'SELECT' { Send-Tagged -Stream $stream -Tag $tag -Reply "* FLAGS (\Answered \Flagged \Deleted \Seen \Draft)`r`n* 2 EXISTS`r`n* 0 RECENT`r`n* OK [UIDVALIDITY 1] UIDs valid`r`n* OK [UIDNEXT 3] Predicted next UID`r`nOK [READ-WRITE] SELECT completed" }
                'EXAMINE' { Send-Tagged -Stream $stream -Tag $tag -Reply "* FLAGS (\Answered \Flagged \Deleted \Seen \Draft)`r`n* 2 EXISTS`r`n* 0 RECENT`r`n* OK [UIDVALIDITY 1] UIDs valid`r`n* OK [UIDNEXT 3] Predicted next UID`r`nOK [READ-ONLY] EXAMINE completed" }
                'FETCH' { Send-FetchReply -Stream $stream -Tag $tag -Verb $verb -Argument $argument }
                'UID FETCH' { Send-FetchReply -Stream $stream -Tag $tag -Verb $verb -Argument $argument }
                'LIST' { Send-Tagged -Stream $stream -Tag $tag -Reply "* LIST (\HasNoChildren) `"/`" INBOX`r`n* LIST (\HasNoChildren) `"/`" Sent`r`nOK LIST completed" }
                'SEARCH' { Send-Tagged -Stream $stream -Tag $tag -Reply "* SEARCH 1 2`r`nOK SEARCH completed" }
                'UID SEARCH' { Send-Tagged -Stream $stream -Tag $tag -Reply "* SEARCH 1 2`r`nOK SEARCH completed" }
                'APPEND' { Send-Tagged -Stream $stream -Tag $tag -Reply 'OK APPEND completed' }
                'NOOP' { Send-Tagged -Stream $stream -Tag $tag -Reply 'OK NOOP completed' }
                'LOGOUT' { Send-Tagged -Stream $stream -Tag $tag -Reply "* BYE Logging out`r`nOK LOGOUT completed"; $continue = $false }
                default { Send-Tagged -Stream $stream -Tag $tag -Reply 'BAD Command not recognized' }
            }
            if (-not $continue) { break }
        }
    } catch [System.IO.IOException] {
        # curl closed the connection mid-reply; what arrived is still recorded.
    } finally {
        $client.Close()
    }
    return , @(, $received.ToArray())
}

# The -Pop3 server: one connection, answered a line at a time, with multi-line replies
# ended by a lone "." and dot-stuffed. It returns every byte curl sent, as one array, and
# writes the two-way transcript into $Transcript (BL-530).
$servePop3Session = {
    param($Listener, [hashtable] $Overrides, [byte[]] $MessageBytes, [System.Text.StringBuilder] $Transcript, $TlsCertificate, [bool] $ImplicitTls, [int] $IdleMilliseconds, [string] $SessionHelpers)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    . ([scriptblock]::Create($SessionHelpers))
    $latin1 = [System.Text.Encoding]::GetEncoding(28591)
    $received = New-Object System.IO.MemoryStream
    $messageSize = $MessageBytes.Length

    function Send-Reply {
        param($Stream, [string] $Reply)
        $bytes = $latin1.GetBytes($Reply + "`r`n")
        $Stream.Write($bytes, 0, $bytes.Length)
        $Stream.Flush()
        foreach ($line in ($Reply -split "`r`n")) { [void] $Transcript.Append("< $line`r`n") }
    }

    # One line from curl, recorded, without its line ending; $null once curl hangs up or
    # stays silent for IdleMilliseconds.
    function Read-Line {
        param($Stream)
        $line = New-Object System.IO.MemoryStream
        while ($true) {
            try {
                $next = $Stream.ReadByte()
            } catch [System.IO.IOException] {
                return $null
            }
            if ($next -lt 0) { return $null }
            $received.WriteByte([byte] $next)
            $line.WriteByte([byte] $next)
            if ($next -eq 10) { break }
        }
        $text = $latin1.GetString($line.ToArray()).TrimEnd("`r", "`n")
        [void] $Transcript.Append("> $text`r`n")
        return $text
    }

    # A multi-line reply (RFC 1939 3): the status line, each content line with a leading
    # "." doubled, then the lone "." that ends it.
    function Send-MultiLine {
        param($Stream, [string] $Status, [string[]] $Lines)
        $stuffed = @($Lines | ForEach-Object { if ($_.StartsWith('.')) { '.' + $_ } else { $_ } })
        Send-Reply -Stream $Stream -Reply ((@($Status) + $stuffed + '.') -join "`r`n")
    }

    # Pop3Message as lines, without the line ending of its last one.
    function Get-MessageLines {
        $text = $latin1.GetString($MessageBytes)
        if ($text.EndsWith("`r`n")) { $text = $text.Substring(0, $text.Length - 2) }
        if ($text.Length -eq 0) { return , @() }
        return , @($text -split "`r`n")
    }

    # TOP's lines: the headers, the blank line and the first $BodyLines body lines.
    function Get-TopLines {
        param([int] $BodyLines)
        $lines = Get-MessageLines
        $blank = [array]::IndexOf($lines, '')
        if ($blank -lt 0) { return , $lines }
        $last = [Math]::Min($lines.Count - 1, $blank + $BodyLines)
        return , @($lines[0..$last])
    }

    function Get-CapabilityLines {
        param([bool] $Secure)
        # RFC 2595 4: STLS is not offered again once the session is TLS.
        if ($Secure) { return , @('USER', 'SASL PLAIN LOGIN', 'TOP', 'UIDL') }
        return , @('USER', 'SASL PLAIN LOGIN', 'STLS', 'TOP', 'UIDL')
    }

    # The "+" continuations each mechanism needs before +OK; $false once curl hangs up.
    function Complete-Authentication {
        param($Stream, [string] $Argument)
        $words = @($Argument -split ' ')
        $mechanism = $words[0].ToUpperInvariant()
        $hasInitialResponse = $words.Count -gt 1
        $challenges = switch ($mechanism) {
            'PLAIN' { if ($hasInitialResponse) { @() } else { @('+ ') } }
            'LOGIN' { if ($hasInitialResponse) { @('+ UGFzc3dvcmQ6') } else { @('+ VXNlcm5hbWU6', '+ UGFzc3dvcmQ6') } }
            default { @() }
        }
        foreach ($challenge in $challenges) {
            Send-Reply -Stream $Stream -Reply $challenge
            if ($null -eq (Read-Line -Stream $Stream)) { return $false }
        }
        Send-Reply -Stream $Stream -Reply '+OK Authenticated'
        return $true
    }

    function Start-Tls {
        param($Stream)
        $secure = Wrap-Tls -Stream $Stream
        $secure.ReadTimeout = $IdleMilliseconds
        [void] $Transcript.Append("= TLS handshake completed on the control connection`r`n")
        return $secure
    }

    try {
        $client = $Listener.AcceptTcpClient()
    } catch {
        return , @(, $received.ToArray())
    }
    try {
        $stream = $client.GetStream()
        $secure = $ImplicitTls
        if ($ImplicitTls) { $stream = Wrap-Tls -Stream $stream }
        $stream.ReadTimeout = $IdleMilliseconds
        $greeting = if ($Overrides.ContainsKey('GREETING')) { Get-Override -Verb 'GREETING' } else { '+OK POP3 ready <1896.697170952@localhost>' }
        Send-Reply -Stream $stream -Reply $greeting
        while ($true) {
            $command = Read-Line -Stream $stream
            if ($null -eq $command) { break }  # Pop3IdleMilliseconds without a byte, or curl hung up.
            $verb = ($command -split ' ', 2)[0].ToUpperInvariant()
            $argument = if ($command.Contains(' ')) { ($command -split ' ', 2)[1] } else { '' }
            $arguments = @($argument -split ' ' | Where-Object { $_ -ne '' })
            if ($Overrides.ContainsKey($verb)) {
                $override = Get-Override -Verb $verb
                if ($override -ceq 'CLOSE') { break }  # Hang up instead of replying.
                Send-Reply -Stream $stream -Reply $override
                if ($verb -eq 'QUIT') { break }
                if ($verb -eq 'STLS' -and $override.StartsWith('+OK')) {
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                continue
            }
            $continue = $true
            switch ($verb) {
                'CAPA' { Send-MultiLine -Stream $stream -Status '+OK Capability list follows' -Lines (Get-CapabilityLines -Secure $secure) }
                'STLS' {
                    Send-Reply -Stream $stream -Reply '+OK Begin TLS negotiation'
                    $stream = Start-Tls -Stream $stream
                    $secure = $true
                }
                'USER' { Send-Reply -Stream $stream -Reply '+OK User accepted' }
                'PASS' { Send-Reply -Stream $stream -Reply '+OK Logged in' }
                'APOP' { Send-Reply -Stream $stream -Reply '+OK Logged in' }
                'AUTH' {
                    if ($arguments.Count -eq 0) {
                        Send-MultiLine -Stream $stream -Status '+OK SASL mechanisms follow' -Lines @('PLAIN', 'LOGIN')
                    } else {
                        $continue = Complete-Authentication -Stream $stream -Argument $argument
                    }
                }
                'STAT' { Send-Reply -Stream $stream -Reply "+OK 2 $(2 * $messageSize)" }
                'LIST' {
                    if ($arguments.Count -gt 0) {
                        Send-Reply -Stream $stream -Reply "+OK $($arguments[0]) $messageSize"
                    } else {
                        Send-MultiLine -Stream $stream -Status "+OK 2 messages ($(2 * $messageSize) octets)" -Lines @("1 $messageSize", "2 $messageSize")
                    }
                }
                'RETR' { Send-MultiLine -Stream $stream -Status "+OK $messageSize octets" -Lines (Get-MessageLines) }
                'TOP' {
                    $bodyLines = 0
                    if ($arguments.Count -gt 1) { [void] [int]::TryParse($arguments[1], [ref] $bodyLines) }
                    Send-MultiLine -Stream $stream -Status '+OK Top of message follows' -Lines (Get-TopLines -BodyLines $bodyLines)
                }
                'UIDL' {
                    if ($arguments.Count -gt 0) {
                        Send-Reply -Stream $stream -Reply "+OK $($arguments[0]) uid-$($arguments[0])"
                    } else {
                        Send-MultiLine -Stream $stream -Status '+OK Unique-ID listing follows' -Lines @('1 uid-1', '2 uid-2')
                    }
                }
                'DELE' { Send-Reply -Stream $stream -Reply '+OK Message deleted' }
                'RSET' { Send-Reply -Stream $stream -Reply '+OK' }
                'NOOP' { Send-Reply -Stream $stream -Reply '+OK' }
                'QUIT' { Send-Reply -Stream $stream -Reply '+OK Bye'; $continue = $false }
                default { Send-Reply -Stream $stream -Reply '-ERR Command not recognized' }
            }
            if (-not $continue) { break }
        }
    } catch [System.IO.IOException] {
        # curl closed the connection mid-reply; what arrived is still recorded.
    } finally {
        $client.Close()
    }
    return , @(, $received.ToArray())
}

# The -Script server: one connection, served by the steps of a script, for binary
# request-reply protocols such as LDAP's BER messages (BL-532). It returns every byte curl
# sent, as one array, and writes a hex transcript of both directions into $Transcript.
$serveScriptedSession = {
    param($Listener, [object[]] $Steps, [System.Text.StringBuilder] $Transcript, $TlsCertificate, [bool] $ImplicitTls, [int] $IdleMilliseconds, [int] $GapMilliseconds, [string] $SessionHelpers)

    Set-StrictMode -Version Latest
    $ErrorActionPreference = 'Stop'
    . ([scriptblock]::Create($SessionHelpers))
    $received = New-Object System.IO.MemoryStream
    $unconsumed = New-Object System.Collections.Generic.List[byte]
    $buffer = New-Object byte[] 65536
    # A read that timed out stays pending, so the next wait picks up its bytes instead of
    # starting a second read on the same stream.
    $reader = @{ Pending = $null }

    function ConvertTo-Hex {
        param([byte[]] $Bytes)
        return ([System.BitConverter]::ToString($Bytes) -replace '-', ' ').ToLowerInvariant()
    }

    # Waits up to TimeoutMilliseconds for bytes from curl, records them and appends them
    # to $unconsumed. Returns how many arrived, 0 when curl closed its end, -1 on timeout.
    function Receive-Bytes {
        param($Stream, [int] $TimeoutMilliseconds)
        if ($null -eq $reader.Pending) { $reader.Pending = $Stream.ReadAsync($buffer, 0, $buffer.Length) }
        try {
            if (-not $reader.Pending.Wait($TimeoutMilliseconds)) { return -1 }
            $count = $reader.Pending.Result
        } catch [System.AggregateException] {
            $count = 0  # The connection was reset: the same as curl closing its end.
        }
        $reader.Pending = $null
        if ($count -gt 0) {
            $received.Write($buffer, 0, $count)
            for ($index = 0; $index -lt $count; $index++) { $unconsumed.Add($buffer[$index]) }
        }
        return $count
    }

    # The length of the whole BER element at the start of $unconsumed (X.690 8.1), or -1
    # while its identifier and length octets have not all arrived. An indefinite length,
    # which LDAP forbids, is refused.
    function Get-BerElementLength {
        $index = 1
        if ($unconsumed.Count -lt 1) { return -1 }
        if (($unconsumed[0] -band 0x1F) -eq 0x1F) {
            while ($index -lt $unconsumed.Count -and ($unconsumed[$index] -band 0x80) -ne 0) { $index++ }
            $index++
        }
        if ($unconsumed.Count -le $index) { return -1 }
        $first = [int] $unconsumed[$index]
        if ($first -lt 0x80) { return $index + 1 + $first }
        $octets = $first -band 0x7F
        if ($octets -eq 0) { throw 'The read ber step met an indefinite length, which it does not support.' }
        if ($unconsumed.Count -le $index + $octets) { return -1 }
        [long] $length = 0
        for ($offset = 1; $offset -le $octets; $offset++) { $length = $length * 256 + $unconsumed[$index + $offset] }
        return $index + 1 + $octets + $length
    }

    # Runs one read step. Returns $true to go on, $false when the session has ended.
    function Invoke-ReadStep {
        param($Stream, $Step)
        $wanted = -1
        while ($true) {
            if ($Step.Framing -eq 'ber') { $wanted = Get-BerElementLength } elseif ($Step.Framing -eq 'count') { $wanted = $Step.ByteCount }
            if ($Step.Framing -eq 'gap') {
                $timeout = if ($unconsumed.Count -eq 0) { $IdleMilliseconds } else { $GapMilliseconds }
            } elseif ($wanted -ge 0 -and $unconsumed.Count -ge $wanted) {
                break
            } else {
                $timeout = $IdleMilliseconds
            }
            $arrived = Receive-Bytes -Stream $Stream -TimeoutMilliseconds $timeout
            if ($arrived -gt 0) { continue }
            if ($Step.Framing -eq 'gap' -and $arrived -lt 0 -and $unconsumed.Count -gt 0) { break }
            if ($unconsumed.Count -gt 0) { [void] $Transcript.Append("> $(ConvertTo-Hex -Bytes $unconsumed.ToArray())`r`n") }
            $expected = if ($wanted -ge 0) { " of the $wanted expected" } else { '' }
            if ($arrived -lt 0) {
                [void] $Transcript.Append("= $($Step.Text) timed out after $IdleMilliseconds ms with $($unconsumed.Count) bytes$expected; session ended`r`n")
            } else {
                [void] $Transcript.Append("= curl closed the connection during $($Step.Text) with $($unconsumed.Count) bytes$expected; session ended`r`n")
            }
            return $false
        }
        if ($wanted -lt 0) { $wanted = $unconsumed.Count }
        $message = $unconsumed.GetRange(0, $wanted).ToArray()
        $unconsumed.RemoveRange(0, $wanted)
        [void] $Transcript.Append("> $(ConvertTo-Hex -Bytes $message)`r`n")
        return $true
    }

    # WinLDAP opens two connections at once and writes on only one of them, so when the
    # script starts with a read, every connection curl opens is accepted and the session
    # is served on the first that has bytes waiting; the others are held open until it ends.
    function Select-SpokenConnection {
        param([System.Collections.Generic.List[object]] $Accepted)
        $clock = [System.Diagnostics.Stopwatch]::StartNew()
        while ($clock.ElapsedMilliseconds -lt $IdleMilliseconds) {
            # curl can exit without writing on any connection, as the Windows build does for
            # an LDAP URL it refuses after connecting (BL-587); the listener is then stopped.
            try {
                while ($Listener.Pending()) { $Accepted.Add($Listener.AcceptTcpClient()) }
            } catch [System.InvalidOperationException] {
                return $null
            }
            foreach ($candidate in $Accepted) {
                if ($candidate.Client.Available -gt 0) { return $candidate }
            }
            Start-Sleep -Milliseconds 10
        }
        return $null
    }

    $accepted = New-Object System.Collections.Generic.List[object]
    try {
        $accepted.Add($Listener.AcceptTcpClient())
    } catch {
        return , @(, $received.ToArray())
    }
    try {
        $client = $accepted[0]
        if ($Steps.Count -gt 0 -and $Steps[0].Kind -eq 'read') {
            $client = Select-SpokenConnection -Accepted $accepted
            if ($accepted.Count -gt 1) { [void] $Transcript.Append("= curl opened $($accepted.Count) connections; the session is served on the first to send`r`n") }
            if ($null -eq $client) {
                [void] $Transcript.Append("= $($Steps[0].Text) timed out after $IdleMilliseconds ms with 0 bytes; session ended`r`n")
                return , @(, $received.ToArray())
            }
        }
        $stream = $client.GetStream()
        if ($ImplicitTls) {
            $stream = Wrap-Tls -Stream $stream
            [void] $Transcript.Append("= TLS handshake completed`r`n")
        }
        $ended = $false
        foreach ($step in $Steps) {
            if ($step.Kind -eq 'read') {
                if (-not (Invoke-ReadStep -Stream $stream -Step $step)) { $ended = $true; break }
            } elseif ($step.Kind -eq 'send') {
                $stream.Write($step.Bytes, 0, $step.Bytes.Length)
                $stream.Flush()
                [void] $Transcript.Append("< $(ConvertTo-Hex -Bytes $step.Bytes)`r`n")
            } elseif ($step.Kind -eq 'reset') {
                # A zero linger time makes Close send RST instead of FIN.
                $client.LingerState = New-Object System.Net.Sockets.LingerOption($true, 0)
                $client.Close()
                [void] $Transcript.Append("= server reset the connection`r`n")
                $ended = $true
                break
            } else {
                [void] $Transcript.Append("= server closed the connection`r`n")
                $ended = $true
                break
            }
        }
        if (-not $ended) { [void] $Transcript.Append("= script ended; server closed the connection`r`n") }
    } catch [System.IO.IOException] {
        [void] $Transcript.Append("= curl closed the connection mid-send; session ended`r`n")
    } finally {
        foreach ($connection in $accepted) { $connection.Close() }
        # The session is over, so a reconnect is refused rather than left in the backlog,
        # where WinLDAP would wait minutes for an answer before curl could exit.
        $Listener.Stop()
    }
    return , @(, $received.ToArray())
}

function ConvertFrom-ExchangeScript {
    # The -Script file as a list of steps; see .PARAMETER Script.
    param([string] $Path)
    $steps = New-Object System.Collections.Generic.List[object]
    $lineNumber = 0
    foreach ($line in [System.IO.File]::ReadAllLines([System.IO.Path]::GetFullPath([System.IO.Path]::Combine((Get-Location).ProviderPath, $Path)))) {
        $lineNumber++
        $text = $line.Trim()
        if ($text.Length -eq 0 -or $text.StartsWith('#')) { continue }
        $verb = ($text -split '\s+', 2)[0]
        $argument = if ($text -match '^\S+\s+(.*)$') { $Matches[1] } else { '' }
        if ($verb -ceq 'send') {
            $steps.Add(@{ Kind = 'send'; Bytes = [byte[]] (ConvertFrom-EscapedResponse -Text $argument); Text = $text })
        } elseif ($verb -ceq 'close' -and $argument -eq '') {
            $steps.Add(@{ Kind = 'close'; Text = $text })
        } elseif ($verb -ceq 'reset' -and $argument -eq '') {
            $steps.Add(@{ Kind = 'reset'; Text = $text })
        } elseif ($verb -ceq 'read' -and $argument -eq '') {
            $steps.Add(@{ Kind = 'read'; Framing = 'gap'; Text = $text })
        } elseif ($verb -ceq 'read' -and $argument -ceq 'ber') {
            $steps.Add(@{ Kind = 'read'; Framing = 'ber'; Text = $text })
        } elseif ($verb -ceq 'read' -and $argument -match '^\d+$' -and [int] $argument -gt 0) {
            $steps.Add(@{ Kind = 'read'; Framing = 'count'; ByteCount = [int] $argument; Text = $text })
        } else {
            throw "Script line ${lineNumber} is not a step: $text"
        }
    }
    return , $steps.ToArray()
}

function ConvertTo-DerElement {
    # One DER element: Tag, the definite length of Content, then Content.
    param([byte] $Tag, [byte[]] $Content)
    $length = $Content.Length
    $lengthBytes = if ($length -lt 0x80) { , [byte] $length }
        elseif ($length -lt 0x100) { [byte[]] (0x81, $length) }
        else { [byte[]] (0x82, ($length -shr 8), ($length -band 0xFF)) }
    return , ([byte[]] (@($Tag) + $lengthBytes + $Content))
}

function Write-ThrowawayCrl {
    # Writes a version 1 CRL issued and signed (SHA-256 with RSA) by Root, revoking
    # RevokedSerial when given and nothing otherwise, to Path as PEM. .NET Framework has no
    # CertificateRevocationListBuilder, so the DER is put together here.
    param([string] $Path, $Root, $RootKey, [byte[]] $RevokedSerial)
    $ascii = New-Object System.Text.ASCIIEncoding
    $now = [System.DateTime]::UtcNow
    $thisUpdate = ConvertTo-DerElement -Tag 0x17 -Content $ascii.GetBytes($now.AddMinutes(-5).ToString('yyMMddHHmmss') + 'Z')
    $nextUpdate = ConvertTo-DerElement -Tag 0x17 -Content $ascii.GetBytes($now.AddDays(1).ToString('yyMMddHHmmss') + 'Z')
    $sha256WithRsa = ConvertTo-DerElement -Tag 0x30 -Content ([byte[]] ((ConvertTo-DerElement -Tag 0x06 -Content ([byte[]] (0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0B))) + [byte[]] (0x05, 0x00)))
    $tbsContent = [byte[]] ($sha256WithRsa + $Root.SubjectName.RawData + $thisUpdate + $nextUpdate)
    if ($null -ne $RevokedSerial) {
        $entry = ConvertTo-DerElement -Tag 0x30 -Content ([byte[]] ((ConvertTo-DerElement -Tag 0x02 -Content $RevokedSerial) + $thisUpdate))
        $tbsContent = [byte[]] ($tbsContent + (ConvertTo-DerElement -Tag 0x30 -Content $entry))
    }
    $tbs = ConvertTo-DerElement -Tag 0x30 -Content $tbsContent
    $signature = $RootKey.SignData($tbs, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
    $signatureBits = ConvertTo-DerElement -Tag 0x03 -Content ([byte[]] (@([byte] 0) + $signature))
    $crl = ConvertTo-DerElement -Tag 0x30 -Content ([byte[]] ($tbs + $sha256WithRsa + $signatureBits))
    $pem = "-----BEGIN X509 CRL-----`n" + [System.Convert]::ToBase64String($crl, [System.Base64FormattingOptions]::InsertLineBreaks).Replace("`r`n", "`n") + "`n-----END X509 CRL-----`n"
    [System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath([System.IO.Path]::Combine((Get-Location).ProviderPath, $Path)), $pem, $ascii)
}

function New-IssuedByThrowawayRoot {
    # Signs Request with a throwaway root CA named by no store and writes the root's PEM
    # to RootCertificateFile. Neither certificate names a CRL or OCSP endpoint.
    param($Request, $Key, [string] $RootCertificateFile, [System.DateTimeOffset] $NotBefore, [System.DateTimeOffset] $NotAfter)
    $rootKey = New-Object System.Security.Cryptography.RSACng(2048)
    try {
        $rootRequest = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest('CN=Record-CurlExchange throwaway root', $rootKey, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $rootRequest.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509BasicConstraintsExtension($true, $false, 0, $true)))
        $rootRequest.CertificateExtensions.Add((New-Object System.Security.Cryptography.X509Certificates.X509KeyUsageExtension(([System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::KeyCertSign -bor [System.Security.Cryptography.X509Certificates.X509KeyUsageFlags]::CrlSign), $true)))
        $root = $rootRequest.CreateSelfSigned($NotBefore.AddMinutes(-5), $NotAfter.AddDays(1))
        try {
            $pem = "-----BEGIN CERTIFICATE-----`n" + [System.Convert]::ToBase64String($root.RawData, [System.Base64FormattingOptions]::InsertLineBreaks) + "`n-----END CERTIFICATE-----`n"
            [System.IO.File]::WriteAllText($RootCertificateFile, $pem.Replace("`r`n", "`n"))
            $serial = New-Object byte[] 16
            [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($serial)
            $serial[0] = $serial[0] -band 0x7F
            $issued = $Request.Create($root, $NotBefore, $NotAfter, $serial)
            if ($TlsEmptyCrlFile) { Write-ThrowawayCrl -Path $TlsEmptyCrlFile -Root $root -RootKey $rootKey -RevokedSerial $null }
            if ($TlsRevokingCrlFile) { Write-ThrowawayCrl -Path $TlsRevokingCrlFile -Root $root -RootKey $rootKey -RevokedSerial $serial }
            try {
                return [System.Security.Cryptography.X509Certificates.RSACertificateExtensions]::CopyWithPrivateKey($issued, $Key)
            } finally {
                $issued.Reset()
            }
        } finally {
            $root.Reset()
        }
    } finally {
        $rootKey.Dispose()
    }
}

function Get-SubjectPublicKeyInfo {
    # The DER SubjectPublicKeyInfo of Certificate's key: SEQUENCE { SEQUENCE { algorithm OID,
    # parameters }, BIT STRING key }. .NET Framework has no ExportSubjectPublicKeyInfo, so
    # it is put together from the parts PublicKey exposes.
    param($Certificate)
    function ConvertTo-Der {
        param([byte] $Tag, [byte[]] $Content)
        $length = $Content.Length
        $lengthBytes = if ($length -lt 0x80) { , [byte] $length }
            elseif ($length -lt 0x100) { [byte[]] (0x81, $length) }
            else { [byte[]] (0x82, ($length -shr 8), ($length -band 0xFF)) }
        return , ([byte[]] (@($Tag) + $lengthBytes + $Content))
    }
    $publicKey = $Certificate.PublicKey
    $oid = ConvertTo-Der -Tag 0x06 -Content ([System.Security.Cryptography.CryptoConfig]::EncodeOID($publicKey.Oid.Value) | Select-Object -Skip 2)
    $algorithm = ConvertTo-Der -Tag 0x30 -Content ([byte[]] ($oid + $publicKey.EncodedParameters.RawData))
    $key = ConvertTo-Der -Tag 0x03 -Content ([byte[]] (@([byte] 0) + $publicKey.EncodedKeyValue.RawData))
    return , (ConvertTo-Der -Tag 0x30 -Content ([byte[]] ($algorithm + $key)))
}

function New-ThrowawayTlsCertificate {
    # Schannel will not serve the ephemeral key CreateSelfSigned returns, so the
    # certificate is reloaded from its PFX export. Loaded without PersistKeySet, its key
    # container is deleted when the certificate is reset; no store is touched. With a
    # RootCertificateFile the leaf is issued by a throwaway root whose PEM is written there.
    param([string] $RootCertificateFile)
    $rsa = New-Object System.Security.Cryptography.RSACng(2048)
    try {
        $request = New-Object System.Security.Cryptography.X509Certificates.CertificateRequest('CN=127.0.0.1', $rsa, [System.Security.Cryptography.HashAlgorithmName]::SHA256, [System.Security.Cryptography.RSASignaturePadding]::Pkcs1)
        $alternativeNames = New-Object System.Security.Cryptography.X509Certificates.SubjectAlternativeNameBuilder
        $alternativeNames.AddIpAddress([System.Net.IPAddress]::Loopback)
        $request.CertificateExtensions.Add($alternativeNames.Build())
        $now = [System.DateTimeOffset]::UtcNow
        $selfSigned = if ([string]::IsNullOrEmpty($RootCertificateFile)) {
            $request.CreateSelfSigned($now.AddMinutes(-5), $now.AddDays(1))
        } else {
            New-IssuedByThrowawayRoot -Request $request -Key $rsa -RootCertificateFile $RootCertificateFile -NotBefore $now.AddMinutes(-5) -NotAfter $now.AddDays(1)
        }
        try {
            $pfx = $selfSigned.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx)
        } finally {
            $selfSigned.Reset()
        }
    } finally {
        $rsa.Dispose()
    }
    return New-Object System.Security.Cryptography.X509Certificates.X509Certificate2(, $pfx)
}

if ([string]::IsNullOrEmpty($Curl)) { $Curl = Get-ReferenceCurlPath }
$responseBytes = New-Object System.Collections.Generic.List[byte[]]
foreach ($text in $Response) { $responseBytes.Add((ConvertFrom-EscapedResponse -Text $text)) }
function ConvertTo-ReplyOverrides {
    # 'VERB=reply' entries as a table of verb to the list of its replies, in order.
    param([string[]] $Entries, [string] $ParameterName)
    $overrides = @{}
    foreach ($entry in $Entries) {
        $separator = $entry.IndexOf('=')
        if ($separator -lt 1) { throw "$ParameterName '$entry' is not VERB=reply." }
        $overrideVerb = $entry.Substring(0, $separator).ToUpperInvariant()
        if (-not $overrides.ContainsKey($overrideVerb)) { $overrides[$overrideVerb] = New-Object System.Collections.Generic.List[string] }
        $overrides[$overrideVerb].Add([System.Text.Encoding]::GetEncoding(28591).GetString((ConvertFrom-EscapedResponse -Text $entry.Substring($separator + 1))))
    }
    return $overrides
}
$ftpOverrides = ConvertTo-ReplyOverrides -Entries $FtpReply -ParameterName 'FtpReply'
$smtpOverrides = ConvertTo-ReplyOverrides -Entries $SmtpReply -ParameterName 'SmtpReply'
$imapOverrides = ConvertTo-ReplyOverrides -Entries $ImapReply -ParameterName 'ImapReply'
$pop3Overrides = ConvertTo-ReplyOverrides -Entries $Pop3Reply -ParameterName 'Pop3Reply'
$tftpOverrides = ConvertTo-ReplyOverrides -Entries $TftpReply -ParameterName 'TftpReply'
foreach ($step in $tftpOverrides.Keys) {
    $reply = $tftpOverrides[$step][0]
    if ($step -match '^(RRQ|WRQ)$') {
        if ($reply -notmatch '^ERROR (\d{1,5})( |$)' -or [int] $Matches[1] -gt 65535) { throw "TftpReply '$step=$reply': a request is answered 'ERROR <code> <text>', the code 0 to 65535." }
    } elseif ($step -match '^(ACK|DATA)\d{1,5}$') {
        if ($reply -ne 'DROP') { throw "TftpReply '$step=$reply': an ACK or DATA packet can only be DROP." }
    } else {
        throw "TftpReply '$step=$reply': the step is RRQ, WRQ, ACK<n> or DATA<n>."
    }
}
$scriptSteps = if ($Script) { ConvertFrom-ExchangeScript -Path $Script } else { $null }
$transcript = New-Object System.Text.StringBuilder
$uploadedData = New-Object System.IO.MemoryStream
$OutDirectory = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine((Get-Location).ProviderPath, $OutDirectory))
New-Item -ItemType Directory -Path $OutDirectory -Force | Out-Null

$tlsCertificate = if ($Tls -or $Ftp -or $Smtp -or $Imap -or $Pop3) { New-ThrowawayTlsCertificate -RootCertificateFile $TlsRootCertificateFile } else { $null }
if ($null -ne $tlsCertificate) {
    $publicKeyInfo = Get-SubjectPublicKeyInfo -Certificate $tlsCertificate
    $publicKeySha256 = [System.Convert]::ToBase64String([System.Security.Cryptography.SHA256]::Create().ComputeHash($publicKeyInfo))
    $CurlArgs = @($CurlArgs | ForEach-Object { $_.Replace('{TlsPublicKeySha256}', $publicKeySha256) })
    if ($TlsPublicKeyFile) {
        $TlsPublicKeyFile = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine((Get-Location).ProviderPath, $TlsPublicKeyFile))
        $pem = "-----BEGIN PUBLIC KEY-----`n" + [System.Convert]::ToBase64String($publicKeyInfo, [System.Base64FormattingOptions]::InsertLineBreaks).Replace("`r`n", "`n") + "`n-----END PUBLIC KEY-----`n"
        [System.IO.File]::WriteAllText($TlsPublicKeyFile, $pem, (New-Object System.Text.ASCIIEncoding))
        [System.IO.File]::WriteAllBytes($TlsPublicKeyFile + '.der', $publicKeyInfo)
    }
}
# -NoServer binds nothing: the caller's own server answers curl.
$listener = $null
$server = $null
if ($UnixSocket) {
    # AcceptTcpClient hands back a TcpClient over the accepted Unix socket, so the HTTP
    # server serves it unchanged. C#, not a PowerShell class, so no method needs this
    # runspace, which is busy waiting for curl.
    Add-Type -TypeDefinition @'
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

public sealed class RecorderUnixEndPoint : EndPoint
{
    private readonly string _path;
    public RecorderUnixEndPoint(string path) { _path = path; }
    public override AddressFamily AddressFamily { get { return AddressFamily.Unix; } }
    public override SocketAddress Serialize()
    {
        byte[] path = Encoding.UTF8.GetBytes(_path);
        var address = new SocketAddress(AddressFamily.Unix, 2 + 108);
        for (int i = 0; i < path.Length; i++) { address[2 + i] = path[i]; }
        return address;
    }
    public override EndPoint Create(SocketAddress socketAddress) { return this; }
}

public sealed class RecorderUnixSocketListener
{
    private readonly Socket _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
    public RecorderUnixSocketListener(string path)
    {
        _socket.Bind(new RecorderUnixEndPoint(path));
        _socket.Listen(16);
    }
    public TcpClient AcceptTcpClient()
    {
        Socket accepted = _socket.Accept();
        var client = new TcpClient();
        client.Client.Dispose();
        client.Client = accepted;
        return client;
    }
    public void Stop() { _socket.Close(); }
}
'@
    $UnixSocket = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine((Get-Location).ProviderPath, $UnixSocket))
    if (Test-Path -LiteralPath $UnixSocket) { Remove-Item -LiteralPath $UnixSocket -Force }
    $listener = New-Object RecorderUnixSocketListener($UnixSocket)
    $server = [System.Management.Automation.PowerShell]::Create()
} elseif (-not $NoServer -and -not $Tftp) {
    $listener = New-Object System.Net.Sockets.TcpListener($ListenAddress, $Port)
    $listener.Start()
    $server = [System.Management.Automation.PowerShell]::Create()
}
$dnsResponder = $null
if ($DnsPort.Count -gt 0) {
    # C#, not a PowerShell class, so its threads need no runspace; Windows PowerShell 5.1
    # compiles it as C# 5.
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public sealed class RecorderDnsResponder
{
    private readonly List<Socket> _sockets = new List<Socket>();
    private readonly List<TcpListener> _listeners = new List<TcpListener>();
    private readonly StringBuilder _log = new StringBuilder();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly IPAddress[] _answers;
    private readonly bool _truncate;
    private int _responseCode;

    public RecorderDnsResponder(IPAddress address, int[] ports, int[] silentPorts, IPAddress[] answers, bool truncate, int responseCode)
    {
        _responseCode = responseCode;
        _answers = answers;
        _truncate = truncate;
        foreach (int port in ports)
        {
            var udp = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            udp.Bind(new IPEndPoint(address, port));
            _sockets.Add(udp);
            bool silent = Array.IndexOf(silentPorts, port) >= 0;
            int boundPort = port;
            new Thread(delegate() { ServeUdp(udp, boundPort, silent); }) { IsBackground = true }.Start();
            var tcp = new TcpListener(address, port);
            tcp.Start();
            _listeners.Add(tcp);
            new Thread(delegate() { ServeTcp(tcp, boundPort); }) { IsBackground = true }.Start();
        }
    }

    public string Log { get { lock (_log) { return _log.ToString(); } } }

    public void Stop()
    {
        foreach (Socket socket in _sockets) { socket.Close(); }
        foreach (TcpListener listener in _listeners) { listener.Stop(); }
    }

    private void Record(string transport, int port, byte[] query, int length)
    {
        lock (_log)
        {
            _log.Append(_clock.ElapsedMilliseconds).Append(' ').Append(transport).Append(' ').Append(port).Append(' ');
            _log.Append(BitConverter.ToString(query, 0, length).Replace("-", string.Empty).ToLowerInvariant()).Append('\n');
        }
    }

    private void ServeUdp(Socket udp, int port, bool silent)
    {
        var buffer = new byte[65535];
        try
        {
            while (true)
            {
                EndPoint sender = new IPEndPoint(udp.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
                int length = udp.ReceiveFrom(buffer, ref sender);
                Record("udp", port, buffer, length);
                if (!silent)
                {
                    byte[] reply = Answer(buffer, length, _truncate);
                    udp.SendTo(reply, sender);
                }
            }
        }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
    }

    private void ServeTcp(TcpListener listener, int port)
    {
        try
        {
            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                new Thread(delegate() { ServeTcpClient(client, port); }) { IsBackground = true }.Start();
            }
        }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
        catch (InvalidOperationException) { }
    }

    private void ServeTcpClient(TcpClient client, int port)
    {
        try
        {
            using (client)
            {
                NetworkStream stream = client.GetStream();
                while (true)
                {
                    byte[] prefix = ReadExactly(stream, 2);
                    if (prefix == null) { return; }
                    byte[] query = ReadExactly(stream, (prefix[0] << 8) | prefix[1]);
                    if (query == null) { return; }
                    Record("tcp", port, query, query.Length);
                    byte[] reply = Answer(query, query.Length, false);
                    stream.Write(new byte[] { (byte)(reply.Length >> 8), (byte)reply.Length }, 0, 2);
                    stream.Write(reply, 0, reply.Length);
                }
            }
        }
        catch (System.IO.IOException) { }
        catch (ObjectDisposedException) { }
    }

    private static byte[] ReadExactly(NetworkStream stream, int count)
    {
        var bytes = new byte[count];
        int read = 0;
        while (read < count)
        {
            int n = stream.Read(bytes, read, count - read);
            if (n == 0) { return null; }
            read += n;
        }
        return bytes;
    }

    /// The query's header and question, then one answer record per address of the asked family.
    private byte[] Answer(byte[] query, int length, bool truncate)
    {
        int end = 12;
        while (end < length && query[end] != 0) { end += 1 + query[end]; }
        end += 1 + 4;
        int type = (query[end - 4] << 8) | query[end - 3];
        var reply = new List<byte>();
        for (int i = 0; i < end; i++) { reply.Add(query[i]); }
        reply[2] = (byte)(truncate ? 0x83 : 0x81);
        reply[3] = (byte)(0x80 | _responseCode);
        for (int i = 6; i < 12; i++) { reply[i] = 0; }
        int answers = 0;
        if (!truncate && _responseCode == 0)
        {
            foreach (IPAddress address in _answers)
            {
                bool wanted = (type == 1 && address.AddressFamily == AddressFamily.InterNetwork)
                    || (type == 28 && address.AddressFamily == AddressFamily.InterNetworkV6);
                if (!wanted) { continue; }
                byte[] data = address.GetAddressBytes();
                reply.AddRange(new byte[] { 0xC0, 0x0C, (byte)(type >> 8), (byte)type, 0, 1, 0, 0, 0, 60, 0, (byte)data.Length });
                reply.AddRange(data);
                answers++;
            }
        }
        reply[7] = (byte)answers;
        return reply.ToArray();
    }
}
'@
    $dnsResponder = New-Object RecorderDnsResponder($ListenAddress, $DnsPort, $DnsSilentPort, $DnsAnswerAddress, [bool] $DnsTruncate, $DnsResponseCode)
}
$tftpResponder = $null
if ($Tftp) {
    # C#, not a PowerShell class, so its thread needs no runspace; Windows PowerShell 5.1
    # compiles it as C# 5.
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

public sealed class RecorderTftpResponder
{
    private static readonly Encoding Latin1 = Encoding.GetEncoding(28591);
    private readonly IPAddress _address;
    private readonly Socket _listen;
    private readonly byte[] _data;
    private readonly bool _noOack;
    private readonly int _idleMilliseconds;
    private readonly Dictionary<string, string> _overrides = new Dictionary<string, string>();
    private readonly HashSet<string> _dropped = new HashSet<string>();
    private readonly StringBuilder _log = new StringBuilder();
    private readonly MemoryStream _upload = new MemoryStream();
    private readonly Thread _thread;
    private Socket _transfer;
    private EndPoint _peer;
    private byte[] _firstAnswer;
    private byte[] _lastSent;

    public RecorderTftpResponder(IPAddress address, int port, byte[] data, bool noOack, int idleMilliseconds, string[] steps, string[] replies)
    {
        _address = address;
        _data = data;
        _noOack = noOack;
        _idleMilliseconds = idleMilliseconds;
        for (int i = 0; i < steps.Length; i++) { _overrides[steps[i]] = replies[i]; }
        _listen = new Socket(address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        _listen.Bind(new IPEndPoint(address, port));
        _thread = new Thread(Serve) { IsBackground = true };
        _thread.Start();
    }

    public string Log { get { lock (_log) { return _log.ToString(); } } }

    public byte[] Uploaded { get { lock (_upload) { return _upload.ToArray(); } } }

    /// Ends the transfer, if it is still waiting, and gives the thread a moment to finish.
    public void Stop()
    {
        _listen.Close();
        Socket transfer = _transfer;
        if (transfer != null) { transfer.Close(); }
        _thread.Join(2000);
    }

    private void Serve()
    {
        try
        {
            var buffer = new byte[65536];
            EndPoint sender = new IPEndPoint(_address.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
            int length = _listen.ReceiveFrom(buffer, ref sender);
            byte[] request = Copy(buffer, length);
            Record('>', request);
            _peer = sender;
            _transfer = new Socket(_address.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
            _transfer.Bind(new IPEndPoint(_address, 0));
            int opcode = Number(request, 0);
            if (opcode != 1 && opcode != 2)
            {
                Send(Error(4, "Illegal TFTP operation"), null);
                return;
            }
            string reply;
            if (_overrides.TryGetValue(opcode == 1 ? "RRQ" : "WRQ", out reply))
            {
                // "ERROR <code> <text>", checked by the script before the responder starts.
                string[] parts = reply.Split(new[] { ' ' }, 3);
                Send(Error(int.Parse(parts[1]), parts.Length > 2 ? parts[2] : string.Empty), null);
                return;
            }
            List<string> fields = Strings(request, 2);
            var options = new List<KeyValuePair<string, string>>();
            int blockSize = 512;
            for (int i = 2; i + 1 < fields.Count; i += 2)
            {
                string name = fields[i].ToLowerInvariant();
                if (name != "blksize" && name != "tsize" && name != "timeout") { continue; }
                string value = name == "tsize" && opcode == 1 ? _data.Length.ToString() : fields[i + 1];
                options.Add(new KeyValuePair<string, string>(fields[i], value));
                if (name == "blksize") { blockSize = int.Parse(value); }
            }
            bool oack = options.Count > 0 && !_noOack;
            if (!oack) { blockSize = 512; }
            if (opcode == 1) { ServeRead(oack, options, blockSize); } else { ServeWrite(oack, options, blockSize); }
        }
        catch (SocketException) { }
        catch (ObjectDisposedException) { }
    }

    private void ServeRead(bool oack, List<KeyValuePair<string, string>> options, int blockSize)
    {
        int expectedAck = 0;
        bool finalSent = false;
        if (oack) { Send(Oack(options), null); } else { finalSent = SendDataBlock(expectedAck = 1, blockSize); }
        _firstAnswer = _lastSent;
        while (true)
        {
            bool fromListen;
            byte[] packet = Receive(out fromListen);
            if (packet == null) { return; }
            if (fromListen) { Resend(_firstAnswer); continue; }
            int opcode = Number(packet, 0);
            if (opcode == 5) { return; }
            if (opcode != 4 || packet.Length < 4) { continue; }
            int block = Number(packet, 2);
            if (block != expectedAck) { Resend(_lastSent); continue; }
            if (DropOnce("ACK" + block)) { continue; }
            if (finalSent) { return; }
            expectedAck = (expectedAck + 1) & 0xFFFF;
            finalSent = SendDataBlock(expectedAck, blockSize);
        }
    }

    private void ServeWrite(bool oack, List<KeyValuePair<string, string>> options, int blockSize)
    {
        int expectedData = 1;
        if (oack) { Send(Oack(options), null); } else { Send(Ack(0), "ACK0"); }
        _firstAnswer = _lastSent;
        while (true)
        {
            bool fromListen;
            byte[] packet = Receive(out fromListen);
            if (packet == null) { return; }
            if (fromListen) { Resend(_firstAnswer); continue; }
            int opcode = Number(packet, 0);
            if (opcode == 5) { return; }
            if (opcode != 3 || packet.Length < 4) { continue; }
            int block = Number(packet, 2);
            // A repeated block means curl missed its ACK; acknowledge it again. After the
            // last block this keeps answering until curl stops asking.
            if (block != expectedData) { Resend(_lastSent); continue; }
            if (DropOnce("DATA" + block)) { continue; }
            lock (_upload) { _upload.Write(packet, 4, packet.Length - 4); }
            Send(Ack(block), "ACK" + block);
            expectedData = (expectedData + 1) & 0xFFFF;
        }
    }

    /// Sends DATA block n of the file; true when it is the last one (shorter than a block).
    private bool SendDataBlock(int block, int blockSize)
    {
        long offset = (long)(block - 1) * blockSize;
        int length = (int)Math.Max(0, Math.Min(blockSize, _data.Length - offset));
        var packet = new byte[4 + length];
        packet[1] = 3;
        packet[2] = (byte)(block >> 8);
        packet[3] = (byte)block;
        if (length > 0) { Array.Copy(_data, offset, packet, 4, length); }
        Send(packet, "DATA" + block);
        return length < blockSize;
    }

    /// True, once, when the step is overridden with DROP.
    private bool DropOnce(string step)
    {
        string reply;
        if (!_overrides.TryGetValue(step, out reply) || reply != "DROP" || _dropped.Contains(step)) { return false; }
        _dropped.Add(step);
        return true;
    }

    private void Send(byte[] packet, string step)
    {
        _lastSent = packet;
        if (step != null && DropOnce(step)) { return; }
        _transfer.SendTo(packet, _peer);
        Record('<', packet);
    }

    private void Resend(byte[] packet)
    {
        if (packet == null) { return; }
        _transfer.SendTo(packet, _peer);
        Record('<', packet);
    }

    /// The next datagram curl sends to either port, or null after the idle time.
    private byte[] Receive(out bool fromListen)
    {
        fromListen = false;
        var readable = new List<Socket> { _transfer, _listen };
        Socket.Select(readable, null, null, _idleMilliseconds * 1000);
        if (readable.Count == 0) { return null; }
        Socket socket = readable[0];
        fromListen = socket == _listen;
        var buffer = new byte[65536];
        EndPoint sender = new IPEndPoint(_address.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any, 0);
        int length = socket.ReceiveFrom(buffer, ref sender);
        byte[] packet = Copy(buffer, length);
        Record('>', packet);
        return packet;
    }

    private static byte[] Ack(int block)
    {
        return new byte[] { 0, 4, (byte)(block >> 8), (byte)block };
    }

    private static byte[] Error(int code, string text)
    {
        var packet = new List<byte> { 0, 5, (byte)(code >> 8), (byte)code };
        packet.AddRange(Latin1.GetBytes(text));
        packet.Add(0);
        return packet.ToArray();
    }

    private static byte[] Oack(List<KeyValuePair<string, string>> options)
    {
        var packet = new List<byte> { 0, 6 };
        foreach (KeyValuePair<string, string> option in options)
        {
            packet.AddRange(Latin1.GetBytes(option.Key));
            packet.Add(0);
            packet.AddRange(Latin1.GetBytes(option.Value));
            packet.Add(0);
        }
        return packet.ToArray();
    }

    private static int Number(byte[] packet, int offset)
    {
        return packet.Length < offset + 2 ? -1 : (packet[offset] << 8) | packet[offset + 1];
    }

    /// The NUL-terminated strings from the offset on; a last one without its NUL is kept.
    private static List<string> Strings(byte[] packet, int offset)
    {
        var strings = new List<string>();
        int start = offset;
        for (int i = offset; i < packet.Length; i++)
        {
            if (packet[i] != 0) { continue; }
            strings.Add(Latin1.GetString(packet, start, i - start));
            start = i + 1;
        }
        if (start < packet.Length) { strings.Add(Latin1.GetString(packet, start, packet.Length - start)); }
        return strings;
    }

    private static byte[] Copy(byte[] buffer, int length)
    {
        var bytes = new byte[length];
        Array.Copy(buffer, bytes, length);
        return bytes;
    }

    private static string Describe(byte[] packet)
    {
        int opcode = Number(packet, 0);
        switch (opcode)
        {
            case 1:
            case 2:
            {
                List<string> fields = Strings(packet, 2);
                var text = new StringBuilder(opcode == 1 ? "RRQ" : "WRQ");
                if (fields.Count > 0) { text.Append(' ').Append(fields[0]); }
                if (fields.Count > 1) { text.Append(' ').Append(fields[1]); }
                for (int i = 2; i + 1 < fields.Count; i += 2) { text.Append(' ').Append(fields[i]).Append('=').Append(fields[i + 1]); }
                return text.ToString();
            }
            case 3:
                return "DATA " + Number(packet, 2) + " " + Math.Max(0, packet.Length - 4) + " bytes";
            case 4:
                return "ACK " + Number(packet, 2);
            case 5:
            {
                List<string> fields = Strings(packet, 4);
                return "ERROR " + Number(packet, 2) + (fields.Count > 0 ? " " + fields[0] : string.Empty);
            }
            case 6:
            {
                List<string> fields = Strings(packet, 2);
                var text = new StringBuilder("OACK");
                for (int i = 0; i + 1 < fields.Count; i += 2) { text.Append(' ').Append(fields[i]).Append('=').Append(fields[i + 1]); }
                return text.ToString();
            }
            default:
                return "opcode " + opcode;
        }
    }

    private void Record(char direction, byte[] packet)
    {
        lock (_log)
        {
            _log.Append(direction).Append(' ');
            _log.Append(BitConverter.ToString(packet).Replace("-", string.Empty).ToLowerInvariant());
            _log.Append(' ').Append(Describe(packet)).Append('\n');
        }
    }
}
'@
    $tftpSteps = [string[]] @($tftpOverrides.Keys)
    $tftpReplies = [string[]] @($tftpSteps | ForEach-Object { $tftpOverrides[$_][0] })
    $tftpResponder = New-Object RecorderTftpResponder($ListenAddress, $Port, [byte[]] (ConvertFrom-EscapedResponse -Text $TftpData), [bool] $TftpNoOack, $TftpIdleMilliseconds, $tftpSteps, $tftpReplies)
}
# The sink never answers; the datagrams wait in its receive buffer until curl exits.
$udpSinkClient = $null
if ($UdpSink) {
    $udpSinkClient = New-Object System.Net.Sockets.UdpClient(New-Object System.Net.IPEndPoint($ListenAddress, $Port))
    $udpSinkClient.Client.ReceiveBufferSize = 8MB
}
try {
    if ($NoServer -or $Tftp) {
        $serverRun = $null
    } elseif ($Ftp) {
        [void] $server.AddScript($serveFtpSession).AddArgument($listener).AddArgument($ftpOverrides).AddArgument([byte[]] (ConvertFrom-EscapedResponse -Text $FtpData)).AddArgument($transcript).AddArgument($uploadedData).AddArgument($tlsCertificate).AddArgument([bool] $Tls).AddArgument($FtpIdleMilliseconds).AddArgument($ListenAddress).AddArgument($sessionHelpers.ToString()).AddArgument($FtpDataHoldMilliseconds)
    } elseif ($Smtp) {
        [void] $server.AddScript($serveSmtpSession).AddArgument($listener).AddArgument($smtpOverrides).AddArgument($transcript).AddArgument($tlsCertificate).AddArgument([bool] $Tls).AddArgument($SmtpIdleMilliseconds).AddArgument($sessionHelpers.ToString())
    } elseif ($Imap) {
        [void] $server.AddScript($serveImapSession).AddArgument($listener).AddArgument($imapOverrides).AddArgument([byte[]] (ConvertFrom-EscapedResponse -Text $ImapMessage)).AddArgument($transcript).AddArgument($tlsCertificate).AddArgument([bool] $Tls).AddArgument($ImapIdleMilliseconds).AddArgument($sessionHelpers.ToString())
    } elseif ($Pop3) {
        [void] $server.AddScript($servePop3Session).AddArgument($listener).AddArgument($pop3Overrides).AddArgument([byte[]] (ConvertFrom-EscapedResponse -Text $Pop3Message)).AddArgument($transcript).AddArgument($tlsCertificate).AddArgument([bool] $Tls).AddArgument($Pop3IdleMilliseconds).AddArgument($sessionHelpers.ToString())
    } elseif ($Script) {
        [void] $server.AddScript($serveScriptedSession).AddArgument($listener).AddArgument($scriptSteps).AddArgument($transcript).AddArgument($tlsCertificate).AddArgument([bool] $Tls).AddArgument($ScriptIdleMilliseconds).AddArgument($ScriptGapMilliseconds).AddArgument($sessionHelpers.ToString())
    } else {
        [void] $server.AddScript($serveConnections).AddArgument($listener).AddArgument($responseBytes).AddArgument($Connections).AddArgument($ResponseDelayMilliseconds).AddArgument([bool] $Reset).AddArgument($RespondAfterBodyBytes).AddArgument($tlsCertificate).AddArgument($HoldOpenMilliseconds).AddArgument([bool] $TlsCloseNotify)
    }
    if ($null -ne $server) { $serverRun = $server.BeginInvoke() }

    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = $Curl
    $startInfo.Arguments = (@($CurlArgs | ForEach-Object { ConvertTo-CommandLineArgument -Argument $_ }) -join ' ')
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.CreateNoWindow = $true

    # The standard input writer takes the console's input encoding, whose UTF-8 byte order
    # mark would reach curl ahead of StandardInput; Latin-1 has none. Windows PowerShell 5.1
    # has no ProcessStartInfo.StandardInputEncoding to set instead.
    $consoleInputEncoding = [System.Console]::InputEncoding
    [System.Console]::InputEncoding = [System.Text.Encoding]::GetEncoding(28591)
    try {
        $curlClock = [System.Diagnostics.Stopwatch]::StartNew()
        $curlProcess = [System.Diagnostics.Process]::Start($startInfo)
    } finally {
        [System.Console]::InputEncoding = $consoleInputEncoding
    }
    try {
        $standardInputBytes = ConvertFrom-EscapedResponse -Text $StandardInput
        $curlProcess.StandardInput.BaseStream.Write($standardInputBytes, 0, $standardInputBytes.Length)
        $curlProcess.StandardInput.Close()
        $stdout = New-Object System.IO.MemoryStream
        $stderr = New-Object System.IO.MemoryStream
        # Both pipes drain at once, so curl never blocks on a full one.
        $stdoutCopy = $curlProcess.StandardOutput.BaseStream.CopyToAsync($stdout)
        $stderrCopy = $curlProcess.StandardError.BaseStream.CopyToAsync($stderr)
        # PeakWorkingSet64 cannot be read once the process has exited on every platform,
        # so sample it every 10 ms while waiting and keep the largest reading.
        $peakWorkingSetBytes = [long] 0
        $peakSamples = 0
        while (-not $curlProcess.WaitForExit(10)) {
            try {
                $curlProcess.Refresh()
                $peakWorkingSetBytes = [System.Math]::Max($peakWorkingSetBytes, $curlProcess.PeakWorkingSet64)
                $peakSamples++
            } catch [System.InvalidOperationException] {
                # The process finished between the wait and the read.
            }
        }
        $curlClock.Stop()
        # WaitForExit(int) can return before the redirected pipes reach end of stream.
        $curlProcess.WaitForExit()
        [System.Threading.Tasks.Task]::WaitAll(@($stdoutCopy, $stderrCopy))
        $exitCode = $curlProcess.ExitCode
    } finally {
        $curlProcess.Dispose()
    }

    $requests = @()
    if ($null -ne $server) {
        # A connection curl did not open would block the server forever; stopping the
        # listener ends the wait. Give an accepted connection a moment to finish first.
        [void] $serverRun.AsyncWaitHandle.WaitOne(2000)
        $listener.Stop()
        $requests = $server.EndInvoke($serverRun)
        if ($server.Streams.Error.Count -gt 0) { throw $server.Streams.Error[0] }
    }

    if ($null -ne $tftpResponder) { $tftpResponder.Stop() }

    $udpDatagrams = New-Object System.Text.StringBuilder
    if ($null -ne $udpSinkClient) {
        $sender = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, 0)
        while ($udpSinkClient.Available -gt 0) {
            $datagram = $udpSinkClient.Receive([ref] $sender)
            [void] $udpDatagrams.Append(([System.BitConverter]::ToString($datagram) -replace '-', '').ToLowerInvariant()).Append("`n")
        }
    }
} finally {
    if ($null -ne $udpSinkClient) { $udpSinkClient.Close() }
    if ($null -ne $dnsResponder) { $dnsResponder.Stop() }
    if ($null -ne $tftpResponder) { $tftpResponder.Stop() }
    if ($null -ne $listener) { $listener.Stop() }
    if ($UnixSocket -and (Test-Path -LiteralPath $UnixSocket)) { Remove-Item -LiteralPath $UnixSocket -Force }
    if ($null -ne $server) { $server.Dispose() }
    # Reset deletes the key container the PFX import created.
    if ($null -ne $tlsCertificate) { $tlsCertificate.Reset() }
}

$requestBytes = New-Object System.IO.MemoryStream
foreach ($request in $requests) {
    foreach ($bytes in $request) { $requestBytes.Write($bytes, 0, $bytes.Length) }
}

if (-not $NoServer -and -not $Tftp) { [System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'request.bin'), $requestBytes.ToArray()) }
[System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'stdout.bin'), $stdout.ToArray())
[System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'stderr.txt'), $stderr.ToArray())
[System.IO.File]::WriteAllText((Join-Path $OutDirectory 'exitcode.txt'), [string] $exitCode, [System.Text.Encoding]::ASCII)
if ($Ftp) {
    [System.IO.File]::WriteAllText((Join-Path $OutDirectory 'transcript.txt'), $transcript.ToString(), [System.Text.Encoding]::GetEncoding(28591))
    [System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'upload.bin'), $uploadedData.ToArray())
}
if ($UdpSink) {
    [System.IO.File]::WriteAllText((Join-Path $OutDirectory 'datagrams.txt'), $udpDatagrams.ToString(), [System.Text.Encoding]::ASCII)
}
if ($Tftp) {
    [System.IO.File]::WriteAllText((Join-Path $OutDirectory 'datagrams.txt'), $tftpResponder.Log, [System.Text.Encoding]::GetEncoding(28591))
    [System.IO.File]::WriteAllBytes((Join-Path $OutDirectory 'upload.bin'), $tftpResponder.Uploaded)
}
if ($null -ne $dnsResponder) {
    [System.IO.File]::WriteAllText((Join-Path $OutDirectory 'dns.txt'), $dnsResponder.Log, [System.Text.Encoding]::ASCII)
}
if ($Smtp -or $Imap -or $Pop3 -or $Script) {
    [System.IO.File]::WriteAllText((Join-Path $OutDirectory 'transcript.txt'), $transcript.ToString(), [System.Text.Encoding]::GetEncoding(28591))
}
$curlExecutable = $Curl
if (Test-Path -LiteralPath $Curl -PathType Leaf) {
    $curlExecutable = (Resolve-Path -LiteralPath $Curl).ProviderPath
} else {
    $curlCommand = Get-Command $Curl -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $curlCommand) { $curlExecutable = $curlCommand.Source }
}
$timing = [ordered] @{
    executable = $curlExecutable
    elapsedMilliseconds = [long] $curlClock.ElapsedMilliseconds
    peakWorkingSetBytes = [long] $peakWorkingSetBytes
    samples = [int] $peakSamples
}
[System.IO.File]::WriteAllText((Join-Path $OutDirectory 'timing.json'), (ConvertTo-Json -InputObject $timing -Compress), (New-Object System.Text.UTF8Encoding($false)))

Write-Host "curl exited $exitCode after $($curlClock.ElapsedMilliseconds) ms; fixtures written to $OutDirectory"
