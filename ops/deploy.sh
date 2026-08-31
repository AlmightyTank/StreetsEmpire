#!/bin/sh
# A deploy, on the VPS.
#
#   ./ops/deploy.sh              the commit now at the head of main, waiting for CI if it has to
#   ./ops/deploy.sh 0.2.7        a released version
#   ./ops/deploy.sh 4f3a91c...   one exact commit, which is what a rollback is
#   ./ops/deploy.sh --now        no waiting: whatever is published at this second
#
# Nothing is built here. CI builds the image once, for a commit that passed its tests, and this pulls
# what CI produced. The VPS used to run the .NET SDK and a Vite bundle itself, on the machine currently
# serving the game, for minutes at a time - and what came out was an image no test had ever seen.
#
# The repository is still pulled, because three files are read from the checkout rather than from the
# image: this compose file, the Caddyfile, and the backup script.
#
# It waits for the image, and pins to a sha rather than taking `latest`. Both come from the same
# problem: a deploy run in the minute after a push is racing a build. `latest` does not fail there, it
# succeeds and puts back the *previous* commit - a deploy that reports success and changes nothing,
# which is the worst answer of the three available. So the tag deployed is the sha of the commit the
# checkout is sitting on, which names one image and never moves, and a registry that does not have it
# yet is CI still working rather than an error. Roughly ten minutes of build, and then it goes.
#
# If this ever comes back "command not found" from a shell that can plainly see the file, the executable
# bit did not survive the trip: it is recorded in git as a file mode, and a Windows checkout with
# core.filemode=false does not record a chmod at all. `sh ops/deploy.sh` runs it either way.

set -eu

cd "$(dirname "$0")/.."

COMPOSE="docker compose -f docker-compose.prod.yml"
WAIT_MINUTES="${DEPLOY_WAIT_MINUTES:-30}"

say() { echo "[deploy] $*"; }

TAG=""
WAIT=yes
for arg in "$@"; do
    case "$arg" in
        --now | --no-wait) WAIT=no ;;
        -*)
            say "unknown option: $arg"
            exit 2
            ;;
        *) TAG="$arg" ;;
    esac
done

# --ff-only rather than a merge. A deploy that stops and says the branch has diverged is worth a great
# deal more than one that invents a merge commit on a server at two in the morning.
say "updating the checkout"
git pull --ff-only

if [ -n "$TAG" ]; then
    # A tag named by hand is not waited for. It is either already published or it is a typo, and
    # thirty minutes is a long time to spend finding out which.
    WAIT=no
elif grep -Eq '^[[:space:]]*IMAGE_TAG=[^[:space:]]' .env 2>/dev/null; then
    # Somebody pinned the deploy in .env and meant it. Leave it pinned; compose will read it.
    say "using the IMAGE_TAG pinned in .env"
elif [ "$WAIT" = yes ]; then
    TAG=$(git rev-parse HEAD)
fi

if [ -n "$TAG" ]; then
    # Ahead of .env on purpose: compose lets the environment win, so this pins one deploy without
    # editing anything.
    export IMAGE_TAG="$TAG"
    say "deploying ${TAG}"
fi

say "pulling the image"
out=$(mktemp)
trap 'rm -f "$out"' EXIT
deadline=$(($(date +%s) + WAIT_MINUTES * 60))
announced=no

while :; do
    if $COMPOSE pull api >"$out" 2>&1; then
        if [ "$announced" = yes ]; then
            say "published; pulled"
        fi
        break
    fi

    # The one failure worth waiting on is the registry saying it has never heard of this tag. Anything
    # else - no route to the host, a package still marked private - does not improve by being asked
    # again for half an hour.
    if ! grep -Eqi 'manifest unknown|not found|no such manifest' "$out"; then
        cat "$out" >&2
        say ""
        say "FAILED: the registry would not hand over the image, and not because it is missing. If that"
        say "        says denied or unauthorized, the package is still private: make it public under the"
        say "        repository's Packages, or docker login ghcr.io on this box with a read-only token."
        exit 1
    fi

    if [ "$WAIT" = no ]; then
        say "FAILED: no image published for ${IMAGE_TAG:-latest}."
        exit 1
    fi

    if [ "$(date +%s)" -ge "$deadline" ]; then
        actions=$(git config --get remote.origin.url 2>/dev/null |
            sed -e 's#^git@github\.com:#https://github.com/#' -e 's#\.git$##' -e 's#$#/actions#')
        say "FAILED: nothing published for ${IMAGE_TAG:-latest} within ${WAIT_MINUTES} minutes. Nothing was"
        say "        touched - what is running is what was running. CI is red, still going, or was never"
        say "        asked, which is what an unpushed commit looks like from here. Look:"
        say ""
        say "          ${actions:-the repository's Actions tab}"
        say ""
        say "        Or deploy the last image that does exist:"
        say ""
        say "          ./ops/deploy.sh --now"
        exit 1
    fi

    if [ "$announced" = no ]; then
        say "not published yet - CI is still building it. Waiting up to ${WAIT_MINUTES} minutes; ^C is safe."
        announced=yes
    fi
    sleep 15
done

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
