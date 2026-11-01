#!/usr/bin/env bash
#
# NAME
#     Measure-CodeQuality.sh - the Linux and macOS twin of Measure-CodeQuality.ps1.
#
# SYNOPSIS
#     Measures line coverage, branch coverage, cyclomatic complexity and CRAP score for
#     every production library in the Curl solution, and fails if any member is outside
#     the thresholds.
#
# DESCRIPTION
#     Everything here comes out of tooling the solution already has, because Curl takes
#     no packages beyond MSTest (CLAUDE.md), and out of tools every Linux and macOS box
#     has - bash (3.2 or later), dotnet, find, sort, sed, grep and awk. It runs no
#     PowerShell, no Python and no C#: the Cobertura reports are parsed with awk.
#
#       * Coverage - Microsoft.Testing.Extensions.CodeCoverage, which the MSTest
#         meta-package already pulls into every .UnitTests project. It is asked for
#         Cobertura, whose <method> elements carry line-rate, branch-rate and a
#         complexity attribute.
#       * Complexity - that same Cobertura complexity attribute. The build itself is
#         gated separately by the SDK's CA1502 analyzer at the threshold in
#         CodeMetricsConfig.txt. CA1502 measures the source and Cobertura measures the
#         compiled method, so on a method full of pattern matching the two can differ by
#         one or two. CA1502 is the one that breaks the build; this is the one that
#         feeds CRAP, because it arrives in the same file as the coverage it pairs with.
#       * CRAP - produced by no tool at all, so it is computed here:
#             CRAP(m) = comp(m)^2 * (1 - cov(m))^3 + comp(m)
#         At full coverage the middle term vanishes and CRAP is just the complexity,
#         which is why the default CRAP limit of 30 only ever bites on a method that is
#         both complex and undertested.
#
#     A library covered by more than one test project is merged line by line, taking the
#     best hit count and the best condition count seen for each line, so a helper in
#     Curl.Protocol.Abstractions.UnitLibrary exercised from another protocol's tests is
#     credited for it.
#
#     A whole-solution run builds Curl.slnx once and then runs dotnet test --no-build on
#     each *.UnitTests project in turn, one Cobertura report per project. It never runs
#     dotnet test on Curl.slnx itself, whose single merged report carried no branch counts
#     on CI (BL-2466).
#
#     A whole-solution run that leaves any member short of line or branch coverage runs
#     the *.UnitTests projects that reach that member's library again, one at a time, into
#     a recheck folder under ResultsDirectory, and merges their reports in the same way
#     before scoring again. A whole-solution run once lost one project's coverage although
#     its tests passed (BL-2269); the recheck restores it, and a real gap still fails.
#
#     Parameters take the .ps1's names, matched without regard to case, with one or two
#     leading dashes and the value as the next argument or after = or : (-Library A,B,
#     --library=A, -ShardCount 3). A bare argument is a -Library name. Exit code 0 means
#     every gate passed, 1 means a member or library failed one or the run itself failed.
#
# PARAMETERS
#     -Library <names>
#         Assembly names to report on, wildcards allowed, comma-separated or repeated.
#         Defaults to every production assembly - anything named *.UnitLibrary, plus
#         Curl.Console, Curl.Api, Curl.Mcp and Libcurl. Curl.Console's assembly is named
#         curl, and is reported and matched here as Curl.Console.
#
#         With -Library, the tests that run are only the *.UnitTests projects whose
#         project references reach a named library, directly or through other projects -
#         the only tests that can execute its code - one dotnet test per project into the
#         same results directory (BL-1318). With -IncludeIntegration as well, the
#         *.IntegrationTests projects that reach a named library run too; without it they
#         are left out, since the Integration filter would skip every test they hold
#         (ADR-0421, BL-1605). Without -Library, or when no test project reaches the
#         names, the whole solution runs.
#
#         Test assemblies of both kinds, *.UnitTests and *.IntegrationTests, are never
#         reported as production code, and no file under either kind of project is listed
#         among the coverage exclusions in production code.
#
#     -Shard <n>
#         With -ShardCount, collect coverage for one shard of a whole-solution run and
#         stop: the *.UnitTests projects are split into -ShardCount shards (see
#         shard_test_projects), and this run builds and tests only shard -Shard (1-based)
#         into ResultsDirectory, one folder per test project, beside a manifest named
#         shard-<Shard>-of-<ShardCount>.txt that lists the projects it ran. It scores
#         nothing, because a library's coverage is the merge of every test project that
#         reaches it and those may sit in other shards; a -FromShards run scores the
#         shards' reports together. A shard with no test project, or whose tests wrote no
#         Cobertura report, fails rather than passing having measured nothing (BL-3464).
#         The unit of a shard is a whole test project, never part of one: the merge keeps
#         the best condition count per line, not a union of branches, so splitting one
#         project's tests would change what it measures.
#
#     -ShardCount <n>
#         How many shards -Shard counts from. Splits the test projects by their measured
#         test-run seconds, so the shards are roughly even.
#
#     -FromShards
#         Score the reports that every shard of a -Shard run left under ResultsDirectory
#         (in any folder layout, such as one folder per downloaded CI artifact), instead
#         of running tests. Fails unless the shards' manifests name every shard of one
#         -ShardCount and every *.UnitTests project exactly once, and unless every gated
#         library - every production project in the checkout - is measured, so a library
#         is never silently dropped. A member short of line or branch coverage gets the
#         same recheck as a whole-solution run's.
#
#     -SelfTest
#         Check the shard split against this checkout and exit: for every shard count from
#         1 to 8 the shards hold every *.UnitTests project exactly once, none is empty,
#         the split does not depend on the order projects are listed in, and every gated
#         library is reached by at least one test project, so by some shard. Exits 1 on a
#         failed check.
#
#     -SkipTestRun
#         Reuse the Cobertura files already in ResultsDirectory instead of running tests.
#         Without -ResultsDirectory these are the reports this checkout's last run left in
#         its own default directory (see ResultsDirectory).
#
#     -ResultsDirectory <dir>
#         Where the Cobertura reports are written and read. Unless -SkipTestRun is given,
#         this directory is deleted and recreated before the tests run, and every
#         *.cobertura.xml under it is merged into the report.
#
#         Defaults to a directory that belongs to this checkout alone:
#         $TEMP (else $TMPDIR, else /tmp)/CurlCodeQuality/<checkout folder name>-<first 16
#         hex digits of the SHA-256 of the checkout's full path, lower-cased>. Each git
#         worktree therefore gets its own directory, so two runs from different checkouts
#         at the same time never delete or read each other's reports, and -SkipTestRun
#         still finds the reports of this checkout's last run. Under Git Bash on Windows
#         the path hashed is the Windows form (Z:\repos\Curl), so the .ps1 and this script
#         share one default directory there. Two runs from the same checkout at the same
#         time still share it.
#
#         An explicit value is used exactly as given.
#
#     -IncludeIntegration
#         Run the integration tests too: drop the TestCategory!=Integration filter and,
#         with -Library, also run the *.IntegrationTests projects that reach the named
#         libraries. Off by default, matching the fast test command in CLAUDE.md, which is
#         the one coverage is measured from (ADR-0421).
#
#     -LineThreshold <percent>         default 100
#     -BranchThreshold <percent>       default 100
#     -ComplexityThreshold <n>         default 10 (CA1502's value in CodeMetricsConfig.txt)
#     -CrapThreshold <score>           default 30
#     -MaxMissingLinesShown <n>        default 12
#         The gates and the report's uncovered-line limit - the same defaults as the
#         .ps1's parameters, which are where the .ps1 keeps them too.
#
#     -ReportPath <file>
#         Also write the Markdown report to this file.
#
#     -JsonPath <file>
#         Also write the measurements as JSON - totals, one row per library, every failing
#         member and every coverage exclusion - for the published coverage report that
#         .github/coverage/make-coverage-report.cs renders.
#
#     -Help, -h, -?
#         Print this help and exit.
#
# EXAMPLES
#     ./Measure-CodeQuality.sh
#     ./Measure-CodeQuality.sh -Library Curl.Original.Protocol.File.UnitLibrary -ReportPath report.md
#     ./Measure-CodeQuality.sh -Shard 2 -ShardCount 3 -ResultsDirectory "$TMPDIR/q/shard-2"
#     ./Measure-CodeQuality.sh -FromShards -ResultsDirectory "$TMPDIR/q" -JsonPath q.json
#
# DIFFERENCES FROM Measure-CodeQuality.ps1
#     Everything the .ps1 measures, gates, writes and exits with is ported. What differs,
#     and why:
#       * Names sort by byte value with case folded (LC_ALL=C sort -f), not by .NET's
#         culture-aware comparison, which no Unix tool reproduces. That changes only the
#         order rows and test runs are listed in, and which project wins a tie of equal
#         seconds when shards are dealt; a shard set made by this script is complete and
#         disjoint on its own, and -FromShards checks the manifests either way, so do not
#         mix shards made by the two scripts in one -FromShards run.
#       * The Cobertura reports are read by an awk tokenizer, not an XML parser. It
#         handles the reports Microsoft's coverage collector writes (any line layout, a
#         byte-order mark, the five predefined entities) but is not a general XML reader:
#         numeric character references (&#..;) are left as written and comments are not
#         stripped from the reports (the collector writes neither). Project files are read
#         the same way, with <!-- --> comments stripped.
#       * PowerShell's parameter-name abbreviation (-Lib for -Library) and its positional
#         binding beyond -Library are not supported; a bare argument is always a -Library
#         name.
#       * The JSON carries the same fields and types, but its whitespace follows
#         PowerShell 7's ConvertTo-Json rather than Windows PowerShell 5.1's, and paths use
#         the platform's separator, as the .ps1's do when run on Linux.
#       * Colours are ANSI escapes, written only when stdout is a terminal and NO_COLOR is
#         unset.
# ---- end of help

set -u

TAB=$(printf '\t')

# --- Parameters -------------------------------------------------------------------

LIBRARY_ARGUMENTS=""
SHARD=0
SHARD_COUNT=0
FROM_SHARDS=0
SELF_TEST=0
SKIP_TEST_RUN=0
INCLUDE_INTEGRATION=0
RESULTS_DIRECTORY=""
LINE_THRESHOLD=100
BRANCH_THRESHOLD=100
COMPLEXITY_THRESHOLD=10
CRAP_THRESHOLD=30
MAX_MISSING_LINES_SHOWN=12
REPORT_PATH=""
JSON_PATH=""

print_help() {
    # The header comment above, from line 3 to the end-of-help marker, without the "# ".
    sed -n '3,/^# ---- end of help/p' "$0" | sed '$d' | sed 's/^# \{0,1\}//'
}

fail_usage() {
    printf '%s\n' "$1" >&2
    printf 'Run %s -Help for the parameters.\n' "$0" >&2
    exit 1
}

while [ $# -gt 0 ]; do
    argument=$1
    shift
    case "$argument" in
        -*) ;;
        *) LIBRARY_ARGUMENTS="$LIBRARY_ARGUMENTS,$argument"; continue ;;
    esac
    name=${argument#-}
    name=${name#-}
    value=""
    has_value=0
    case "$name" in
        *=*|*:*) value=${name#*[=:]}; name=${name%%[=:]*}; has_value=1 ;;
    esac
    lower_name=$(printf '%s' "$name" | tr '[:upper:]' '[:lower:]')
    case "$lower_name" in
        library|shard|shardcount|resultsdirectory|linethreshold|branchthreshold|complexitythreshold|crapthreshold|maxmissinglinesshown|reportpath|jsonpath)
            if [ $has_value -eq 0 ]; then
                [ $# -gt 0 ] || fail_usage "Missing an argument for parameter '$name'."
                value=$1
                shift
            fi
            ;;
    esac
    case "$lower_name" in
        library) LIBRARY_ARGUMENTS="$LIBRARY_ARGUMENTS,$value" ;;
        shard) SHARD=$value ;;
        shardcount) SHARD_COUNT=$value ;;
        fromshards) FROM_SHARDS=1 ;;
        selftest) SELF_TEST=1 ;;
        skiptestrun) SKIP_TEST_RUN=1 ;;
        includeintegration) INCLUDE_INTEGRATION=1 ;;
        resultsdirectory) RESULTS_DIRECTORY=$value ;;
        linethreshold) LINE_THRESHOLD=$value ;;
        branchthreshold) BRANCH_THRESHOLD=$value ;;
        complexitythreshold) COMPLEXITY_THRESHOLD=$value ;;
        crapthreshold) CRAP_THRESHOLD=$value ;;
        maxmissinglinesshown) MAX_MISSING_LINES_SHOWN=$value ;;
        reportpath) REPORT_PATH=$value ;;
        jsonpath) JSON_PATH=$value ;;
        h|help|\?) print_help; exit 0 ;;
        *) fail_usage "A parameter cannot be found that matches parameter name '$name'." ;;
    esac
done

# The .ps1's [int] and [double] parameters refuse what is not a number; so does this.
is_integer() { printf '%s' "$1" | grep -Eq '^[+-]?[0-9]+$'; }
is_number() { printf '%s' "$1" | grep -Eq '^[+-]?([0-9]+\.?[0-9]*|\.[0-9]+)$'; }
for pair in "Shard=$SHARD" "ShardCount=$SHARD_COUNT" "ComplexityThreshold=$COMPLEXITY_THRESHOLD" "MaxMissingLinesShown=$MAX_MISSING_LINES_SHOWN"; do
    is_integer "${pair#*=}" || fail_usage "Cannot convert value \"${pair#*=}\" of parameter -${pair%%=*} to an integer."
done
for pair in "LineThreshold=$LINE_THRESHOLD" "BranchThreshold=$BRANCH_THRESHOLD" "CrapThreshold=$CRAP_THRESHOLD"; do
    is_number "${pair#*=}" || fail_usage "Cannot convert value \"${pair#*=}\" of parameter -${pair%%=*} to a number."
done

# A number as .NET prints a double: 100 not 100.0, 87.5 not 87.50.
format_number() { LC_ALL=C awk -v x="$1" 'BEGIN { printf "%.15g", x + 0 }'; }
LINE_THRESHOLD=$(format_number "$LINE_THRESHOLD")
BRANCH_THRESHOLD=$(format_number "$BRANCH_THRESHOLD")
CRAP_THRESHOLD=$(format_number "$CRAP_THRESHOLD")
SHARD=$((SHARD + 0))
SHARD_COUNT=$((SHARD_COUNT + 0))
COMPLEXITY_THRESHOLD=$((COMPLEXITY_THRESHOLD + 0))
MAX_MISSING_LINES_SHOWN=$((MAX_MISSING_LINES_SHOWN + 0))

# --- Console output ---------------------------------------------------------------

if [ -t 1 ] && [ -z "${NO_COLOR:-}" ]; then
    COLOR_CYAN=$(printf '\033[36m'); COLOR_DARK_CYAN=$(printf '\033[2;36m')
    COLOR_YELLOW=$(printf '\033[33m'); COLOR_GREEN=$(printf '\033[32m')
    COLOR_RED=$(printf '\033[31m'); COLOR_RESET=$(printf '\033[0m')
else
    COLOR_CYAN=""; COLOR_DARK_CYAN=""; COLOR_YELLOW=""; COLOR_GREEN=""; COLOR_RED=""; COLOR_RESET=""
fi

# Write-Host -ForegroundColor <colour>.
say() { printf '%s%s%s\n' "$1" "$2" "$COLOR_RESET"; }

# Write-Warning.
warn() { printf '%sWARNING: %s%s\n' "$COLOR_YELLOW" "$1" "$COLOR_RESET" >&2; }

# throw: the message on stderr and exit code 1, as an uncaught throw under -File gives.
die() { printf '%s%s%s\n' "$COLOR_RED" "$1" "$COLOR_RESET" >&2; exit 1; }

# --- Paths ------------------------------------------------------------------------

repository_root=$(cd "$(dirname "$0")" && pwd)

# Under Git Bash on Windows, dotnet and the coverage collector speak Windows paths
# (Z:\repos\Curl), while find and the shell speak /z/repos/Curl. cygpath turns one into
# the other; on Linux and macOS there is no cygpath and the two forms are the same.
if command -v cygpath >/dev/null 2>&1; then HAVE_CYGPATH=1; else HAVE_CYGPATH=0; fi
native_path() {
    if [ $HAVE_CYGPATH -eq 1 ]; then cygpath -w "$1"; else printf '%s' "$1"; fi
}
repository_root_native=$(native_path "$repository_root")

work_directory=$(mktemp -d "${TMPDIR:-/tmp}/measure-code-quality.XXXXXX") || die "Could not create a temporary directory."
trap 'rm -rf "$work_directory"' EXIT

# Through some callers "-Library A,B" arrives as the one string "A,B", which matches no
# project, so the run fell back to the whole solution and the report named nothing.
# Split it into names (BL-1360, the same trap as the audit tools' BL-1245).
library_file="$work_directory/libraries.txt"
printf '%s\n' "$LIBRARY_ARGUMENTS" | tr ',' '\n' | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' | grep -v '^$' > "$library_file"
if [ -s "$library_file" ]; then HAVE_LIBRARY=1; else HAVE_LIBRARY=0; fi

sha256_hex() {
    # SHA-256 of stdin as hex: sha256sum on Linux, shasum on macOS, openssl elsewhere.
    if command -v sha256sum >/dev/null 2>&1; then sha256sum | cut -d' ' -f1
    elif command -v shasum >/dev/null 2>&1; then shasum -a 256 | cut -d' ' -f1
    else openssl dgst -sha256 | sed 's/^.*= *//'
    fi
}

checkout_results_directory() {
    # $TEMP/CurlCodeQuality/<checkout name>-<first 16 hex digits of the SHA-256 of the
    # checkout's full path, without a trailing separator, lower-cased>.
    full_path=$repository_root_native
    while :; do
        case "$full_path" in
            ?*\\|?*/) full_path=${full_path%?} ;;
            *) break ;;
        esac
    done
    lower_path=$(printf '%s' "$full_path" | tr '[:upper:]' '[:lower:]')
    hash_text=$(printf '%s' "$lower_path" | sha256_hex | cut -c1-16)
    checkout_name=${lower_path##*[\\/]}
    temp_root=${TEMP:-${TMPDIR:-/tmp}}
    if [ $HAVE_CYGPATH -eq 1 ]; then temp_root=$(cygpath -u "$temp_root"); fi
    temp_root=${temp_root%/}
    printf '%s/CurlCodeQuality/%s-%s' "$temp_root" "$checkout_name" "$hash_text"
}

if [ -z "$(printf '%s' "$RESULTS_DIRECTORY" | tr -d '[:space:]')" ]; then
    RESULTS_DIRECTORY=$(checkout_results_directory)
fi

# --- Projects ---------------------------------------------------------------------

# Glob-to-regex and the production-assembly test, shared by every awk program below.
# -like in PowerShell ignores case, so both sides are lower-cased before matching.
read -r -d '' AWK_LIBRARY <<'AWK_EOF'
function glob_to_regex(glob,   i, c, regex) {
    regex = "^"
    for (i = 1; i <= length(glob); i++) {
        c = substr(glob, i, 1)
        if (c == "*") regex = regex ".*"
        else if (c == "?") regex = regex "."
        else if (c == "[" || c == "]") regex = regex c
        else if (index("\\.^$+(){}|", c)) regex = regex "\\" c
        else regex = regex c
    }
    return regex "$"
}
# Production code is *.UnitLibrary, Curl.Console, Curl.Api, Curl.Mcp and Libcurl only.
# Test assemblies of both kinds, *.UnitTests and *.IntegrationTests, are never
# production code (ADR-0421).
function is_production_assembly(name,   lower) {
    lower = tolower(name)
    if (lower ~ /\.unittests$/ || lower ~ /\.integrationtests$/) return 0
    if (lower ~ /\.unitlibrary$/) return 1
    return lower == "curl.console" || lower == "curl.api" || lower == "curl.mcp" || lower == "libcurl"
}
AWK_EOF

project_inventory() {
    # Every project file at the root or one folder down, as name<TAB>path in
    # projects.tsv, and every ProjectReference, as project<TAB>referenced project in
    # references.tsv. Built once per run.
    [ -f "$work_directory/projects.tsv" ] && return 0
    find "$repository_root" -maxdepth 2 -type f -name '*.csproj' | LC_ALL=C sort > "$work_directory/csproj.lst"
    while IFS= read -r project_file; do
        printf '%s\t%s\n' "$(basename "$project_file" .csproj)" "$project_file"
    done < "$work_directory/csproj.lst" > "$work_directory/projects.tsv"
    # Each project file is read whole, its comments stripped, and every Include of a
    # ProjectReference element reduced to the referenced project's file name.
    MCQ_LIST="$work_directory/csproj.lst" LC_ALL=C awk '
        BEGIN {
            list = ENVIRON["MCQ_LIST"]
            while ((getline file < list) > 0) {
                text = ""
                while ((getline line < file) > 0) text = text line "\n"
                close(file)
                while ((start = index(text, "<!--")) > 0) {
                    rest = substr(text, start + 4)
                    finish = index(rest, "-->")
                    if (finish == 0) { text = substr(text, 1, start - 1); break }
                    text = substr(text, 1, start - 1) substr(rest, finish + 3)
                }
                project = file
                sub(/^.*\//, "", project)
                sub(/\.csproj$/, "", project)
                count = split(text, tags, ">")
                for (i = 1; i <= count; i++) {
                    tag = tags[i]
                    at = index(tag, "<ProjectReference")
                    if (at == 0) continue
                    tag = substr(tag, at)
                    if (!match(tag, /[ \t\r\n]Include="[^"]*"/)) continue
                    include = substr(tag, RSTART + 10, RLENGTH - 11)
                    gsub(/\\/, "/", include)
                    sub(/^.*\//, "", include)
                    sub(/\.[^.]*$/, "", include)
                    print project "\t" include
                }
            }
        }' > "$work_directory/references.tsv"
}

test_projects() {
    # Every *.UnitTests project file and, with include-integration 1, every
    # *.IntegrationTests one: the test projects a whole-solution run measures.
    # $1: 1 to include the *.IntegrationTests projects.
    project_inventory
    awk -F "$TAB" -v integration="$1" '{
            lower = tolower($1)
            if (lower ~ /\.unittests$/ || (integration == 1 && lower ~ /\.integrationtests$/)) print $1 "\t" $2
        }' "$work_directory/projects.tsv" | LC_ALL=C sort -f -t "$TAB" -k1,1 | cut -f2
}

test_projects_reaching() {
    # The test project files whose ProjectReference graph reaches a project whose name
    # matches one of the names in file $1 (wildcards allowed; Curl.Console is the
    # console project). Test projects are the *.UnitTests ones and, with $2 = 1, the
    # *.IntegrationTests ones too; without it those are left out, because every test in
    # them carries the Integration category the filter skips (ADR-0421, BL-1605).
    # With $3 = reached, prints instead every project a test project reaches (other than
    # itself), name<TAB>name, which is what -SelfTest needs for every library at once.
    project_inventory
    MCQ_PATTERNS="$1" LC_ALL=C awk -F "$TAB" -v integration="$2" -v mode="${3:-tests}" "$AWK_LIBRARY"'
        BEGIN {
            patterns = ENVIRON["MCQ_PATTERNS"]
            while ((getline pattern < patterns) > 0) {
                sub(/\r$/, "", pattern)
                if (pattern != "") regexes[++regex_count] = glob_to_regex(tolower(pattern))
            }
            close(patterns)
        }
        FILENAME == ARGV[1] {
            key = tolower($1)
            if (!(key in path)) order[++project_count] = key
            name[key] = $1
            path[key] = $2
            next
        }
        { references[tolower($1)] = references[tolower($1)] SUBSEP tolower($2) }
        END {
            for (i = 1; i <= project_count; i++) {
                test = order[i]
                if (!(test ~ /\.unittests$/ || (integration == 1 && test ~ /\.integrationtests$/))) continue
                split("", seen)
                head = 1; tail = 0
                queue[++tail] = test
                while (head <= tail) {
                    current = queue[head++]
                    if (current in seen) continue
                    seen[current] = 1
                    count = split(references[current], next_names, SUBSEP)
                    for (j = 1; j <= count; j++) if (next_names[j] != "" && (next_names[j] in path)) queue[++tail] = next_names[j]
                }
                hit = 0
                for (current in seen) {
                    if (current == test) continue
                    if (mode == "reached") { print name[current] "\t" name[current]; continue }
                    for (p = 1; p <= regex_count; p++) if (current ~ regexes[p]) hit = 1
                }
                if (hit) print name[test] "\t" path[test]
            }
        }' "$work_directory/projects.tsv" "$work_directory/references.tsv" | LC_ALL=C sort -f -t "$TAB" -k1,1 | cut -f2
}

gated_libraries() {
    # The gated libraries: every production project in the checkout, by project name.
    project_inventory
    cut -f1 "$work_directory/projects.tsv" | awk "$AWK_LIBRARY"'is_production_assembly($0)' | LC_ALL=C sort -f -u
}

run_coverage_tests() {
    # Runs dotnet test with Cobertura coverage for each target listed in file $1, writing
    # into directory $2, and fails the run if any test failed. With $3 = 1 (no build) the
    # targets must already be built.
    targets_file=$1
    directory=$2
    no_build=$3
    command -v dotnet >/dev/null 2>&1 || die "dotnet is not on PATH."
    say "$COLOR_CYAN" "Running tests with coverage into $directory ..."
    test_log="$work_directory/test-output.log"
    : > "$test_log"
    test_exit_code=0
    while IFS= read -r target; do
        [ -n "$target" ] || continue
        # One folder per test project, so each project's reports sit apart when shards'
        # results are gathered; the reports are merged from every folder alike.
        project_directory="$directory/$(basename "$target" .csproj)"
        set -- test "$(native_path "$target")" '--collect:Code Coverage;Format=cobertura' --results-directory "$(native_path "$project_directory")"
        if [ "$no_build" -eq 1 ]; then set -- "$@" --no-build; fi
        if [ $INCLUDE_INTEGRATION -eq 0 ]; then set -- "$@" --filter 'TestCategory!=Integration'; fi
        say "$COLOR_DARK_CYAN" "dotnet test $(basename "$target")"
        dotnet "$@" < /dev/null | tee -a "$test_log"
        project_exit_code=${PIPESTATUS[0]}
        if [ "$project_exit_code" -ne 0 ]; then test_exit_code=$project_exit_code; fi
    done < "$targets_file"
    if [ "$test_exit_code" -ne 0 ]; then
        # A scaffolded .UnitTests project with no tests yet prints "No test matches the
        # given testcase filter" and can make dotnet test exit non-zero although nothing
        # failed. Measure anyway, but only when the output shows no real failure: no
        # "Failed!" summary (it can land mid-line when projects' output interleaves), no
        # aborted run and no build error. Matched without regard to case, as -match does.
        if grep -Eiq 'Failed![[:space:]]+-[[:space:]]+Failed:|Test Run Aborted|test run was aborted|:[[:space:]]+error[[:space:]]+[A-Za-z]+[0-9]+|Build FAILED' "$test_log" ||
            ! grep -iq 'No test matches the given testcase filter' "$test_log"; then
            die "dotnet test failed with exit code $test_exit_code. Fix the tests before measuring quality."
        fi
        warn "dotnet test exited with code $test_exit_code, but no test failed: a test project has no tests matching the filter yet. Measuring anyway."
    fi
}

# --- Shards (BL-3464) -------------------------------------------------------------

# Seconds each test project's coverage run took on CI's slow Windows runner, CI run
# 40108510840 (the job that took 25 minutes): what shard_test_projects balances. A
# project not listed counts as default_test_project_seconds. Only the balance depends on
# these numbers; every project lands in exactly one shard whatever they say.
measured_test_project_seconds='Curl.Original.Protocol.Ssh.UnitTests=707 Curl.Conformance.UnitTests=72 Curl.Cryptography.UnitTests=58
Curl.Tls.UnitTests=24 Curl.Networking.UnitTests=23 Curl.Console.UnitTests=17
Curl.Conformance.LibTest.UnitTests=15 Curl.Engine.UnitTests=13 Curl.Original.Protocol.Http.UnitTests=11
Curl.Cli.UnitTests=10 Libcurl.UnitTests=10 Curl.Mcp.UnitTests=9 Curl.Api.UnitTests=8
Curl.Kerberos.UnitTests=8 Curl.Conformance.Http3Server.UnitTests=7 Curl.Conformance.SshServer.UnitTests=7'
default_test_project_seconds=5

shard_test_projects() {
    # The test project paths (listed in file $1) in shard $2 (1-based) of $3, sorted by
    # name. The projects are dealt longest first, by measured_test_project_seconds (ties
    # by name), each to the shard with the fewest seconds so far (ties to the lowest
    # shard), so the split depends only on the set of projects, never on the order the
    # file lists them in.
    LC_ALL=C sort -f -u "$1" |
        MCQ_SECONDS="$measured_test_project_seconds" awk -v default_seconds="$default_test_project_seconds" '
            BEGIN {
                count = split(ENVIRON["MCQ_SECONDS"], pairs, /[ \n]+/)
                for (i = 1; i <= count; i++) {
                    if (split(pairs[i], pair, "=") == 2) seconds[tolower(pair[1])] = pair[2] + 0
                }
            }
            $0 != "" {
                name = $0
                sub(/^.*[\\\/]/, "", name)
                sub(/\.[^.]*$/, "", name)
                project_seconds = (tolower(name) in seconds) ? seconds[tolower(name)] : default_seconds
                print project_seconds "\t" name "\t" $0
            }' |
        LC_ALL=C sort -t "$TAB" -k1,1nr -k2,2f |
        awk -F "$TAB" -v shard="$2" -v shard_count="$3" '
            BEGIN { for (i = 0; i < shard_count; i++) load[i] = 0 }
            {
                lightest = 0
                for (i = 1; i < shard_count; i++) if (load[i] < load[lightest]) lightest = i
                load[lightest] += $1
                if (lightest + 1 == shard) print $2 "\t" $3
            }' |
        LC_ALL=C sort -f -t "$TAB" -k1,1 | cut -f2
}

join_names() {
    # The file names, without folder or extension, of the paths on stdin, joined by ", ".
    sed 's|^.*[\\/]||; s|\.[^.]*$||' | awk 'NR > 1 { printf ", " } { printf "%s", $0 }'
}

join_lines() {
    # The lines on stdin joined by $1.
    awk -v separator="$1" 'NR > 1 { printf "%s", separator } { printf "%s", $0 }'
}

shard_self_test() {
    # -SelfTest: the shards partition this checkout's test projects exactly, and every
    # gated library is reached by some test project. Prints the number of failed checks
    # on its last line.
    failed=0
    all_file="$work_directory/selftest-all.lst"
    reversed_file="$work_directory/selftest-reversed.lst"
    test_projects 0 > "$all_file"
    awk '{ lines[NR] = $0 } END { for (i = NR; i >= 1; i--) print lines[i] }' "$all_file" > "$reversed_file"
    all_count=$(grep -c . "$all_file")
    count=1
    while [ $count -le 8 ]; do
        : > "$work_directory/selftest-seen.lst"
        empty=0
        order_dependent=0
        sizes=""
        index_number=1
        while [ $index_number -le $count ]; do
            shard_test_projects "$all_file" $index_number $count > "$work_directory/selftest-shard.lst"
            shard_test_projects "$reversed_file" $index_number $count > "$work_directory/selftest-shard-reversed.lst"
            size=$(grep -c . "$work_directory/selftest-shard.lst")
            if [ "$size" -eq 0 ]; then empty=$((empty + 1)); fi
            if ! cmp -s "$work_directory/selftest-shard.lst" "$work_directory/selftest-shard-reversed.lst"; then order_dependent=$((order_dependent + 1)); fi
            cat "$work_directory/selftest-shard.lst" >> "$work_directory/selftest-seen.lst"
            if [ -n "$sizes" ]; then sizes="$sizes, "; fi
            sizes="$sizes$size"
            index_number=$((index_number + 1))
        done
        twice=$(sort "$work_directory/selftest-seen.lst" | uniq -c | awk '$1 != 1' | grep -c .)
        missing=$(awk 'FILENAME == ARGV[1] { seen[$0] = 1; next } !($0 in seen)' "$work_directory/selftest-seen.lst" "$all_file" | grep -c .)
        if [ "$twice" -eq 0 ] && [ "$missing" -eq 0 ] && [ $empty -eq 0 ] && [ $order_dependent -eq 0 ]; then verdict=PASS; else verdict=FAIL; failed=$((failed + 1)); fi
        printf '%s %s shard(s) of %s test projects: sizes %s; in more than one %s, in none %s, empty %s, order-dependent %s\n' \
            "$verdict" "$count" "$all_count" "$sizes" "$twice" "$missing" "$empty" "$order_dependent"
        count=$((count + 1))
    done
    # A library is reached when some *.UnitTests project's references reach it: the same
    # walk test_projects_reaching makes for one library, made once for all of them.
    : > "$work_directory/selftest-no-patterns.txt"
    test_projects_reaching "$work_directory/selftest-no-patterns.txt" 0 reached | LC_ALL=C sort -f -u > "$work_directory/selftest-reached.txt"
    unreached=$(gated_libraries | awk 'FILENAME == ARGV[1] { reached[tolower($0)] = 1; next } !(tolower($0) in reached)' "$work_directory/selftest-reached.txt" - | join_lines ', ')
    if [ -n "$unreached" ]; then
        failed=$((failed + 1))
        printf 'FAIL every gated library is reached by a test project; unreached: %s\n' "$unreached"
    else
        printf 'PASS every gated library is reached by a test project; unreached: none\n'
    fi
    printf '%s\n' "$failed"
}

if [ $SELF_TEST -eq 1 ]; then
    shard_self_test > "$work_directory/selftest.out"
    sed '$d' "$work_directory/selftest.out"
    failed_checks=$(tail -n 1 "$work_directory/selftest.out")
    if [ "$failed_checks" -ne 0 ]; then say "$COLOR_RED" "$failed_checks shard check(s) failed."; exit 1; fi
    say "$COLOR_GREEN" 'Every shard check passed.'
    exit 0
fi

json_string() {
    # $1 as a JSON string literal, quotes included.
    printf '%s' "$1" | LC_ALL=C awk 'BEGIN { RS = "\001" } {
            out = ""
            for (i = 1; i <= length($0); i++) {
                c = substr($0, i, 1)
                if (c == "\\") out = out "\\\\"
                else if (c == "\"") out = out "\\\""
                else if (c == "\n") out = out "\\n"
                else if (c == "\r") out = out "\\r"
                else if (c == "\t") out = out "\\t"
                else out = out c
            }
            printf "\"%s\"", out
        } END { if (NR == 0) printf "\"\"" }'
}

if [ $SHARD_COUNT -gt 0 ] || [ $SHARD -gt 0 ]; then
    if [ $SHARD_COUNT -lt 1 ] || [ $SHARD -lt 1 ] || [ $SHARD -gt $SHARD_COUNT ]; then die "-Shard must be from 1 to -ShardCount; got -Shard $SHARD -ShardCount $SHARD_COUNT."; fi
    if [ $HAVE_LIBRARY -eq 1 ] || [ $FROM_SHARDS -eq 1 ] || [ $SKIP_TEST_RUN -eq 1 ] || [ $INCLUDE_INTEGRATION -eq 1 ]; then die '-Shard measures a shard of a whole-solution run; it takes none of -Library, -FromShards, -SkipTestRun or -IncludeIntegration.'; fi
    test_projects 0 > "$work_directory/all-tests.lst"
    shard_test_projects "$work_directory/all-tests.lst" $SHARD $SHARD_COUNT > "$work_directory/shard-tests.lst"
    if [ ! -s "$work_directory/shard-tests.lst" ]; then die "Shard $SHARD of $SHARD_COUNT holds no test project, so it would measure nothing."; fi
    rm -rf "$RESULTS_DIRECTORY"
    mkdir -p "$RESULTS_DIRECTORY" || die "Could not create $RESULTS_DIRECTORY."
    say "$COLOR_CYAN" "Shard $SHARD of ${SHARD_COUNT}: $(join_names < "$work_directory/shard-tests.lst")"

    # Build only what this shard tests: a solution filter of its test projects, whose
    # project references build with them.
    filter_path="$RESULTS_DIRECTORY/shard-$SHARD-of-$SHARD_COUNT.slnf"
    {
        printf '{\n  "solution": {\n    "path": %s,\n    "projects": [\n' "$(json_string "$(native_path "$repository_root/Curl.slnx")")"
        first=1
        while IFS= read -r project; do
            relative=${project#"$repository_root"}
            relative=${relative#/}
            if [ $HAVE_CYGPATH -eq 1 ]; then relative=$(printf '%s' "$relative" | tr '/' '\\'); fi
            if [ $first -eq 0 ]; then printf ',\n'; fi
            printf '      %s' "$(json_string "$relative")"
            first=0
        done < "$work_directory/shard-tests.lst"
        printf '\n    ]\n  }\n}\n'
    } > "$filter_path"
    command -v dotnet >/dev/null 2>&1 || die "dotnet is not on PATH."
    say "$COLOR_CYAN" "Building shard $SHARD of $SHARD_COUNT ..."
    dotnet build "$(native_path "$filter_path")" < /dev/null
    build_exit_code=$?
    if [ $build_exit_code -ne 0 ]; then die "dotnet build failed with exit code $build_exit_code. Fix the build before measuring quality."; fi

    run_coverage_tests "$work_directory/shard-tests.lst" "$RESULTS_DIRECTORY" 1
    shard_report_count=$(find "$RESULTS_DIRECTORY" -type f -name '*.cobertura.xml' 2>/dev/null | grep -c .)
    if [ "$shard_report_count" -eq 0 ]; then die "Shard $SHARD of $SHARD_COUNT wrote no Cobertura report, so it measured nothing."; fi
    {
        printf 'shard %s of %s\n' "$SHARD" "$SHARD_COUNT"
        sed 's|^.*[\\/]||; s|\.[^.]*$||' "$work_directory/shard-tests.lst"
    } > "$RESULTS_DIRECTORY/shard-$SHARD-of-$SHARD_COUNT.txt"
    say "$COLOR_CYAN" "Shard $SHARD of $SHARD_COUNT wrote $shard_report_count Cobertura report(s) for $(grep -c . "$work_directory/shard-tests.lst") test project(s)."
    exit 0
fi

if [ $FROM_SHARDS -eq 1 ]; then
    if [ $HAVE_LIBRARY -eq 1 ] || [ $SKIP_TEST_RUN -eq 1 ] || [ $INCLUDE_INTEGRATION -eq 1 ]; then die '-FromShards scores a whole-solution run; it takes none of -Library, -SkipTestRun or -IncludeIntegration.'; fi
    if [ ! -e "$RESULTS_DIRECTORY" ]; then die "-FromShards found no $RESULTS_DIRECTORY."; fi
    # Every shard of one count reported, and together they ran every test project once.
    find "$RESULTS_DIRECTORY" -type f -iname 'shard-*-of-*.txt' > "$work_directory/manifests.lst"
    manifest_count=$(grep -c . "$work_directory/manifests.lst")
    sed 's|^.*/||' "$work_directory/manifests.lst" | grep -Ei '^shard-[0-9]+-of-[0-9]+\.txt$' > "$work_directory/manifest-names.lst"
    sed 's/^[^-]*-[0-9]*-[^-]*-//; s/\.[^.]*$//' "$work_directory/manifest-names.lst" | awk '{ print $0 + 0 }' | sort -n -u > "$work_directory/shard-counts.lst"
    if [ "$(grep -c . "$work_directory/shard-counts.lst")" -ne 1 ]; then
        die "-FromShards needs the manifests of one shard count under $RESULTS_DIRECTORY; found $manifest_count manifest(s) of count(s) '$(join_lines ', ' < "$work_directory/shard-counts.lst")'."
    fi
    shard_total=$(cat "$work_directory/shard-counts.lst")
    shard_numbers=$(sed 's/^[^-]*-//; s/-.*$//' "$work_directory/manifest-names.lst" | awk '{ print $0 + 0 }' | sort -n | join_lines ',')
    expected_numbers=$(awk -v n="$shard_total" 'BEGIN { for (i = 1; i <= n; i++) print i }' | join_lines ',')
    if [ "$shard_numbers" != "$expected_numbers" ]; then die "-FromShards needs shards 1 to $shard_total once each; found shard(s) $(printf '%s' "$shard_numbers" | sed 's/,/, /g')."; fi
    # Each manifest's lines after the first, trimmed, without a byte-order mark or CR.
    : > "$work_directory/ran.lst"
    while IFS= read -r manifest; do
        sed '1d' "$manifest" | tr -d '\r' | sed 's/^[[:space:]]*//; s/[[:space:]]*$//' | grep -v '^$' >> "$work_directory/ran.lst"
    done < "$work_directory/manifests.lst"
    test_projects 0 | sed 's|^.*/||; s|\.[^.]*$||' > "$work_directory/expected.lst"
    # The .ps1 compares these without regard to case, as -contains and Group-Object do.
    repeated=$(awk '{
            key = tolower($0)
            if (!(key in first)) { first[key] = $0; order[++count] = key }
            seen[key]++
        } END { for (i = 1; i <= count; i++) if (seen[order[i]] > 1) print first[order[i]] }' "$work_directory/ran.lst" | join_lines ', ')
    not_run=$(awk 'FILENAME == ARGV[1] { ran[tolower($0)] = 1; next } !(tolower($0) in ran)' "$work_directory/ran.lst" "$work_directory/expected.lst" | join_lines ', ')
    unknown=$(awk 'FILENAME == ARGV[1] { expected[tolower($0)] = 1; next } !(tolower($0) in expected)' "$work_directory/expected.lst" "$work_directory/ran.lst" | LC_ALL=C sort -f -u | join_lines ', ')
    if [ -n "$repeated" ] || [ -n "$not_run" ] || [ -n "$unknown" ]; then
        die "The shards did not run every test project exactly once. Run twice: $repeated. Not run: $not_run. Not in this checkout: $unknown."
    fi
    say "$COLOR_CYAN" "Scoring $shard_total shard(s) that ran all $(grep -c . "$work_directory/expected.lst") test projects."
fi

# --- 1. Collect coverage ----------------------------------------------------------

whole_solution=0
if [ $SKIP_TEST_RUN -eq 0 ] && [ $FROM_SHARDS -eq 0 ]; then
    rm -rf "$RESULTS_DIRECTORY"
    mkdir -p "$RESULTS_DIRECTORY" || die "Could not create $RESULTS_DIRECTORY."

    # With -Library, run only the test projects whose project references reach a named
    # library, directly or through other projects, rather than the whole solution. Those
    # are the only tests that can execute its code, so its coverage, merged across them,
    # is what a whole-solution run would report; on nine busy lanes that is minutes, not
    # the 40-58 a whole-solution run took (BL-1318). Without -Library, or when no test
    # project reaches the names given, every test project runs.
    : > "$work_directory/test-targets.lst"
    if [ $HAVE_LIBRARY -eq 1 ]; then
        test_projects_reaching "$library_file" $INCLUDE_INTEGRATION > "$work_directory/test-targets.lst"
    fi
    if [ ! -s "$work_directory/test-targets.lst" ]; then whole_solution=1; fi

    if [ $whole_solution -eq 1 ]; then
        # A whole-solution run builds Curl.slnx once and then tests each project on its
        # own, one report per project, rather than running dotnet test on Curl.slnx: that
        # merges every project's coverage into one Cobertura file, and on CI that merged
        # file carried no branch counts, so the branch gate passed without measuring a
        # branch until a recheck added one project's counts and 663 members failed (CI run
        # 38876049958, BL-2466). It also lost a whole project's coverage once (BL-2269).
        command -v dotnet >/dev/null 2>&1 || die "dotnet is not on PATH."
        say "$COLOR_CYAN" 'Building Curl.slnx ...'
        dotnet build "$(native_path "$repository_root/Curl.slnx")" < /dev/null
        build_exit_code=$?
        if [ $build_exit_code -ne 0 ]; then die "dotnet build failed with exit code $build_exit_code. Fix the build before measuring quality."; fi
        test_projects $INCLUDE_INTEGRATION > "$work_directory/test-targets.lst"
    fi

    run_coverage_tests "$work_directory/test-targets.lst" "$RESULTS_DIRECTORY" $whole_solution
fi

coverage_list="$work_directory/coverage-files.lst"
find "$RESULTS_DIRECTORY" -type f -name '*.cobertura.xml' 2>/dev/null > "$coverage_list"
if [ ! -s "$coverage_list" ]; then
    die "No Cobertura report found under $RESULTS_DIRECTORY."
fi

# --- 2. Merge every report into one table of production methods -------------------
# --- 3. Score each method ---------------------------------------------------------

# One awk program does both. It reads each Cobertura file as a stream of tags (records
# end at '>'), so it does not care how the file is laid out on lines, and keeps a table
# of production methods keyed by assembly, class and member, merging each line's best
# hit count and best condition counts across reports. Then it scores every method and
# prints one row per method, tab-separated:
#   assembly class member file first-line lines-covered lines-total branches-covered
#   branches-total line% branch% complexity crap missing-lines(comma) failures(space)
#
# Curl.Console.csproj sets <AssemblyName>curl</AssemblyName> so the binary is a drop-in
# for curl, which makes its Cobertura package "curl". It is reported under its project
# name, so the table, the failing-member heading and -Library all see Curl.Console.
read -r -d '' AWK_MERGE_AND_SCORE <<'AWK_EOF'
function attribute(tag, name,   value) {
    if (!match(tag, "[ \t\r\n]" name "=\"[^\"]*\"")) return ""
    value = substr(tag, RSTART + 1, RLENGTH - 1)
    value = substr(value, length(name) + 3)
    return substr(value, 1, length(value) - 1)
}
function decode(text) {
    gsub(/&lt;/, "<", text); gsub(/&gt;/, ">", text)
    gsub(/&quot;/, "\"", text); gsub(/&apos;/, "'", text)
    gsub(/&amp;/, "\\&", text)
    return text
}
function reported_assembly_name(package_name) {
    return package_name == "curl" ? "Curl.Console" : package_name
}
# Math.Round(value, 2): scale by 100, round half to even, scale back.
function round2(value,   scaled, whole, fraction) {
    scaled = value * 100
    whole = int(scaled)
    fraction = scaled - whole
    if (fraction > 0.5 || (fraction == 0.5 && whole % 2 == 1)) whole++
    return sprintf("%.15g", whole / 100)
}
function relative_path(file,   lower_file, roots, i, root) {
    lower_file = tolower(file)
    roots[1] = ENVIRON["MCQ_ROOT_NATIVE"]; roots[2] = ENVIRON["MCQ_ROOT"]
    for (i = 1; i <= 2; i++) {
        root = tolower(roots[i])
        if (root != "" && substr(lower_file, 1, length(root)) == root) { file = substr(file, length(root) + 1); break }
    }
    sub("^[\\\\/]+", "", file)
    return file
}
function add_line(tag,   number, hits, condition, covered, total, pair, line_key) {
    number = attribute(tag, "number") + 0
    hits = attribute(tag, "hits") + 0
    covered = 0; total = 0
    condition = attribute(tag, "condition-coverage")
    if (match(condition, /\([0-9]+\/[0-9]+\)/)) {
        split(substr(condition, RSTART + 1, RLENGTH - 2), pair, "/")
        covered = pair[1] + 0; total = pair[2] + 0
    }
    line_key = method_key SUBSEP number
    if (!(line_key in line_hits)) {
        line_hits[line_key] = hits
        line_covered[line_key] = covered
        line_total[line_key] = total
        method_lines[method_key] = method_lines[method_key] (method_lines[method_key] == "" ? "" : " ") number
    } else {
        if (hits > line_hits[line_key]) line_hits[line_key] = hits
        if (covered > line_covered[line_key]) line_covered[line_key] = covered
        if (total > line_total[line_key]) line_total[line_key] = total
    }
}
BEGIN {
    list = ENVIRON["MCQ_LIST"]
    while ((getline file < list) > 0) { sub(/\r$/, "", file); if (file != "") files[++file_count] = file }
    close(list)
    RS = ">"
    for (f = 1; f <= file_count; f++) {
        production = 0; in_method = 0
        while ((getline record < files[f]) > 0) {
            start = index(record, "<")
            if (start == 0) continue
            tag = substr(record, start)
            self_closing = substr(tag, length(tag), 1) == "/"
            if (tag ~ /^<line[ \t\r\n\/]/) { if (in_method) add_line(tag); continue }
            if (tag ~ /^<package[ \t\r\n\/]/) {
                assembly = reported_assembly_name(decode(attribute(tag, "name")))
                production = is_production_assembly(assembly)
            } else if (tag ~ /^<\/package[ \t\r\n]*$/) {
                production = 0
            } else if (tag ~ /^<class[ \t\r\n\/]/) {
                class_name = decode(attribute(tag, "name"))
                source_file = decode(attribute(tag, "filename"))
            } else if (tag ~ /^<method[ \t\r\n\/]/) {
                if (!production) continue
                member = decode(attribute(tag, "name")) decode(attribute(tag, "signature"))
                method_key = assembly SUBSEP class_name SUBSEP member
                if (!(method_key in method_complexity)) {
                    method_order[++method_count] = method_key
                    method_assembly[method_key] = assembly
                    method_class[method_key] = class_name
                    method_member[method_key] = member
                    method_file[method_key] = source_file
                    method_complexity[method_key] = 0
                }
                # Complexity is a property of the code, not of the run, so every report
                # that saw this method agrees on it and the maximum is simply the value.
                complexity = attribute(tag, "complexity") + 0
                if (complexity > method_complexity[method_key]) method_complexity[method_key] = complexity
                in_method = !self_closing
            } else if (tag ~ /^<\/method[ \t\r\n]*$/) {
                in_method = 0
            }
        }
        close(files[f])
    }

    OFS = "\t"
    for (m = 1; m <= method_count; m++) {
        key = method_order[m]
        line_count = split(method_lines[key], numbers, " ")
        if (line_count == 0) continue
        for (a = 2; a <= line_count; a++) {
            value = numbers[a] + 0
            for (b = a - 1; b >= 1 && numbers[b] + 0 > value; b--) numbers[b + 1] = numbers[b]
            numbers[b + 1] = value
        }
        missing = ""; missing_count = 0; branch_total = 0; branch_covered = 0
        for (a = 1; a <= line_count; a++) {
            line_key = key SUBSEP numbers[a]
            if (line_hits[line_key] == 0) { missing = missing (missing_count ? "," : "") numbers[a]; missing_count++ }
            branch_total += line_total[line_key]
            branch_covered += line_covered[line_key]
        }
        line_rate = (line_count - missing_count) / line_count
        branch_rate = branch_total == 0 ? 1.0 : branch_covered / branch_total
        complexity = method_complexity[key]
        crap = (complexity * complexity) * (1 - line_rate) ^ 3 + complexity
        failures = ""
        if (line_rate * 100 < ENVIRON["MCQ_LINE"] + 0) failures = failures " line"
        if (branch_rate * 100 < ENVIRON["MCQ_BRANCH"] + 0) failures = failures " branch"
        if (complexity > ENVIRON["MCQ_COMPLEXITY"] + 0) failures = failures " complexity"
        if (crap > ENVIRON["MCQ_CRAP"] + 0) failures = failures " crap"
        sub(/^ /, "", failures)
        print method_assembly[key], method_class[key], method_member[key], relative_path(method_file[key]), \
            numbers[1], line_count - missing_count, line_count, branch_covered, branch_total, \
            round2(line_rate * 100), round2(branch_rate * 100), complexity, round2(crap), missing, failures
    }
}
AWK_EOF

scored_file="$work_directory/scored.tsv"
score_members() {
    # Merges every report listed in coverage_list and scores every merged method into
    # scored_file.
    MCQ_LIST="$coverage_list" MCQ_ROOT="$repository_root" MCQ_ROOT_NATIVE="$repository_root_native" \
        MCQ_LINE="$LINE_THRESHOLD" MCQ_BRANCH="$BRANCH_THRESHOLD" MCQ_COMPLEXITY="$COMPLEXITY_THRESHOLD" MCQ_CRAP="$CRAP_THRESHOLD" \
        LC_ALL=C awk "$AWK_LIBRARY$AWK_MERGE_AND_SCORE" > "$scored_file" || die "Could not read the Cobertura reports under $RESULTS_DIRECTORY."
}

score_members

# Once in a while a whole-solution run lost one project's coverage although its tests
# all passed: CI run 38700283564 lost Curl.Cryptography.UnitTests' and reported 108
# members uncovered that the same commit's other run covered in full (BL-2269). So when a
# whole-solution run leaves a member short of line or branch coverage, run the test
# projects that reach its library again, one at a time, and merge their reports in. A
# real gap stays a gap, since merging only ever adds lines some test executed.
if [ $FROM_SHARDS -eq 1 ] || { [ $SKIP_TEST_RUN -eq 0 ] && [ $whole_solution -eq 1 ]; }; then
    awk -F "$TAB" '$15 ~ /(^| )(line|branch)( |$)/ { print $1 }' "$scored_file" | LC_ALL=C sort -f -u > "$work_directory/short-assemblies.txt"
    if [ -s "$work_directory/short-assemblies.txt" ]; then
        test_projects_reaching "$work_directory/short-assemblies.txt" $INCLUDE_INTEGRATION > "$work_directory/recheck-targets.lst"
        if [ -s "$work_directory/recheck-targets.lst" ]; then
            say "$COLOR_YELLOW" "Coverage short in $(join_lines ', ' < "$work_directory/short-assemblies.txt"); measuring the test projects that reach them again."
            recheck_directory="$RESULTS_DIRECTORY/recheck"
            run_coverage_tests "$work_directory/recheck-targets.lst" "$recheck_directory" 0
            find "$recheck_directory" -type f -name '*.cobertura.xml' 2>/dev/null >> "$coverage_list"
            score_members
        fi
    fi
fi

# A -FromShards run fails when a gated library is not in the merged reports at all, so a
# shard that lost a library can never pass by leaving it out (BL-3464).
: > "$work_directory/missing-libraries.txt"
if [ $FROM_SHARDS -eq 1 ]; then
    cut -f1 "$scored_file" > "$work_directory/measured.txt"
    gated_libraries | awk 'FILENAME == ARGV[1] { measured[tolower($0)] = 1; next } !(tolower($0) in measured)' "$work_directory/measured.txt" - > "$work_directory/missing-libraries.txt"
fi

# The members reported on: every one, or with -Library those whose assembly matches a
# name. Sorted by CRAP, highest first, without reordering ties, for the failing tables.
selected_file="$work_directory/selected.tsv"
MCQ_PATTERNS="$library_file" LC_ALL=C awk -F "$TAB" -v filter="$HAVE_LIBRARY" "$AWK_LIBRARY"'
    BEGIN {
        patterns = ENVIRON["MCQ_PATTERNS"]
        while ((getline pattern < patterns) > 0) if (pattern != "") regexes[++regex_count] = glob_to_regex(tolower(pattern))
        close(patterns)
    }
    {
        if (filter == 1) {
            hit = 0
            for (p = 1; p <= regex_count; p++) if (tolower($1) ~ regexes[p]) hit = 1
            if (!hit) next
        }
        print
    }' "$scored_file" | LC_ALL=C sort -s -t "$TAB" -k13,13nr > "$selected_file"

# --- 4. Report --------------------------------------------------------------------

cut -f1 "$selected_file" | LC_ALL=C sort -u | LC_ALL=C sort -f > "$work_directory/assemblies.txt"

# The library table, the failing-member tables, and the same numbers as JSON fragments
# (libraries, failing members) and totals for the -JsonPath file.
read -r -d '' AWK_REPORT <<'AWK_EOF'
function round2(value,   scaled, whole, fraction) {
    scaled = value * 100
    whole = int(scaled)
    fraction = scaled - whole
    if (fraction > 0.5 || (fraction == 0.5 && whole % 2 == 1)) whole++
    return sprintf("%.15g", whole / 100)
}
function json(text,   out, i, c) {
    out = ""
    for (i = 1; i <= length(text); i++) {
        c = substr(text, i, 1)
        if (c == "\\") out = out "\\\\"
        else if (c == "\"") out = out "\\\""
        else if (c == "\t") out = out "\\t"
        else if (c == "\r") out = out "\\r"
        else if (c == "\n") out = out "\\n"
        else out = out c
    }
    return "\"" out "\""
}
# A double as PowerShell 7's ConvertTo-Json writes one: 100.0, not 100.
function json_double(number) {
    return number ~ /[.eE]/ ? number : number ".0"
}
BEGIN { FS = "\t" }
FILENAME == ARGV[1] { assemblies[++assembly_count] = $0; next }
{
    a = $1
    members[a]++
    lines_total[a] += $7; lines_covered[a] += $6
    branches_total[a] += $9; branches_covered[a] += $8
    if (!(a in worst_crap) || $13 + 0 > worst_crap[a] + 0) worst_crap[a] = $13
    if ($15 != "") failing_rows[a, ++failing[a]] = $0
}
END {
    library_json = ENVIRON["MCQ_LIBRARY_JSON"]
    failing_json = ENVIRON["MCQ_FAILING_JSON"]
    max_shown = ENVIRON["MCQ_MAX_SHOWN"] + 0
    printf "" > library_json
    printf "" > failing_json
    print "## Libraries"
    print ""
    print "| Library | Line % | Branch % | Members | Failing | Worst CRAP |"
    print "| --- | ---: | ---: | ---: | ---: | ---: |"
    for (i = 1; i <= assembly_count; i++) {
        a = assemblies[i]
        failing_count = failing[a] + 0
        total_violations += failing_count
        if (failing_count == 0) libraries_at_gate++
        # Weighted by line, not an average of per-member percentages: a one-line property
        # left uncovered should not cost as much as a forty-line method left uncovered.
        line_percent = lines_total[a] == 0 ? 100 : round2(100 * lines_covered[a] / lines_total[a])
        branch_percent = branches_total[a] == 0 ? 100 : round2(100 * branches_covered[a] / branches_total[a])
        printf "| %s | %s | %s | %s | %s | %s |\n", a, line_percent, branch_percent, members[a], failing_count, worst_crap[a]
        all_lines += lines_total[a]; all_lines_covered += lines_covered[a]
        all_branches += branches_total[a]; all_branches_covered += branches_covered[a]
        printf "%s    {\n      \"name\": %s,\n      \"linePercent\": %s,\n      \"branchPercent\": %s,\n      \"linesCovered\": %d,\n      \"linesTotal\": %d,\n      \"branchesCovered\": %d,\n      \"branchesTotal\": %d,\n      \"members\": %d,\n      \"failingMembers\": %d,\n      \"worstCrap\": %s\n    }", \
            (i > 1 ? ",\n" : ""), json(a), (lines_total[a] == 0 ? 100 : json_double(line_percent)), \
            (branches_total[a] == 0 ? 100 : json_double(branch_percent)), lines_covered[a], lines_total[a], \
            branches_covered[a], branches_total[a], members[a], failing_count, json_double(worst_crap[a]) >> library_json
    }
    print ""
    failing_written = 0
    for (i = 1; i <= assembly_count; i++) {
        a = assemblies[i]
        if (failing[a] + 0 == 0) continue
        printf "## %s - %d failing member(s)\n", a, failing[a]
        print ""
        print "| Member | Where | Line % | Branch % | Cx | CRAP | Fails | Uncovered lines |"
        print "| --- | --- | ---: | ---: | ---: | ---: | --- | --- |"
        for (r = 1; r <= failing[a]; r++) {
            split(failing_rows[a, r], row, "\t")
            missing_count = split(row[14], missing, ",")
            missing_text = ""
            shown = missing_count < max_shown ? missing_count : max_shown
            for (k = 1; k <= shown; k++) missing_text = missing_text (k > 1 ? ", " : "") missing[k]
            if (shown > 0 && missing_count > shown) missing_text = missing_text sprintf(" (+%d more)", missing_count - shown)

            # An async method is compiled into a state-machine class, so coverage arrives
            # as Owner.<TheMethod>d__14.MoveNext(). Reported literally, nobody can find it;
            # reported as Owner.TheMethod (async), it is where the developer left it.
            segment_count = split(row[2], segments, ".")
            short_class = segments[segment_count]
            display_member = row[3]
            if (short_class ~ /^<[^>]+>d__[0-9]+$/) {
                display_member = substr(short_class, 2, index(short_class, ">") - 2) " (async)"
                if (segment_count >= 2) short_class = segments[segment_count - 1]
            }

            printf "| `%s.%s` | %s:%s | %s | %s | %s | %s | %s | %s |\n", short_class, display_member, row[4], row[5], \
                row[10], row[11], row[12], row[13], row[15], missing_text
            fails_count = split(row[15], fails, " ")
            fails_json = ""
            for (k = 1; k <= fails_count; k++) fails_json = fails_json (k > 1 ? ",\n" : "") "        " json(fails[k])
            printf "%s    {\n      \"library\": %s,\n      \"member\": %s,\n      \"file\": %s,\n      \"line\": %d,\n      \"linePercent\": %s,\n      \"branchPercent\": %s,\n      \"complexity\": %d,\n      \"crap\": %s,\n      \"fails\": [\n%s\n      ],\n      \"uncoveredLines\": %s\n    }", \
                (failing_written++ ? ",\n" : ""), json(a), json(short_class "." display_member), json(row[4]), row[5], \
                json_double(row[10]), json_double(row[11]), row[12], json_double(row[13]), fails_json, json(missing_text) >> failing_json
        }
        print ""
    }
    totals = ENVIRON["MCQ_TOTALS"]
    printf "total_violations=%d\n", total_violations > totals
    printf "assembly_count=%d\n", assembly_count >> totals
    printf "libraries_at_gate=%d\n", libraries_at_gate >> totals
    printf "all_lines=%d\nall_lines_covered=%d\nall_branches=%d\nall_branches_covered=%d\n", all_lines, all_lines_covered, all_branches, all_branches_covered >> totals
    printf "total_line_percent=%s\n", all_lines ? json_double(round2(100 * all_lines_covered / all_lines)) : 100 >> totals
    printf "total_branch_percent=%s\n", all_branches ? json_double(round2(100 * all_branches_covered / all_branches)) : 100 >> totals
}
AWK_EOF

report_file="$work_directory/report.md"
library_json_file="$work_directory/libraries.json"
failing_json_file="$work_directory/failing.json"
totals_file="$work_directory/totals.txt"

{
    printf '# Curl code-quality audit\n\n'
    printf 'Measured %s. Thresholds: line %s%%, branch %s%%, cyclomatic complexity %s, CRAP %s.\n\n' \
        "$(date '+%Y-%m-%d %H:%M')" "$LINE_THRESHOLD" "$BRANCH_THRESHOLD" "$COMPLEXITY_THRESHOLD" "$CRAP_THRESHOLD"
    MCQ_LIBRARY_JSON="$library_json_file" MCQ_FAILING_JSON="$failing_json_file" MCQ_TOTALS="$totals_file" \
        MCQ_MAX_SHOWN="$MAX_MISSING_LINES_SHOWN" LC_ALL=C awk "$AWK_REPORT" "$work_directory/assemblies.txt" "$selected_file"
} > "$report_file"

total_violations=$(sed -n 's/^total_violations=//p' "$totals_file")
assembly_count=$(sed -n 's/^assembly_count=//p' "$totals_file")

# An excluded member is not in the coverage report at all, so exclusions are invisible
# unless they are listed on purpose. An [ExcludeFromCodeCoverage] with no justifying
# comment above it is a finding in its own right. Files in test projects of both kinds,
# *.UnitTests and *.IntegrationTests, are not production code and are skipped, and so are
# bin, obj and data folders and .git. Each hit is path<TAB>line<TAB>trimmed text, path
# relative to the checkout, sorted by path and then line.
exclusions_file="$work_directory/exclusions.tsv"
find "$repository_root" \( -type d \( -iname bin -o -iname obj -o -iname data -o -iname '*.UnitTests' -o -iname '*.IntegrationTests' -o -name .git \) -prune \) \
    -o -type f -name '*.cs' -print0 2>/dev/null |
    xargs -0 grep -n -H -F -- 'ExcludeFromCodeCoverage' 2>/dev/null |
    MCQ_ROOT="$repository_root" LC_ALL=C awk '{
            root = ENVIRON["MCQ_ROOT"]
            first = index($0, ":")
            path = substr($0, 1, first - 1)
            rest = substr($0, first + 1)
            second = index(rest, ":")
            number = substr(rest, 1, second - 1)
            text = substr(rest, second + 1)
            gsub(/^[ \t\r\n]+|[ \t\r\n]+$/, "", text)
            if (substr(tolower(path), 1, length(root)) == tolower(root)) path = substr(path, length(root) + 1)
            sub(/^[\\\/]+/, "", path)
            print path "\t" number "\t" text
        }' | LC_ALL=C sort -t "$TAB" -k1,1 -k2,2n > "$exclusions_file"

{
    printf '## Coverage exclusions in production code\n\n'
    if [ ! -s "$exclusions_file" ]; then
        printf 'None. Every production member is measured.\n'
    else
        awk -F "$TAB" '{ printf "- `%s:%s` - %s\n", $1, $2, $3 }' "$exclusions_file"
    fi
    printf '\n'
    if [ -s "$work_directory/missing-libraries.txt" ]; then
        printf "## Gated libraries missing from the shards' reports\n\n"
        sed 's/^/- /' "$work_directory/missing-libraries.txt"
        printf '\n'
    fi
    printf '**%s failing member(s) across %s production assembly/assemblies.**\n' "$total_violations" "$assembly_count"
} >> "$report_file"

cat "$report_file"

if [ -n "$REPORT_PATH" ]; then
    cp "$report_file" "$REPORT_PATH" || die "Could not write $REPORT_PATH."
    say "$COLOR_CYAN" "Report written to $REPORT_PATH"
fi

# The same numbers as data, for the published coverage report (.github/coverage).
if [ -n "$JSON_PATH" ]; then
    totals_value() { sed -n "s/^$1=//p" "$totals_file"; }
    json_array_or_empty() {
        # The array body in file $1 between brackets, or [] when it is empty.
        if [ -s "$1" ]; then printf '[\n'; cat "$1"; printf '\n  ]'; else printf '[]'; fi
    }
    # The thresholds are doubles, which PowerShell 7 writes as 100.0, not 100.
    json_double_threshold() { case "$1" in *.*|*e*|*E*) printf '%s' "$1" ;; *) printf '%s.0' "$1" ;; esac; }
    # The exclusions and the missing libraries as JSON array bodies, in one awk pass
    # rather than a process per entry.
    MCQ_EXCLUSIONS_JSON="$work_directory/exclusions.json" MCQ_MISSING_JSON="$work_directory/missing-libraries.json" \
        LC_ALL=C awk -F "$TAB" '
        function json(text,   out, i, c) {
            out = ""
            for (i = 1; i <= length(text); i++) {
                c = substr(text, i, 1)
                if (c == "\\") out = out "\\\\"
                else if (c == "\"") out = out "\\\""
                else if (c == "\t") out = out "\\t"
                else if (c == "\r") out = out "\\r"
                else if (c == "\n") out = out "\\n"
                else out = out c
            }
            return "\"" out "\""
        }
        BEGIN { exclusions = ENVIRON["MCQ_EXCLUSIONS_JSON"]; missing = ENVIRON["MCQ_MISSING_JSON"]; printf "" > exclusions; printf "" > missing }
        FILENAME == ARGV[1] {
            printf "%s    {\n      \"file\": %s,\n      \"line\": %d,\n      \"text\": %s\n    }", (FNR > 1 ? ",\n" : ""), json($1), $2, json($3) > exclusions
            next
        }
        { printf "%s    %s", (FNR > 1 ? ",\n" : ""), json($0) > missing }' "$exclusions_file" "$work_directory/missing-libraries.txt"
    {
        printf '{\n'
        printf '  "measuredAt": "%s",\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')"
        printf '  "thresholds": {\n    "linePercent": %s,\n    "branchPercent": %s,\n    "complexity": %s,\n    "crap": %s\n  },\n' \
            "$(json_double_threshold "$LINE_THRESHOLD")" "$(json_double_threshold "$BRANCH_THRESHOLD")" "$COMPLEXITY_THRESHOLD" "$(json_double_threshold "$CRAP_THRESHOLD")"
        printf '  "totals": {\n'
        printf '    "linePercent": %s,\n    "branchPercent": %s,\n' "$(totals_value total_line_percent)" "$(totals_value total_branch_percent)"
        printf '    "linesCovered": %s,\n    "linesTotal": %s,\n' "$(totals_value all_lines_covered)" "$(totals_value all_lines)"
        printf '    "branchesCovered": %s,\n    "branchesTotal": %s,\n' "$(totals_value all_branches_covered)" "$(totals_value all_branches)"
        printf '    "libraries": %s,\n    "librariesAtGate": %s,\n    "failingMembers": %s\n  },\n' \
            "$assembly_count" "$(totals_value libraries_at_gate)" "$total_violations"
        printf '  "libraries": %s,\n' "$(json_array_or_empty "$library_json_file")"
        printf '  "failing": %s,\n' "$(json_array_or_empty "$failing_json_file")"
        printf '  "exclusions": %s,\n' "$(json_array_or_empty "$work_directory/exclusions.json")"
        printf '  "missingLibraries": %s\n' "$(json_array_or_empty "$work_directory/missing-libraries.json")"
        printf '}\n'
    } > "$JSON_PATH" || die "Could not write $JSON_PATH."
    say "$COLOR_CYAN" "Data written to $JSON_PATH"
fi

if [ "$total_violations" -gt 0 ] || [ -s "$work_directory/missing-libraries.txt" ]; then exit 1; fi
exit 0
