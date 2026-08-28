#!/bin/sh
# A deploy, on the VPS.
#
#   ./ops/deploy.sh              the newest build of main
#   ./ops/deploy.sh 0.2.6        a released version
#   ./ops/deploy.sh 4f3a91c...   one exact commit, which is what a rollback is
#
# Nothing is built here. CI builds the image once, for a commit that passed its tests, and this pulls
# what CI produced. The VPS used to run the .NET SDK and a Vite bundle itself, on the machine currently
# serving the game, for minutes at a time - and what came out was an image no test had ever seen.
#
# The repository is still pulled, because three files are read from the checkout rather than from the
# image: this compose file, the Caddyfile, and the backup script.

set -eu

cd "$(dirname "$0")/.."

COMPOSE="docker compose -f docker-compose.prod.yml"
TAG="${1:-}"

say() { echo "[deploy] $*"; }

# --ff-only rather than a merge. A deploy that stops and says the branch has diverged is worth a great
# deal more than one that invents a merge commit on a server at two in the morning.
say "updating the checkout"
git pull --ff-only

if [ -n "$TAG" ]; then
    # Ahead of .env on purpose: compose lets the environment win, so this pins one deploy without
    # editing anything. The next run with no argument goes back to whatever .env says.
    export IMAGE_TAG="$TAG"
    say "pinned to ${TAG}"
fi

say "pulling the image"
$COMPOSE pull api

say "starting"
$COMPOSE up -d

# Up is not the same as working. The app migrates on boot and can fail there - a bad migration, a
# database that is not answering - and the container exits and is restarted, which from the outside
# looks like a deploy that went fine right up until somebody loads the page.
say "waiting for it to answer"
i=0
while [ "$i" -lt 60 ]; do
    if health=$($COMPOSE exec -T api curl --fail --silent http://localhost:8080/api/health 2>/dev/null); then
        say "up: ${health}"
        say "done"
        exit 0
    fi
    i=$((i + 1))
    sleep 2
done

say "FAILED: it did not answer within two minutes. It has not been rolled back - it may still be"
say "        migrating, and interrupting that is worse than waiting. Look first:"
say ""
say "          $COMPOSE logs --tail=50 api"
say ""
say "        Then, if it is not coming up, go back to the commit that was working:"
say ""
say "          ./ops/deploy.sh <the previous commit sha>"
exit 1
