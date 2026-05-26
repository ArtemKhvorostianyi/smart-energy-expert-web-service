#!/usr/bin/env sh
cd "$(dirname "$0")"
export PORT=5010
export ASPNETCORE_URLS=http://127.0.0.1:5010
exec ivy run --port 5010 --browse --i-kill-for-this-port "$@"
