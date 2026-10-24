#!/bin/sh
# Starts the Server. With FAMILYGRAPH_MIGRATE=true it first applies the EF Core migrations, and a
# failed migration stops the start (D-011). Any arguments run instead, for example ./efbundle.
set -e
if [ "$#" -gt 0 ]; then
    exec "$@"
fi
if [ "$FAMILYGRAPH_MIGRATE" = "true" ]; then
    ./efbundle
fi
exec dotnet Server.dll
