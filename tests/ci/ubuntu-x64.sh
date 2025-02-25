#!/bin/sh

apt-get update

apt-get install -y      \
    cmake               \
    wget                \
    build-essential     \
    apt-transport-https \
    dotnet-sdk-8.0      

wget https://github.com/go-task/task/releases/download/v3.41.0/task_linux_amd64.deb -O go-task.deb
dpkg -i go-task.deb
rm go-task.deb

wget https://www.vaughnnugent.com/public/resources/software/builds/vnbuild/f517973e79d1b6c29ea451e48e6c4908827a879c/vnbuild/linux-x64-release.tgz -O vnbuild.tgz
tar -xzf vnbuild.tgz -C /usr/local/bin
chmod +x /usr/local/bin/vnbuild
rm vnbuild.tgz
