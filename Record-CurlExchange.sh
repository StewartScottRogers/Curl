#!/usr/bin/env bash
# SYNOPSIS
#     Records one real curl exchange against a canned loopback server, as test fixtures:
#     the request bytes curl sent, its stdout, its stderr and its exit code.
#
#     The Bash port, for Linux and macOS, of Record-CurlExchange.ps1. It writes the same
#     files in the same formats, so a fixture recorded by either is read the same way.
#     Phase 1 of the port: the HTTP and HTTPS (-Tls) server, -NoServer and -UnixSocket.
#
# NOT PORTED YET
#     These modes of Record-CurlExchange.ps1 are not in this port. Each one stops at once,
#     printing "Record-CurlExchange.sh: mode <X> is not ported yet; use
#     Record-CurlExchange.ps1 under pwsh" to stderr, and exits 2:
#
#       -Ftp (FTP and FTPS sessions, AUTH, PROT, CCC, active mode)  -Smtp   -Imap   -Pop3
#       -Tftp            -Script (scripted binary sessions, LDAP and SMB)
#       -Http2 and -Http2Origin (HTTP/2 proxy and origin framing, HPACK)
#       -UdpSink         -DnsPort and -DohPort (the DNS and DNS-over-HTTPS responders)
#       -HalfCloseAfterResponse together with -Tls
#
#     Their parameters (FtpReply, SmtpReply, ..., DnsAnswerAddress) are still accepted, and
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
#         Plain HTTP only in this port.
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

rce_not_ported() {
    printf '%s: mode %s is not ported yet; use Record-CurlExchange.ps1 under pwsh\n' "$RCE_NAME" "$1" >&2
    exit 2
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

rce_serve_connection() {
    local index=$1 answered=0
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
    if [ "$RCE_HALF_CLOSE" = 1 ]; then "$RCE_SOCAT" -u OPEN:/dev/null FD:1,shut-down; fi
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
        exec "$RCE_SOCAT" -lf "$RCE_STATE/relay.$index.log" -t 0 STDIO "UNIX-CONNECT:$socket"
    fi
    trap '' PIPE
    rce_serve_connection "$index"
    : >"$RCE_STATE/done.$index"
    exit 0
}

if [ "${1:-}" = '--rce-connection' ]; then
    RCE_STATE=$2
    . "$RCE_STATE/config.sh"
    rce_connection_entry
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
RCE_FTP=0 RCE_SMTP=0 RCE_IMAP=0 RCE_POP3=0 RCE_TFTP=0 RCE_SCRIPT='' RCE_HTTP2=0 RCE_HTTP2_ORIGIN=''
RCE_HTTP2_ANSWER='' RCE_UDP_SINK=0 RCE_DNS_PORTS='' RCE_DNS_SILENT_PORTS='' RCE_DOH_PORTS=''

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
        tftpnooack | http2nosettings | dnstruncate) shift; continue ;;
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
        http2answer) RCE_HTTP2_ANSWER=$value ;;
        dnsport) RCE_DNS_PORTS="$RCE_DNS_PORTS $value" ;;
        dnssilentport) RCE_DNS_SILENT_PORTS="$RCE_DNS_SILENT_PORTS $value" ;;
        dohport) RCE_DOH_PORTS="$RCE_DOH_PORTS $value" ;;
        ftpreply | ftpdata | ftpidlemilliseconds | ftpdataholdmilliseconds | smtpreply | smtpidlemilliseconds | \
            imapreply | imapmessage | imapidlemilliseconds | pop3reply | pop3message | pop3idlemilliseconds | \
            tftpdata | tftpreply | tftpidlemilliseconds | scriptidlemilliseconds | scriptgapmilliseconds | \
            proxystatus | proxyalpn | http2resetcode | dnsansweraddress | dnsresponsecode | dnsechconfiglist) ;;
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
if [ $RCE_FTP = 1 ]; then rce_not_ported -Ftp; fi
if [ $RCE_SMTP = 1 ]; then rce_not_ported -Smtp; fi
if [ $RCE_IMAP = 1 ]; then rce_not_ported -Imap; fi
if [ $RCE_POP3 = 1 ]; then rce_not_ported -Pop3; fi
if [ $RCE_TFTP = 1 ]; then rce_not_ported -Tftp; fi
if [ -n "$RCE_SCRIPT" ]; then rce_not_ported -Script; fi
if [ -n "$RCE_HTTP2_ORIGIN" ]; then rce_not_ported -Http2Origin; fi
if [ $RCE_HTTP2 = 1 ]; then rce_not_ported -Http2; fi
if [ $RCE_UDP_SINK = 1 ]; then rce_not_ported -UdpSink; fi
if [ -n "$RCE_DNS_PORTS" ]; then rce_not_ported -DnsPort; fi
if [ -n "$RCE_DOH_PORTS" ]; then rce_not_ported -DohPort; fi
if [ $RCE_HALF_CLOSE = 1 ] && [ $RCE_TLS = 1 ]; then rce_not_ported '-HalfCloseAfterResponse with -Tls'; fi

# One connection per Response entry unless -Connections says otherwise, so a --retry
# attempt after a 503 is answered with the next entry.
if [ $RCE_CONNECTIONS_GIVEN = 0 ] && [ "$RCE_ANSWER_HELD" -eq 0 ]; then RCE_CONNECTIONS=${#RESPONSES[@]}; fi
RCE_RESPONSE_COUNT=${#RESPONSES[@]}

# --- Tools ----------------------------------------------------------------------------

if [ -z "$RCE_CURL" ]; then RCE_CURL=curl; fi
RCE_OPENSSL=${OPENSSL:-openssl}
RCE_SOCAT=${SOCAT:-socat}
# socat is the listener, except for one TLS connection with no reset, which s_server
# takes on the port itself.
RCE_USE_SOCAT=0
if [ $RCE_NO_SERVER = 0 ]; then
    if [ $RCE_TLS = 0 ] || [ "$RCE_CONNECTIONS" -gt 1 ] || [ $RCE_RESET = 1 ] || [ $RCE_RESET_AFTER = 1 ]; then RCE_USE_SOCAT=1; fi
fi
rce_missing=''
for rce_tool in awk od mktemp sleep wc tr cat; do
    command -v "$rce_tool" >/dev/null 2>&1 || rce_missing="$rce_missing, $rce_tool"
done
if [ $RCE_USE_SOCAT = 1 ] && ! command -v "$RCE_SOCAT" >/dev/null 2>&1; then
    rce_missing="$rce_missing, socat (the listener for plain HTTP, -UnixSocket, -Reset, -ResetAfterResponse and several -Tls connections; set SOCAT to name it)"
fi
if [ $RCE_TLS = 1 ]; then
    command -v mkfifo >/dev/null 2>&1 || rce_missing="$rce_missing, mkfifo"
    if ! command -v "$RCE_OPENSSL" >/dev/null 2>&1; then
        rce_missing="$rce_missing, openssl (OpenSSL 1.1.1 or later, for -Tls; set OPENSSL to name it)"
    elif ! "$RCE_OPENSSL" s_server -help 2>&1 | grep -q -- '-msgfile' || ! "$RCE_OPENSSL" s_server -help 2>&1 | grep -q -- '-naccept'; then
        rce_missing="$rce_missing, an openssl whose s_server has -msgfile and -naccept ($RCE_OPENSSL has not: LibreSSL? set OPENSSL to an OpenSSL 1.1.1 or later)"
    fi
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
                if [ $RCE_TLS = 1 ]; then rce_die "-Tls needs a Unix openssl, and $(command -v "$RCE_OPENSSL") is a native Windows program, which cannot read this script's FIFOs; use Record-CurlExchange.ps1 on Windows."; fi
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

rce_cleanup() {
    : >"$RCE_STATE/stop" 2>/dev/null
    if [ -n "$RCE_LISTENER_PID" ]; then kill "$RCE_LISTENER_PID" 2>/dev/null; fi
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

if [ $RCE_TLS = 1 ]; then
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
    RCE_OPENSSL RCE_SOCAT; do
    eval "printf '%s=%q\n' \"\$rce_variable\" \"\$$rce_variable\""
done >"$RCE_STATE/config.sh"
printf '#!%s\nexec %q %q --rce-connection %q\n' "$BASH" "$BASH" "$(cd "$(dirname "$0")" && pwd)/$(basename "$0")" "$RCE_STATE" >"$RCE_STATE/connection"
chmod +x "$RCE_STATE/connection"

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
