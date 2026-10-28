#!/usr/bin/env bash
# SYNOPSIS
#     Records one real curl exchange against a canned loopback server, as test fixtures:
#     the request bytes curl sent, its stdout, its stderr and its exit code.
#
#     The Bash port, for Linux and macOS, of Record-CurlExchange.ps1. It writes the same
#     files in the same formats, so a fixture recorded by either is read the same way.
#     Ported so far: the HTTP and HTTPS (-Tls) server, -NoServer, -UnixSocket, -Ftp
#     (passive and active mode, explicit and implicit FTPS, PROT, CCC; see FTP below) and
#     -Smtp, -Imap and -Pop3 (plain, STARTTLS / STLS and implicit TLS with -Tls) and
#     -Tftp (see TFTP below), -UdpSink, -DnsPort and -DohPort (see DNS below), and -Http2 and
#     -Http2Origin (see HTTP/2 below).
#
# TFTP
#     -Tftp serves one TFTP transfer on UDP -Port with TftpData, TftpReply, TftpNoOack and
#     TftpIdleMilliseconds as the PowerShell script does, and writes datagrams.txt and
#     upload.bin (no request.bin). A reply comes from a new UDP port, which bash cannot
#     open, so a perl (core modules only) written to the state directory serves it; -Tftp
#     needs perl besides curl.
#
# DIFFERENCES FROM THE POWERSHELL SCRIPT (FTP)
#     A data connection curl opens that no transfer uses (for example after an overridden
#     RETR that sends only a 4xx) is closed with a FIN here; the PowerShell script's
#     listener resets it, so curl reports exit 18 (transfer closed) here and exit 56
#     (connection reset) there. Everything else measured is byte-identical (see the
#     log of BL-3929): passive, active (EPRT), explicit and implicit FTPS, PROT P data,
#     CCC in passive and active mode, a refused CCC, uploads, REST, FtpReply overrides.
#
# DNS
#     -UdpSink writes datagrams.txt, -DnsPort (with -DnsSilentPort, -DnsAnswerAddress,
#     -DnsTruncate, -DnsResponseCode and -DnsEchConfigList) and -DohPort write dns.txt, as the
#     PowerShell script does. bash cannot receive a UDP datagram, so a perl (core modules only)
#     written to the state directory serves them; -DohPort puts TLS 1.2 in front of it with
#     socat and the throwaway certificate of -Tls. They need perl, and -DohPort socat and openssl.
#     -DnsAnswerAddress may be repeated or take a comma-separated list.
#
# HTTP/2
#     -Http2 (an HTTPS proxy that picks h2 or http/1.1 by ALPN, with ProxyAlpn and ProxyStatus) and
#     -Http2Origin (h2c-upgrade, h2c-upgrade-ignored, prior-knowledge, alpn; with Http2ResetCode,
#     Http2Answer and Http2NoSettings) write request.bin and transcript.txt as the PowerShell script
#     does: HTTP/2 framing and HPACK (Huffman included) are served by a perl (core modules only)
#     written to the state directory. Over TLS socat has no ALPN option, so openssl s_server offers the
#     protocol and perl serves its plaintext through FIFOs; that needs openssl and mkfifo as -Tls does.
#     With -Http2ResetCode over TLS every connection curl opens is served: perl listens on the port,
#     and each connection gets an s_server of its own on a Unix socket, a perl relay copying curl's
#     bytes to it and its plaintext on two pipes.
#     A run with several connections (-Http2ResetCode) logs their frames in the order they arrive, which
#     differs between runs here as in the PowerShell script.
#
# -SCRIPT (scripted binary sessions, LDAP and SMB)
#     Served by a perl (core modules only) written to the state directory, which parses the steps
#     file as the PowerShell script does (a bad line is refused at the start) and writes request.bin
#     and transcript.txt in the same bytes. With -Tls, s_server takes the connection and perl serves
#     its plaintext through two FIFOs as for -Http2; once the session is over s_server is stopped,
#     a close without close_notify. Two differences, both off Windows only:
#       - One connection is served. The PowerShell script accepts every connection curl opens and
#         serves the first that writes, for the Windows build's WinLDAP, which opens a second
#         connection of its own; no Unix curl does, so there is nothing to select and the
#         "curl opened N connections" line of transcript.txt never appears.
#       - A reset step over TLS closes the connection with a FIN, not a RST: s_server holds the
#         socket. Over plain TCP it is a RST, as in the PowerShell script.
#     Needs perl besides curl, and for -Tls openssl and mkfifo as -Tls does.
#
# -HALFCLOSEAFTERRESPONSE WITH -TLS
#     The FIN reaches curl through a relay (perl, core modules only) in place of socat's, which a
#     signal from the handler tells to shut down its send side once s_server has sent the
#     response; s_server goes on reading, so with HoldOpenMilliseconds what curl still sends is
#     recorded. Needs perl besides socat, openssl and mkfifo.
#
# PARAMETERS OF OTHER MODES
#     A mode's parameters (FtpReply, SmtpReply, ..., ProxyStatus) are accepted, and
#     ignored without their mode, as the PowerShell script ignores them.
#
# DESCRIPTION
#     Expected bytes in Curl's HTTP tests come from real curl, measured first. This script
#     is how they are measured on Linux and macOS. It binds ListenAddress:<Port> with socat,
#     runs curl with the given arguments, and for each of the first <Connections>
#     connections curl opens it:
#
#       1. reads the request until the header block ends (CRLF CRLF) and, when the
#          headers carry Transfer-Encoding: chunked (checked first, even beside a
#          Content-Length, as upstream's sws.c does: until the closing chunk) or
#          Content-Length, until the body has arrived - each read waits at most one second
#          (RequestReadMilliseconds), so a body that never comes ends the read instead of
#          hanging it;
#       2. records the raw request bytes;
#       3. sends that connection's canned response and closes the connection.
#
#     It then writes five files to OutDirectory:
#
#       request.bin   the raw request bytes, every connection's in the order accepted
#       stdout.bin    curl's standard output, byte for byte
#       stderr.txt    curl's standard error, byte for byte
#       exitcode.txt  curl's exit code, as decimal digits with no line ending
#       timing.json   one UTF-8 JSON object: "executable" (the full path of the curl
#                     run), "elapsedMilliseconds" (wall clock from start to exit),
#                     "peakWorkingSetBytes" and "samples", so the same exchange can be
#                     timed for real curl and for Curl
#
#     The peak working set is sampled every 10 ms while curl runs: VmHWM from
#     /proc/<pid>/status on Linux, the largest resident set ps reports elsewhere. Very
#     short runs can under-report it, and a run that ends before the first sample
#     reports 0 bytes and 0 samples.
#
#     A connection curl never opens is not waited for: once curl exits, the listener is
#     stopped. The script exits 0 when the fixtures were written, whatever curl's own
#     exit code was; recording a failing curl is as useful as recording a passing one.
#
# USAGE
#     Record-CurlExchange.sh -Port <n> [-Response <escaped>]... [options] -OutDirectory <dir>
#                            -CurlArgs <arg> [<arg>...]
#
#     Parameter names are the PowerShell script's, given with one dash, in any case
#     (-Port, -port). -CurlArgs takes every argument after it, so it comes last; "--" is
#     the same as -CurlArgs. A parameter that takes several values (-Response) is given
#     once per value, in order. -h, -help or --help prints this text.
#
# PARAMETERS
#     -Port <n>
#         The loopback TCP port to listen on. The URL in CurlArgs must use the same port.
#
#     -Response <escaped>
#         The canned response, as a string with backslash escapes: \r \n \t \0 \\ \" \' and
#         \xHH (two hex digits). After decoding, each character becomes one byte (Latin-1):
#         the argument is read as UTF-8, and a character above U+00FF is an error. The
#         response is sent exactly as given; the script adds nothing. Defaults to an empty
#         200 with Content-Length: 0. Give several, one per connection, to answer a
#         followed redirect's hops differently: connection N gets the Nth, and every
#         connection past the last gets the last.
#
#     -CurlArgs <arg>...
#         The arguments passed to curl, every argument after it, each reaching curl as one
#         argument exactly as given (Bash passes argv through; no Windows quoting is
#         needed). An empty argument reaches curl as an empty argument.
#
#     -OutDirectory <dir>
#         Where the fixture files are written. Created if missing; existing fixture files
#         there are overwritten.
#
#     -Connections <n>
#         How many connections to serve. Default one per Response entry (1 for a single
#         Response). Use more for a run that makes several requests, such as several URLs
#         or a followed redirect. A connection past the last is accepted and never answered,
#         as the PowerShell script leaves it in the listen backlog.
#
#     -ResponseDelayMilliseconds <n>
#         How long to wait after reading each request before sending the response. Default 0.
#
#     -Reset
#         Instead of reading a request and answering it, reset each connection as soon as it
#         is accepted: the socket closes with a zero linger time, so the kernel sends a TCP
#         RST rather than a FIN. request.bin is then empty. With -Tls, before any handshake.
#
#     -ResetAfterResponse
#         After sending each response (and after HoldOpenMilliseconds, when given), close the
#         connection with a zero linger time, so a TCP RST is sent rather than a FIN.
#
#     -HalfCloseAfterResponse
#         After sending each response, shut down the send side (a FIN) and go on reading, so
#         with HoldOpenMilliseconds anything curl still sends on the connection is recorded.
#         With -Tls the FIN is sent by a perl relay; see -HALFCLOSEAFTERRESPONSE WITH -TLS above.
#
#     -HoldOpenMilliseconds <n>
#         After sending each response, keep the connection open instead of closing it, until
#         curl closes its end or this many milliseconds pass without a byte from curl, and
#         record anything curl sends meanwhile. Default 0: close at once.
#
#     -AnswerHeldRequests <n>
#         With HoldOpenMilliseconds, how many more requests arriving on a held connection are
#         answered, each with the response after the one before it (the last repeats).
#         Default 0: what arrives is only recorded.
#
#     -RespondAfterBodyBytes <n>
#         Instead of reading the whole request, send the response as soon as the header block
#         and this many body bytes have arrived, then go on reading (and recording) whatever
#         curl still sends until it stops for two seconds or closes its end. Default -1: off.
#         Each read then waits up to five seconds.
#
#     -RequestReadMilliseconds <n>
#         How long each read of an HTTP request waits for its next byte before the request is
#         taken as it stands and answered. Default 1000. Ignored with RespondAfterBodyBytes.
#
#     -StandardInput <escaped>
#         What curl reads from standard input, with the same backslash escapes as Response.
#         Default empty: standard input is at end of file at once.
#
#     -Tls
#         Answer each connection over TLS 1.2 instead of plain TCP, with openssl s_server,
#         so the recorder can stand in for an HTTPS server or an HTTPS proxy. The
#         certificate served is a throwaway self-signed RSA 2048 certificate for
#         CN=127.0.0.1 (subject alternative name IP 127.0.0.1), made for this run and valid
#         for one day; curl needs -k (or --proxy-insecure for a proxy) to accept it.
#         request.bin, and the Response matching, hold the decrypted bytes, not the TLS
#         records. The key is written only to the run's temporary directory, deleted at the
#         end. A connection whose handshake fails, or does not finish within five seconds,
#         is recorded as empty.
#
#     -TlsCloseNotify
#         With -Tls, send TLS close_notify after each response before closing the connection.
#         Without it the connection closes with no close_notify, a bare end of the transport.
#
#     -TlsRootCertificateFile <path>
#         With -Tls, serve a certificate issued by a throwaway private root CA in place of the
#         self-signed one, and write that root's PEM to this path so curl can be given
#         --cacert <path>; the root may sign certificates and CRLs. The root's key is never
#         written outside the temporary directory; the file is left for the caller to delete.
#
#     -TlsEmptyCrlFile <path>
#         With -TlsRootCertificateFile, also write a PEM "X509 CRL" signed by the throwaway
#         root that revokes nothing, so curl can be given --crlfile <path>. It is a version 1
#         list (no extensions), valid from now for one day. Left for the caller to delete.
#
#     -TlsRevokingCrlFile <path>
#         With -TlsRootCertificateFile, also write a PEM "X509 CRL" signed by the throwaway
#         root that revokes the served certificate by its serial number, otherwise as
#         TlsEmptyCrlFile. Left for the caller to delete.
#
#     -TlsPublicKeyFile <path>
#         With -Tls, write the served certificate's public key (its SubjectPublicKeyInfo) to
#         this path as a PEM "PUBLIC KEY" block, and the same key as DER to this path with
#         ".der" appended, before curl runs, so curl can be given --pinnedpubkey <path>. With
#         -Tls, whether or not this is given, every "{TlsPublicKeySha256}" in CurlArgs is
#         replaced by the base64 SHA-256 of that SubjectPublicKeyInfo.
#
#     -Ftp
#         Serve one FTP session instead of HTTP responses, as Record-CurlExchange.ps1 -Ftp
#         does: a greeting, then curl's commands one line at a time, each answered from a
#         table of replies (the defaults are the PowerShell script's: 220, USER 331, PASS 230,
#         PWD 257, EPSV 229, PASV 227, TYPE 200, SIZE 213 with FtpData's length, MDTM 213,
#         CWD 250, REST 350, RETR/LIST/NLST 150 + FtpData + 226, STOR/APPE 150 + upload + 226,
#         QUIT 221, anything else 502). Passive data connections are accepted on a port the
#         script listens on from the start; EPRT and PORT switch to active mode, where the
#         script dials curl's address. AUTH is answered 234 and the control connection then
#         served over TLS (same throwaway certificate as -Tls; curl needs -k), PBSZ 200,
#         PROT 200 (PROT P, or -Tls until a PROT C, makes data connections TLS), CCC 200 and
#         TLS then cleared from the control connection: close_notify is sent, curl's
#         close_notify read if that is what comes next, and the rest read in plain text.
#         With -Tls, implicit FTPS: the control connection is TLS from its first byte.
#         request.bin holds every command line curl sent on the control connection (the
#         bytes after TLS decryption), transcript.txt both directions ("> " curl, "< " the
#         server, "= " events), and upload.bin the bytes uploaded by STOR and APPE.
#         Response, Connections, ResponseDelayMilliseconds and Reset are ignored.
#         Needs socat, and for AUTH, PROT and -Tls openssl and mkfifo as -Tls does.
#
#     -FtpReply <VERB=reply>
#         Reply overrides, one per use, with the PowerShell script's meaning: VERB is a
#         command name in capitals, GREETING, RETROPEN, RETRDONE or STORDONE; the reply has
#         Response's escapes; CLOSE hangs up and STALL answers nothing; several for one VERB
#         are used in turn, the last repeating; {DATAPORT}, {DATAPORT_HI}, {DATAPORT_LO}
#         and a {PAUSE} line work as there.
#
#     -FtpData <escaped>
#         The file RETR serves, and the listing LIST and NLST serve. Default empty.
#
#     -FtpIdleMilliseconds <n>
#         How long the server waits for curl's next command before it hangs up. Default 5000.
#
#     -FtpDataHoldMilliseconds <n>
#         How long a RETR, LIST or NLST data connection is held open after FtpData is sent,
#         before it is closed and the 226 sent. Default 0.
#
#     -Curl <path>
#         The curl executable to run. Defaults to the curl on PATH, the platform's own build
#         (OpenSSL on Linux), which is the reference build there.
#
#     -ListenAddress <address>
#         The address the server binds. Default 127.0.0.1. An address with a colon is IPv6.
#
#     -NoServer
#         Bind no port and serve nothing: run curl against a server the caller started. Only
#         stdout.bin, stderr.txt, exitcode.txt and timing.json are written. Combining it with
#         -Tls or any session mode is refused.
#
#     -UnixSocket <path>
#         Listen on a Unix domain socket at this path instead of TCP, for --unix-socket. Any
#         file already at the path is deleted first, and the socket file is deleted at the
#         end. HTTP only: combining it with -NoServer, -Tls or a session mode is refused.
#
# REQUIREMENTS AND PLATFORM NOTES
#     Bash 3.2 or later and standard Unix tools (awk, od, mkfifo, mktemp, sleep, wc), plus:
#       socat     the listener for plain HTTP, -UnixSocket, -Reset and -ResetAfterResponse,
#                 and for -Tls whenever more than one connection is served
#       openssl   OpenSSL 1.1.1 or later, for -Tls: certificates and the s_server that
#                 terminates TLS (-msgfile, -naccept and -unix are needed; LibreSSL's
#                 openssl, macOS's default, lacks them - put Homebrew's openssl first on
#                 PATH, or name it in the OPENSSL environment variable)
#     The script checks for what the chosen mode needs at startup and names what is missing.
#
#     It reads curl's bytes one at a time in the C locale, so NUL and high bytes reach
#     request.bin intact. That reads about 40 KB a second (300 KB took 8 seconds under
#     Bash 5.3 on Linux): fine for requests of a few hundred kilobytes, slow for
#     multi-megabyte uploads. curl itself runs in the caller's locale.
#
#     Under Bash 3.2 (macOS's /bin/bash) a read can only wait whole seconds, so every
#     millisecond wait shorter than a second, or between seconds, is rounded up to the next
#     whole second. Where neither Bash 5's EPOCHREALTIME nor date's %N is available
#     (macOS's date), elapsedMilliseconds is measured in whole seconds.
#
#     socat hands each accepted socket to a fresh process of this script (EXEC with
#     nofork), so the handler's exit is the connection's close, a FIN, or a RST under the
#     zero linger time -Reset and -ResetAfterResponse set; -HalfCloseAfterResponse shuts
#     down the send side with a second, one-shot socat. With -Tls and one connection, and
#     no -Reset or -ResetAfterResponse, socat is not needed: openssl s_server listens on
#     the port itself. Otherwise socat accepts each connection and relays it to a fresh
#     s_server on a Unix socket. The decrypted stream passes through two FIFOs. A close
#     without close_notify is made by killing s_server once its msgfile shows records
#     enough for the response, and a short moment more.
#
#     On Windows (Git Bash, MSYS2) -Tls is refused when openssl is a native Windows
#     program, which cannot read the FIFOs; use Record-CurlExchange.ps1 there.
#
# EXAMPLES
#     ./Record-CurlExchange.sh -Port 18081 -Response 'HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello' -OutDirectory fixtures/default-get -CurlArgs 'http://127.0.0.1:18081/a?b'
#
#         request.bin then holds
#         GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:18081\r\nUser-Agent: curl/8.18.0\r\nAccept: */*\r\n\r\n
#         and stdout.bin holds hello.
#
#     ./Record-CurlExchange.sh -Port 18443 -Tls -TlsCloseNotify -Response 'HTTP/1.1 200 OK\r\n\r\nclosed' -OutDirectory fixtures/https-close -CurlArgs -sk 'https://127.0.0.1:18443/'
#
#         Serves one HTTPS connection whose response ends when the server closes, with a
#         close_notify, and records the decrypted request.
#
#     ./Record-CurlExchange.sh -NoServer -OutDirectory fixtures/sftp-get -CurlArgs -sS -k 'sftp://tester:secret@127.0.0.1:2222/home/tester/a.txt'
#
#         Runs curl against an sshd the caller already started and records only what curl
#         printed and its exit code.

set -u

# Byte-exact reading needs the C locale: one character per byte, NUL and high bytes
# intact. The caller's locale is kept to run curl in.
RCE_CALLER_LC_ALL_SET=${LC_ALL+set}
RCE_CALLER_LC_ALL=${LC_ALL-}
export LC_ALL=C

RCE_NAME='Record-CurlExchange.sh'

rce_die() {
    printf '%s: %s\n' "$RCE_NAME" "$*" >&2
    exit 1
}


rce_print_help() {
    # The comment block at the top of this file, from the line after the shebang to the
    # first line that is not a comment.
    awk 'NR == 1 { next } /^#/ { sub(/^# ?/, ""); print; next } { exit }' "$0"
}

# Sets RCE_SECONDS to Milliseconds as the argument read -t takes. Bash 4 and later take a
# fraction; Bash 3.2 takes whole seconds only, so the wait is rounded up (at least 1).
rce_seconds() {
    if [ "${BASH_VERSINFO[0]}" -ge 4 ]; then
        printf -v RCE_SECONDS '%d.%03d' $(($1 / 1000)) $(($1 % 1000))
    else
        RCE_SECONDS=$((($1 + 999) / 1000))
        if [ "$RCE_SECONDS" -lt 1 ]; then RCE_SECONDS=1; fi
    fi
}

# Sleeps Milliseconds. GNU and BSD sleep both take a fraction.
rce_sleep_ms() {
    local seconds
    printf -v seconds '%d.%03d' $(($1 / 1000)) $(($1 % 1000))
    sleep "$seconds"
}

# Sets RCE_NOW_MS to the wall clock in milliseconds: Bash 5's EPOCHREALTIME, else date's
# %N (GNU), else whole seconds (macOS's date has no %N).
rce_now_ms() {
    local now
    if [ -n "${EPOCHREALTIME:-}" ]; then
        now=${EPOCHREALTIME/./}
        now=${now/,/}
        RCE_NOW_MS=$((10#$now / 1000))
        return
    fi
    now=$(date +%s%N 2>/dev/null)
    case $now in
        *[!0-9]* | '') RCE_NOW_MS=$(($(date +%s) * 1000)) ;;
        *) RCE_NOW_MS=$((10#$now / 1000000)) ;;
    esac
}

# Writes Text's bytes to OutFile, decoding the backslash escapes Response takes: \r \n \t
# \0 \\ \" \' and \xHH. Text is read as UTF-8 and each character becomes one byte
# (Latin-1), so a character above U+00FF is an error, as in Record-CurlExchange.ps1. A
# byte that is not part of a UTF-8 sequence is written as it is.
rce_decode_escaped() {
    local text=$1 out=$2 chunk escape hex index=0
    exec 3>"$out" || rce_die "cannot write $out"
    while [ -n "$text" ]; do
        chunk=${text%%\\*}
        if [ -n "$chunk" ]; then
            case $chunk in
                *[$'\200'-$'\377']*) rce_write_latin1 "$chunk" ;;
                *) printf '%s' "$chunk" >&3; RCE_CHARACTERS=${#chunk} ;;
            esac
            index=$((index + RCE_CHARACTERS))
            text=${text:${#chunk}}
            continue
        fi
        if [ ${#text} -lt 2 ]; then exec 3>&-; rce_die 'Response ends with a lone backslash.'; fi
        escape=${text:1:1}
        case $escape in
            r) printf '\r' >&3 ;;
            n) printf '\n' >&3 ;;
            t) printf '\t' >&3 ;;
            0) printf '\000' >&3 ;;
            \\) printf '\\' >&3 ;;
            \") printf '"' >&3 ;;
            \') printf "'" >&3 ;;
            x)
                hex=${text:2:2}
                case $hex in
                    [0-9A-Fa-f][0-9A-Fa-f]) printf "\\$(printf '%03o' $((16#$hex)))" >&3 ;;
                    *) exec 3>&-; rce_die "Response has a malformed \\x escape at index $index." ;;
                esac
                text=${text:4}
                index=$((index + 4))
                continue
                ;;
            *) exec 3>&-; rce_die "Response has an unknown escape \\$escape at index $index." ;;
        esac
        text=${text:2}
        index=$((index + 2))
    done
    exec 3>&-
}

# Writes Chunk (no backslashes, some bytes above 0x7F) to fd 3, one Latin-1 byte per
# UTF-8 character, and sets RCE_CHARACTERS to the number of characters.
rce_write_latin1() {
    local chunk=$1 length=${#1} position=0 byte next third code octal
    RCE_CHARACTERS=0
    while [ $position -lt $length ]; do
        printf -v byte '%d' "'${chunk:$position:1}"
        RCE_CHARACTERS=$((RCE_CHARACTERS + 1))
        if [ "$byte" -lt 128 ]; then
            printf '%s' "${chunk:$position:1}" >&3
            position=$((position + 1))
            continue
        fi
        next=-1
        if [ $((position + 1)) -lt $length ]; then printf -v next '%d' "'${chunk:$((position + 1)):1}"; fi
        if [ "$byte" -ge 194 ] && [ "$byte" -le 195 ] && [ "$next" -ge 128 ] && [ "$next" -le 191 ]; then
            code=$(((byte & 31) << 6 | (next & 63)))
            printf -v octal '\\%03o' "$code"
            printf "$octal" >&3
            position=$((position + 2))
            continue
        fi
        if [ "$byte" -ge 196 ] && [ "$byte" -le 223 ] && [ "$next" -ge 128 ] && [ "$next" -le 191 ]; then
            code=$(((byte & 31) << 6 | (next & 63)))
            exec 3>&-
            rce_die "$(printf 'Response character U+%04X is not Latin-1.' "$code")"
        fi
        if [ "$byte" -ge 224 ] && [ "$byte" -le 244 ] && [ "$next" -ge 128 ] && [ "$next" -le 191 ]; then
            third=0
            if [ $((position + 2)) -lt $length ]; then printf -v third '%d' "'${chunk:$((position + 2)):1}"; fi
            if [ "$byte" -lt 240 ]; then
                code=$(((byte & 15) << 12 | (next & 63) << 6 | (third & 63)))
            else
                code=$(((byte & 7) << 18 | (next & 63) << 12 | (third & 63) << 6))
            fi
            exec 3>&-
            rce_die "$(printf 'Response character U+%04X is not Latin-1.' "$code")"
        fi
        printf -v octal '\\%03o' "$byte"
        printf "$octal" >&3
        position=$((position + 1))
    done
}

# --- The request reader ---------------------------------------------------------------
# A request is fed one byte at a time; rce_request_complete then answers what the
# PowerShell script's Test-RequestComplete answers for the bytes so far. A NUL byte is
# held as \001 in these strings, since a Bash string cannot hold NUL; neither byte is part
# of any pattern matched here.

RCE_TRANSFER_ENCODING_CHUNKED='^transfer-encoding:(.*[^a-z0-9_])?chunked([^a-z0-9_]|$)'
RCE_CONTENT_LENGTH=$'^content-length:[ \t]*([0-9]+)[ \t]*\r?$'

rce_request_reset() {
    RQ_HEAD=''
    RQ_TAIL=''
    RQ_IN_BODY=0
    RQ_BODY_COUNT=0
    RQ_BODY_START=''
    RQ_BODY_END=''
    RQ_KIND=none
    RQ_LENGTH=0
}

rce_request_feed() {
    local byte=$1
    if [ $RQ_IN_BODY -eq 0 ]; then
        RQ_HEAD=$RQ_HEAD$byte
        RQ_TAIL=$RQ_TAIL$byte
        if [ ${#RQ_TAIL} -gt 4 ]; then RQ_TAIL=${RQ_TAIL:1}; fi
        if [ "$RQ_TAIL" = $'\r\n\r\n' ]; then
            RQ_IN_BODY=1
            rce_request_parse_head "${RQ_HEAD%????}"
        fi
        return
    fi
    RQ_BODY_COUNT=$((RQ_BODY_COUNT + 1))
    if [ ${#RQ_BODY_START} -lt 5 ]; then RQ_BODY_START=$RQ_BODY_START$byte; fi
    RQ_BODY_END=$RQ_BODY_END$byte
    if [ ${#RQ_BODY_END} -gt 7 ]; then RQ_BODY_END=${RQ_BODY_END:1}; fi
}

# Chunked wins over Content-Length, as in upstream's sws.c and RFC 9112 section 6.3.
rce_request_parse_head() {
    local line chunked=0 length=''
    shopt -s nocasematch
    while IFS= read -r line; do
        if [[ $line =~ $RCE_TRANSFER_ENCODING_CHUNKED ]]; then chunked=1; fi
        if [ -z "$length" ] && [[ $line =~ $RCE_CONTENT_LENGTH ]]; then length=${BASH_REMATCH[1]}; fi
    done <<EOF
$1
EOF
    shopt -u nocasematch
    if [ $chunked -eq 1 ]; then
        RQ_KIND=chunked
    elif [ -n "$length" ]; then
        RQ_KIND=length
        # Past 18 digits the length would overflow Bash's arithmetic; no body that long arrives.
        length=${length#"${length%%[!0]*}"}
        if [ ${#length} -gt 18 ]; then RQ_LENGTH=999999999999999999; else RQ_LENGTH=$((10#${length:-0})); fi
    fi
}

rce_request_complete() {
    [ $RQ_IN_BODY -eq 1 ] || return 1
    if [ "$RCE_EARLY_RESPONSE_BODY_BYTES" -ge 0 ]; then
        [ $RQ_BODY_COUNT -ge "$RCE_EARLY_RESPONSE_BODY_BYTES" ]
        return
    fi
    case $RQ_KIND in
        chunked) [ "$RQ_BODY_START" = $'0\r\n\r\n' ] || [ "$RQ_BODY_END" = $'\r\n0\r\n\r\n' ] ;;
        length) [ $RQ_BODY_COUNT -ge $RQ_LENGTH ] ;;
        *) return 0 ;;
    esac
}

# --- One connection -------------------------------------------------------------------
# The connection's plaintext arrives on fd 0 and the response goes out on fd 1: the socket
# itself, which socat hands over, or s_server's decrypted stream through two FIFOs.

# Reads one byte from curl, waiting at most RCE_SECONDS, records it to fd 6 and feeds it
# to the request reader. Returns non-zero at end of stream or when the wait passes.
rce_take_byte() {
    local byte
    IFS= read -r -d '' -n 1 -t "$RCE_SECONDS" byte || return 1
    if [ -z "$byte" ]; then
        printf '\000' >&6
        byte=$'\001'
    else
        printf '%s' "$byte" >&6
    fi
    rce_request_feed "$byte"
}

# Sends the Nth response (the last for every N past it). curl may already have closed its
# end; the request is still worth recording, so a failed write is not an error.
rce_send_response() {
    local index=$1
    if [ "$index" -ge "$RCE_RESPONSE_COUNT" ]; then index=$((RCE_RESPONSE_COUNT - 1)); fi
    cat "$RCE_STATE/response.$index.bin" 2>/dev/null
    set -- $RCE_RESPONSE_SIZES
    shift "$index"
    RCE_TLS_WRITTEN=$((RCE_TLS_WRITTEN + $1))
}

# With -ResetAfterResponse over TLS, resets curl's connection once s_server has stopped:
# the relay that holds the socket is killed outright, a moment after s_server's last
# bytes, so the kernel's close is the RST the listener's zero linger time makes. In plain
# HTTP the handler holds the socket itself, and its exit is that close.
rce_reset_relay() {
    if [ "$RCE_RESET_AFTER" = 1 ] && [ -n "${RCE_FRONT_PID:-}" ]; then
        sleep 0.05
        kill -KILL "$RCE_FRONT_PID" 2>/dev/null
    fi
}

# With -HalfCloseAfterResponse over TLS the relay (relay.pl) holds curl's socket, so it is told to
# send the FIN: once s_server's msgfile shows records enough for every byte written, as
# rce_finish_tls waits, so the FIN follows the response.
rce_half_close_tls() {
    local polls=0 bytes records
    while kill -0 "$RCE_TLS_PID" 2>/dev/null && [ $polls -lt 150 ]; do
        set -- $(rce_sent_application_records)
        bytes=$1
        records=$2
        if [ $((bytes - 16 * records)) -ge "$RCE_TLS_WRITTEN" ]; then break; fi
        polls=$((polls + 1))
        sleep 0.02
    done
    sleep 0.1
    kill -USR1 "$RCE_FRONT_PID" 2>/dev/null
}

rce_serve_connection() {    local index=$1 answered=0
    RCE_TLS_WRITTEN=0
    exec 6>"$RCE_STATE/request.$index.bin"
    if [ "$RCE_TLS" = 1 ] && ! rce_wait_for_handshake; then
        # curl refused the certificate or hung up mid-handshake: nothing was sent.
        exec 6>&-
        rce_stop_tls_server
        return
    fi
    if [ "$RCE_EARLY_RESPONSE_BODY_BYTES" -ge 0 ]; then rce_seconds 5000; else rce_seconds "$RCE_REQUEST_READ"; fi
    rce_request_reset
    while ! rce_request_complete; do
        rce_take_byte || break  # The read wait passed without a byte: take what arrived.
    done
    if [ "$RCE_DELAY" -gt 0 ]; then rce_sleep_ms "$RCE_DELAY"; fi
    rce_send_response "$index"
    if [ "$RCE_EARLY_RESPONSE_BODY_BYTES" -ge 0 ]; then
        # Answered mid-body: record whatever curl goes on sending until it stops.
        rce_seconds 2000
        while rce_take_byte; do :; done
    fi
    # FIN, then keep reading. Bash has no shutdown(2), but socat has: at the end of
    # /dev/null it shuts down the send side of the socket on fd 1, which the handler shares.
    if [ "$RCE_HALF_CLOSE" = 1 ]; then
        if [ "$RCE_TLS" = 1 ]; then rce_half_close_tls; else "$RCE_SOCAT" -u OPEN:/dev/null FD:1,shut-down; fi
    fi
    if [ "$RCE_HOLD_OPEN" -gt 0 ]; then
        # Held open: wait for curl to hang up, recording what it still sends and answering
        # up to RCE_ANSWER_HELD more requests with the responses after this one.
        rce_seconds "$RCE_HOLD_OPEN"
        rce_request_reset
        while rce_take_byte; do
            if [ $answered -lt "$RCE_ANSWER_HELD" ] && rce_request_complete; then
                answered=$((answered + 1))
                rce_request_reset
                rce_send_response $((index + answered))
            fi
        done
    fi
    exec 6>&-
    if [ "$RCE_TLS" = 1 ]; then rce_finish_tls; fi
}

# --- TLS through openssl s_server -----------------------------------------------------

# Waits for curl's first TLS record, for as long as the run lasts, then up to five seconds
# for both Finished messages in s_server's msgfile.
rce_wait_for_handshake() {
    local polls=0
    while [ ! -s "$RCE_TLS_MSG" ]; do
        kill -0 "$RCE_TLS_PID" 2>/dev/null || return 1
        if [ -e "$RCE_STATE/stop" ]; then return 1; fi
        sleep 0.02
    done
    while :; do
        if grep -q '^<<< .*Finished' "$RCE_TLS_MSG" && grep -q '^>>> .*Finished' "$RCE_TLS_MSG"; then return 0; fi
        kill -0 "$RCE_TLS_PID" 2>/dev/null || return 1
        polls=$((polls + 1))
        if [ $polls -ge 250 ]; then return 1; fi
        sleep 0.02
    done
}

# Prints "<bytes> <records>": the record lengths of the application data records s_server
# has sent, from the record headers in its msgfile.
rce_sent_application_records() {
    awk '
        function hex(text,    i, value) {
            value = 0
            text = toupper(text)
            for (i = 1; i <= length(text); i++) value = value * 16 + index("0123456789ABCDEF", substr(text, i, 1)) - 1
            return value
        }
        header { if ($1 == "17") { records++; bytes += hex($4 $5) } header = 0 }
        /^>>> .*RecordHeader/ { header = 1 }
        END { printf "%d %d\n", bytes, records }
    ' "$RCE_TLS_MSG"
}

# SIGKILL, since the handler ignores SIGTERM and s_server inherits that.
rce_stop_tls_server() {
    kill -KILL "$RCE_TLS_PID" 2>/dev/null
    wait "$RCE_TLS_PID" 2>/dev/null
}

rce_finish_tls() {
    local polls=0 bytes records
    if [ "$RCE_CLOSE_NOTIFY" = 1 ]; then
        # End of file on s_server's input: with -no_ign_eof it sends close_notify, closes
        # the connection and exits. curl may have hung up already.
        exec 1>&-
        while kill -0 "$RCE_TLS_PID" 2>/dev/null && [ $polls -lt 250 ]; do
            polls=$((polls + 1))
            sleep 0.02
        done
        rce_stop_tls_server
        return
    fi
    # No close_notify: s_server is stopped, a bare end of the transport, once its msgfile
    # shows records enough for every byte written. An AEAD record carries 16 to 24 bytes
    # beyond its plaintext, so 16 per record is the least; the short settle after covers
    # the up to 8 bytes per record that bound can leave unsent.
    while kill -0 "$RCE_TLS_PID" 2>/dev/null && [ $polls -lt 150 ]; do
        set -- $(rce_sent_application_records)
        bytes=$1
        records=$2
        if [ $((bytes - 16 * records)) -ge "$RCE_TLS_WRITTEN" ]; then break; fi
        polls=$((polls + 1))
        sleep 0.02
    done
    sleep 0.1
    rce_stop_tls_server
}

# Serves connection Index over TLS: s_server takes the connection on its listen
# arguments (-unix <path>, or -accept <address:port>), and the connection's plaintext is
# served through two FIFOs. Each side opens its FIFOs in the same order, so neither open
# waits for the other forever.
rce_serve_tls_connection() {
    local index=$1 eof_option=''
    shift
    trap '' PIPE
    RCE_TLS_MSG="$RCE_STATE/tls.$index.msg"
    mkfifo "$RCE_STATE/tls.$index.in" "$RCE_STATE/tls.$index.out" || rce_die 'mkfifo failed'
    if [ "$RCE_CLOSE_NOTIFY" = 1 ]; then eof_option='-no_ign_eof'; fi
    "$RCE_OPENSSL" s_server -quiet $eof_option -naccept 1 -tls1_2 \
        -cert "$RCE_STATE/server.crt" -key "$RCE_STATE/server.key" "$@" \
        -msg -msgfile "$RCE_TLS_MSG" >"$RCE_STATE/tls.$index.out" <"$RCE_STATE/tls.$index.in" 2>>"$RCE_STATE/tls.$index.err" &
    RCE_TLS_PID=$!
    printf '%s\n' "$RCE_TLS_PID" >"$RCE_STATE/tls-pid.$index"
    rce_serve_connection "$index" <"$RCE_STATE/tls.$index.out" >"$RCE_STATE/tls.$index.in"
    rce_reset_relay
    : >"$RCE_STATE/done.$index"
}

# --- The socat connection entry -------------------------------------------------------
# socat runs this script with --rce-connection <state directory> once per accepted
# connection (EXEC with nofork), the accepted socket itself on stdin and stdout, so the
# handler's exit is the close of the connection: a FIN, or a RST under the listener's
# zero linger time.

rce_connection_entry() {
    local index=0 polls=0 socket
    exec 2>>"$RCE_STATE/handler.err"
    # The listener signals its children when it is stopped; an accepted connection is
    # served to its end regardless, as the PowerShell script's are.
    trap '' TERM HUP
    # The connection's number, in the order accepted: the first free claim.
    while ! mkdir "$RCE_STATE/claim.$index" 2>/dev/null; do index=$((index + 1)); done
    printf '%s\n' "$$" >"$RCE_STATE/pid.$index"
    if [ "$index" -ge "$RCE_CONNECTIONS" ]; then
        # Past the connections served: accepted, never answered, until the run ends.
        while [ ! -e "$RCE_STATE/stop" ]; do sleep 0.1; done
        exit 0
    fi
    if [ -n "$RCE_MAIL" ]; then
        rce_mail_session
        : >"$RCE_STATE/done.$index"
        exit 0
    fi
    if [ "$RCE_FTP" = 1 ]; then
        rce_ftp_session
        : >"$RCE_STATE/ftp-ended"
        : >"$RCE_STATE/done.$index"
        exit 0
    fi
    if [ "$RCE_RESET" = 1 ]; then
        # Closed unread: under the listener's zero linger time the close is a RST.
        : >"$RCE_STATE/request.$index.bin"
        : >"$RCE_STATE/done.$index"
        exit 0
    fi
    if [ "$RCE_TLS" = 1 ]; then
        # This process becomes the relay between curl's connection and s_server's Unix
        # socket, and keeps its process ID through exec.
        RCE_FRONT_PID=$$
        socket="$RCE_STATE/tls.$index.sock"
        (rce_serve_tls_connection "$index" -unix "$socket") </dev/null >/dev/null &
        while [ ! -S "$socket" ] && [ $polls -lt 250 ]; do
            polls=$((polls + 1))
            sleep 0.02
        done
        if [ "$RCE_HALF_CLOSE" = 1 ]; then exec perl "$RCE_STATE/relay.pl" "$socket"; fi
        exec "$RCE_SOCAT" -lf "$RCE_STATE/relay.$index.log" -t 0 STDIO "UNIX-CONNECT:$socket"
    fi
    trap '' PIPE
    rce_serve_connection "$index"
    : >"$RCE_STATE/done.$index"
    exit 0
}

# --- The FTP session ------------------------------------------------------------------
# One control connection is served by the handler socat starts (rce_ftp_session). Data
# connections are served by fresh handlers (rce_ftp_data_entry): passive ones accepted by
# a second socat listener on a port chosen at start, active ones dialled by socat to the
# address curl announced. The control side hands each data connection a job file and
# waits for its "done" file, so the 226 follows the data as in the PowerShell script.
#
# Control TLS (AUTH, or -Tls from the first byte) is s_server on a loopback port that the
# session reaches through /dev/tcp, with its plaintext on two FIFOs. Two pumps join that
# socket to curl's: one is cat (s_server to curl), the other (rce_ftp_up_pump) copies
# curl's bytes TLS record by TLS record, so it knows where TLS ends. CCC then needs
# nothing more than closing s_server's input (close_notify) and reading what the up pump
# stopped at: the first byte that is not a TLS record header, which is plain text.

# Waits up to five seconds for a TCP listener on Port: /proc/net/tcp on Linux, netstat
# elsewhere, else one second blind.
rce_wait_for_listening_port() {
    local port=$1 hex polls=0
    printf -v hex '%04X' "$port"
    while [ $polls -lt 250 ]; do
        if [ -r /proc/net/tcp ]; then
            cat /proc/net/tcp /proc/net/tcp6 2>/dev/null | grep -Eq "^ *[0-9]+: [0-9A-F]+:$hex [0-9A-F]+:0000 0A" && return 0
        elif command -v netstat >/dev/null 2>&1; then
            netstat -an 2>/dev/null | grep -Eq "[.:]$port[[:space:]].*LISTEN" && return 0
        else
            sleep 1
            return 0
        fi
        polls=$((polls + 1))
        sleep 0.02
    done
    return 1
}

# Succeeds when VERB (capitals and digits only) has a FtpReply.
rce_ftp_has_override() {
    case $1 in '' | *[!A-Z0-9_]*) return 1 ;; esac
    eval "[ \"\${RCE_FR_COUNT_$1:-0}\" -gt 0 ]"
}

# Sets RCE_OVERRIDE to VERB's next reply: several are used in turn, the last repeating.
rce_ftp_override() {
    local count position
    eval "count=\${RCE_FR_COUNT_$1:-0}; position=\${RCE_FR_POS_$1:-0}"
    eval "RCE_OVERRIDE=\${RCE_FR_${1}_$position}"
    if [ $((position + 1)) -lt "$count" ]; then eval "RCE_FR_POS_$1=$((position + 1))"; fi
}

rce_ftp_log() {
    printf '%s\r\n' "$1" >>"$RCE_STATE/transcript.txt"
}

# Sends Reply on the control connection: {DATAPORT...} filled in, a {PAUSE} line waiting a
# second between the lines before and after it, every line logged.
rce_ftp_reply() {
    local reply=$1 sep=$'\r\n{PAUSE}\r\n' part rest first=1 more=1 hi lo
    hi='{DATAPORT_HI}' lo='{DATAPORT_LO}'
    reply=${reply//"$hi"/$((RCE_FTP_DATA_PORT / 256))}
    reply=${reply//"$lo"/$((RCE_FTP_DATA_PORT % 256))}
    hi='{DATAPORT}'
    reply=${reply//"$hi"/$RCE_FTP_DATA_PORT}
    while [ $more = 1 ]; do
        case $reply in
            *"$sep"*) part=${reply%%"$sep"*}; reply=${reply#*"$sep"} ;;
            *) part=$reply; more=0 ;;
        esac
        if [ $first = 0 ]; then
            rce_sleep_ms 1000
            rce_ftp_log '= paused 1000 ms'
        fi
        first=0
        printf '%s\r\n' "$part" >&"$RCE_FTP_WRITE" 2>/dev/null
        rest=$part
        while :; do
            case $rest in
                *$'\r\n'*) rce_ftp_log "< ${rest%%$'\r\n'*}"; rest=${rest#*$'\r\n'} ;;
                *) rce_ftp_log "< $rest"; break ;;
            esac
        done
    done
}

# Prints a loopback port nothing listens on yet.
rce_ftp_free_port() {
    local port tries=0
    while [ $tries -lt 50 ]; do
        port=$((20000 + RANDOM % 30000))
        if ! { cat /proc/net/tcp /proc/net/tcp6 2>/dev/null | grep -Eqi "^ *[0-9]+: [0-9A-F]+:$(printf '%04X' $port) "; }; then
            printf '%s' "$port"
            return 0
        fi
        tries=$((tries + 1))
    done
    return 1
}

# Copies curl's bytes (descriptor 3) to s_server's socket (descriptor 5) one TLS record at
# a time: a record header starts 0x14 to 0x17 (20 to 23), the length in its fourth and
# fifth bytes. The first byte that is not one is plain text: it is kept in ftp.pending
# and the pump ends. A close_notify (an alert, 21) leaves ftp.alert behind.
rce_ftp_up_pump() {
    local rec="$RCE_STATE/ftp.up.rec" first length
    trap '' PIPE
    while :; do
        dd bs=1 count=1 <&3 >"$rec" 2>/dev/null
        if [ ! -s "$rec" ]; then return 0; fi
        first=$(od -An -tu1 <"$rec" | tr -d ' \n')
        case $first in
            20 | 21 | 22 | 23) ;;
            *) cp "$rec" "$RCE_STATE/ftp.pending"; return 0 ;;
        esac
        if [ "$first" = 21 ]; then : >"$RCE_STATE/ftp.alert"; fi
        dd bs=1 count=4 <&3 >>"$rec" 2>/dev/null
        set -- $(od -An -tu1 -j3 -N2 <"$rec")
        length=$((${1:-0} * 256 + ${2:-0}))
        dd bs=1 count="$length" <&3 >>"$rec" 2>/dev/null
        cat "$rec" >&5 2>/dev/null
    done
}

# Starts TLS on the control connection: s_server, its two FIFOs, the two pumps.
rce_ftp_enter_tls() {
    local port cin cout polls=0
    RCE_FTP_TLS_COUNT=$((RCE_FTP_TLS_COUNT + 1))
    cin="$RCE_STATE/ftp.cin.$RCE_FTP_TLS_COUNT"
    cout="$RCE_STATE/ftp.cout.$RCE_FTP_TLS_COUNT"
    mkfifo "$cin" "$cout" || return 1
    port=$(rce_ftp_free_port) || return 1
    "$RCE_OPENSSL" s_server -quiet -no_ign_eof -naccept 1 -tls1_2 -cert "$RCE_STATE/server.crt" -key "$RCE_STATE/server.key" \
        -accept "127.0.0.1:$port" >"$cout" <"$cin" 2>>"$RCE_STATE/ftp-tls.err" 3<&- 4<&- 6>&- &
    RCE_FTP_TLS_PID=$!
    printf '%s\n' "$RCE_FTP_TLS_PID" >"$RCE_STATE/tls-pid.ftp$RCE_FTP_TLS_COUNT"
    exec 7<"$cout" 8>"$cin"
    rce_wait_for_listening_port "$port" || return 1
    exec 5<>"/dev/tcp/127.0.0.1/$port" || return 1
    cat <&5 >&4 5<&- 7<&- 8>&- 2>/dev/null &
    RCE_FTP_DOWN_PID=$!
    (rce_ftp_up_pump) 7<&- 8>&- &
    RCE_FTP_UP_PID=$!
    printf '%s\n%s\n' "$RCE_FTP_DOWN_PID" "$RCE_FTP_UP_PID" >"$RCE_STATE/tls-pid.ftpp$RCE_FTP_TLS_COUNT"
    exec 5>&-
    rm -f "$RCE_STATE/ftp.pending" "$RCE_STATE/ftp.alert"
    RCE_FTP_READ=7
    RCE_FTP_WRITE=8
    RCE_FTP_TLS_ON=1
}

# CCC accepted: close_notify to curl, then plain text. s_server's input is closed (close_notify
# and shutdown); the down pump ends once curl has it. Then the up pump is given the idle
# time to reach curl's first plain byte, which it leaves in ftp.pending (BL-636).
rce_ftp_clear_tls() {
    local polls=0 limit suffix=" without curl's close_notify"
    exec 8>&-
    while kill -0 "$RCE_FTP_DOWN_PID" 2>/dev/null && [ $polls -lt 250 ]; do
        polls=$((polls + 1))
        sleep 0.02
    done
    polls=0
    limit=$((RCE_FTP_IDLE / 20))
    while [ ! -e "$RCE_STATE/ftp.pending" ] && kill -0 "$RCE_FTP_UP_PID" 2>/dev/null && [ $polls -lt $limit ]; do
        polls=$((polls + 1))
        sleep 0.02
    done
    if kill -0 "$RCE_FTP_UP_PID" 2>/dev/null; then
        # Still waiting on curl: stop it, and the dd it is blocked in.
        if command -v pkill >/dev/null 2>&1; then pkill -KILL -P "$RCE_FTP_UP_PID" 2>/dev/null; fi
        kill -KILL "$RCE_FTP_UP_PID" 2>/dev/null
        wait "$RCE_FTP_UP_PID" 2>/dev/null
    fi
    if [ -e "$RCE_STATE/ftp.alert" ]; then suffix=''; fi
    if [ -s "$RCE_STATE/ftp.pending" ]; then
        RCE_FTP_PENDING=$(cat "$RCE_STATE/ftp.pending")
        rm -f "$RCE_STATE/ftp.pending"
    fi
    rce_ftp_log "= TLS cleared from the control connection$suffix"
    exec 7<&-
    kill -KILL "$RCE_FTP_TLS_PID" 2>/dev/null
    RCE_FTP_READ=3
    RCE_FTP_WRITE=4
    RCE_FTP_TLS_ON=0
}

# Runs one data transfer (Verb RETR, LIST, NLST, STOR or APPE) over a data connection:
# writes its job file, dials curl in active mode, waits for the connection to be taken
# and for the job to finish. Returns 1 when no data connection came within five seconds.
rce_ftp_transfer() {
    local verb=$1 index=$RCE_FTP_JOB polls=0 limit
    RCE_FTP_JOB=$((index + 1))
    printf 'RCE_JOB_VERB=%s\nRCE_JOB_TLS=%s\nRCE_JOB_REST=%s\n' "$verb" "$RCE_FTP_PROTECT" "$RCE_FTP_REST" >"$RCE_STATE/job.$index.tmp"
    mv "$RCE_STATE/job.$index.tmp" "$RCE_STATE/job.$index"
    if [ -n "$RCE_FTP_ACTIVE_HOST" ]; then
        case $RCE_FTP_ACTIVE_HOST in
            *:*) rce_target="TCP6:[$RCE_FTP_ACTIVE_HOST]:$RCE_FTP_ACTIVE_PORT" ;;
            *) rce_target="TCP4:$RCE_FTP_ACTIVE_HOST:$RCE_FTP_ACTIVE_PORT" ;;
        esac
        "$RCE_SOCAT" -lf "$RCE_STATE/ftp-active.$index.log" "$rce_target" "EXEC:$RCE_STATE/ftp-data,nofork" \
            </dev/null >/dev/null 2>&1 3<&- 4<&- 6>&- 7<&- 8>&- &
    fi
    while [ ! -d "$RCE_STATE/dclaim.$index" ]; do
        polls=$((polls + 1))
        if [ $polls -ge 250 ]; then
            printf '%s\n' "curl sent $verb but opened no data connection within five seconds." >>"$RCE_STATE/handler.err"
            return 1
        fi
        sleep 0.02
    done
    polls=0
    limit=$(((RCE_FTP_HOLD + 30000) / 20))
    while [ ! -e "$RCE_STATE/jobdone.$index" ] && [ $polls -lt $limit ]; do
        polls=$((polls + 1))
        sleep 0.02
    done
    return 0
}

# Appends what STOR or APPE data connection Index received to upload.bin and the transcript.
rce_ftp_note_upload() {
    local file="$RCE_STATE/upload.$1" size=0
    if [ -f "$file" ]; then size=$(wc -c <"$file" | tr -d ' '); cat "$file" >>"$RCE_STATE/upload.bin"; fi
    {
        printf '= %s bytes received on the data connection: ' "$size"
        if [ -f "$file" ]; then cat "$file"; fi
        printf '\r\n'
    } >>"$RCE_STATE/transcript.txt"
}

# EPRT |1|127.0.0.1|5000| or PORT 127,0,0,1,19,136: sets the address curl listens on.
rce_ftp_parse_active() {
    local verb=$1 argument=$2 delimiter fields
    if [ "$verb" = EPRT ]; then
        delimiter=${argument:0:1}
        IFS=$delimiter read -r -a fields <<<"$argument"
        RCE_FTP_ACTIVE_HOST=${fields[2]:-}
        RCE_FTP_ACTIVE_PORT=${fields[3]:-0}
    else
        IFS=, read -r -a fields <<<"$argument"
        RCE_FTP_ACTIVE_HOST="${fields[0]:-0}.${fields[1]:-0}.${fields[2]:-0}.${fields[3]:-0}"
        RCE_FTP_ACTIVE_PORT=$((${fields[4]:-0} * 256 + ${fields[5]:-0}))
    fi
}

rce_ftp_session() {
    local line command verb argument ok greeting reply done_reply idle_seconds listen_commas
    trap '' PIPE
    exec 3<&0 4>&1 0</dev/null 1>/dev/null
    exec 6>"$RCE_STATE/request.0.bin"
    RCE_FTP_READ=3 RCE_FTP_WRITE=4 RCE_FTP_TLS_ON=0 RCE_FTP_TLS_COUNT=0 RCE_FTP_PENDING='' RCE_FTP_JOB=0
    RCE_FTP_REST=0 RCE_FTP_PROTECT=$RCE_TLS RCE_FTP_ACTIVE_HOST='' RCE_FTP_ACTIVE_PORT=0
    RCE_FTP_DATA_PORT=$(cat "$RCE_STATE/dataport")
    RCE_FTP_DATA_SIZE=$(wc -c <"$RCE_STATE/ftpdata.bin" | tr -d ' ')
    listen_commas=${RCE_LISTEN_ADDRESS//./,}
    rce_seconds "$RCE_FTP_IDLE"
    idle_seconds=$RCE_SECONDS
    if [ "$RCE_TLS" = 1 ]; then rce_ftp_enter_tls || { printf '%s\n' 'could not start TLS on the control connection' >>"$RCE_STATE/handler.err"; exec 6>&-; return; }; fi
    if rce_ftp_has_override GREETING; then rce_ftp_override GREETING; greeting=$RCE_OVERRIDE; else greeting='220 Recorder ready'; fi
    if [ "$greeting" != STALL ]; then rce_ftp_reply "$greeting"; fi
    while :; do
        line=''
        ok=0
        IFS= read -r -t "$idle_seconds" -u "$RCE_FTP_READ" line && ok=1
        line=$RCE_FTP_PENDING$line
        RCE_FTP_PENDING=''
        if [ $ok = 0 ]; then
            # Idle time without a newline, or curl hung up: what arrived is still recorded.
            if [ -n "$line" ]; then printf '%s' "$line" >&6; fi
            break
        fi
        printf '%s\n' "$line" >&6
        command=${line%$'\r'}
        rce_ftp_log "> $command"
        verb=${command%% *}
        verb=$(printf '%s' "$verb" | tr '[:lower:]' '[:upper:]')
        argument=''
        case $command in *' '*) argument=${command#* } ;; esac
        if rce_ftp_has_override "$verb"; then
            rce_ftp_override "$verb"
            reply=$RCE_OVERRIDE
            if [ "$reply" = CLOSE ]; then break; fi
            if [ "$reply" = STALL ]; then continue; fi
            rce_ftp_reply "$reply"
            if [ "$verb" = QUIT ]; then break; fi
            case $verb in
                PROT) case $reply in 2*) if [ "$argument" = P ]; then RCE_FTP_PROTECT=1; else RCE_FTP_PROTECT=0; fi ;; *) RCE_FTP_PROTECT=0 ;; esac ;;
                AUTH)
                    case $reply in
                        234*) rce_ftp_enter_tls && rce_ftp_log '= TLS handshake completed on the control connection' ;;
                    esac
                    ;;
                CCC)
                    case $reply in
                        [1-4]*) if [ $RCE_FTP_TLS_ON = 1 ]; then rce_ftp_clear_tls; fi ;;
                    esac
                    ;;
            esac
            continue
        fi
        case $verb in
            AUTH)
                rce_ftp_reply '234 AUTH accepted'
                rce_ftp_enter_tls && rce_ftp_log '= TLS handshake completed on the control connection'
                ;;
            PBSZ) rce_ftp_reply '200 PBSZ=0' ;;
            CCC)
                rce_ftp_reply '200 CCC command successful'
                if [ $RCE_FTP_TLS_ON = 1 ]; then rce_ftp_clear_tls; fi
                ;;
            PROT)
                if [ "$argument" = P ]; then RCE_FTP_PROTECT=1; else RCE_FTP_PROTECT=0; fi
                rce_ftp_reply "200 Protection level set to $argument"
                ;;
            EPRT | PORT)
                rce_ftp_parse_active "$verb" "$argument"
                rce_ftp_reply "200 $verb command successful"
                ;;
            USER) rce_ftp_reply '331 Password required' ;;
            PASS) rce_ftp_reply '230 Logged in' ;;
            PWD) rce_ftp_reply '257 "/" is current directory' ;;
            CWD) rce_ftp_reply '250 OK' ;;
            TYPE) rce_ftp_reply '200 Type set' ;;
            SIZE) rce_ftp_reply "213 $RCE_FTP_DATA_SIZE" ;;
            MDTM) rce_ftp_reply '213 20260927123456' ;;
            REST)
                RCE_FTP_REST=${argument//[!0-9]/}
                if [ -z "$RCE_FTP_REST" ]; then RCE_FTP_REST=0; fi
                rce_ftp_reply "350 Restarting at $RCE_FTP_REST"
                ;;
            EPSV) rce_ftp_reply "229 Entering Extended Passive Mode (|||$RCE_FTP_DATA_PORT|)" ;;
            PASV) rce_ftp_reply "227 Entering Passive Mode ($listen_commas,$((RCE_FTP_DATA_PORT / 256)),$((RCE_FTP_DATA_PORT % 256)))" ;;
            RETR | LIST | NLST)
                if rce_ftp_has_override RETROPEN; then rce_ftp_override RETROPEN; reply=$RCE_OVERRIDE; else reply='150 Opening BINARY mode data connection'; fi
                rce_ftp_reply "$reply"
                rce_ftp_transfer "$verb" || break
                if rce_ftp_has_override RETRDONE; then rce_ftp_override RETRDONE; done_reply=$RCE_OVERRIDE; else done_reply='226 Transfer complete'; fi
                rce_ftp_reply "$done_reply"
                ;;
            STOR | APPE)
                rce_ftp_reply '150 Opening BINARY mode data connection'
                rce_ftp_transfer "$verb" || break
                rce_ftp_note_upload $((RCE_FTP_JOB - 1))
                if rce_ftp_has_override STORDONE; then rce_ftp_override STORDONE; done_reply=$RCE_OVERRIDE; else done_reply='226 Transfer complete'; fi
                rce_ftp_reply "$done_reply"
                ;;
            QUIT) rce_ftp_reply '221 Bye'; break ;;
            *) rce_ftp_reply '502 Command not implemented' ;;
        esac
    done
    exec 6>&-
    rce_ftp_stop_helpers
}

# The pumps and s_server hold copies of curl's socket: stop them so it closes with us,
# a moment after the last reply has been encrypted and sent.
rce_ftp_stop_helpers() {
    if [ "$RCE_FTP_TLS_ON" = 1 ]; then sleep 0.2; fi
    if [ -n "${RCE_FTP_UP_PID:-}" ] && command -v pkill >/dev/null 2>&1; then pkill -KILL -P "$RCE_FTP_UP_PID" 2>/dev/null; fi
    for rce_pid in "${RCE_FTP_UP_PID:-}" "${RCE_FTP_DOWN_PID:-}" "${RCE_FTP_TLS_PID:-}"; do
        if [ -n "$rce_pid" ]; then kill -KILL "$rce_pid" 2>/dev/null; fi
    done
}

# --- The SMTP, IMAP and POP3 sessions -------------------------------------------------
# One connection, served a line at a time by the handler socat starts (rce_mail_session),
# with the FTP control connection's TLS machinery (rce_ftp_enter_tls) for implicit TLS and
# STARTTLS / STLS. The replies come from the same override table as FtpReply
# (RCE_FR_<VERB>_<n>; an IMAP verb with a space, UID FETCH, is UID_FETCH there).

# Sends Reply (one or more CRLF-separated lines) to curl, every line logged.
rce_mail_send() {
    local rest=$1
    printf '%s\r\n' "$rest" >&"$RCE_FTP_WRITE" 2>/dev/null
    while :; do
        case $rest in
            *$'\r\n'*) rce_ftp_log "< ${rest%%$'\r\n'*}"; rest=${rest#*$'\r\n'} ;;
            *) rce_ftp_log "< $rest"; break ;;
        esac
    done
}

# Reads one line from curl into RCE_LINE (no line ending), records it and logs it. Returns
# 1 once curl hangs up or stays silent for the idle time; what arrived is still recorded.
rce_mail_read_line() {
    local line='' ok=0
    IFS= read -r -t "$RCE_MAIL_SECONDS" -u "$RCE_FTP_READ" line && ok=1
    if [ $ok = 0 ]; then
        if [ -n "$line" ]; then printf '%s' "$line" >&6; fi
        return 1
    fi
    printf '%s\n' "$line" >&6
    RCE_LINE=${line%$'\r'}
    rce_ftp_log "> $RCE_LINE"
}

# Sets RCE_MAIL_COMMAND to the upper-case first word of Text, RCE_MAIL_REST to the rest.
rce_mail_split_verb() {
    RCE_MAIL_COMMAND=${1%% *}
    RCE_MAIL_REST=''
    case $1 in *' '*) RCE_MAIL_REST=${1#* } ;; esac
    RCE_MAIL_COMMAND=$(printf '%s' "$RCE_MAIL_COMMAND" | tr '[:lower:]' '[:upper:]')
}

# Looks Verb up in the override table. Returns 0 with RCE_OVERRIDE set when there is one.
rce_mail_has_override() {
    local key=${1// /_}
    rce_ftp_has_override "$key" || return 1
    rce_ftp_override "$key"
}

rce_mail_start_tls() {
    rce_ftp_enter_tls && rce_ftp_log '= TLS handshake completed on the control connection'
    RCE_MAIL_SECURE=1
}

# Fills RCE_MAIL_CHALLENGES with the continuations a SASL mechanism needs before success.
# Prefix is "334 " for SMTP and "+ " for IMAP and POP3; Argument is "MECHANISM [response]".
rce_mail_challenges() {
    local prefix=$1 argument=$2 mechanism initial=0 cram digest1 digest2
    cram='PDE4OTYuNjk3MTcwOTUyQGxvY2FsaG9zdD4='
    digest1='cmVhbG09ImxvY2FsaG9zdCIsbm9uY2U9Ik9BNk1HOXRFUUdtMmhoIixxb3A9ImF1dGgiLGFsZ29yaXRobT1tZDUtc2VzcyxjaGFyc2V0PXV0Zi04'
    digest2='cnNwYXV0aD1lYTQwZjYwMzM1YzQyN2I1NTI3Yjg0ZGJhYmNkZmZmZA=='
    RCE_MAIL_CHALLENGES=()
    mechanism=${argument%% *}
    mechanism=$(printf '%s' "$mechanism" | tr '[:lower:]' '[:upper:]')
    case $argument in *' '*) initial=1 ;; esac
    case $mechanism in
        PLAIN) if [ $initial = 0 ]; then RCE_MAIL_CHALLENGES=("$prefix"); fi ;;
        LOGIN)
            if [ $initial = 1 ]; then RCE_MAIL_CHALLENGES=("${prefix}UGFzc3dvcmQ6")
            else RCE_MAIL_CHALLENGES=("${prefix}VXNlcm5hbWU6" "${prefix}UGFzc3dvcmQ6"); fi
            ;;
        CRAM-MD5) if [ "$RCE_MAIL" = smtp ]; then RCE_MAIL_CHALLENGES=("${prefix}$cram"); fi ;;
        DIGEST-MD5) if [ "$RCE_MAIL" = smtp ]; then RCE_MAIL_CHALLENGES=("${prefix}$digest1" "${prefix}$digest2"); fi ;;
    esac
}

# Sends the challenges, reading curl's answer to each. Returns 1 once curl hangs up.
rce_mail_run_challenges() {
    local challenge
    for challenge in ${RCE_MAIL_CHALLENGES[@]+"${RCE_MAIL_CHALLENGES[@]}"}; do
        rce_mail_send "$challenge"
        rce_mail_read_line || return 1
    done
    return 0
}

rce_smtp_ehlo_reply() {
    RCE_MAIL_REPLY=$'250-localhost\r\n250-AUTH PLAIN LOGIN CRAM-MD5'
    if [ $RCE_MAIL_SECURE = 0 ]; then RCE_MAIL_REPLY+=$'\r\n250-STARTTLS'; fi
    RCE_MAIL_REPLY+=$'\r\n250-SIZE 1000000\r\n250-8BITMIME\r\n250 SMTPUTF8'
}

# The message after DATA's 354, up to its lone "." line, then the reply. 1 once curl hangs up.
rce_smtp_receive_body() {
    local done_reply
    while :; do
        rce_mail_read_line || return 1
        if [ "$RCE_LINE" = . ]; then break; fi
    done
    if rce_mail_has_override DATADONE; then done_reply=$RCE_OVERRIDE; else done_reply='250 OK message accepted'; fi
    rce_mail_send "$done_reply"
}

# Answers one SMTP command. Returns 1 to end the session.
rce_smtp_command() {
    local verb argument
    rce_mail_split_verb "$1"
    verb=$RCE_MAIL_COMMAND
    argument=$RCE_MAIL_REST
    if rce_mail_has_override "$verb"; then
        if [ "$RCE_OVERRIDE" = CLOSE ]; then return 1; fi
        rce_mail_send "$RCE_OVERRIDE"
        if [ "$verb" = QUIT ]; then return 1; fi
        case $verb in
            DATA) case $RCE_OVERRIDE in 354*) rce_smtp_receive_body || return 1 ;; esac ;;
            STARTTLS) case $RCE_OVERRIDE in 220*) rce_mail_start_tls ;; esac ;;
        esac
        return 0
    fi
    case $verb in
        EHLO) rce_smtp_ehlo_reply; rce_mail_send "$RCE_MAIL_REPLY" ;;
        HELO) rce_mail_send '250 localhost' ;;
        STARTTLS) rce_mail_send '220 Ready to start TLS'; rce_mail_start_tls ;;
        AUTH)
            rce_mail_challenges '334 ' "$argument"
            rce_mail_run_challenges || return 1
            rce_mail_send '235 Authentication successful'
            ;;
        MAIL | RCPT | NOOP | RSET) rce_mail_send '250 OK' ;;
        DATA)
            rce_mail_send '354 End data with <CR><LF>.<CR><LF>'
            rce_smtp_receive_body || return 1
            ;;
        VRFY | EXPN) rce_mail_send '250 Recorder <recorder@localhost>' ;;
        HELP) rce_mail_send '214 EHLO HELO STARTTLS AUTH MAIL RCPT DATA VRFY EXPN HELP NOOP RSET QUIT' ;;
        QUIT) rce_mail_send '221 Bye'; return 1 ;;
        *) rce_mail_send '502 Command not implemented' ;;
    esac
    return 0
}

# Reads one IMAP command into RCE_LINE: a line and, while it ends in a literal {n}, the "+"
# continuation (none for {n+}), the n literal bytes and the line that follows them.
# RCE_LINE is the first line. Returns 1 once curl hangs up.
rce_imap_read_command() {
    local first line length plus literal text
    rce_mail_read_line || return 1
    first=$RCE_LINE
    line=$first
    while [[ $line =~ \{([0-9]+)(\+?)\}$ ]]; do
        length=${BASH_REMATCH[1]}
        plus=${BASH_REMATCH[2]}
        if [ -z "$plus" ]; then rce_mail_send '+ Ready for literal data'; fi
        literal=''
        if [ "$length" -gt 0 ]; then
            IFS= read -r -N "$length" -t "$RCE_MAIL_SECONDS" -u "$RCE_FTP_READ" literal
            if [ ${#literal} -lt "$length" ]; then return 1; fi
        fi
        printf '%s' "$literal" >&6
        text=$literal
        case $text in *$'\r\n') text=${text%$'\r\n'} ;; esac
        while :; do
            case $text in
                *$'\r\n'*) rce_ftp_log "> ${text%%$'\r\n'*}"; text=${text#*$'\r\n'} ;;
                *) rce_ftp_log "> $text"; break ;;
            esac
        done
        rce_mail_read_line || return 1
        line=$RCE_LINE
    done
    RCE_LINE=$first
}

# Sends Reply for the command tagged Tag: the last line gets the tag in front.
rce_imap_tagged() {
    local tag=$1 reply=$2
    case $reply in
        *$'\r\n'*) reply="${reply%$'\r\n'*}"$'\r\n'"$tag ${reply##*$'\r\n'}" ;;
        *) reply="$tag $reply" ;;
    esac
    rce_mail_send "$reply"
}

rce_imap_fetch() {
    local tag=$1 verb=$2 argument=$3 sequence item uid=''
    sequence=${argument%% *}
    sequence=${sequence%%[:,]*}
    case $sequence in '' | *[!0-9]*) sequence=1 ;; esac
    item='BODY[]'
    case $argument in
        *' '*)
            item=${argument#* }
            item=${item#"${item%%[!()]*}"}
            item=${item%"${item##*[!()]}"}
            ;;
    esac
    if [ "$verb" = 'UID FETCH' ]; then uid="UID $sequence "; fi
    rce_mail_send "* $sequence FETCH ($uid$item {$RCE_MAIL_MESSAGE_SIZE}"$'\r\n'"$RCE_MAIL_MESSAGE)"
    rce_imap_tagged "$tag" 'OK FETCH completed'
}

# Sets RCE_MAIL_REPLY to SELECT's or EXAMINE's reply, ending in the line Completion.
rce_imap_select_reply() {
    local crlf=$'\r\n'
    RCE_MAIL_REPLY="* FLAGS (\\Answered \\Flagged \\Deleted \\Seen \\Draft)$crlf* 2 EXISTS$crlf* 0 RECENT$crlf* OK [UIDVALIDITY 1] UIDs valid$crlf* OK [UIDNEXT 3] Predicted next UID$crlf$1"
}

# Answers one IMAP command. Returns 1 to end the session.
rce_imap_command() {
    local tag verb argument rest
    tag=${1%% *}
    rest=''
    case $1 in *' '*) rest=${1#* } ;; esac
    rce_mail_split_verb "$rest"
    verb=$RCE_MAIL_COMMAND
    argument=$RCE_MAIL_REST
    if [ "$verb" = UID ] && [ -n "$argument" ]; then
        rce_mail_split_verb "$argument"
        verb="UID $RCE_MAIL_COMMAND"
        argument=$RCE_MAIL_REST
    fi
    if rce_mail_has_override "$verb"; then
        if [ "$RCE_OVERRIDE" = CLOSE ]; then return 1; fi
        rce_imap_tagged "$tag" "$RCE_OVERRIDE"
        if [ "$verb" = LOGOUT ]; then return 1; fi
        if [ "$verb" = STARTTLS ]; then
            case ${RCE_OVERRIDE##*$'\r\n'} in OK*) rce_mail_start_tls ;; esac
        fi
        return 0
    fi
    case $verb in
        CAPABILITY)
            if [ $RCE_MAIL_SECURE = 1 ]; then RCE_MAIL_REPLY='* CAPABILITY IMAP4rev1 AUTH=PLAIN AUTH=LOGIN'
            else RCE_MAIL_REPLY='* CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN'; fi
            rce_imap_tagged "$tag" "$RCE_MAIL_REPLY"$'\r\nOK CAPABILITY completed'
            ;;
        STARTTLS) rce_imap_tagged "$tag" 'OK Begin TLS negotiation now'; rce_mail_start_tls ;;
        LOGIN) rce_imap_tagged "$tag" 'OK LOGIN completed' ;;
        AUTHENTICATE)
            rce_mail_challenges '+ ' "$argument"
            rce_mail_run_challenges || return 1
            rce_imap_tagged "$tag" 'OK Authenticated'
            ;;
        SELECT) rce_imap_select_reply 'OK [READ-WRITE] SELECT completed'; rce_imap_tagged "$tag" "$RCE_MAIL_REPLY" ;;
        EXAMINE) rce_imap_select_reply 'OK [READ-ONLY] EXAMINE completed'; rce_imap_tagged "$tag" "$RCE_MAIL_REPLY" ;;
        FETCH | 'UID FETCH') rce_imap_fetch "$tag" "$verb" "$argument" ;;
        LIST) rce_imap_tagged "$tag" '* LIST (\HasNoChildren) "/" INBOX'$'\r\n''* LIST (\HasNoChildren) "/" Sent'$'\r\nOK LIST completed' ;;
        SEARCH | 'UID SEARCH') rce_imap_tagged "$tag" $'* SEARCH 1 2\r\nOK SEARCH completed' ;;
        APPEND) rce_imap_tagged "$tag" 'OK APPEND completed' ;;
        NOOP) rce_imap_tagged "$tag" 'OK NOOP completed' ;;
        LOGOUT) rce_imap_tagged "$tag" $'* BYE Logging out\r\nOK LOGOUT completed'; return 1 ;;
        *) rce_imap_tagged "$tag" 'BAD Command not recognized' ;;
    esac
    return 0
}

# Sends a multi-line POP3 reply: Status, each other argument with a leading "." doubled, a lone ".".
rce_pop3_multi() {
    local out=$1 line
    shift
    for line in "$@"; do
        case $line in .*) line=.$line ;; esac
        out+=$'\r\n'$line
    done
    rce_mail_send "$out"$'\r\n.'
}

# Sets RCE_POP3_LINES to the message's lines, without the line ending of the last one.
rce_pop3_message_lines() {
    local text=$RCE_MAIL_MESSAGE
    RCE_POP3_LINES=()
    case $text in *$'\r\n') text=${text%$'\r\n'} ;; esac
    if [ -z "$text" ]; then return 0; fi
    while :; do
        case $text in
            *$'\r\n'*) RCE_POP3_LINES+=("${text%%$'\r\n'*}"); text=${text#*$'\r\n'} ;;
            *) RCE_POP3_LINES+=("$text"); break ;;
        esac
    done
}

# Sets RCE_POP3_LINES to TOP's lines: the headers, the blank line and BodyLines body lines.
rce_pop3_top_lines() {
    local body=$1 blank=-1 index=0 last line
    rce_pop3_message_lines
    for line in ${RCE_POP3_LINES[@]+"${RCE_POP3_LINES[@]}"}; do
        if [ -z "$line" ]; then blank=$index; break; fi
        index=$((index + 1))
    done
    if [ $blank -lt 0 ]; then return 0; fi
    last=$((blank + body))
    if [ $last -gt $((${#RCE_POP3_LINES[@]} - 1)) ]; then last=$((${#RCE_POP3_LINES[@]} - 1)); fi
    RCE_POP3_LINES=("${RCE_POP3_LINES[@]:0:$((last + 1))}")
}

# Answers one POP3 command. Returns 1 to end the session.
rce_pop3_command() {
    local verb argument size=$RCE_MAIL_MESSAGE_SIZE body=0
    local -a arguments
    rce_mail_split_verb "$1"
    verb=$RCE_MAIL_COMMAND
    argument=$RCE_MAIL_REST
    arguments=()
    read -r -a arguments <<<"$argument"
    if rce_mail_has_override "$verb"; then
        if [ "$RCE_OVERRIDE" = CLOSE ]; then return 1; fi
        rce_mail_send "$RCE_OVERRIDE"
        if [ "$verb" = QUIT ]; then return 1; fi
        if [ "$verb" = STLS ]; then
            case $RCE_OVERRIDE in '+OK'*) rce_mail_start_tls ;; esac
        fi
        return 0
    fi
    case $verb in
        CAPA)
            if [ $RCE_MAIL_SECURE = 1 ]; then rce_pop3_multi '+OK Capability list follows' USER 'SASL PLAIN LOGIN' TOP UIDL
            else rce_pop3_multi '+OK Capability list follows' USER 'SASL PLAIN LOGIN' STLS TOP UIDL; fi
            ;;
        STLS) rce_mail_send '+OK Begin TLS negotiation'; rce_mail_start_tls ;;
        USER) rce_mail_send '+OK User accepted' ;;
        PASS | APOP) rce_mail_send '+OK Logged in' ;;
        AUTH)
            if [ ${#arguments[@]} -eq 0 ]; then
                rce_pop3_multi '+OK SASL mechanisms follow' PLAIN LOGIN
            else
                rce_mail_challenges '+ ' "$argument"
                rce_mail_run_challenges || return 1
                rce_mail_send '+OK Authenticated'
            fi
            ;;
        STAT) rce_mail_send "+OK 2 $((2 * size))" ;;
        LIST)
            if [ ${#arguments[@]} -gt 0 ]; then rce_mail_send "+OK ${arguments[0]} $size"
            else rce_pop3_multi "+OK 2 messages ($((2 * size)) octets)" "1 $size" "2 $size"; fi
            ;;
        RETR) rce_pop3_message_lines; rce_pop3_multi "+OK $size octets" ${RCE_POP3_LINES[@]+"${RCE_POP3_LINES[@]}"} ;;
        TOP)
            if [ ${#arguments[@]} -gt 1 ]; then case ${arguments[1]} in '' | *[!0-9]*) ;; *) body=${arguments[1]} ;; esac; fi
            rce_pop3_top_lines "$body"
            rce_pop3_multi '+OK Top of message follows' ${RCE_POP3_LINES[@]+"${RCE_POP3_LINES[@]}"}
            ;;
        UIDL)
            if [ ${#arguments[@]} -gt 0 ]; then rce_mail_send "+OK ${arguments[0]} uid-${arguments[0]}"
            else rce_pop3_multi '+OK Unique-ID listing follows' 'uid-1' 'uid-2'; fi
            ;;
        DELE) rce_mail_send '+OK Message deleted' ;;
        RSET | NOOP) rce_mail_send '+OK' ;;
        QUIT) rce_mail_send '+OK Bye'; return 1 ;;
        *) rce_mail_send '-ERR Command not recognized' ;;
    esac
    return 0
}

rce_mail_session() {
    local greeting
    LC_ALL=C
    trap '' PIPE
    exec 3<&0 4>&1 0</dev/null 1>/dev/null
    exec 6>"$RCE_STATE/request.0.bin"
    RCE_FTP_READ=3 RCE_FTP_WRITE=4 RCE_FTP_TLS_ON=0 RCE_FTP_TLS_COUNT=0 RCE_FTP_PENDING='' RCE_FTP_IDLE=$RCE_MAIL_IDLE
    RCE_MAIL_SECURE=0
    rce_seconds "$RCE_MAIL_IDLE"
    RCE_MAIL_SECONDS=$RCE_SECONDS
    RCE_MAIL_MESSAGE=$(cat "$RCE_STATE/mailmessage.bin"; printf x)
    RCE_MAIL_MESSAGE=${RCE_MAIL_MESSAGE%x}
    RCE_MAIL_MESSAGE_SIZE=$(wc -c <"$RCE_STATE/mailmessage.bin" | tr -d ' ')
    if [ "$RCE_TLS" = 1 ]; then
        rce_ftp_enter_tls || { printf '%s\n' 'could not start TLS on the control connection' >>"$RCE_STATE/handler.err"; exec 6>&-; return; }
        RCE_MAIL_SECURE=1
    fi
    if rce_mail_has_override GREETING; then
        greeting=$RCE_OVERRIDE
    else
        case $RCE_MAIL in
            smtp) greeting='220 localhost ESMTP' ;;
            imap) greeting='* OK [CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN] ready' ;;
            *) greeting='+OK POP3 ready <1896.697170952@localhost>' ;;
        esac
    fi
    rce_mail_send "$greeting"
    while :; do
        if [ "$RCE_MAIL" = imap ]; then rce_imap_read_command || break; else rce_mail_read_line || break; fi
        "rce_${RCE_MAIL}_command" "$RCE_LINE" || break
    done
    exec 6>&-
    rce_ftp_stop_helpers
}

# A data connection: the accepted (or dialled) socket is on stdin and stdout. It waits for
# the job the control side writes for it, serves it, closes the socket and signals done.
rce_ftp_data_entry() {
    local index=0 polls=0 socket
    exec 2>>"$RCE_STATE/handler.err"
    trap '' TERM HUP PIPE
    while ! mkdir "$RCE_STATE/dclaim.$index" 2>/dev/null; do index=$((index + 1)); done
    printf '%s\n' "$$" >"$RCE_STATE/pid.fd$index"
    while [ ! -f "$RCE_STATE/job.$index" ]; do
        # No job is coming once the control session has ended: closing is all that is left.
        if [ -e "$RCE_STATE/stop" ] || [ -e "$RCE_STATE/ftp-ended" ]; then exit 0; fi
        sleep 0.02
    done
    . "$RCE_STATE/job.$index"
    if [ "$RCE_JOB_TLS" = 1 ]; then
        # Like HTTPS: this process becomes the relay to s_server's Unix socket while a
        # sibling serves the plaintext through FIFOs and signals done.
        socket="$RCE_STATE/ftp-data.$index.sock"
        (rce_ftp_data_tls "$index" "$socket") </dev/null >/dev/null &
        while [ ! -S "$socket" ] && [ $polls -lt 250 ]; do
            polls=$((polls + 1))
            sleep 0.02
        done
        exec "$RCE_SOCAT" -lf "$RCE_STATE/ftp-data.$index.relay.log" -t 0 STDIO "UNIX-CONNECT:$socket"
    fi
    case $RCE_JOB_VERB in
        RETR | LIST | NLST)
            tail -c +$((RCE_JOB_REST + 1)) "$RCE_STATE/ftpdata.bin" 2>/dev/null
            if [ "$RCE_FTP_HOLD" -gt 0 ]; then rce_sleep_ms "$RCE_FTP_HOLD"; fi
            ;;
        *) cat >"$RCE_STATE/upload.$index" ;;
    esac
    exec 0<&- 1>&-
    : >"$RCE_STATE/jobdone.$index"
    exit 0
}

# The plaintext side of a TLS data connection, behind s_server on Socket.
rce_ftp_data_tls() {
    local index=$1 socket=$2 in out pid drain
    in="$RCE_STATE/ftp-data.$index.in"
    out="$RCE_STATE/ftp-data.$index.out"
    mkfifo "$in" "$out" || return 1
    "$RCE_OPENSSL" s_server -quiet -no_ign_eof -naccept 1 -tls1_2 -cert "$RCE_STATE/server.crt" -key "$RCE_STATE/server.key" \
        -unix "$socket" >"$out" <"$in" 2>>"$RCE_STATE/ftp-tls.err" &
    pid=$!
    printf '%s\n' "$pid" >"$RCE_STATE/tls-pid.ftpd$index"
    exec 8<"$out" 9>"$in"
    case $RCE_JOB_VERB in
        RETR | LIST | NLST)
            cat <&8 >/dev/null 9>&- 2>/dev/null &
            drain=$!
            tail -c +$((RCE_JOB_REST + 1)) "$RCE_STATE/ftpdata.bin" >&9 2>/dev/null
            if [ "$RCE_FTP_HOLD" -gt 0 ]; then rce_sleep_ms "$RCE_FTP_HOLD"; fi
            # End of file on s_server's input: close_notify, then it closes and exits.
            exec 9>&-
            ;;
        *)
            cat <&8 >"$RCE_STATE/upload.$index" 9>&- 2>/dev/null &
            drain=$!
            ;;
    esac
    wait "$pid" 2>/dev/null
    wait "$drain" 2>/dev/null
    sleep 0.05
    : >"$RCE_STATE/jobdone.$index"
}

if [ "${1:-}" = '--rce-connection' ]; then
    RCE_STATE=$2
    . "$RCE_STATE/config.sh"
    if [ -f "$RCE_STATE/ftp-config.sh" ]; then . "$RCE_STATE/ftp-config.sh"; fi
    rce_connection_entry
fi

if [ "${1:-}" = '--rce-ftp-data' ]; then
    RCE_STATE=$2
    . "$RCE_STATE/config.sh"
    rce_ftp_data_entry
fi

# --- Parameters -----------------------------------------------------------------------

RCE_PORT=0
RESPONSES=('HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n')
RCE_RESPONSE_GIVEN=0
CURL_ARGS=()
RCE_CURL_ARGS_GIVEN=0
RCE_OUT_DIRECTORY=''
RCE_CONNECTIONS=1
RCE_CONNECTIONS_GIVEN=0
RCE_DELAY=0
RCE_RESET=0
RCE_RESET_AFTER=0
RCE_HALF_CLOSE=0
RCE_HOLD_OPEN=0
RCE_ANSWER_HELD=0
RCE_EARLY_RESPONSE_BODY_BYTES=-1
RCE_REQUEST_READ=1000
RCE_STANDARD_INPUT=''
RCE_TLS=0
RCE_CLOSE_NOTIFY=0
RCE_TLS_ROOT=''
RCE_TLS_EMPTY_CRL=''
RCE_TLS_REVOKING_CRL=''
RCE_TLS_PUBLIC_KEY=''
RCE_CURL=''
RCE_LISTEN_ADDRESS='127.0.0.1'
RCE_NO_SERVER=0
RCE_UNIX_SOCKET=''
# The unported modes' parameters, parsed so a command line written for the PowerShell
# script reaches the not-ported message rather than an unknown-parameter error.
RCE_FTP_REPLIES=()
RCE_FTP_DATA=''
RCE_FTP_IDLE=5000
RCE_SCRIPT_IDLE=5000 RCE_SCRIPT_GAP=250 RCE_SCRIPT_PID=''
RCE_FTP_HOLD=0
RCE_TFTP_DATA='hello\n' RCE_TFTP_REPLIES=() RCE_TFTP_NOOACK=0 RCE_TFTP_IDLE=15000 RCE_TFTP_PID=''
RCE_SMTP_REPLIES=() RCE_IMAP_REPLIES=() RCE_POP3_REPLIES=()
RCE_SMTP_IDLE=5000 RCE_IMAP_IDLE=5000 RCE_POP3_IDLE=5000
RCE_IMAP_MESSAGE='From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n'
RCE_POP3_MESSAGE='From: sender@example.com\r\nTo: recipient@example.com\r\nSubject: Recorded\r\n\r\nHello from the recorder.\r\n.A line that starts with a dot.\r\n'
RCE_FTP=0 RCE_SMTP=0 RCE_IMAP=0 RCE_POP3=0 RCE_TFTP=0 RCE_SCRIPT='' RCE_HTTP2=0 RCE_HTTP2_ORIGIN=''
RCE_HTTP2_ANSWER='' RCE_HTTP2_NO_SETTINGS=0 RCE_PROXY_STATUS=200 RCE_PROXY_ALPN=h2 RCE_HTTP2_RESET_CODE=-1 RCE_H2_PID='' RCE_UDP_SINK=0 RCE_DNS_PORTS='' RCE_DNS_SILENT_PORTS='' RCE_DOH_PORTS=''
RCE_DNS_ANSWER_ADDRESSES='' RCE_DNS_TRUNCATE=0 RCE_DNS_RESPONSE_CODE=0 RCE_DNS_ECH='' RCE_DNS_PID='' RCE_DOH_PID='' RCE_SINK_PID=''

rce_require_value() {
    if [ "$2" -lt 2 ]; then rce_die "-$1 needs a value."; fi
}

# Checks Value is an integer from Minimum to Maximum, as the PowerShell ValidateRange does.
rce_integer() {
    local name=$1 value=$2 minimum=$3 maximum=$4
    case $value in
        '' | - | *[!0-9-]* | ?*-*) rce_die "-$name must be an integer from $minimum to $maximum; '$value' is not." ;;
    esac
    if [ ${#value} -gt 11 ] || [ "$value" -lt "$minimum" ] || [ "$value" -gt "$maximum" ]; then
        rce_die "-$name must be an integer from $minimum to $maximum; '$value' is not."
    fi
}

while [ $# -gt 0 ]; do
    case $1 in
        -h | -\? | --help) rce_print_help; exit 0 ;;
        --) shift; CURL_ARGS=("$@"); RCE_CURL_ARGS_GIVEN=1; break ;;
    esac
    name=$(printf '%s' "${1#-}" | tr '[:upper:]' '[:lower:]')
    name=${name#-}
    case $name in
        help) rce_print_help; exit 0 ;;
        curlargs) shift; CURL_ARGS=("$@"); RCE_CURL_ARGS_GIVEN=1; break ;;
        reset) RCE_RESET=1; shift; continue ;;
        resetafterresponse) RCE_RESET_AFTER=1; shift; continue ;;
        halfcloseafterresponse) RCE_HALF_CLOSE=1; shift; continue ;;
        tls) RCE_TLS=1; shift; continue ;;
        tlsclosenotify) RCE_CLOSE_NOTIFY=1; shift; continue ;;
        noserver) RCE_NO_SERVER=1; shift; continue ;;
        ftp) RCE_FTP=1; shift; continue ;;
        smtp) RCE_SMTP=1; shift; continue ;;
        imap) RCE_IMAP=1; shift; continue ;;
        pop3) RCE_POP3=1; shift; continue ;;
        tftp) RCE_TFTP=1; shift; continue ;;
        http2) RCE_HTTP2=1; shift; continue ;;
        udpsink) RCE_UDP_SINK=1; shift; continue ;;
        tftpnooack) RCE_TFTP_NOOACK=1; shift; continue ;;
        dnstruncate) RCE_DNS_TRUNCATE=1; shift; continue ;;
        http2nosettings) RCE_HTTP2_NO_SETTINGS=1; shift; continue ;;
    esac
    rce_require_value "$name" $#
    value=$2
    case $name in
        port) rce_integer Port "$value" 0 65535; RCE_PORT=$value ;;
        response)
            if [ $RCE_RESPONSE_GIVEN -eq 0 ]; then RESPONSES=(); RCE_RESPONSE_GIVEN=1; fi
            RESPONSES[${#RESPONSES[@]}]=$value
            ;;
        outdirectory) RCE_OUT_DIRECTORY=$value ;;
        connections) rce_integer Connections "$value" 1 1000; RCE_CONNECTIONS=$value; RCE_CONNECTIONS_GIVEN=1 ;;
        responsedelaymilliseconds) rce_integer ResponseDelayMilliseconds "$value" 0 600000; RCE_DELAY=$value ;;
        holdopenmilliseconds) rce_integer HoldOpenMilliseconds "$value" 0 600000; RCE_HOLD_OPEN=$value ;;
        answerheldrequests) rce_integer AnswerHeldRequests "$value" 0 1000; RCE_ANSWER_HELD=$value ;;
        respondafterbodybytes) rce_integer RespondAfterBodyBytes "$value" -1 2147483647; RCE_EARLY_RESPONSE_BODY_BYTES=$value ;;
        requestreadmilliseconds) rce_integer RequestReadMilliseconds "$value" 1 600000; RCE_REQUEST_READ=$value ;;
        standardinput) RCE_STANDARD_INPUT=$value ;;
        tlsrootcertificatefile) RCE_TLS_ROOT=$value ;;
        tlsemptycrlfile) RCE_TLS_EMPTY_CRL=$value ;;
        tlsrevokingcrlfile) RCE_TLS_REVOKING_CRL=$value ;;
        tlspublickeyfile) RCE_TLS_PUBLIC_KEY=$value ;;
        curl) RCE_CURL=$value ;;
        listenaddress)
            case $value in
                *:* | [0-9]*.[0-9]*.[0-9]*.[0-9]*) RCE_LISTEN_ADDRESS=$value ;;
                *) rce_die "-ListenAddress '$value' is not an IP address." ;;
            esac
            ;;
        unixsocket) RCE_UNIX_SOCKET=$value ;;
        script) RCE_SCRIPT=$value ;;
        http2origin)
            case $value in
                '' | h2c-upgrade | h2c-upgrade-ignored | prior-knowledge | alpn) RCE_HTTP2_ORIGIN=$value ;;
                *) rce_die "-Http2Origin must be one of h2c-upgrade, h2c-upgrade-ignored, prior-knowledge or alpn; '$value' is not." ;;
            esac
            ;;
        http2answer)
            case $value in
                '' | push | nostatus | status:*) RCE_HTTP2_ANSWER=$value ;;
                *) rce_die "-Http2Answer must be push, nostatus or status:<value>; '$value' is not." ;;
            esac
            ;;
        proxystatus) rce_integer ProxyStatus "$value" 100 999; RCE_PROXY_STATUS=$value ;;
        proxyalpn)
            case $value in
                h2 | http/1.1) RCE_PROXY_ALPN=$value ;;
                *) rce_die "-ProxyAlpn must be h2 or http/1.1; '$value' is not." ;;
            esac
            ;;
        http2resetcode) rce_integer Http2ResetCode "$value" -1 2147483647; RCE_HTTP2_RESET_CODE=$value ;;
        dnsport) RCE_DNS_PORTS="$RCE_DNS_PORTS $value" ;;
        dnssilentport) RCE_DNS_SILENT_PORTS="$RCE_DNS_SILENT_PORTS $value" ;;
        dohport) RCE_DOH_PORTS="$RCE_DOH_PORTS $value" ;;
        dnsansweraddress) RCE_DNS_ANSWER_ADDRESSES="$RCE_DNS_ANSWER_ADDRESSES ${value//,/ }" ;;
        dnsresponsecode) rce_integer DnsResponseCode "$value" 0 15; RCE_DNS_RESPONSE_CODE=$value ;;
        dnsechconfiglist) RCE_DNS_ECH=$value ;;
        ftpreply) RCE_FTP_REPLIES[${#RCE_FTP_REPLIES[@]}]=$value ;;
        ftpdata) RCE_FTP_DATA=$value ;;
        ftpidlemilliseconds) rce_integer FtpIdleMilliseconds "$value" 1 600000; RCE_FTP_IDLE=$value ;;
        ftpdataholdmilliseconds) rce_integer FtpDataHoldMilliseconds "$value" 0 600000; RCE_FTP_HOLD=$value ;;
        smtpreply) RCE_SMTP_REPLIES[${#RCE_SMTP_REPLIES[@]}]=$value ;;
        imapreply) RCE_IMAP_REPLIES[${#RCE_IMAP_REPLIES[@]}]=$value ;;
        pop3reply) RCE_POP3_REPLIES[${#RCE_POP3_REPLIES[@]}]=$value ;;
        imapmessage) RCE_IMAP_MESSAGE=$value ;;
        pop3message) RCE_POP3_MESSAGE=$value ;;
        smtpidlemilliseconds) rce_integer SmtpIdleMilliseconds "$value" 1 600000; RCE_SMTP_IDLE=$value ;;
        imapidlemilliseconds) rce_integer ImapIdleMilliseconds "$value" 1 600000; RCE_IMAP_IDLE=$value ;;
        pop3idlemilliseconds) rce_integer Pop3IdleMilliseconds "$value" 1 600000; RCE_POP3_IDLE=$value ;;
        tftpdata) RCE_TFTP_DATA=$value ;;
        tftpreply) RCE_TFTP_REPLIES[${#RCE_TFTP_REPLIES[@]}]=$value ;;
        tftpidlemilliseconds) rce_integer TftpIdleMilliseconds "$value" 1 600000; RCE_TFTP_IDLE=$value ;;
        scriptidlemilliseconds) rce_integer ScriptIdleMilliseconds "$value" 1 600000; RCE_SCRIPT_IDLE=$value ;;
        scriptgapmilliseconds) rce_integer ScriptGapMilliseconds "$value" 1 600000; RCE_SCRIPT_GAP=$value ;;
        *) rce_die "unknown parameter '$1'; -help lists them." ;;
    esac
    shift 2
done

if [ $RCE_CURL_ARGS_GIVEN -eq 0 ] || [ ${#CURL_ARGS[@]} -eq 0 ]; then rce_die '-CurlArgs is required: give it last, followed by the arguments for curl.'; fi
if [ -z "$RCE_OUT_DIRECTORY" ]; then rce_die '-OutDirectory is required.'; fi

# The PowerShell script's own refusals, in its order.
rce_sessions=$((RCE_FTP + RCE_SMTP + RCE_IMAP + RCE_POP3))
if [ -n "$RCE_SCRIPT" ]; then rce_sessions=$((rce_sessions + 1)); fi
if [ $RCE_NO_SERVER = 1 ] && { [ $rce_sessions -gt 0 ] || [ $RCE_TLS = 1 ]; }; then rce_die '-NoServer runs no server, so it cannot be combined with -Ftp, -Smtp, -Imap, -Pop3, -Script or -Tls.'; fi
if [ $rce_sessions -gt 1 ]; then rce_die '-Ftp, -Smtp, -Imap, -Pop3 and -Script each serve a whole session; give one of them.'; fi
if [ -n "$RCE_UNIX_SOCKET" ] && { [ $RCE_NO_SERVER = 1 ] || [ $rce_sessions -gt 0 ] || [ $RCE_TLS = 1 ] || [ $RCE_UDP_SINK = 1 ]; }; then rce_die '-UnixSocket serves HTTP only, so it cannot be combined with -NoServer, -Ftp, -Smtp, -Imap, -Pop3, -Script, -Tls or -UdpSink.'; fi
if [ $RCE_NO_SERVER = 0 ] && [ -z "$RCE_UNIX_SOCKET" ] && [ "$RCE_PORT" -eq 0 ]; then rce_die '-Port is required unless -NoServer is given: the URL in CurlArgs must name the port the server listens on.'; fi
if [ $RCE_UDP_SINK = 1 ] && [ "$RCE_PORT" -eq 0 ]; then rce_die '-UdpSink binds UDP on -Port, so -Port is required with it.'; fi
if [ $RCE_TFTP = 1 ] && { [ $rce_sessions -gt 0 ] || [ $RCE_TLS = 1 ] || [ $RCE_NO_SERVER = 1 ] || [ -n "$RCE_UNIX_SOCKET" ] || [ $RCE_UDP_SINK = 1 ]; }; then rce_die '-Tftp serves one TFTP transfer on UDP -Port, so it cannot be combined with -Ftp, -Smtp, -Imap, -Pop3, -Script, -Tls, -NoServer, -UnixSocket or -UdpSink.'; fi
for rce_port in $RCE_DNS_SILENT_PORTS; do
    case " $RCE_DNS_PORTS " in *" $rce_port "*) ;; *) rce_die '-DnsSilentPort names a port -DnsPort does not; give each silent port in -DnsPort too.' ;; esac
done
if [ -n "$RCE_HTTP2_ORIGIN" ]; then RCE_HTTP2=1; fi
if [ -n "$RCE_HTTP2_ANSWER" ] && [ -z "$RCE_HTTP2_ORIGIN" ]; then rce_die '-Http2Answer answers an HTTP/2 origin, so it needs -Http2Origin.'; fi
if [ "$RCE_HTTP2_ORIGIN" = alpn ] && [ $RCE_TLS = 0 ]; then rce_die '-Http2Origin alpn is an HTTPS origin that picks h2 or http/1.1 by ALPN, so it needs -Tls.'; fi
if [ -n "$RCE_HTTP2_ORIGIN" ] && [ "$RCE_HTTP2_ORIGIN" != alpn ] && [ $RCE_TLS = 1 ]; then rce_die '-Http2Origin h2c-upgrade, h2c-upgrade-ignored and prior-knowledge are plain TCP, so they cannot be combined with -Tls.'; fi
if [ $RCE_HTTP2 = 1 ] && [ $RCE_TLS = 0 ] && [ -z "$RCE_HTTP2_ORIGIN" ]; then rce_die '-Http2 is an HTTPS proxy that picks h2 by ALPN, so it needs -Tls.'; fi
if [ $RCE_HTTP2 = 1 ] && { [ $rce_sessions -gt 0 ] || [ $RCE_NO_SERVER = 1 ] || [ -n "$RCE_UNIX_SOCKET" ] || [ $RCE_TFTP = 1 ]; }; then rce_die '-Http2 serves one HTTPS proxy connection, so it cannot be combined with -Ftp, -Smtp, -Imap, -Pop3, -Script, -NoServer, -UnixSocket or -Tftp.'; fi

# The modes this port does not serve yet.
# -Tftp starts no socat listener and no HTTP server: its own responder serves the UDP port.
if [ $RCE_TFTP = 1 ]; then RCE_NO_SERVER=1; fi
# -Script runs its own perl server (script.pl), like -Http2; it needs a script file that parses.
if [ -n "$RCE_SCRIPT" ]; then RCE_NO_SERVER=1; fi
# -Http2 and -Http2Origin run their own perl server, not socat or the HTTP handlers.
if [ $RCE_HTTP2 = 1 ]; then RCE_NO_SERVER=1; fi

# One connection per Response entry unless -Connections says otherwise, so a --retry
# attempt after a 503 is answered with the next entry.
if [ $RCE_CONNECTIONS_GIVEN = 0 ] && [ "$RCE_ANSWER_HELD" -eq 0 ]; then RCE_CONNECTIONS=${#RESPONSES[@]}; fi
RCE_RESPONSE_COUNT=${#RESPONSES[@]}
# An FTP session is one control connection.
RCE_MAIL=''
RCE_MAIL_IDLE=5000
RCE_MAIL_MESSAGE_TEXT=''
if [ $RCE_SMTP = 1 ]; then RCE_MAIL=smtp; RCE_MAIL_IDLE=$RCE_SMTP_IDLE; fi
if [ $RCE_IMAP = 1 ]; then RCE_MAIL=imap; RCE_MAIL_IDLE=$RCE_IMAP_IDLE; RCE_MAIL_MESSAGE_TEXT=$RCE_IMAP_MESSAGE; fi
if [ $RCE_POP3 = 1 ]; then RCE_MAIL=pop3; RCE_MAIL_IDLE=$RCE_POP3_IDLE; RCE_MAIL_MESSAGE_TEXT=$RCE_POP3_MESSAGE; fi
# An FTP, SMTP, IMAP or POP3 session is one control connection, served a line at a time.
RCE_LINE_SESSION=$((RCE_FTP + RCE_SMTP + RCE_IMAP + RCE_POP3))
if [ $RCE_LINE_SESSION = 1 ]; then RCE_CONNECTIONS=1; fi

# --- Tools ----------------------------------------------------------------------------

if [ -z "$RCE_CURL" ]; then RCE_CURL=curl; fi
RCE_OPENSSL=${OPENSSL:-openssl}
RCE_SOCAT=${SOCAT:-socat}
# socat is the listener, except for one TLS connection with no reset, which s_server
# takes on the port itself.
RCE_USE_SOCAT=0
if [ $RCE_NO_SERVER = 0 ]; then
    if [ $RCE_LINE_SESSION = 1 ] || [ $RCE_TLS = 0 ] || [ "$RCE_CONNECTIONS" -gt 1 ] || [ $RCE_RESET = 1 ] || [ $RCE_RESET_AFTER = 1 ] || [ $RCE_HALF_CLOSE = 1 ]; then RCE_USE_SOCAT=1; fi
fi
rce_missing=''
for rce_tool in awk od mktemp sleep wc tr cat; do
    command -v "$rce_tool" >/dev/null 2>&1 || rce_missing="$rce_missing, $rce_tool"
done
if [ $RCE_USE_SOCAT = 1 ] && ! command -v "$RCE_SOCAT" >/dev/null 2>&1; then
    rce_missing="$rce_missing, socat (the listener for plain HTTP, -UnixSocket, -Reset, -ResetAfterResponse and several -Tls connections; set SOCAT to name it)"
fi
if [ $RCE_TLS = 1 ] || [ $RCE_LINE_SESSION = 1 ]; then
    command -v mkfifo >/dev/null 2>&1 || rce_missing="$rce_missing, mkfifo"
    if ! command -v "$RCE_OPENSSL" >/dev/null 2>&1; then
        rce_missing="$rce_missing, openssl (OpenSSL 1.1.1 or later, for -Tls; set OPENSSL to name it)"
    elif ! "$RCE_OPENSSL" s_server -help 2>&1 | grep -q -- '-msgfile' || ! "$RCE_OPENSSL" s_server -help 2>&1 | grep -q -- '-naccept'; then
        rce_missing="$rce_missing, an openssl whose s_server has -msgfile and -naccept ($RCE_OPENSSL has not: LibreSSL? set OPENSSL to an OpenSSL 1.1.1 or later)"
    fi
fi
if [ -n "$RCE_DOH_PORTS" ]; then
    command -v "$RCE_OPENSSL" >/dev/null 2>&1 || rce_missing="$rce_missing, openssl (the throwaway certificate of -DohPort; set OPENSSL to name it)"
    command -v "$RCE_SOCAT" >/dev/null 2>&1 || rce_missing="$rce_missing, socat (terminates TLS for -DohPort; set SOCAT to name it)"
fi
if { [ $RCE_UDP_SINK = 1 ] || [ -n "$RCE_DNS_PORTS" ] || [ -n "$RCE_DOH_PORTS" ]; } && ! command -v perl >/dev/null 2>&1; then
    rce_missing="$rce_missing, perl (serves -UdpSink, -DnsPort and -DohPort: bash cannot receive a UDP datagram)"
fi
if [ $RCE_HTTP2 = 1 ] && ! command -v perl >/dev/null 2>&1; then
    rce_missing="$rce_missing, perl (serves -Http2 and -Http2Origin: HTTP/2 framing and HPACK)"
fi
if { [ -n "$RCE_SCRIPT" ] || { [ $RCE_HALF_CLOSE = 1 ] && [ $RCE_TLS = 1 ]; }; } && ! command -v perl >/dev/null 2>&1; then
    rce_missing="$rce_missing, perl (serves -Script, and relays -HalfCloseAfterResponse with -Tls)"
fi
if [ $RCE_TFTP = 1 ] && ! command -v perl >/dev/null 2>&1; then
    rce_missing="$rce_missing, perl (serves -Tftp: a TFTP answer comes from a new UDP port, which bash cannot open)"
fi
if ! command -v "$RCE_CURL" >/dev/null 2>&1; then
    rce_missing="$rce_missing, curl ('$RCE_CURL' was not found; pass -Curl)"
fi
if [ -n "$rce_missing" ]; then rce_die "missing required tools: ${rce_missing#, }"; fi
# Git for Windows' openssl is a native Windows program: it reads an MSYS FIFO as garbage
# (SSL_write: bad length), so it cannot stand behind this script's FIFOs.
case $(uname -s 2>/dev/null) in
    MINGW* | MSYS*)
        case $(command -v "$RCE_OPENSSL") in
            /mingw* | /ucrt64* | /clang* | /[a-zA-Z]/*)
                if [ $RCE_TLS = 1 ] || [ $RCE_LINE_SESSION = 1 ]; then rce_die "-Tls, -Ftp, -Smtp, -Imap and -Pop3 need a Unix openssl, and $(command -v "$RCE_OPENSSL") is a native Windows program, which cannot read this script's FIFOs; use Record-CurlExchange.ps1 on Windows."; fi
                ;;
        esac
        ;;
esac

# --- The run --------------------------------------------------------------------------

# The state directory's path goes into socat's address, which a space, colon or comma
# would split, so it falls back to /tmp when TMPDIR holds one.
rce_temp=${TMPDIR:-/tmp}
case $rce_temp in *[' :,']*) rce_temp=/tmp ;; esac
RCE_STATE=$(mktemp -d "${rce_temp%/}/rce.XXXXXX") || rce_die 'mktemp failed'
RCE_LISTENER_PID=''
RCE_DATA_LISTENER_PID=''

rce_cleanup() {
    : >"$RCE_STATE/stop" 2>/dev/null
    if [ -n "$RCE_LISTENER_PID" ]; then kill "$RCE_LISTENER_PID" 2>/dev/null; fi
    if [ -n "${RCE_DATA_LISTENER_PID:-}" ]; then kill "$RCE_DATA_LISTENER_PID" 2>/dev/null; fi
    if [ -n "$RCE_TFTP_PID" ]; then kill "$RCE_TFTP_PID" 2>/dev/null; fi
    if [ -n "$RCE_H2_PID" ]; then kill "$RCE_H2_PID" 2>/dev/null; fi
    if [ -n "$RCE_DNS_PID" ]; then kill "$RCE_DNS_PID" 2>/dev/null; fi
    if [ -n "$RCE_DOH_PID" ]; then kill $RCE_DOH_PID 2>/dev/null; fi
    if [ -n "$RCE_SINK_PID" ]; then kill "$RCE_SINK_PID" 2>/dev/null; fi
    # Whatever is still serving a connection: handlers, held connections, s_servers.
    for rce_pid_file in "$RCE_STATE"/pid.* "$RCE_STATE"/tls-pid.*; do
        [ -f "$rce_pid_file" ] && kill -KILL "$(cat "$rce_pid_file")" 2>/dev/null
    done
    if [ -n "$RCE_UNIX_SOCKET" ] && [ -S "$RCE_UNIX_SOCKET" ]; then rm -f "$RCE_UNIX_SOCKET"; fi
    rm -rf "$RCE_STATE"
}
trap rce_cleanup EXIT
trap 'exit 130' INT TERM

case $RCE_OUT_DIRECTORY in /*) ;; *) RCE_OUT_DIRECTORY="$(pwd)/$RCE_OUT_DIRECTORY" ;; esac
mkdir -p "$RCE_OUT_DIRECTORY" || rce_die "cannot create $RCE_OUT_DIRECTORY"
RCE_OUT_DIRECTORY=$(cd "$RCE_OUT_DIRECTORY" && pwd)

rce_index=0
for rce_response in "${RESPONSES[@]}"; do
    rce_decode_escaped "$rce_response" "$RCE_STATE/response.$rce_index.bin"
    rce_index=$((rce_index + 1))
done
rce_decode_escaped "$RCE_STANDARD_INPUT" "$RCE_STATE/stdin.bin"

# Writes DerFile as PEM to Path, base64 in 76-character lines as .NET's InsertLineBreaks
# writes it, so the files match the PowerShell script's byte for byte in layout.
rce_write_pem() {
    local label=$1 der=$2 path=$3
    {
        printf -- '-----BEGIN %s-----\n' "$label"
        "$RCE_OPENSSL" base64 -A -in "$der" | tr -d '\n' | fold -w 76
        printf -- '\n-----END %s-----\n' "$label"
    } >"$path" || rce_die "cannot write $path"
}

# Formats Epoch seconds as a UTCTime, YYMMDDHHMMSSZ: GNU date -d, else BSD date -r.
rce_utc_time() {
    date -u -d "@$1" +%y%m%d%H%M%SZ 2>/dev/null || date -u -r "$1" +%y%m%d%H%M%SZ
}

# Writes a version 1 CRL issued by the throwaway root, revoking RevokedSerial (hex) when
# given and nothing otherwise: openssl ca with no crlnumber and no CRL extensions.
rce_write_crl() {
    local path=$1 revoked=${2:-} ca="$RCE_STATE/ca"
    rm -rf "$ca"
    mkdir "$ca"
    : >"$ca/index.txt"
    if [ -n "$revoked" ]; then
        printf 'R\t491231235959Z\t%s\t%s\tunknown\t/CN=127.0.0.1\n' "$(rce_utc_time $(($(date +%s) - 300)))" "$revoked" >"$ca/index.txt"
    fi
    printf '[ca]\ndefault_ca = rce\n[rce]\ndatabase = %s\ndefault_md = sha256\ndefault_crl_days = 1\n' "$ca/index.txt" >"$ca/ca.cnf"
    "$RCE_OPENSSL" ca -gencrl -config "$ca/ca.cnf" -cert "$RCE_STATE/root.crt" -keyfile "$RCE_STATE/root.key" \
        -crldays 1 -out "$ca/crl.pem" 2>"$ca/err.txt" || rce_die "openssl ca -gencrl failed: $(cat "$ca/err.txt")"
    "$RCE_OPENSSL" crl -in "$ca/crl.pem" -outform DER -out "$ca/crl.der" || rce_die 'openssl crl failed'
    rce_write_pem 'X509 CRL' "$ca/crl.der" "$path"
}

# The throwaway certificate: self-signed, or issued by a throwaway root whose PEM goes to
# TlsRootCertificateFile. A minimal configuration file, so no system openssl.cnf adds
# extensions .NET's CertificateRequest would not.
rce_make_tls_certificate() {
    local config="$RCE_STATE/openssl.cnf" serial first
    printf '%s\n' '[req]' 'distinguished_name = dn' 'prompt = no' '[dn]' 'CN = 127.0.0.1' \
        '[leaf]' 'subjectAltName = IP:127.0.0.1' \
        '[root]' 'basicConstraints = critical,CA:true' 'keyUsage = critical,keyCertSign,cRLSign' >"$config"
    if [ -z "$RCE_TLS_ROOT" ]; then
        "$RCE_OPENSSL" req -x509 -new -newkey rsa:2048 -nodes -sha256 -days 1 -config "$config" -extensions leaf \
            -keyout "$RCE_STATE/server.key" -out "$RCE_STATE/server.crt" 2>"$RCE_STATE/openssl.err" ||
            rce_die "openssl req failed: $(cat "$RCE_STATE/openssl.err")"
        return
    fi
    "$RCE_OPENSSL" req -x509 -new -newkey rsa:2048 -nodes -sha256 -days 2 -config "$config" -extensions root \
        -subj '/CN=Record-CurlExchange throwaway root' -keyout "$RCE_STATE/root.key" -out "$RCE_STATE/root.crt" 2>"$RCE_STATE/openssl.err" ||
        rce_die "openssl req (root) failed: $(cat "$RCE_STATE/openssl.err")"
    "$RCE_OPENSSL" req -new -newkey rsa:2048 -nodes -config "$config" \
        -keyout "$RCE_STATE/server.key" -out "$RCE_STATE/server.csr" 2>"$RCE_STATE/openssl.err" ||
        rce_die "openssl req (leaf) failed: $(cat "$RCE_STATE/openssl.err")"
    # A random positive 16-byte serial, as the PowerShell script draws it.
    serial=$(od -An -tx1 -N16 /dev/urandom | tr -d ' \n' | tr '[:lower:]' '[:upper:]')
    first=$((16#${serial:0:2} & 127))
    if [ $first -eq 0 ]; then first=1; fi
    printf -v serial '%02X%s' "$first" "${serial:2}"
    "$RCE_OPENSSL" x509 -req -in "$RCE_STATE/server.csr" -CA "$RCE_STATE/root.crt" -CAkey "$RCE_STATE/root.key" \
        -set_serial "0x$serial" -days 1 -sha256 -extfile "$config" -extensions leaf -out "$RCE_STATE/server.crt" 2>"$RCE_STATE/openssl.err" ||
        rce_die "openssl x509 failed: $(cat "$RCE_STATE/openssl.err")"
    "$RCE_OPENSSL" x509 -in "$RCE_STATE/root.crt" -outform DER -out "$RCE_STATE/root.der" || rce_die 'openssl x509 failed'
    rce_write_pem CERTIFICATE "$RCE_STATE/root.der" "$RCE_TLS_ROOT"
    if [ -n "$RCE_TLS_EMPTY_CRL" ]; then rce_write_crl "$RCE_TLS_EMPTY_CRL"; fi
    if [ -n "$RCE_TLS_REVOKING_CRL" ]; then rce_write_crl "$RCE_TLS_REVOKING_CRL" "$serial"; fi
}

if [ $RCE_TLS = 1 ] || [ $RCE_LINE_SESSION = 1 ] || [ -n "$RCE_DOH_PORTS" ]; then
    rce_make_tls_certificate
    "$RCE_OPENSSL" x509 -in "$RCE_STATE/server.crt" -noout -pubkey >"$RCE_STATE/public.pem" || rce_die 'openssl x509 -pubkey failed'
    "$RCE_OPENSSL" pkey -pubin -in "$RCE_STATE/public.pem" -outform DER -out "$RCE_STATE/public.der" || rce_die 'openssl pkey failed'
    rce_public_key_sha256=$("$RCE_OPENSSL" dgst -sha256 -binary "$RCE_STATE/public.der" | "$RCE_OPENSSL" base64 -A | tr -d '\n')
    rce_placeholder='{TlsPublicKeySha256}'
    rce_index=0
    while [ $rce_index -lt ${#CURL_ARGS[@]} ]; do
        CURL_ARGS[$rce_index]=${CURL_ARGS[$rce_index]//"$rce_placeholder"/$rce_public_key_sha256}
        rce_index=$((rce_index + 1))
    done
    if [ -n "$RCE_TLS_PUBLIC_KEY" ]; then
        rce_write_pem 'PUBLIC KEY' "$RCE_STATE/public.der" "$RCE_TLS_PUBLIC_KEY"
        cat "$RCE_STATE/public.der" >"$RCE_TLS_PUBLIC_KEY.der" || rce_die "cannot write $RCE_TLS_PUBLIC_KEY.der"
    fi
fi

RCE_RESPONSE_SIZES=''
rce_index=0
while [ $rce_index -lt "$RCE_RESPONSE_COUNT" ]; do
    RCE_RESPONSE_SIZES="$RCE_RESPONSE_SIZES $(wc -c <"$RCE_STATE/response.$rce_index.bin" | tr -d ' ')"
    rce_index=$((rce_index + 1))
done

# What each connection handler reads: socat starts them as fresh processes.
for rce_variable in RCE_STATE RCE_CONNECTIONS RCE_RESPONSE_COUNT RCE_RESPONSE_SIZES RCE_DELAY RCE_RESET RCE_RESET_AFTER \
    RCE_HALF_CLOSE RCE_HOLD_OPEN RCE_ANSWER_HELD RCE_EARLY_RESPONSE_BODY_BYTES RCE_REQUEST_READ RCE_TLS RCE_CLOSE_NOTIFY \
    RCE_OPENSSL RCE_SOCAT RCE_FTP RCE_MAIL RCE_MAIL_IDLE RCE_FTP_IDLE RCE_FTP_HOLD RCE_LISTEN_ADDRESS; do
    eval "printf '%s=%q\n' \"\$rce_variable\" \"\$$rce_variable\""
done >"$RCE_STATE/config.sh"
printf '#!%s\nexec %q %q --rce-connection %q\n' "$BASH" "$BASH" "$(cd "$(dirname "$0")" && pwd)/$(basename "$0")" "$RCE_STATE" >"$RCE_STATE/connection"
chmod +x "$RCE_STATE/connection"

# -Ftp, -Smtp, -Imap and -Pop3: the reply table and, for IMAP and POP3, the message. -Ftp: FtpData, the FtpReply table (as variables the control handler reads: RCE_FR_COUNT_<VERB>
# and RCE_FR_<VERB>_<n>) and the passive data listener, whose port the handler reads.
if [ $RCE_LINE_SESSION = 1 ]; then
    : >"$RCE_STATE/upload.bin"
    : >"$RCE_STATE/transcript.txt"
    : >"$RCE_STATE/ftp-config.sh"
    rce_reply_parameter=FtpReply
    rce_entries=(${RCE_FTP_REPLIES[@]+"${RCE_FTP_REPLIES[@]}"})
    case $RCE_MAIL in
        smtp) rce_reply_parameter=SmtpReply; rce_entries=(${RCE_SMTP_REPLIES[@]+"${RCE_SMTP_REPLIES[@]}"}) ;;
        imap) rce_reply_parameter=ImapReply; rce_entries=(${RCE_IMAP_REPLIES[@]+"${RCE_IMAP_REPLIES[@]}"}) ;;
        pop3) rce_reply_parameter=Pop3Reply; rce_entries=(${RCE_POP3_REPLIES[@]+"${RCE_POP3_REPLIES[@]}"}) ;;
    esac
    if [ -n "$RCE_MAIL" ]; then rce_decode_escaped "$RCE_MAIL_MESSAGE_TEXT" "$RCE_STATE/mailmessage.bin"; fi
    for rce_entry in ${rce_entries[@]+"${rce_entries[@]}"}; do
        case $rce_entry in *=*) ;; *) rce_die "$rce_reply_parameter '$rce_entry' is not VERB=reply." ;; esac
        rce_verb=$(printf '%s' "${rce_entry%%=*}" | tr '[:lower:]' '[:upper:]')
        rce_verb=${rce_verb// /_}
        case $rce_verb in '' | *[!A-Z0-9_]*) rce_die "$rce_reply_parameter '$rce_entry': VERB is letters and digits." ;; esac
        rce_decode_escaped "${rce_entry#*=}" "$RCE_STATE/ftp-reply.bin"
        rce_reply=$(cat "$RCE_STATE/ftp-reply.bin"; printf x)
        eval "rce_position=\${RCE_FR_COUNT_$rce_verb:-0}"
        eval "RCE_FR_COUNT_$rce_verb=$((rce_position + 1))"
        printf 'RCE_FR_COUNT_%s=%d\nRCE_FR_%s_%d=%q\n' "$rce_verb" $((rce_position + 1)) "$rce_verb" "$rce_position" "${rce_reply%x}" >>"$RCE_STATE/ftp-config.sh"
    done
fi
if [ $RCE_FTP = 1 ]; then
    rce_decode_escaped "$RCE_FTP_DATA" "$RCE_STATE/ftpdata.bin"
    printf '#!%s\nexec %q %q --rce-ftp-data %q\n' "$BASH" "$BASH" "$(cd "$(dirname "$0")" && pwd)/$(basename "$0")" "$RCE_STATE" >"$RCE_STATE/ftp-data"
    chmod +x "$RCE_STATE/ftp-data"
    case $RCE_LISTEN_ADDRESS in
        *:*) rce_data_listen="TCP6-LISTEN:0,bind=[$RCE_LISTEN_ADDRESS],reuseaddr,fork" ;;
        *) rce_data_listen="TCP4-LISTEN:0,bind=$RCE_LISTEN_ADDRESS,reuseaddr,fork" ;;
    esac
    "$RCE_SOCAT" -d -d -lf "$RCE_STATE/data-socat.log" "$rce_data_listen" "EXEC:$RCE_STATE/ftp-data,nofork" </dev/null >/dev/null 2>&1 &
    RCE_DATA_LISTENER_PID=$!
    rce_polls=0
    while ! grep -q 'listening on' "$RCE_STATE/data-socat.log" 2>/dev/null; do
        if ! kill -0 "$RCE_DATA_LISTENER_PID" 2>/dev/null || [ $rce_polls -ge 250 ]; then
            rce_die "socat did not start the FTP data listener: $(grep -E ' [EF] ' "$RCE_STATE/data-socat.log" 2>/dev/null | head -n 3)"
        fi
        rce_polls=$((rce_polls + 1))
        sleep 0.02
    done
    rce_data_port=$(grep 'listening on' "$RCE_STATE/data-socat.log" | head -n 1)
    printf '%s' "${rce_data_port##*:}" >"$RCE_STATE/dataport"
fi

# --- The TFTP responder ---------------------------------------------------------------
# One transfer on UDP: the request arrives on the listening port and every answer comes from
# a new port, as RFC 1350 requires. Bash cannot hold two UDP sockets, so perl (core modules
# only) serves it, as RecorderTftpResponder does in the PowerShell script. It writes the
# datagram log and the uploaded bytes to files as it goes.
if [ $RCE_TFTP = 1 ]; then
    rce_decode_escaped "$RCE_TFTP_DATA" "$RCE_STATE/tftp.data"
    : >"$RCE_STATE/tftp.steps"
    rce_index=0
    for rce_entry in ${RCE_TFTP_REPLIES[@]+"${RCE_TFTP_REPLIES[@]}"}; do
        case $rce_entry in
            ?*=*) ;;
            *) rce_die "TftpReply '$rce_entry' is not VERB=reply." ;;
        esac
        rce_step=$(printf '%s' "${rce_entry%%=*}" | tr '[:lower:]' '[:upper:]')
        rce_decode_escaped "${rce_entry#*=}" "$RCE_STATE/tftp.reply.$rce_index"
        rce_reply=$(cat "$RCE_STATE/tftp.reply.$rce_index")
        # Only a step's first reply is checked, as the PowerShell script checks it.
        if ! grep -q -x -F -e "$rce_step" "$RCE_STATE/tftp.steps"; then
            if [[ $rce_reply == 'PACKET '* ]]; then
                if ! [[ $rce_step =~ ^(RRQ|WRQ|(ACK|DATA)[0-9]{1,5})$ ]] || ! [[ $rce_reply =~ ^PACKET\ ([0-9a-fA-F]{2})+$ ]]; then rce_die "TftpReply '$rce_step=$rce_reply': 'PACKET <hex>' needs whole bytes in hex, on step RRQ, WRQ, ACK<n> or DATA<n>."; fi
            elif [[ $rce_step =~ ^(RRQ|WRQ)$ ]]; then
                if ! [[ $rce_reply =~ ^ERROR\ ([0-9]{1,5})(\ |$) ]] || [ $((10#${BASH_REMATCH[1]})) -gt 65535 ]; then rce_die "TftpReply '$rce_step=$rce_reply': a request is answered 'ERROR <code> <text>', the code 0 to 65535."; fi
            elif [[ $rce_step =~ ^(ACK|DATA)[0-9]{1,5}$ ]]; then
                if [ "$rce_reply" != DROP ]; then rce_die "TftpReply '$rce_step=$rce_reply': an ACK or DATA packet can only be DROP."; fi
            else
                rce_die "TftpReply '$rce_step=$rce_reply': the step is RRQ, WRQ, ACK<n> or DATA<n>."
            fi
        fi
        printf '%s\n' "$rce_step" >>"$RCE_STATE/tftp.steps"
        rce_index=$((rce_index + 1))
    done
    cat >"$RCE_STATE/tftp.pl" <<'RCE_TFTP_PERL'
use strict;
use warnings;
use IO::Socket::IP;
use IO::Select;

my ($state, $address, $port, $noOack, $idleMilliseconds) = @ARGV;
my (%overrides, %dropped);
{
    open(my $steps, '<', "$state/tftp.steps") or die "tftp.steps: $!";
    my $index = 0;
    while (my $step = <$steps>) {
        chomp $step;
        open(my $reply, '<:raw', "$state/tftp.reply.$index") or die "tftp.reply.$index: $!";
        local $/;
        my $text = <$reply>;
        $overrides{$step} = defined $text ? $text : '';
        close $reply;
        $index++;
    }
}
open(my $dataFile, '<:raw', "$state/tftp.data") or die "tftp.data: $!";
my $data = do { local $/; <$dataFile> };
$data = '' unless defined $data;
open(my $log, '>:raw', "$state/tftp.log") or die "tftp.log: $!";
open(my $upload, '>:raw', "$state/tftp.upload") or die "tftp.upload: $!";
select((select($log), $| = 1)[0]);
select((select($upload), $| = 1)[0]);

my $listen = IO::Socket::IP->new(Proto => 'udp', LocalAddr => $address, LocalPort => $port);
if (!$listen) {
    open(my $failure, '>', "$state/tftp.error");
    print $failure "cannot bind UDP $address:$port: $@\n";
    close $failure;
    exit 1;
}
$SIG{TERM} = sub { exit 0 };
open(my $ready, '>', "$state/tftp.ready");
close $ready;

my ($transfer, $peer, $firstAnswer, $lastSent);

sub number {
    my ($packet, $offset) = @_;
    return length($packet) < $offset + 2 ? -1 : (ord(substr($packet, $offset, 1)) << 8) | ord(substr($packet, $offset + 1, 1));
}
# The NUL-terminated strings from the offset on; a last one without its NUL is kept.
sub strings {
    my ($packet, $offset) = @_;
    my @strings;
    my $start = $offset;
    for (my $i = $offset; $i < length($packet); $i++) {
        next if substr($packet, $i, 1) ne "\0";
        push @strings, substr($packet, $start, $i - $start);
        $start = $i + 1;
    }
    push @strings, substr($packet, $start) if $start < length($packet);
    return @strings;
}
sub describe {
    my ($packet) = @_;
    my $opcode = number($packet, 0);
    if ($opcode == 1 || $opcode == 2) {
        my @fields = strings($packet, 2);
        my $text = $opcode == 1 ? 'RRQ' : 'WRQ';
        $text .= " $fields[0]" if @fields > 0;
        $text .= " $fields[1]" if @fields > 1;
        for (my $i = 2; $i + 1 < @fields; $i += 2) { $text .= " $fields[$i]=$fields[$i + 1]"; }
        return $text;
    }
    return 'DATA ' . number($packet, 2) . ' ' . (length($packet) > 4 ? length($packet) - 4 : 0) . ' bytes' if $opcode == 3;
    return 'ACK ' . number($packet, 2) if $opcode == 4;
    if ($opcode == 5) {
        my @fields = strings($packet, 4);
        return 'ERROR ' . number($packet, 2) . (@fields > 0 ? " $fields[0]" : '');
    }
    if ($opcode == 6) {
        my @fields = strings($packet, 2);
        my $text = 'OACK';
        for (my $i = 0; $i + 1 < @fields; $i += 2) { $text .= " $fields[$i]=$fields[$i + 1]"; }
        return $text;
    }
    return "opcode $opcode";
}
sub record {
    my ($direction, $packet) = @_;
    print $log $direction, ' ', unpack('H*', $packet), ' ', describe($packet), "\n";
}
# Logged before it is sent, so curl cannot exit between the send and the log line.
sub transmit {
    my ($packet) = @_;
    record('<', $packet);
    $transfer->send($packet, 0, $peer);
}
sub resend {
    my ($packet) = @_;
    transmit($packet) if defined $packet;
}
sub ackPacket { return pack('nn', 4, $_[0]); }
sub errorPacket { return pack('nn', 5, $_[0]) . $_[1] . "\0"; }
sub oackPacket { return pack('n', 6) . join('', map { "$_\0" } @_); }
# True, once, when the step is overridden with DROP.
sub dropOnce {
    my ($step) = @_;
    return 0 unless defined $overrides{$step} && $overrides{$step} eq 'DROP' && !$dropped{$step};
    $dropped{$step} = 1;
    return 1;
}
# Sends the raw datagram a step is overridden with, "PACKET <hex>", once.
sub sendInjected {
    my ($step) = @_;
    my $reply = $overrides{$step};
    return unless defined $reply && $reply =~ /^PACKET / && !$dropped{"$step PACKET"};
    $dropped{"$step PACKET"} = 1;
    transmit(pack('H*', substr($reply, 7)));
}
sub sendPacket {
    my ($packet, $step) = @_;
    $lastSent = $packet;
    return if defined $step && dropOnce($step);
    transmit($packet);
}
# Sends DATA block n of the file; true when it is the last one (shorter than a block).
sub sendDataBlock {
    my ($block, $blockSize) = @_;
    my $offset = ($block - 1) * $blockSize;
    my $length = length($data) - $offset;
    $length = $blockSize if $length > $blockSize;
    $length = 0 if $length < 0;
    sendPacket(pack('nn', 3, $block) . ($length > 0 ? substr($data, $offset, $length) : ''), "DATA$block");
    return $length < $blockSize;
}
# The next datagram curl sends to either port, or undef after the idle time.
sub receive {
    my @readable = IO::Select->new($transfer, $listen)->can_read($idleMilliseconds / 1000);
    return (undef, 0) unless @readable;
    my $socket = $readable[0];
    my $packet;
    $socket->recv($packet, 65536);
    record('>', $packet);
    return ($packet, $socket == $listen ? 1 : 0);
}

sub serveRead {
    my ($oack, $options, $blockSize) = @_;
    my $expectedAck = 0;
    my $finalSent = 0;
    if ($oack) { sendPacket(oackPacket(@$options), undef); } else { $expectedAck = 1; $finalSent = sendDataBlock($expectedAck, $blockSize); }
    $firstAnswer = $lastSent;
    while (1) {
        my ($packet, $fromListen) = receive();
        return unless defined $packet;
        if ($fromListen) { resend($firstAnswer); next; }
        my $opcode = number($packet, 0);
        return if $opcode == 5;
        next if $opcode != 4 || length($packet) < 4;
        my $block = number($packet, 2);
        if ($block != $expectedAck) { resend($lastSent); next; }
        next if dropOnce("ACK$block");
        sendInjected("ACK$block");
        return if $finalSent;
        $expectedAck = ($expectedAck + 1) & 0xFFFF;
        $finalSent = sendDataBlock($expectedAck, $blockSize);
    }
}
sub serveWrite {
    my ($oack) = @_;
    my $expectedData = 1;
    if ($oack) { sendPacket(oackPacket(@{$_[1]}), undef); } else { sendPacket(ackPacket(0), 'ACK0'); }
    $firstAnswer = $lastSent;
    while (1) {
        my ($packet, $fromListen) = receive();
        return unless defined $packet;
        if ($fromListen) { resend($firstAnswer); next; }
        my $opcode = number($packet, 0);
        return if $opcode == 5;
        next if $opcode != 3 || length($packet) < 4;
        my $block = number($packet, 2);
        # A repeated block means curl missed its ACK; acknowledge it again.
        if ($block != $expectedData) { resend($lastSent); next; }
        next if dropOnce("DATA$block");
        print $upload substr($packet, 4);
        sendInjected("DATA$block");
        sendPacket(ackPacket($block), "ACK$block");
        $expectedData = ($expectedData + 1) & 0xFFFF;
    }
}

my $request;
$peer = $listen->recv($request, 65536);
record('>', $request);
$transfer = IO::Socket::IP->new(Proto => 'udp', LocalAddr => $address, LocalPort => 0) or exit 1;
my $opcode = number($request, 0);
if ($opcode != 1 && $opcode != 2) {
    sendPacket(errorPacket(4, 'Illegal TFTP operation'), undef);
    exit 0;
}
my $kind = $opcode == 1 ? 'RRQ' : 'WRQ';
sendInjected($kind);
if (defined $overrides{$kind} && $overrides{$kind} =~ /^ERROR /) {
    # "ERROR <code> <text>", checked before the responder starts.
    my @parts = split(/ /, $overrides{$kind}, 3);
    sendPacket(errorPacket($parts[1], defined $parts[2] ? $parts[2] : ''), undef);
    exit 0;
}
my @fields = strings($request, 2);
my @options;
my $blockSize = 512;
for (my $i = 2; $i + 1 < @fields; $i += 2) {
    my $name = lc $fields[$i];
    next unless $name eq 'blksize' || $name eq 'tsize' || $name eq 'timeout';
    my $value = ($name eq 'tsize' && $opcode == 1) ? length($data) : $fields[$i + 1];
    push @options, $fields[$i], $value;
    $blockSize = int($value) if $name eq 'blksize';
}
my $oack = @options > 0 && !$noOack;
$blockSize = 512 unless $oack;
if ($opcode == 1) { serveRead($oack, \@options, $blockSize); } else { serveWrite($oack, \@options); }
RCE_TFTP_PERL
    perl "$RCE_STATE/tftp.pl" "$RCE_STATE" "$RCE_LISTEN_ADDRESS" "$RCE_PORT" "$RCE_TFTP_NOOACK" "$RCE_TFTP_IDLE" </dev/null >/dev/null 2>"$RCE_STATE/tftp.err" &
    RCE_TFTP_PID=$!
    rce_polls=0
    while [ ! -e "$RCE_STATE/tftp.ready" ]; do
        if ! kill -0 "$RCE_TFTP_PID" 2>/dev/null || [ $rce_polls -ge 250 ]; then
            rce_die "the TFTP responder did not start: $(cat "$RCE_STATE/tftp.error" "$RCE_STATE/tftp.err" 2>/dev/null | head -n 3)"
        fi
        rce_polls=$((rce_polls + 1))
        sleep 0.02
    done
fi

# --- The UDP sink and the DNS and DNS-over-HTTPS responders -----------------------------
# A UDP datagram, and a TCP or UDP answer from the bound port, are beyond bash, so one perl
# (core modules only) written to the state directory serves them as RecorderDnsResponder and
# the UDP sink do in the PowerShell script. TLS for -DohPort is socat's, one listener per
# port, each connection handed to the same perl in its doh mode.

# Waits for Marker to appear in the state directory, or dies with what Name reported.
rce_wait_for_marker() {
    local marker=$1 pid=$2 name=$3 polls=0
    while [ ! -e "$RCE_STATE/$marker" ]; do
        if ! kill -0 "$pid" 2>/dev/null || [ $polls -ge 250 ]; then
            rce_die "the $name did not start: $(cat "$RCE_STATE/dns.error" "$RCE_STATE/$marker.err" 2>/dev/null | head -n 3)"
        fi
        polls=$((polls + 1))
        sleep 0.02
    done
}

if [ $RCE_UDP_SINK = 1 ] || [ -n "$RCE_DNS_PORTS" ] || [ -n "$RCE_DOH_PORTS" ]; then
    cat >"$RCE_STATE/dns.pl" <<'RCE_DNS_PERL'
use strict;
use warnings;
use IO::Socket::IP;
use IO::Select;
use Socket qw(inet_pton AF_INET AF_INET6);
use MIME::Base64;
use Fcntl qw(:flock);
use Time::HiRes;

my ($state, $mode, $port) = @ARGV;
my %config;
open(my $configFile, '<', "$state/dns.conf") or die "dns.conf: $!";
while (my $line = <$configFile>) { chomp $line; my ($key, $value) = split(/=/, $line, 2); $config{$key} = $value; }
close $configFile;

sub fail { open(my $failure, '>', "$state/dns.error"); print $failure $_[0]; close $failure; exit 1; }

sub record {
    my ($transport, $onPort, $query) = @_;
    open(my $log, '>>:raw', "$state/dns.log") or return;
    flock($log, LOCK_EX);
    print $log int(Time::HiRes::time() * 1000) - $config{start}, " $transport $onPort ", unpack('H*', $query), "\n";
    close $log;
}

# The query's header and question, then one answer record per address of the asked family.
sub answer {
    my ($query, $truncate) = @_;
    my $end = 12;
    while ($end < length($query) && ord(substr($query, $end, 1)) != 0) { $end += 1 + ord(substr($query, $end, 1)); }
    $end += 5;
    my $reply = substr($query, 0, $end);
    $reply .= "\0" x ($end - length($reply));
    my $type = unpack('n', substr($reply, $end - 4, 2));
    substr($reply, 2, 1) = chr($truncate ? 0x83 : 0x81);
    substr($reply, 3, 1) = chr(0x80 | $config{rcode});
    substr($reply, 6, 6) = "\0" x 6;
    my $answers = 0;
    if (!$truncate && $config{rcode} == 0) {
        foreach my $address (split(/ /, $config{answers})) {
            next if $address eq '';
            my $version6 = index($address, ':') >= 0;
            next unless $type == ($version6 ? 28 : 1);
            my $data = inet_pton($version6 ? AF_INET6 : AF_INET, $address);
            next unless defined $data;
            $reply .= pack('nnnNn', 0xC00C, $type, 1, 60, length($data)) . $data;
            $answers++;
        }
        if ($type == 65 && length($config{ech})) {
            # ServiceMode, priority 1, target ".", then ech=<list> (RFC 9460, key 5).
            my $list = decode_base64($config{ech});
            $reply .= pack('nnnNn', 0xC00C, 65, 1, 60, 2 + 1 + 4 + length($list)) . pack('nCnn', 1, 0, 5, length($list)) . $list;
            $answers++;
        }
    }
    substr($reply, 7, 1) = chr($answers);
    return $reply;
}

sub ready { open(my $marker, '>', "$state/$_[0]"); close $marker; }

if ($mode eq 'sink') {
    my $sink = IO::Socket::IP->new(LocalAddr => $config{address}, LocalPort => $port, Proto => 'udp') or fail("UDP $port: $@");
    open(my $datagrams, '>:raw', "$state/udp.log") or fail("udp.log: $!");
    select((select($datagrams), $| = 1)[0]);
    ready('udp.ready');
    while (1) {
        my $datagram;
        defined($sink->recv($datagram, 65535)) or next;
        print $datagrams unpack('H*', $datagram), "\n";
    }
} elsif ($mode eq 'doh') {
    binmode STDIN;
    binmode STDOUT;
    $| = 1;
    while (1) {
        my $head = '';
        while ($head !~ /\r\n\r\n\z/) { read(STDIN, my $byte, 1) or exit 0; $head .= $byte; }
        $head =~ s/\r\n\r\n\z//;
        my @lines = split(/\r\n/, $head, -1);
        my @requestLine = split(/ /, $lines[0]);
        my $query;
        if ($requestLine[0] eq 'POST') {
            my $length = 0;
            foreach my $line (@lines) { $length = $1 + 0 if $line =~ /^Content-Length:\s*(\d+)/i; }
            $query = '';
            if ($length > 0) { (read(STDIN, $query, $length) // -1) == $length or exit 0; }
        } else {
            my $encoded = (split(/&/, substr($requestLine[1], index($requestLine[1], 'dns=') + 4)))[0];
            $encoded =~ tr{-_}{+/};
            $query = decode_base64($encoded);
        }
        record('doh', $port, $query);
        my $reply = answer($query, 0);
        print "HTTP/1.1 200 OK\r\nContent-Type: application/dns-message\r\nContent-Length: " . length($reply) . "\r\n\r\n" . $reply;
    }
} else {
    my (%udp, %tcp);
    my $selector = IO::Select->new;
    my %silent = map { $_ => 1 } split(/ /, $config{silent});
    foreach my $bound (split(/ /, $config{ports})) {
        next if $bound eq '';
        my $udpSocket = IO::Socket::IP->new(LocalAddr => $config{address}, LocalPort => $bound, Proto => 'udp') or fail("UDP $bound: $@");
        my $tcpSocket = IO::Socket::IP->new(LocalAddr => $config{address}, LocalPort => $bound, Proto => 'tcp', Listen => 16, ReuseAddr => 1) or fail("TCP $bound: $@");
        $udp{fileno($udpSocket)} = [$udpSocket, $bound];
        $tcp{fileno($tcpSocket)} = [$tcpSocket, $bound];
        $selector->add($udpSocket, $tcpSocket);
    }
    $SIG{CHLD} = 'IGNORE';
    ready('dns.ready');
    while (1) {
        foreach my $socket ($selector->can_read) {
            my $number = fileno($socket);
            if ($udp{$number}) {
                my $onPort = $udp{$number}[1];
                my $query;
                my $sender = $socket->recv($query, 65535);
                defined($sender) or next;
                record('udp', $onPort, $query);
                $socket->send(answer($query, $config{truncate}), 0, $sender) unless $silent{$onPort};
            } else {
                my $client = $socket->accept or next;
                my $onPort = $tcp{$number}[1];
                if (fork() == 0) {
                    binmode $client;
                    while (1) {
                        read($client, my $prefix, 2) == 2 or exit 0;
                        my $length = unpack('n', $prefix);
                        (read($client, my $query, $length) // -1) == $length or exit 0;
                        record('tcp', $onPort, $query);
                        my $reply = answer($query, 0);
                        print $client pack('n', length($reply)) . $reply;
                        $client->flush;
                    }
                }
                close $client;
            }
        }
    }
}
RCE_DNS_PERL
    {
        printf 'address=%s\n' "$RCE_LISTEN_ADDRESS"
        printf 'ports=%s\n' "$RCE_DNS_PORTS"
        printf 'silent=%s\n' "$RCE_DNS_SILENT_PORTS"
        printf 'answers=%s\n' "${RCE_DNS_ANSWER_ADDRESSES:- 127.0.0.1}"
        printf 'truncate=%s\n' "$RCE_DNS_TRUNCATE"
        printf 'rcode=%s\n' "$RCE_DNS_RESPONSE_CODE"
        printf 'ech=%s\n' "$RCE_DNS_ECH"
    } >"$RCE_STATE/dns.conf"
    rce_now_ms
    printf 'start=%s\n' "$RCE_NOW_MS" >>"$RCE_STATE/dns.conf"
    : >"$RCE_STATE/dns.log"
    if [ -n "$RCE_DNS_PORTS" ]; then
        perl "$RCE_STATE/dns.pl" "$RCE_STATE" serve 0 </dev/null >/dev/null 2>"$RCE_STATE/dns.ready.err" &
        RCE_DNS_PID=$!
        rce_wait_for_marker dns.ready "$RCE_DNS_PID" 'DNS responder'
    fi
    if [ $RCE_UDP_SINK = 1 ]; then
        perl "$RCE_STATE/dns.pl" "$RCE_STATE" sink "$RCE_PORT" </dev/null >/dev/null 2>"$RCE_STATE/udp.ready.err" &
        RCE_SINK_PID=$!
        rce_wait_for_marker udp.ready "$RCE_SINK_PID" 'UDP sink'
    fi
    for rce_doh_port in $RCE_DOH_PORTS; do
        case $RCE_LISTEN_ADDRESS in
            *:*) rce_doh_listen="OPENSSL-LISTEN:$rce_doh_port,pf=ip6,bind=[$RCE_LISTEN_ADDRESS]" ;;
            *) rce_doh_listen="OPENSSL-LISTEN:$rce_doh_port,bind=$RCE_LISTEN_ADDRESS" ;;
        esac
        "$RCE_SOCAT" -d -d -lf "$RCE_STATE/doh.$rce_doh_port.log" "$rce_doh_listen,reuseaddr,fork,cert=$RCE_STATE/server.crt,key=$RCE_STATE/server.key,verify=0,openssl-min-proto-version=TLS1.2,openssl-max-proto-version=TLS1.2" \
            "EXEC:perl $RCE_STATE/dns.pl $RCE_STATE doh $rce_doh_port" </dev/null >/dev/null 2>&1 &
        rce_doh_pid=$!
        RCE_DOH_PID="$RCE_DOH_PID $rce_doh_pid"
        rce_polls=0
        while ! grep -q 'listening on' "$RCE_STATE/doh.$rce_doh_port.log" 2>/dev/null; do
            if ! kill -0 "$rce_doh_pid" 2>/dev/null || [ $rce_polls -ge 250 ]; then
                rce_die "socat did not start listening for DNS over HTTPS on port $rce_doh_port: $(grep -E ' [EF] ' "$RCE_STATE/doh.$rce_doh_port.log" 2>/dev/null | head -n 3)"
            fi
            rce_polls=$((rce_polls + 1))
            sleep 0.02
        done
    done
fi

# --- -Script and the relay of -HalfCloseAfterResponse with -Tls ---------------------------
if [ -n "$RCE_SCRIPT" ]; then
    cat >"$RCE_STATE/script.pl" <<'RCE_SCRIPT_PERL'
use strict;
use warnings;
use IO::Socket::IP;
use Socket qw(SOL_SOCKET SO_LINGER);
use Encode ();

my ($state, $mode) = @ARGV;
my %config;
open(my $configFile, '<', "$state/script.conf") or die "script.conf: $!";
while (my $line = <$configFile>) { chomp $line; my ($key, $value) = split(/=/, $line, 2); $config{$key} = $value; }
close $configFile;

# The text after a verb, as bytes: backslash escapes \r \n \t \0 \\ \" \' and \xHH, every character a Latin-1 byte.
sub unescape_bytes {
    my ($text) = @_;
    my $bytes = '';
    my $index = 0;
    while ($index < length $text) {
        my $character = substr($text, $index, 1);
        if ($character ne '\\') {
            die sprintf("Response character U+%04X is not Latin-1.\n", ord $character) if ord($character) > 255;
            $bytes .= $character;
            $index++;
            next;
        }
        die "Response ends with a lone backslash.\n" if $index + 1 >= length $text;
        my $escape = substr($text, $index + 1, 1);
        my %simple = ('r' => "\r", 'n' => "\n", 't' => "\t", '0' => "\0", '\\' => '\\', '"' => '"', "'" => "'");
        if (exists $simple{$escape}) { $bytes .= $simple{$escape}; $index += 2; next; }
        die "Response has an unknown escape \\$escape at index $index.\n" if $escape ne 'x';
        my $hex = $index + 4 <= length $text ? substr($text, $index + 2, 2) : '';
        die "Response has a malformed \\x escape at index $index.\n" if $hex !~ /^[0-9A-Fa-f]{2}$/;
        $bytes .= chr(hex $hex);
        $index += 4;
    }
    return $bytes;
}

# The -Script file as a list of steps; see the PowerShell script's .PARAMETER Script.
sub parse_steps {
    my ($path) = @_;
    open(my $file, '<:raw', $path) or die "cannot read script file $path: $!\n";
    local $/;
    my $content = Encode::decode('UTF-8', <$file>);
    close $file;
    $content =~ s/^\x{FEFF}//;
    my @steps;
    my $lineNumber = 0;
    foreach my $line (split(/\r\n|\n|\r/, $content)) {
        $lineNumber++;
        (my $text = $line) =~ s/^\s+|\s+$//g;
        next if $text eq '' || substr($text, 0, 1) eq '#';
        my ($verb) = split(/\s+/, $text, 2);
        my $argument = $text =~ /^\S+\s+(.*)$/ ? $1 : '';
        if ($verb eq 'send') { push @steps, { kind => 'send', bytes => unescape_bytes($argument), text => $text }; }
        elsif ($verb eq 'pause' && $argument =~ /^\d+$/) { push @steps, { kind => 'pause', milliseconds => $argument + 0, text => $text }; }
        elsif ($verb eq 'close' && $argument eq '') { push @steps, { kind => 'close', text => $text }; }
        elsif ($verb eq 'reset' && $argument eq '') { push @steps, { kind => 'reset', text => $text }; }
        elsif ($verb eq 'read' && $argument eq '') { push @steps, { kind => 'read', framing => 'gap', text => $text }; }
        elsif ($verb eq 'read' && $argument eq 'ber') { push @steps, { kind => 'read', framing => 'ber', text => $text }; }
        elsif ($verb eq 'read' && $argument =~ /^\d+$/ && $argument > 0) { push @steps, { kind => 'read', framing => 'count', count => $argument + 0, text => $text }; }
        else { die "Script line $lineNumber is not a step: $text\n"; }
    }
    return \@steps;
}

my $steps = eval { parse_steps($config{script}) };
if (!$steps) { print STDERR $@; exit 1; }
exit 0 if defined $mode && $mode eq 'check';

$SIG{PIPE} = 'IGNORE';
my $stopping = 0;
$SIG{TERM} = sub { $stopping = 1 };
my $idle = $config{idle} + 0;
my $gap = $config{gap} + 0;
my ($log, $received, $unconsumed, $written) = ('', '', '', 0);
my ($listener, $connection, $input, $output);

sub finish {
    foreach my $pair (['script.received', $received], ['script.log', $log]) {
        open(my $file, '>:raw', "$state/$pair->[0]") or next;
        print $file $pair->[1];
        close $file;
    }
    open(my $count, '>', "$state/script.written"); print $count $written; close $count;
    close $connection if $connection;
    close $listener if $listener;
    close $output if $output && !$connection;
    open(my $done, '>', "$state/script.done"); close $done;
    exit 0;
}
sub note { $log .= "$_[0]\r\n"; }
sub hex_pairs { return join(' ', unpack('(H2)*', $_[0])); }

# Waits up to the given milliseconds for readable input; true when there is some (or end of file).
sub wait_readable {
    my ($handle, $milliseconds) = @_;
    my $deadline = time + $milliseconds / 1000;
    while (1) {
        finish() if $stopping;
        my $left = $deadline - time;
        return 0 if $left <= 0;
        my $bits = '';
        vec($bits, fileno($handle), 1) = 1;
        my $count = select(my $ready = $bits, undef, undef, $left > 0.1 ? 0.1 : $left);
        return 1 if $count > 0;
    }
}

# Bytes from curl, recorded and added to the unconsumed ones: how many arrived, 0 when curl
# closed its end (or reset), -1 on timeout.
sub receive_bytes {
    my ($milliseconds) = @_;
    return -1 unless wait_readable($input, $milliseconds);
    my $count = sysread($input, my $chunk, 65536);
    return 0 unless $count;
    $received .= $chunk;
    $unconsumed .= $chunk;
    return $count;
}

# The length of the whole BER element at the start of the unconsumed bytes, or -1 while its
# identifier and length octets have not all arrived (X.690 8.1).
sub ber_element_length {
    return -1 if length($unconsumed) < 1;
    my @octets = unpack('C*', substr($unconsumed, 0, 16));
    my $index = 1;
    if (($octets[0] & 0x1F) == 0x1F) {
        $index++ while $index < @octets && ($octets[$index] & 0x80) != 0;
        $index++;
    }
    return -1 if @octets <= $index;
    my $first = $octets[$index];
    return $index + 1 + $first if $first < 0x80;
    my $count = $first & 0x7F;
    if ($count == 0) { note('= curl sent an indefinite length, which the read ber step does not support'); finish(); }
    return -1 if @octets <= $index + $count;
    my $length = 0;
    $length = $length * 256 + $octets[$index + $_] for 1 .. $count;
    return $index + 1 + $count + $length;
}

# Runs one read step; false when the session has ended.
sub run_read {
    my ($step) = @_;
    my $wanted = -1;
    while (1) {
        $wanted = ber_element_length() if $step->{framing} eq 'ber';
        $wanted = $step->{count} if $step->{framing} eq 'count';
        my $timeout;
        if ($step->{framing} eq 'gap') { $timeout = length($unconsumed) == 0 ? $idle : $gap; }
        elsif ($wanted >= 0 && length($unconsumed) >= $wanted) { last; }
        else { $timeout = $idle; }
        my $arrived = receive_bytes($timeout);
        next if $arrived > 0;
        last if $step->{framing} eq 'gap' && $arrived < 0 && length($unconsumed) > 0;
        note('> ' . hex_pairs($unconsumed)) if length $unconsumed;
        my $expected = $wanted >= 0 ? " of the $wanted expected" : '';
        if ($arrived < 0) { note("= $step->{text} timed out after $idle ms with " . length($unconsumed) . " bytes$expected; session ended"); }
        else { note("= curl closed the connection during $step->{text} with " . length($unconsumed) . " bytes$expected; session ended"); }
        return 0;
    }
    $wanted = length $unconsumed if $wanted < 0;
    my $message = substr($unconsumed, 0, $wanted, '');
    note('> ' . hex_pairs($message));
    return 1;
}

sub pause_for {
    my ($milliseconds) = @_;
    my $deadline = time + $milliseconds / 1000;
    while ((my $left = $deadline - time) > 0) { finish() if $stopping; select(undef, undef, undef, $left > 0.1 ? 0.1 : $left); }
}

sub send_bytes {
    my ($bytes) = @_;
    my $offset = 0;
    while ($offset < length $bytes) {
        my $count = syswrite($output, $bytes, length($bytes) - $offset, $offset);
        return 0 unless defined $count;
        $offset += $count;
    }
    return 1;
}

if ($config{tls}) {
    # s_server holds the socket; its plaintext comes through the FIFOs on this process's input and output.
    open($input, '<&=', 0) or die "stdin: $!";
    open($output, '>&=', 1) or die "stdout: $!";
    open(my $ready, '>', "$state/script.ready"); close $ready;
} else {
    $listener = IO::Socket::IP->new(LocalAddr => $config{address}, LocalPort => $config{port}, Proto => 'tcp', Listen => 5, ReuseAddr => 1);
    if (!$listener) { open(my $failure, '>', "$state/script.error"); print $failure "cannot listen on $config{address}:$config{port}: $@"; close $failure; exit 1; }
    open(my $ready, '>', "$state/script.ready"); close $ready;
    while (!$connection) {
        finish() if $stopping;
        my $bits = '';
        vec($bits, fileno($listener), 1) = 1;
        if (select(my $ready = $bits, undef, undef, 0.1) > 0) { $connection = $listener->accept; }
    }
    $input = $output = $connection;
}

my $ended = 0;
my $startsWithRead = @$steps && $steps->[0]{kind} eq 'read';
if ($startsWithRead) {
    # Waits for curl's first byte, as the PowerShell script does before it serves the session.
    my $arrived = receive_bytes($idle);
    if ($arrived <= 0) {
        note("= $steps->[0]{text} timed out after $idle ms with 0 bytes; session ended");
        finish();
    }
}
note('= TLS handshake completed') if $config{tls};
STEP: foreach my $step (@$steps) {
    if ($step->{kind} eq 'read') {
        if (!run_read($step)) { $ended = 1; last STEP; }
    } elsif ($step->{kind} eq 'send') {
        if (!send_bytes($step->{bytes})) { note('= curl closed the connection mid-send; session ended'); $ended = 1; last STEP; }
        $written += length $step->{bytes};
        note('< ' . hex_pairs($step->{bytes}));
    } elsif ($step->{kind} eq 'pause') {
        pause_for($step->{milliseconds});
        note("= paused $step->{milliseconds} ms");
    } elsif ($step->{kind} eq 'reset') {
        # A zero linger time makes the close a RST. Over TLS s_server holds the socket and the run stops it, so curl sees a FIN.
        setsockopt($connection, SOL_SOCKET, SO_LINGER, pack('ii', 1, 0)) if $connection;
        note('= server reset the connection');
        $ended = 1;
        last STEP;
    } else {
        note('= server closed the connection');
        $ended = 1;
        last STEP;
    }
}
note('= script ended; server closed the connection') if !$ended;
finish();
RCE_SCRIPT_PERL
    {
        printf 'address=%s\n' "$RCE_LISTEN_ADDRESS"
        printf 'port=%s\n' "$RCE_PORT"
        printf 'tls=%s\n' "$RCE_TLS"
        printf 'script=%s\n' "$RCE_SCRIPT"
        printf 'idle=%s\n' "$RCE_SCRIPT_IDLE"
        printf 'gap=%s\n' "$RCE_SCRIPT_GAP"
    } >"$RCE_STATE/script.conf"
    rce_script_error=$(perl "$RCE_STATE/script.pl" "$RCE_STATE" check 2>&1) || rce_die "$rce_script_error"
    : >"$RCE_STATE/script.log"
    : >"$RCE_STATE/script.received"
    if [ $RCE_TLS = 1 ]; then
        # As for -Http2: s_server takes the one connection, and perl serves its plaintext through two
        # FIFOs opened in the same order on both sides. -no_ign_eof is not given: the run stops s_server
        # (rce_script_watch) once the session is over, a close without close_notify as the PowerShell script's.
        mkfifo "$RCE_STATE/script.out" "$RCE_STATE/script.in" || rce_die 'mkfifo failed'
        case $RCE_LISTEN_ADDRESS in *:*) rce_accept="[$RCE_LISTEN_ADDRESS]:$RCE_PORT" ;; *) rce_accept="$RCE_LISTEN_ADDRESS:$RCE_PORT" ;; esac
        RCE_TLS_MSG="$RCE_STATE/script.msg"
        "$RCE_OPENSSL" s_server -quiet -naccept 1 -tls1_2 -accept "$rce_accept" \
            -cert "$RCE_STATE/server.crt" -key "$RCE_STATE/server.key" -msg -msgfile "$RCE_TLS_MSG" \
            >"$RCE_STATE/script.out" <"$RCE_STATE/script.in" 2>"$RCE_STATE/script.tls.err" &
        RCE_TLS_PID=$!
        printf '%s\n' "$RCE_TLS_PID" >"$RCE_STATE/tls-pid.script"
        perl "$RCE_STATE/script.pl" "$RCE_STATE" serve <"$RCE_STATE/script.out" >"$RCE_STATE/script.in" 2>"$RCE_STATE/script.err" &
        RCE_SCRIPT_PID=$!
        rce_wait_for_listening_port "$RCE_PORT" || rce_die "openssl s_server did not start listening on port $RCE_PORT: $(cat "$RCE_STATE/script.tls.err" "$RCE_STATE/script.err" 2>/dev/null)"
        # Once the session is over, s_server is stopped when its msgfile shows records enough for every
        # byte sent, as rce_finish_tls does, so curl sees the close.
        (
            rce_polls=0
            while [ ! -e "$RCE_STATE/script.done" ] && kill -0 "$RCE_SCRIPT_PID" 2>/dev/null; do sleep 0.02; done
            RCE_TLS_WRITTEN=$(cat "$RCE_STATE/script.written" 2>/dev/null)
            while kill -0 "$RCE_TLS_PID" 2>/dev/null && [ $rce_polls -lt 150 ]; do
                set -- $(rce_sent_application_records)
                if [ $(($1 - 16 * $2)) -ge "${RCE_TLS_WRITTEN:-0}" ]; then break; fi
                rce_polls=$((rce_polls + 1))
                sleep 0.02
            done
            sleep 0.1
            kill -KILL "$RCE_TLS_PID" 2>/dev/null
        ) </dev/null >/dev/null 2>&1 &
    else
        perl "$RCE_STATE/script.pl" "$RCE_STATE" serve </dev/null >/dev/null 2>"$RCE_STATE/script.err" &
        RCE_SCRIPT_PID=$!
        rce_polls=0
        while [ ! -e "$RCE_STATE/script.ready" ]; do
            if ! kill -0 "$RCE_SCRIPT_PID" 2>/dev/null || [ $rce_polls -ge 250 ]; then
                rce_die "the scripted server did not start: $(cat "$RCE_STATE/script.error" "$RCE_STATE/script.err" 2>/dev/null | head -n 3)"
            fi
            rce_polls=$((rce_polls + 1))
            sleep 0.02
        done
    fi
    printf '%s\n' "$RCE_SCRIPT_PID" >"$RCE_STATE/pid.script"
fi

if [ $RCE_HALF_CLOSE = 1 ] && [ $RCE_TLS = 1 ]; then
    cat >"$RCE_STATE/relay.pl" <<'RCE_RELAY_PERL'
use strict;
use warnings;
use IO::Socket::UNIX;

# Relays curl's connection (standard input and output) to s_server's Unix socket; SIGUSR1 sends
# curl a FIN while s_server goes on reading (-HalfCloseAfterResponse with -Tls).
$SIG{PIPE} = 'IGNORE';
my $socket = IO::Socket::UNIX->new(Peer => $ARGV[0]) or exit 1;
open(my $curl, '>&=', 1) or exit 1;
$SIG{USR1} = sub { shutdown($curl, 1); };
sub write_all {
    my ($handle, $data) = @_;
    my $offset = 0;
    while ($offset < length $data) {
        my $count = syswrite($handle, $data, length($data) - $offset, $offset);
        return 0 unless defined $count;
        $offset += $count;
    }
    return 1;
}
my $curlOpen = 1;
my $socketFd = fileno($socket);
while (1) {
    my $bits = '';
    vec($bits, $socketFd, 1) = 1;
    vec($bits, 0, 1) = 1 if $curlOpen;
    my $count = select(my $ready = $bits, undef, undef, undef);
    next if $count < 0;
    if ($curlOpen && vec($ready, 0, 1)) {
        my $read = sysread(STDIN, my $chunk, 65536);
        if ($read) { last unless write_all($socket, $chunk); }
        else { $curlOpen = 0; shutdown($socket, 1); }
    }
    if (vec($ready, $socketFd, 1)) {
        my $read = sysread($socket, my $chunk, 65536);
        last unless $read;
        last unless write_all($curl, $chunk);
    }
}
RCE_RELAY_PERL
fi

# RFC 7541 appendix B, the Huffman codes of symbols 0 to 255 as hex code/bit length.
RCE_HPACK_HUFFMAN='1ff8/13 7fffd8/23 fffffe2/28 fffffe3/28 fffffe4/28 fffffe5/28 fffffe6/28 fffffe7/28 fffffe8/28 ffffea/24 3ffffffc/30 fffffe9/28 fffffea/28 3ffffffd/30 fffffeb/28 fffffec/28 fffffed/28 fffffee/28 fffffef/28 ffffff0/28 ffffff1/28 ffffff2/28 3ffffffe/30 ffffff3/28 ffffff4/28 ffffff5/28 ffffff6/28 ffffff7/28 ffffff8/28 ffffff9/28 ffffffa/28 ffffffb/28 14/6 3f8/10 3f9/10 ffa/12 1ff9/13 15/6 f8/8 7fa/11 3fa/10 3fb/10 f9/8 7fb/11 fa/8 16/6 17/6 18/6 0/5 1/5 2/5 19/6 1a/6 1b/6 1c/6 1d/6 1e/6 1f/6 5c/7 fb/8 7ffc/15 20/6 ffb/12 3fc/10 1ffa/13 21/6 5d/7 5e/7 5f/7 60/7 61/7 62/7 63/7 64/7 65/7 66/7 67/7 68/7 69/7 6a/7 6b/7 6c/7 6d/7 6e/7 6f/7 70/7 71/7 72/7 fc/8 73/7 fd/8 1ffb/13 7fff0/19 1ffc/13 3ffc/14 22/6 7ffd/15 3/5 23/6 4/5 24/6 5/5 25/6 26/6 27/6 6/5 74/7 75/7 28/6 29/6 2a/6 7/5 2b/6 76/7 2c/6 8/5 9/5 2d/6 77/7 78/7 79/7 7a/7 7b/7 7ffe/15 7fc/11 3ffd/14 1ffd/13 ffffffc/28 fffe6/20 3fffd2/22 fffe7/20 fffe8/20 3fffd3/22 3fffd4/22 3fffd5/22 7fffd9/23 3fffd6/22 7fffda/23 7fffdb/23 7fffdc/23 7fffdd/23 7fffde/23 ffffeb/24 7fffdf/23 ffffec/24 ffffed/24 3fffd7/22 7fffe0/23 ffffee/24 7fffe1/23 7fffe2/23 7fffe3/23 7fffe4/23 1fffdc/21 3fffd8/22 7fffe5/23 3fffd9/22 7fffe6/23 7fffe7/23 ffffef/24 3fffda/22 1fffdd/21 fffe9/20 3fffdb/22 3fffdc/22 7fffe8/23 7fffe9/23 1fffde/21 7fffea/23 3fffdd/22 3fffde/22 fffff0/24 1fffdf/21 3fffdf/22 7fffeb/23 7fffec/23 1fffe0/21 1fffe1/21 3fffe0/22 1fffe2/21 7fffed/23 3fffe1/22 7fffee/23 7fffef/23 fffea/20 3fffe2/22 3fffe3/22 3fffe4/22 7ffff0/23 3fffe5/22 3fffe6/22 7ffff1/23 3ffffe0/26 3ffffe1/26 fffeb/20 7fff1/19 3fffe7/22 7ffff2/23 3fffe8/22 1ffffec/25 3ffffe2/26 3ffffe3/26 3ffffe4/26 7ffffde/27 7ffffdf/27 3ffffe5/26 fffff1/24 1ffffed/25 7fff2/19 1fffe3/21 3ffffe6/26 7ffffe0/27 7ffffe1/27 3ffffe7/26 7ffffe2/27 fffff2/24 1fffe4/21 1fffe5/21 3ffffe8/26 3ffffe9/26 ffffffd/28 7ffffe3/27 7ffffe4/27 7ffffe5/27 fffec/20 fffff3/24 fffed/20 1fffe6/21 3fffe9/22 1fffe7/21 1fffe8/21 7ffff3/23 3fffea/22 3fffeb/22 1ffffee/25 1ffffef/25 fffff4/24 fffff5/24 3ffffea/26 7ffff4/23 3ffffeb/26 7ffffe6/27 3ffffec/26 3ffffed/26 7ffffe7/27 7ffffe8/27 7ffffe9/27 7ffffea/27 7ffffeb/27 ffffffe/28 7ffffec/27 7ffffed/27 7ffffee/27 7ffffef/27 7fffff0/27 3ffffee/26'
if [ $RCE_HTTP2 = 1 ]; then
    cat >"$RCE_STATE/h2.pl" <<'RCE_H2_PERL'
use strict;
use warnings;
use IO::Socket::IP;
use IO::Select;
use IO::Socket::UNIX;
use Fcntl qw(:flock);
use POSIX ();

my ($state) = @ARGV;
my %config;
open(my $configFile, '<', "$state/h2.conf") or die "h2.conf: $!";
while (my $line = <$configFile>) { chomp $line; my ($key, $value) = split(/=/, $line, 2); $config{$key} = $value; }
close $configFile;
# s_server's message file, where the negotiated protocol is read: one per connection of a reset run over TLS.
my $messageFile = "$state/h2.msg";
$SIG{PIPE} = 'IGNORE';
my $stopping = 0;
$SIG{TERM} = sub { $stopping = 1 };

sub append { my ($name, $data) = @_; open(my $file, '>>:raw', "$state/$name") or return; flock($file, LOCK_EX); print $file $data; close $file; }
sub note { append('h2.log', "$_[0]\n"); }
sub escape { my $text = $_[0]; $text =~ s/\r/\\r/g; $text =~ s/\n/\\n/g; return $text; }
sub note_lines { my ($prefix, $text) = @_; foreach my $line (split(/\r\n/, $text, -1)) { note($prefix . $line) if length $line; } }

# Reads exactly count bytes, recording them; undef when curl hung up or the recorder is stopping.
sub read_exact {
    my ($in, $count) = @_;
    my $buffer = '';
    while (length($buffer) < $count) {
        my $read = sysread($in, my $chunk, $count - length($buffer));
        return undef unless $read;
        $buffer .= $chunk;
    }
    append('h2.received', $buffer) if $count > 0;
    return $buffer;
}

sub write_all {
    my ($out, $data) = @_;
    my $offset = 0;
    while ($offset < length($data)) {
        my $written = syswrite($out, $data, length($data) - $offset, $offset);
        return unless defined $written;
        $offset += $written;
    }
}

sub write_frame {
    my ($out, $type, $flags, $id, $payload) = @_;
    my $length = length($payload);
    write_all($out, pack('CnCCN', $length >> 16, $length & 0xffff, $type, $flags, $id) . $payload);
}

# Reads up to and including the blank line ending a header block; undef if curl stops first.
sub read_header_block {
    my ($in) = @_;
    my $block = '';
    while (1) {
        my $one = read_exact($in, 1);
        return undef unless defined $one;
        $block .= $one;
        return $block if $block =~ /\r\n\r\n\z/;
    }
}

sub wait_for_hang_up {
    my ($in) = @_;
    while (defined read_exact($in, 1)) { }
    note('= curl closed the connection');
}

# --- HPACK (RFC 7541) --------------------------------------------------------------------

my @staticNames = (':authority', ':method', ':method', ':path', ':path', ':scheme', ':scheme', ':status', ':status', ':status',
    ':status', ':status', ':status', ':status', 'accept-charset', 'accept-encoding', 'accept-language', 'accept-ranges', 'accept', 'access-control-allow-origin',
    'age', 'allow', 'authorization', 'cache-control', 'content-disposition', 'content-encoding', 'content-language', 'content-length', 'content-location', 'content-range',
    'content-type', 'cookie', 'date', 'etag', 'expect', 'expires', 'from', 'host', 'if-match', 'if-modified-since',
    'if-none-match', 'if-range', 'if-unmodified-since', 'last-modified', 'link', 'location', 'max-forwards', 'proxy-authenticate', 'proxy-authorization', 'range',
    'referer', 'refresh', 'retry-after', 'server', 'set-cookie', 'strict-transport-security', 'transfer-encoding', 'user-agent', 'vary', 'via',
    'www-authenticate');
my @staticValues = ('', 'GET', 'POST', '/', '/index.html', 'http', 'https', '200', '204', '206',
    '304', '400', '404', '500', '', 'gzip, deflate');

# The 256 codes of RFC 7541 appendix B as code/length in hex and decimal, symbol 0 first.
my %huffman;
{
    my $symbol = 0;
    foreach my $entry (split(/ /, '@@HUFFMAN@@')) {
        my ($code, $bits) = split(/\//, $entry);
        $huffman{substr(unpack('B32', pack('N', hex($code))), 32 - $bits)} = chr($symbol);
        $symbol++;
    }
}

my @dynamicTable;

sub read_integer {
    my ($block, $position, $prefixBits) = @_;
    my $limit = (1 << $prefixBits) - 1;
    my $value = ord(substr($$block, $$position++, 1)) & $limit;
    return $value if $value < $limit;
    my $shift = 0;
    my $next;
    do {
        $next = ord(substr($$block, $$position++, 1));
        $value += ($next & 0x7f) << $shift;
        $shift += 7;
    } while ($next & 0x80);
    return $value;
}

sub read_string {
    my ($block, $position) = @_;
    my $huffmanCoded = ord(substr($$block, $$position, 1)) & 0x80;
    my $length = read_integer($block, $position, 7);
    my $text = substr($$block, $$position, $length);
    $$position += $length;
    return $text unless $huffmanCoded;
    my ($bits, $decoded, $candidate) = (unpack('B*', $text), '', '');
    foreach my $bit (split(//, $bits)) {
        $candidate .= $bit;
        if (exists $huffman{$candidate}) { $decoded .= $huffman{$candidate}; $candidate = ''; }
    }
    return $decoded;
}

sub lookup {
    my ($index) = @_;
    if ($index <= @staticNames) { return [$staticNames[$index - 1], $index <= @staticValues ? $staticValues[$index - 1] : '']; }
    return $dynamicTable[$index - @staticNames - 1];
}

# Indexed fields, literals with, without and never indexed, and size updates. The dynamic
# table is never evicted: one CONNECT never fills it.
sub decode_header_block {
    my ($block) = @_;
    my @fields;
    my $position = 0;
    while ($position < length($block)) {
        my $first = ord(substr($block, $position, 1));
        if ($first & 0x80) {
            push @fields, lookup(read_integer(\$block, \$position, 7));
        } elsif (($first & 0xe0) == 0x20) {
            read_integer(\$block, \$position, 5);
        } else {
            my $indexed = ($first & 0xc0) == 0x40;
            my $index = read_integer(\$block, \$position, $indexed ? 6 : 4);
            my $name = $index == 0 ? read_string(\$block, \$position) : lookup($index)->[0];
            my $field = [$name, read_string(\$block, \$position)];
            unshift @dynamicTable, $field if $indexed;
            push @fields, $field;
        }
    }
    return @fields;
}

sub literal { return chr(length($_[0])) . $_[0]; }

# --- HTTP/2 -------------------------------------------------------------------------------

# :status as a literal without indexing (name index 8). A refusal, or the answer to a request
# that is not a CONNECT (a forwarded GET), has no body and ends the stream.
sub answer_connect {
    my ($out, $streamId, $connect) = @_;
    if ($config{reset} >= 0) {
        write_frame($out, 3, 0, $streamId, pack('N', $config{reset}));
        note("< RST_STREAM stream $streamId error $config{reset}");
        return;
    }
    if (length($config{answer}) > 0 && !$connect) { answer_scenario($out, $streamId); return; }
    my $block = "\x08" . literal($config{status});
    my $refused = int($config{status} / 100) != 2 || !$connect;
    # content-length is static index 28: 15 in the 4-bit prefix, then 13.
    $block .= "\x0f\x0d" . literal('0') if $refused;
    my $flags = $refused ? 0x05 : 0x04;
    write_frame($out, 1, $flags, $streamId, $block);
    note("< HEADERS stream $streamId flags 0x" . sprintf('%02x', $flags));
    note("<   :status: $config{status}");
    note('<   content-length: 0') if $refused;
}

# push, status:<value> and nostatus answers; each ends the stream with content-length 0.
sub answer_scenario {
    my ($out, $streamId) = @_;
    my $answer = $config{answer};
    if ($answer eq 'push') {
        write_frame($out, 5, 0x04, $streamId, "\0\0\0\2\x82\x86\x84");
        note("< PUSH_PROMISE stream $streamId flags 0x04 promised stream 2");
    }
    my $block = '';
    my $status = $answer =~ /^status:/ ? substr($answer, 7) : $answer eq 'push' ? '200' : undef;
    $block .= "\x08" . literal($status) if defined $status;
    $block .= "\x0f\x0d" . literal('0');
    write_frame($out, 1, 0x05, $streamId, $block);
    note("< HEADERS stream $streamId flags 0x05");
    note(defined $status ? "<   :status: $status" : '<   (no :status)');
    note('<   content-length: 0');
}

# already: connection bytes the caller has read. upgraded: the request that asked for the
# h2c upgrade becomes stream 1 and is answered once SETTINGS are out.
sub serve_http2 {
    my ($in, $out, $already, $upgraded) = @_;
    my $more = read_exact($in, 24 - length($already));
    if (!defined $more) { note('= curl hung up before the connection preface'); return; }
    my $preface = $already . $more;
    note('> PREFACE ' . escape($preface));
    if ($config{omitsettings}) {
        note('= no SETTINGS sent');
    } else {
        write_frame($out, 4, 0, 0, '');
        note('< SETTINGS');
    }
    my ($headerBlock, $tunnel, $tunnelStream, $answered) = ('', '', 0, 0);
    if ($upgraded) { $tunnelStream = 1; answer_connect($out, 1, 0); }
    while (1) {
        my $head = read_exact($in, 9);
        if (!defined $head) { note('= curl closed the connection'); return; }
        my ($high, $low, $type, $flags, $streamId) = unpack('CnCCN', $head);
        my $length = ($high << 16) | $low;
        $streamId &= 0x7fffffff;
        my $payload = $length == 0 ? '' : read_exact($in, $length);
        if (!defined $payload) { note('= curl closed the connection mid-frame'); return; }
        my $frame = " stream $streamId flags 0x" . sprintf('%02x', $flags);
        if ($type == 0) {
            my ($start, $end) = (0, $length);
            if ($flags & 0x8) { $start = 1; $end = $length - ord(substr($payload, 0, 1)); }
            my $data = substr($payload, $start, $end - $start);
            note("> DATA$frame " . ($end - $start) . ' bytes: ' . escape($data));
            $tunnel .= $data;
            if ($end > $start) {
                write_frame($out, 8, 0, 0, pack('N', $length));
                write_frame($out, 8, 0, $streamId, pack('N', $length));
            }
            if (!$answered && $streamId == $tunnelStream && int($config{status} / 100) == 2 && index($tunnel, "\r\n\r\n") >= 0) {
                $answered = 1;
                write_frame($out, 0, 1, $streamId, $config{tunnelResponse});
                note("< DATA stream $streamId flags 0x01 " . length($config{tunnelResponse}) . ' bytes: ' . escape($config{tunnelResponse}));
            }
        } elsif ($type == 1 || $type == 9) {
            my ($start, $end) = (0, $length);
            if ($type == 1 && ($flags & 0x8)) { $start = 1; $end = $length - ord(substr($payload, 0, 1)); }
            if ($type == 1 && ($flags & 0x20)) { $start += 5; }
            note(($type == 1 ? '> HEADERS' : '> CONTINUATION') . $frame);
            $headerBlock .= substr($payload, $start, $end - $start);
            if ($flags & 0x4) {
                my $connect = 0;
                foreach my $field (decode_header_block($headerBlock)) {
                    note(">   $field->[0]: $field->[1]");
                    $connect = 1 if $field->[0] eq ':method' && $field->[1] eq 'CONNECT';
                }
                $headerBlock = '';
                if ($tunnelStream == 0) { $tunnelStream = $streamId; answer_connect($out, $streamId, $connect); }
            }
        } elsif ($type == 2) {
            note("> PRIORITY$frame");
        } elsif ($type == 3) {
            note("> RST_STREAM$frame error " . unpack('N', $payload));
        } elsif ($type == 4) {
            if ($flags & 0x1) { note('> SETTINGS ACK'); next; }
            my $settings = '> SETTINGS';
            for (my $i = 0; $i + 6 <= $length; $i += 6) {
                my ($id, $value) = unpack('nN', substr($payload, $i, 6));
                $settings .= " $id=$value";
            }
            note($settings);
            write_frame($out, 4, 1, 0, '');
            note('< SETTINGS ACK');
        } elsif ($type == 6) {
            note("> PING$frame");
            if (!($flags & 0x1)) { write_frame($out, 6, 1, 0, $payload); note('< PING ACK'); }
        } elsif ($type == 7) {
            my ($last, $error) = unpack('NN', $payload);
            note('> GOAWAY last stream ' . ($last & 0x7fffffff) . " error $error" . ($length > 8 ? ' debug ' . escape(substr($payload, 8)) : ''));
            return;
        } elsif ($type == 8) {
            note("> WINDOW_UPDATE$frame increment " . (unpack('N', $payload) & 0x7fffffff));
        } else {
            note("> frame type $type$frame $length bytes");
        }
    }
}

# --- HTTP/1.1 -----------------------------------------------------------------------------

# An HTTP/1.1 answer to a request already read: the first Response, then wait for the hang-up.
sub serve_http11_origin {
    my ($in, $out, $request) = @_;
    if (!defined $request) { note('= curl hung up before its request ended'); return; }
    note_lines('> ', $request);
    write_all($out, $config{tunnelResponse});
    note('< ' . escape($config{tunnelResponse}));
    wait_for_hang_up($in);
}

# An origin on plain TCP: the first bytes tell prior knowledge (the HTTP/2 preface, which
# starts "PRI ") from an HTTP/1.1 request. In h2c-upgrade mode a request asking for
# "Upgrade: h2c" is answered 101; in every other mode it is answered as HTTP/1.1.
sub serve_plain_origin {
    my ($in, $out) = @_;
    my $first = read_exact($in, 4);
    if (!defined $first) { note('= curl hung up before sending anything'); return; }
    if ($first eq 'PRI ') { note('= prior knowledge: curl opened with the HTTP/2 preface'); serve_http2($in, $out, $first, 0); return; }
    my $rest = read_header_block($in);
    if (!defined $rest) { note('= curl hung up before its request ended'); return; }
    my $request = $first . $rest;
    if ($config{origin} eq 'h2c-upgrade' && index(lc($request), "\r\nupgrade: h2c\r\n") >= 0) {
        note_lines('> ', $request);
        my $switching = "HTTP/1.1 101 Switching Protocols\r\nConnection: Upgrade\r\nUpgrade: h2c\r\n\r\n";
        write_all($out, $switching);
        note_lines('< ', $switching);
        serve_http2($in, $out, '', 1);
        return;
    }
    serve_http11_origin($in, $out, $request);
}

# The http/1.1 fallback: a CONNECT answered with a status line, then the tunnelled request.
sub serve_http11 {
    my ($in, $out) = @_;
    my $connect = read_header_block($in);
    if (!defined $connect) { note('= curl hung up before its CONNECT ended'); return; }
    note_lines('> ', $connect);
    my $status = $config{status};
    my $refused = int($status / 100) != 2;
    my $reason = $status == 200 ? 'Connection established' : $status == 407 ? 'Proxy Authentication Required' : 'Status';
    my $reply = "HTTP/1.1 $status $reason\r\n" . ($refused ? "Content-Length: 0\r\n" : '') . "\r\n";
    write_all($out, $reply);
    note_lines('< ', $reply);
    if ($refused) { wait_for_hang_up($in); return; }
    my $request = read_header_block($in);
    if (!defined $request) { note('= curl hung up before its tunnelled request ended'); return; }
    note_lines('>   ', $request);
    write_all($out, $config{tunnelResponse});
    note('<   ' . escape($config{tunnelResponse}));
    wait_for_hang_up($in);
}

# The protocol s_server negotiated, read from the ServerHello in its message file once both
# Finished messages are in; undef when no handshake completes before the recorder stops.
sub negotiated_protocol {
    my ($in) = @_;
    my $select = IO::Select->new($in);
    my ($messages, $readable) = ('', 0);
    while (1) {
        if (open(my $file, '<', $messageFile)) { local $/; $messages = <$file>; close $file; }
        last if $messages =~ /^<<< [^\n]*Finished/m && $messages =~ /^>>> [^\n]*Finished/m;
        return undef if $stopping;
        # Bytes or the end of the stream before a Finished: give the message file a second.
        if ($select->can_read(0.02) && ++$readable > 50) { return undef; }
    }
    return 'none' unless $messages =~ /^>>> [^\n]*ServerHello[^\n]*\n((?:[ \t]+[0-9A-Fa-f ]+\n)+)/m;
    my $hex = lc($1);
    $hex =~ s/\s//g;
    return 'h2' if index($hex, '001000050003026832') >= 0;
    return 'http/1.1' if index($hex, '0010000b000908687474702f312e31') >= 0;
    return 'none';
}

sub serve_client {
    my ($in, $out) = @_;
    if (length($config{origin}) > 0 && $config{origin} ne 'alpn') { serve_plain_origin($in, $out); return; }
    my $negotiated = negotiated_protocol($in);
    if (!defined $negotiated) { note('= curl opened no connection'); return; }
    note('= TLS handshake completed, ALPN ' . $negotiated);
    if ($negotiated eq 'h2') { serve_http2($in, $out, '', 0); }
    elsif (length($config{origin}) > 0) { serve_http11_origin($in, $out, read_header_block($in)); }
    else { serve_http11($in, $out); }
}

# Copies bytes both ways between curl's connection and s_server's Unix socket; the end of
# one side's stream shuts down sending to the other, until both have ended.
sub relay_to_tls_server {
    my ($client, $socketPath) = @_;
    my $server = IO::Socket::UNIX->new(Peer => $socketPath) or return;
    binmode $server;
    my $select = IO::Select->new($client, $server);
    while ($select->count) {
        foreach my $from ($select->can_read) {
            my $to = $from == $client ? $server : $client;
            my $read = sysread($from, my $chunk, 65536);
            if ($read) { write_all($to, $chunk); next; }
            $select->remove($from);
            shutdown($to, 1);
        }
    }
}

# One connection of a reset run over TLS: an s_server of its own takes it on a Unix socket,
# its plaintext on two pipes, while a relay process copies curl's bytes to and from that socket.
# It offers h2 and http/1.1, as the PowerShell script does for a reset run, so curl's retry over
# HTTP/1.1 is answered.
sub serve_tls_connection {
    my ($client, $number) = @_;
    my $socketPath = "$state/h2.$number.sock";
    $messageFile = "$state/h2.$number.msg";
    pipe(my $fromServer, my $serverOut) or die "pipe: $!";
    pipe(my $serverIn, my $toServer) or die "pipe: $!";
    my $server = fork();
    if (!$server) {
        open(STDIN, '<&', $serverIn); open(STDOUT, '>&', $serverOut); open(STDERR, '>>', "$state/h2.tls.err");
        exec($config{openssl}, 's_server', '-quiet', '-naccept', '1', '-tls1_2', '-alpn', 'h2,http/1.1', '-unix', $socketPath,
            '-cert', "$state/server.crt", '-key', "$state/server.key", '-msg', '-msgfile', $messageFile) or POSIX::_exit(127);
    }
    close $serverIn;
    close $serverOut;
    for (my $polls = 0; !-S $socketPath && $polls < 250; $polls++) { select(undef, undef, undef, 0.02); }
    my $relay = fork();
    if (!$relay) { relay_to_tls_server($client, $socketPath); POSIX::_exit(0); }
    close $client;
    binmode $fromServer;
    binmode $toServer;
    serve_client($fromServer, $toServer);
    close $toServer;
    kill 'KILL', $server, $relay;
    waitpid($server, 0);
    waitpid($relay, 0);
}

sub finish { open(my $done, '>', "$state/h2.done"); close $done; exit 0; }

# --- Serving ------------------------------------------------------------------------------

open(my $responseFile, '<:raw', "$state/response.0.bin") or die "response.0.bin: $!";
{ local $/; $config{tunnelResponse} = <$responseFile>; }
close $responseFile;
$config{tunnelResponse} = '' unless defined $config{tunnelResponse};
binmode STDIN;
binmode STDOUT;

if ($config{tls} && $config{reset} < 0) {
    # s_server owns the port; the connection's plaintext is on stdin and stdout.
    serve_client(\*STDIN, \*STDOUT);
    finish();
}

my $listener = IO::Socket::IP->new(LocalAddr => $config{address}, LocalPort => $config{port}, Listen => 16, ReuseAddr => 1, Proto => 'tcp');
if (!$listener) { open(my $failure, '>', "$state/h2.error"); print $failure "cannot listen on $config{address}:$config{port}: $@"; close $failure; exit 1; }
open(my $ready, '>', "$state/h2.ready"); close $ready;
my $select = IO::Select->new($listener);
my @children;
for (my $number = 1; ; $number++) {
    my $client;
    while (!$stopping && !$client) { $client = $listener->accept if $select->can_read(0.1); }
    if (!$client) { note('= curl opened no connection') if $number == 1; last; }
    binmode $client;
    if ($config{reset} < 0) { serve_client($client, $client); close $client; last; }
    note("= connection $number");
    my $child = fork();
    if (!$child) {
        $listener->close;
        if ($config{tls}) { serve_tls_connection($client, $number); } else { serve_client($client, $client); }
        POSIX::_exit(0);
    }
    close $client;
    push @children, $child;
}
# Each connection of a reset run is on a process of its own; give them a moment to end.
for (my $waited = 0; @children && $waited < 300; $waited++) {
    @children = grep { waitpid($_, POSIX::WNOHANG()) == 0 } @children;
    select(undef, undef, undef, 0.02) if @children;
}
finish();
RCE_H2_PERL
    sed -i "s|@@HUFFMAN@@|$RCE_HPACK_HUFFMAN|" "$RCE_STATE/h2.pl"
    {
        printf 'address=%s\n' "$RCE_LISTEN_ADDRESS"
        printf 'port=%s\n' "$RCE_PORT"
        printf 'tls=%s\n' "$RCE_TLS"
        printf 'origin=%s\n' "$RCE_HTTP2_ORIGIN"
        printf 'status=%s\n' "$RCE_PROXY_STATUS"
        printf 'reset=%s\n' "$RCE_HTTP2_RESET_CODE"
        printf 'omitsettings=%s\n' "$RCE_HTTP2_NO_SETTINGS"
        printf 'answer=%s\n' "$RCE_HTTP2_ANSWER"
        printf 'openssl=%s\n' "$RCE_OPENSSL"
    } >"$RCE_STATE/h2.conf"
    : >"$RCE_STATE/h2.log"
    : >"$RCE_STATE/h2.received"
    if [ $RCE_TLS = 1 ] && [ "$RCE_HTTP2_RESET_CODE" -lt 0 ]; then
        # socat has no ALPN option, so s_server takes the one connection and offers the
        # protocol; perl serves its plaintext through two FIFOs, opened in the same order
        # on both sides.
        mkfifo "$RCE_STATE/h2.out" "$RCE_STATE/h2.in" || rce_die 'mkfifo failed'
        case $RCE_LISTEN_ADDRESS in *:*) rce_accept="[$RCE_LISTEN_ADDRESS]:$RCE_PORT" ;; *) rce_accept="$RCE_LISTEN_ADDRESS:$RCE_PORT" ;; esac
        rce_alpn=$RCE_PROXY_ALPN
        "$RCE_OPENSSL" s_server -quiet -naccept 1 -tls1_2 -alpn "$rce_alpn" -accept "$rce_accept" \
            -cert "$RCE_STATE/server.crt" -key "$RCE_STATE/server.key" -msg -msgfile "$RCE_STATE/h2.msg" \
            >"$RCE_STATE/h2.out" <"$RCE_STATE/h2.in" 2>"$RCE_STATE/h2.tls.err" &
        printf '%s\n' "$!" >"$RCE_STATE/tls-pid.h2"
        perl "$RCE_STATE/h2.pl" "$RCE_STATE" <"$RCE_STATE/h2.out" >"$RCE_STATE/h2.in" 2>"$RCE_STATE/h2.err" &
        RCE_H2_PID=$!
        rce_wait_for_listening_port "$RCE_PORT" || rce_die "openssl s_server did not start listening on port $RCE_PORT: $(cat "$RCE_STATE/h2.tls.err" "$RCE_STATE/h2.err" 2>/dev/null)"
    else
        # Plain TCP, or a reset run over TLS: perl listens and serves every connection, over
        # TLS each through an s_server of its own.
        perl "$RCE_STATE/h2.pl" "$RCE_STATE" </dev/null >/dev/null 2>"$RCE_STATE/h2.err" &
        RCE_H2_PID=$!
        rce_polls=0
        while [ ! -e "$RCE_STATE/h2.ready" ]; do
            if ! kill -0 "$RCE_H2_PID" 2>/dev/null || [ $rce_polls -ge 250 ]; then
                rce_die "the HTTP/2 server did not start: $(cat "$RCE_STATE/h2.error" "$RCE_STATE/h2.err" 2>/dev/null | head -n 3)"
            fi
            rce_polls=$((rce_polls + 1))
            sleep 0.02
        done
    fi
    printf '%s\n' "$RCE_H2_PID" >"$RCE_STATE/pid.h2"
fi

if [ $RCE_USE_SOCAT = 1 ]; then
    if [ -n "$RCE_UNIX_SOCKET" ]; then
        case $RCE_UNIX_SOCKET in /*) ;; *) RCE_UNIX_SOCKET="$(pwd)/$RCE_UNIX_SOCKET" ;; esac
        case $RCE_UNIX_SOCKET in *[:,]*) rce_die "-UnixSocket '$RCE_UNIX_SOCKET' holds a colon or comma, which socat's address cannot take." ;; esac
        rm -f "$RCE_UNIX_SOCKET"
        rce_listen="UNIX-LISTEN:$RCE_UNIX_SOCKET,fork"
    else
        case $RCE_LISTEN_ADDRESS in
            *:*) rce_listen="TCP6-LISTEN:$RCE_PORT,bind=[$RCE_LISTEN_ADDRESS],reuseaddr,fork" ;;
            *) rce_listen="TCP4-LISTEN:$RCE_PORT,bind=$RCE_LISTEN_ADDRESS,reuseaddr,fork" ;;
        esac
        # A zero linger time makes the close send RST instead of FIN; accepted sockets
        # inherit it from the listening one.
        if [ $RCE_RESET = 1 ] || [ $RCE_RESET_AFTER = 1 ]; then rce_listen="$rce_listen,so-linger=0"; fi
    fi
    # nofork: each accepted connection's socat process execs the handler on the socket
    # itself, with no relay between them.
    "$RCE_SOCAT" -d -d -lf "$RCE_STATE/socat.log" "$rce_listen" "EXEC:$RCE_STATE/connection,nofork" </dev/null >/dev/null 2>&1 &
    RCE_LISTENER_PID=$!
    rce_polls=0
    while ! grep -q 'listening on' "$RCE_STATE/socat.log" 2>/dev/null; do
        if ! kill -0 "$RCE_LISTENER_PID" 2>/dev/null || [ $rce_polls -ge 250 ]; then
            rce_die "socat did not start listening: $(grep -E ' [EF] ' "$RCE_STATE/socat.log" 2>/dev/null | head -n 3)"
        fi
        rce_polls=$((rce_polls + 1))
        sleep 0.02
    done
elif [ $RCE_NO_SERVER = 0 ]; then
    # One TLS connection, no socat: s_server takes it on the port itself.
    . "$RCE_STATE/config.sh"
    mkdir "$RCE_STATE/claim.0"
    case $RCE_LISTEN_ADDRESS in *:*) rce_accept="[$RCE_LISTEN_ADDRESS]:$RCE_PORT" ;; *) rce_accept="$RCE_LISTEN_ADDRESS:$RCE_PORT" ;; esac
    (exec 2>>"$RCE_STATE/handler.err"; rce_serve_tls_connection 0 -accept "$rce_accept") </dev/null >/dev/null &
    printf '%s\n' "$!" >"$RCE_STATE/pid.0"
    rce_wait_for_listening_port "$RCE_PORT" || rce_die "openssl s_server did not start listening on port $RCE_PORT: $(cat "$RCE_STATE"/tls.0.err 2>/dev/null)"
fi

# --- curl -----------------------------------------------------------------------------

# Samples curl's peak resident set into RCE_PEAK_BYTES and counts RCE_SAMPLES.
rce_sample_peak() {
    local pid=$1 key value unit
    if [ -r "/proc/$pid/status" ]; then
        while read -r key value unit; do
            if [ "$key" = 'VmHWM:' ]; then
                if [ $((value * 1024)) -gt "$RCE_PEAK_BYTES" ]; then RCE_PEAK_BYTES=$((value * 1024)); fi
                RCE_SAMPLES=$((RCE_SAMPLES + 1))
                return
            fi
        done <"/proc/$pid/status" 2>/dev/null
    elif [ ! -d /proc ]; then
        value=$(ps -o rss= -p "$pid" 2>/dev/null | tr -d ' ')
        case $value in '' | *[!0-9]*) return ;; esac
        if [ $((value * 1024)) -gt "$RCE_PEAK_BYTES" ]; then RCE_PEAK_BYTES=$((value * 1024)); fi
        RCE_SAMPLES=$((RCE_SAMPLES + 1))
    fi
}

RCE_PEAK_BYTES=0
RCE_SAMPLES=0
rce_now_ms
rce_started=$RCE_NOW_MS
(
    # curl runs in the caller's locale, not the C locale the recorder reads bytes in.
    if [ -n "$RCE_CALLER_LC_ALL_SET" ]; then export LC_ALL="$RCE_CALLER_LC_ALL"; else unset LC_ALL; fi
    exec "$RCE_CURL" ${CURL_ARGS[@]+"${CURL_ARGS[@]}"}
) <"$RCE_STATE/stdin.bin" >"$RCE_STATE/stdout.bin" 2>"$RCE_STATE/stderr.txt" &
rce_curl_pid=$!
while kill -0 "$rce_curl_pid" 2>/dev/null; do
    rce_sample_peak "$rce_curl_pid"
    sleep 0.01
done
rce_now_ms
rce_elapsed=$((RCE_NOW_MS - rce_started))
wait "$rce_curl_pid"
rce_exit_code=$?

# --- Stopping the TFTP responder ------------------------------------------------------

if [ -n "$RCE_DNS_PID$RCE_SINK_PID" ]; then
    # A datagram sent as curl exits is still being logged: give that a moment too.
    sleep 0.2
    kill $RCE_DNS_PID $RCE_SINK_PID 2>/dev/null
    wait $RCE_DNS_PID $RCE_SINK_PID 2>/dev/null
    RCE_DNS_PID='' RCE_SINK_PID=''
fi
if [ $RCE_TFTP = 1 ]; then
    # The responder logs a datagram as it handles it, and curl may exit as it sends its last
    # one: give that a moment, then end a transfer still waiting.
    sleep 0.2
    kill "$RCE_TFTP_PID" 2>/dev/null
    wait "$RCE_TFTP_PID" 2>/dev/null
fi
if [ $RCE_HTTP2 = 1 ]; then
    # Give the connection a moment to finish, then stop waiting for one, as the PowerShell
    # script does (2 s, then up to 6 s for the server to end).
    rce_polls=0
    while [ ! -e "$RCE_STATE/h2.done" ] && kill -0 "$RCE_H2_PID" 2>/dev/null && [ $rce_polls -lt 100 ]; do
        rce_polls=$((rce_polls + 1))
        sleep 0.02
    done
    if [ ! -e "$RCE_STATE/h2.done" ]; then
        kill -TERM "$RCE_H2_PID" 2>/dev/null
        rce_polls=0
        while kill -0 "$RCE_H2_PID" 2>/dev/null && [ $rce_polls -lt 300 ]; do
            rce_polls=$((rce_polls + 1))
            sleep 0.02
        done
    fi
    kill -KILL "$RCE_H2_PID" 2>/dev/null
    wait "$RCE_H2_PID" 2>/dev/null
    RCE_H2_PID=''
    if [ -s "$RCE_STATE/h2.err" ]; then
        printf '%s: the server reported:\n' "$RCE_NAME" >&2
        cat "$RCE_STATE/h2.err" >&2
    fi
fi

if [ -n "$RCE_SCRIPT" ]; then
    # Give the session a moment to finish, then stop waiting for one (2 s, then up to 6 s).
    rce_polls=0
    while [ ! -e "$RCE_STATE/script.done" ] && kill -0 "$RCE_SCRIPT_PID" 2>/dev/null && [ $rce_polls -lt 100 ]; do
        rce_polls=$((rce_polls + 1))
        sleep 0.02
    done
    if [ ! -e "$RCE_STATE/script.done" ]; then
        kill -TERM "$RCE_SCRIPT_PID" 2>/dev/null
        rce_polls=0
        while kill -0 "$RCE_SCRIPT_PID" 2>/dev/null && [ $rce_polls -lt 300 ]; do
            rce_polls=$((rce_polls + 1))
            sleep 0.02
        done
    fi
    kill -KILL "$RCE_SCRIPT_PID" 2>/dev/null
    wait "$RCE_SCRIPT_PID" 2>/dev/null
    RCE_SCRIPT_PID=''
    if [ -s "$RCE_STATE/script.err" ]; then
        printf '%s: the server reported:\n' "$RCE_NAME" >&2
        cat "$RCE_STATE/script.err" >&2
    fi
fi

# --- Stopping the server --------------------------------------------------------------

if [ $RCE_NO_SERVER = 0 ]; then
    # A connection curl did not open would keep the listener waiting; stopping it ends the
    # wait. Give accepted connections a moment to finish first.
    rce_polls=0
    while [ $rce_polls -lt 100 ]; do
        rce_done=0
        rce_index=0
        while [ $rce_index -lt "$RCE_CONNECTIONS" ]; do
            if [ -e "$RCE_STATE/done.$rce_index" ]; then rce_done=$((rce_done + 1)); fi
            rce_index=$((rce_index + 1))
        done
        if [ $rce_done -ge "$RCE_CONNECTIONS" ]; then break; fi
        rce_polls=$((rce_polls + 1))
        sleep 0.02
    done
    if [ -n "$RCE_LISTENER_PID" ]; then
        kill "$RCE_LISTENER_PID" 2>/dev/null
        wait "$RCE_LISTENER_PID" 2>/dev/null
        RCE_LISTENER_PID=''
RCE_DATA_LISTENER_PID=''
    fi
    : >"$RCE_STATE/stop"
    # Every connection accepted is served to its end, as the PowerShell script waits for
    # its server; a handler that died without finishing counts as finished.
    rce_index=0
    while [ $rce_index -lt "$RCE_CONNECTIONS" ]; do
        if [ -d "$RCE_STATE/claim.$rce_index" ]; then
            while [ ! -e "$RCE_STATE/done.$rce_index" ]; do
                rce_pid=$(cat "$RCE_STATE/pid.$rce_index" 2>/dev/null)
                rce_tls_pid=$(cat "$RCE_STATE/tls-pid.$rce_index" 2>/dev/null)
                if ! { [ -n "$rce_pid" ] && kill -0 "$rce_pid" 2>/dev/null; } && ! { [ -n "$rce_tls_pid" ] && kill -0 "$rce_tls_pid" 2>/dev/null; }; then
                    sleep 0.1
                    break
                fi
                sleep 0.02
            done
        fi
        rce_index=$((rce_index + 1))
    done
    if [ -s "$RCE_STATE/handler.err" ]; then
        printf '%s: the server reported:\n' "$RCE_NAME" >&2
        cat "$RCE_STATE/handler.err" >&2
    fi
fi

# --- The fixtures ---------------------------------------------------------------------

if [ $RCE_NO_SERVER = 0 ]; then
    rce_index=0
    {
        while [ $rce_index -lt "$RCE_CONNECTIONS" ]; do
            if [ -f "$RCE_STATE/request.$rce_index.bin" ]; then cat "$RCE_STATE/request.$rce_index.bin"; fi
            rce_index=$((rce_index + 1))
        done
    } >"$RCE_OUT_DIRECTORY/request.bin"
fi
if [ $RCE_HTTP2 = 1 ]; then
    cat "$RCE_STATE/h2.received" >"$RCE_OUT_DIRECTORY/request.bin"
    cat "$RCE_STATE/h2.log" >"$RCE_OUT_DIRECTORY/transcript.txt"
fi
if [ -n "$RCE_SCRIPT" ]; then
    cat "$RCE_STATE/script.received" >"$RCE_OUT_DIRECTORY/request.bin"
    cat "$RCE_STATE/script.log" >"$RCE_OUT_DIRECTORY/transcript.txt"
fi
if [ $RCE_LINE_SESSION = 1 ]; then cat "$RCE_STATE/transcript.txt" >"$RCE_OUT_DIRECTORY/transcript.txt"; fi
if [ $RCE_FTP = 1 ]; then cat "$RCE_STATE/upload.bin" >"$RCE_OUT_DIRECTORY/upload.bin"; fi
if [ $RCE_TFTP = 1 ]; then
    cat "$RCE_STATE/tftp.log" >"$RCE_OUT_DIRECTORY/datagrams.txt"
    cat "$RCE_STATE/tftp.upload" >"$RCE_OUT_DIRECTORY/upload.bin"
fi
if [ $RCE_UDP_SINK = 1 ]; then cat "$RCE_STATE/udp.log" >"$RCE_OUT_DIRECTORY/datagrams.txt"; fi
if [ -n "$RCE_DNS_PORTS$RCE_DOH_PORTS" ]; then cat "$RCE_STATE/dns.log" >"$RCE_OUT_DIRECTORY/dns.txt"; fi
cat "$RCE_STATE/stdout.bin" >"$RCE_OUT_DIRECTORY/stdout.bin"
cat "$RCE_STATE/stderr.txt" >"$RCE_OUT_DIRECTORY/stderr.txt"
printf '%s' "$rce_exit_code" >"$RCE_OUT_DIRECTORY/exitcode.txt"

rce_executable=$RCE_CURL
if [ -f "$RCE_CURL" ]; then
    rce_executable="$(cd "$(dirname "$RCE_CURL")" && pwd)/$(basename "$RCE_CURL")"
else
    rce_executable=$(command -v "$RCE_CURL")
fi
rce_executable=${rce_executable//\\/\\\\}
rce_executable=${rce_executable//\"/\\\"}
printf '{"executable":"%s","elapsedMilliseconds":%d,"peakWorkingSetBytes":%d,"samples":%d}' \
    "$rce_executable" "$rce_elapsed" "$RCE_PEAK_BYTES" "$RCE_SAMPLES" >"$RCE_OUT_DIRECTORY/timing.json"

printf 'curl exited %s after %s ms; fixtures written to %s\n' "$rce_exit_code" "$rce_elapsed" "$RCE_OUT_DIRECTORY"
