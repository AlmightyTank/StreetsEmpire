#!/bin/sh
# Takes a dump of the game database on a schedule, checks it is readable, and throws away old ones.
#
# Runs from the same postgres image the server does, which is not incidental: pg_dump refuses to dump a
# server newer than itself, so a backup container pinned to a different major version is a backup that
# stops working the day the database is upgraded - and stops working silently, because nobody looks at
# a backup job that has been fine for a year.
#
# Three things here are worth more than the dump itself.
#
# It dumps once, immediately, on startup. A backup misconfigured at deploy time otherwise announces
# itself a day later, which is a day of believing there are backups.
#
# It writes to a temporary name and renames only on success. A rename within a directory is atomic, so
# a dump interrupted half way through can never be left sitting there looking like a good one.
#
# And it reads every archive back with pg_restore --list before accepting it. A file existing and a
# file being restorable are different claims, and only the second one is worth anything at three in
# the morning.

set -eu

: "${POSTGRES_HOST:=postgres}"
# Passed on every command below rather than left to libpq's default, and that is not tidiness. The
# database is on the host now, where port 5432 is whatever answered first - during testing that turned
# out to be an unrelated Postgres, which pg_isready happily called alive because it does not
# authenticate. A dump aimed at the wrong server is worse than no dump, because it looks like one.
: "${POSTGRES_PORT:=5432}"
: "${POSTGRES_DB:=street_empire}"
: "${POSTGRES_USER:=street_empire}"
: "${BACKUP_DIR:=/backups}"
: "${BACKUP_INTERVAL_HOURS:=24}"
: "${BACKUP_RETENTION_DAYS:=7}"

# Set on the container by compose, so anything exec'd in here can use it as well.
: "${PGPASSWORD:?PGPASSWORD is required}"
export PGPASSWORD

say() { echo "[backup] $(date -u '+%Y-%m-%dT%H:%M:%SZ') $*"; }

take_one() {
    stamp=$(date -u '+%Y%m%d-%H%M%S')
    final="${BACKUP_DIR}/${POSTGRES_DB}-${stamp}.dump"
    partial="${final}.partial"

    # -Fc is the custom format: compressed, and restorable a table at a time rather than all or
    # nothing, which is what you want when the thing you are undoing is one bad migration.
    if ! pg_dump --host="$POSTGRES_HOST" --port="$POSTGRES_PORT" \
                 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" \
                 --format=custom --compress=6 --file="$partial" 2>&1
    then
        say "FAILED: pg_dump would not complete. Nothing was written."
        rm -f "$partial"
        return 1
    fi

    # Readable, or it is not a backup. This catches a truncated write and a corrupted archive, both of
    # which leave a file of a perfectly plausible size.
    if ! pg_restore --list "$partial" > /dev/null 2>&1; then
        say "FAILED: the archive could not be read back. Discarded rather than kept."
        rm -f "$partial"
        return 1
    fi

    mv "$partial" "$final"
    say "wrote $(basename "$final") ($(du -h "$final" | cut -f1))"
}

prune() {
    # Only ever files this script named, so nothing else in the directory is at risk from a mistyped
    # BACKUP_DIR - which would otherwise be a delete loop pointed at somebody's home directory.
    gone=$(find "$BACKUP_DIR" -maxdepth 1 -type f -name "${POSTGRES_DB}-*.dump" \
                -mtime "+${BACKUP_RETENTION_DAYS}" -print -delete | wc -l)
    [ "$gone" -gt 0 ] && say "pruned ${gone} dump(s) older than ${BACKUP_RETENTION_DAYS} day(s)"
    return 0
}

mkdir -p "$BACKUP_DIR"

# The database may still be starting, or still be down after a reboot of the host it now lives on.
# Nothing orders this container after it any more - and nothing ever ordered a restart of this container
# on its own - so waiting is this loop's job.
until pg_isready --host="$POSTGRES_HOST" --port="$POSTGRES_PORT" \
                 --username="$POSTGRES_USER" --dbname="$POSTGRES_DB" > /dev/null 2>&1; do
    say "waiting for ${POSTGRES_HOST}:${POSTGRES_PORT}"
    sleep 5
done

say "${POSTGRES_HOST}:${POSTGRES_PORT} every ${BACKUP_INTERVAL_HOURS}h into ${BACKUP_DIR}, keeping ${BACKUP_RETENTION_DAYS} day(s)"

while true; do
    # Never fatal. A failed dump is loud in the log and tries again next time; exiting would take the
    # container down and stop every future attempt because one of them went wrong.
    take_one || true
    prune || true
    sleep "$((BACKUP_INTERVAL_HOURS * 3600))"
done
