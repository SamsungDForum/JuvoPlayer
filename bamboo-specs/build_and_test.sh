#!/bin/bash

echo "Running build_and_test.sh"

yell() { echo "$0: $*" >&2; }
die() { yell "$*"; exit 111; }
try() { "$@" || die "cannot $*"; }

SCRIPT=`realpath $0`
SCRIPT_PATH=`dirname ${SCRIPT}`
CERTS_PATH="${SCRIPT_PATH}"/certs

echo "SCRIPT=${SCRIPT}"
echo "SCRIPT_PATH=${SCRIPT_PATH}"
echo "CERTS_PATH=${CERTS_PATH}"

try dotnet build /p:"AuthorPath=${CERTS_PATH}/partner_2019.p12;AuthorPass=${bamboo_AuthorPassword}" /p:"DistributorPath=${CERTS_PATH}/tizen-distributor-signer.p12;DistributorPass=${bamboo_DistributorPassword}"

try dotnet test JuvoPlayer.Tests/JuvoPlayer.Tests.csproj --logger:trx -f netcoreapp2.0

echo "Cyclomatic complexity results:"
CCM.exe bamboo-specs/ccm.config | tee CyclomaticComplexityReport.log
