"""Writes a Gource custom log of every branch's history to stdout.

Gource on its own reads only HEAD. This walks every local and remote branch except
the gource media branch, so feature work shows up before it reaches master, and
prints one "timestamp|author|A/M/D|/path" line per file change, oldest first.

A commit with a Co-authored-by trailer is drawn once for each author, so work Claude
co-authored shows Claude and the human at the same files. Every Claude model version
is drawn as one user, "Claude". Runs of whitespace in a name collapse to one space, so a
typo in git's user.name does not split one person into two.
"""
import subprocess
import sys

GIT = [
    'git', 'log',
    '--exclude=gource', '--branches',
    '--exclude=origin/gource', '--exclude=origin/HEAD', '--remotes',
    '--raw', '--no-renames',
    '--pretty=format:commit|%at|%aN|%(trailers:key=Co-authored-by,valueonly=true,separator=;)',
]


def display_name(raw):
    """The Gource user name for a git author or co-author value."""
    name = ' '.join(raw.split('<', 1)[0].split())
    return 'Claude' if name.startswith('Claude') else name


def main():
    text = subprocess.run(GIT, check=True, capture_output=True, encoding='utf-8').stdout
    rows = []
    stamp, authors = None, []
    for line in text.splitlines():
        if line.startswith('commit|'):
            _, stamp, author, coauthors = line.split('|', 3)
            authors = [display_name(author)]
            for value in coauthors.split(';'):
                if value.strip() and display_name(value) not in authors:
                    authors.append(display_name(value))
        elif line.startswith(':') and stamp:
            meta, path = line.split('\t', 1)
            status = meta.split()[4][0]
            kind = status if status in 'AD' else 'M'
            for author in authors:
                rows.append((int(stamp), author, kind, path))
    rows.sort(key=lambda r: r[0])
    out = sys.stdout
    for stamp, author, kind, path in rows:
        out.write(f'{stamp}|{author}|{kind}|/{path}\n')


if __name__ == '__main__':
    main()
