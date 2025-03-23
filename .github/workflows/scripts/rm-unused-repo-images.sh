#!/bin/bash

[ -z "$1" ] && {
  echo "Error: Repository argument required." >&2
  exit 1
}

REPO="$1"

ALL_IMAGES=$(docker images "$REPO" --format '{{.Repository}}:{{.Tag}}')
[ -z "$ALL_IMAGES" ] && exit 0

RUNNING_IMAGES=$(docker ps -q | sort -u | xargs -r -n 1 docker inspect --format '{{.Config.Image}}' | grep "^$REPO")

UNUSED_IMAGES=$(echo "$ALL_IMAGES" | grep -v -f <(echo "$RUNNING_IMAGES"))

[ -z "$UNUSED_IMAGES" ] && exit 0

echo "$UNUSED_IMAGES" | xargs -r docker rmi 2>/dev/null || {
  echo "Error: Some images could not be removed." >&2
  exit 1
}