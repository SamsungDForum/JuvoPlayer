#!/bin/bash

echo "Running build_and_test.sh"

yell() { echo "$0: $*" >&2; }
die() { yell "$*"; exit 111; }
try() { "$@" || die "cannot $*"; }

ACTIVE_PROFILE=`tizen security-profiles list | grep "O.*$" | awk -F "[ ]+" '{ print $1 }'`
try tizen build-cs -C Release -s ${ACTIVE_PROFILE}

try dotnet test JuvoPlayer.Tests/JuvoPlayer.Tests.csproj --logger:trx -f netcoreapp2.0