#!/bin/bash

echo "Running build_and_test.sh"

dotnet sln remove NUIPlayer/NUIPlayer.csproj

ACTIVE_PROFILE=`tizen security-profiles list | grep "O.*$" | awk -F "[ ]+" '{ print $1 }'`
tizen build-cs -C Release -s ${ACTIVE_PROFILE}

dotnet test JuvoPlayer.Tests/JuvoPlayer.Tests.csproj --logger:trx -f netcoreapp2.0