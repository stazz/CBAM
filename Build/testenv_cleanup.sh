#!/bin/bash

set -xe

# First argument will be folder dedicated to store state of test environment
# Our state are various container IDs

STATE_DIR="$1"

# Stop and remove all running containers
find "${STATE_DIR}" -mindepth 1 -maxdepth 1 -type f -name 'cid_*' -exec sh -c 'docker logs $(cat "{}")' \;
find "${STATE_DIR}" -mindepth 1 -maxdepth 1 -type f -name 'cid_*' -exec sh -c 'docker rm -f $(cat "{}")' \;

# Remove the network as well
docker network rm cbam_test_nw
