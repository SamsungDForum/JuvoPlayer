#!/bin/bash

echo "Running build_and_test.sh"

dotnet sln remove NUIPlayer/NUIPlayer.csproj
dotnet restore
dotnet build -c Release
dotnet test JuvoPlayer.Tests/JuvoPlayer.Tests.csproj --logger:trx -f netcoreapp2.0