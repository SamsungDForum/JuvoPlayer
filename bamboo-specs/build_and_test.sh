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

#try dotnet sln remove JuvoReactNative/Tizen/JuvoReactNative.csproj

#try dotnet build /nodeReuse:false /p:"AuthorPath=${CERTS_PATH}/partner_2019.p12;AuthorPass=${bamboo_AuthorPassword}" /p:"DistributorPath=${CERTS_PATH}/tizen-distributor-signer.p12;DistributorPass=${bamboo_DistributorPassword}"
try dotnet build /nodeReuse:false /p:"AuthorPath=/home/p.buczynski/SamsungCertificate/t55_linux_local/author.p12;AuthorPass=1234567890" /p:"DistributorPath=/home/p.buczynski/SamsungCertificate/t55_linux_local/distributor.p12;DistributorPass=1234567890"

try dotnet test /nodeReuse:false JuvoPlayer.Tests/JuvoPlayer.Tests.csproj --logger:trx -f netcoreapp2.0

#echo "Cyclomatic complexity results:"
#CCM.exe bamboo-specs/ccm.config | tee CyclomaticComplexityReport.log
