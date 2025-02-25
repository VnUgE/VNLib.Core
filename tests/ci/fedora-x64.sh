#!/bin/sh
set -e

dnf install -y  \
    cmake       \
    curl        \
    gcc         \
    go-task     \
    git         \
    dotnet-sdk-8.0


curl https://www.vaughnnugent.com/public/resources/software/builds/vnbuild/f517973e79d1b6c29ea451e48e6c4908827a879c/vnbuild/linux-x64-release.tgz -o vnbuild.tgz
tar -xzf vnbuild.tgz -C /usr/local/bin
chmod +x /usr/local/bin/vnbuild
rm vnbuild.tgz

vnbuild --version