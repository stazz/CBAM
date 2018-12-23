#!/bin/bash

set -xe

# First argument will be folder dedicated to store state of test environment
# Our state are various container IDs
SCRIPTPATH=$(readlink -f "$0")
SCRIPTDIR=$(dirname "$SCRIPTPATH")
STATE_DIR="$1"
PG_SSL_KEY="${STATE_DIR}/pg_ssl_key"
PG_SSL_CRT="${STATE_DIR}/pg_ssl_crt"
STATE_DIR="$(realpath --relative-to="." "$STATE_DIR")"

# We must first create all required containers...
docker create --expose 4222 --name cbam_test_nats --cidfile "${STATE_DIR}/cid_nats" nats:1.3.0-linux
docker create --expose 5432 --name cbam_test_pgsql -e POSTGRES_PASSWORD=postgres --cidfile "${STATE_DIR}/cid_pgsql" postgres:11.1-alpine
openssl req -newkey rsa:4096 -nodes -keyout "${PG_SSL_KEY}" -x509 -days 9999 -out "${PG_SSL_CRT}" -subj "/CN=cbam_test_pgsql_ssl"
chmod u=rw,g=r,o= "${PG_SSL_KEY}"
chmod u=rw,g=r,o= "${PG_SSL_CRT}"
docker create --expose 5432 --name cbam_test_pgsql_ssl -e POSTGRES_PASSWORD=postgres -v "${PG_SSL_KEY}:/etc/postgresql/server.key:ro" -v "${PG_SSL_CRT}:/etc/postgresql/server.crt:ro" -v "${SCRIPTDIR}/postgresql.conf.ssl:/etc/postgresql/postgresql.conf:ro" -v /etc/passwd:/etc/passwd:ro --cidfile "${STATE_DIR}/cid_pgsql_ssl" --user "$(id -u):$(id -g)" postgres:11.1-alpine -c 'config_file=/etc/postgresql/postgresql.conf'
# TODO create nginx/apache/other webserver so the CBAM HTTP tests won't need to use google.com

# Create common network
docker network create -d bridge cbam_test_nw

# Connect containers to network
find "${STATE_DIR}" -mindepth 1 -maxdepth 1 -type f -name 'cid_*' -exec sh -c 'docker network connect cbam_test_nw $(cat "{}")' \;

# And then start all containers
find "${STATE_DIR}" -mindepth 1 -maxdepth 1 -type f -name 'cid_*' -exec sh -c 'docker start $(cat "{}")' \;

# Wait till all endpoints respond
"${SCRIPTDIR}/wait-for.sh" -t 60 cbam_test_nats:4222 cbam_test_pgsql:5432 cbam_test_pgsql_ssl:5432
