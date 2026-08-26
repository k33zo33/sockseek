FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build

ARG TARGETPLATFORM
ARG DOCKER_ARCH

WORKDIR /src

COPY --chown=root:root . /src

RUN if [ "$DOCKER_ARCH" = "amd64" ] || [ "$TARGETPLATFORM" = "linux/amd64" ]; then export DN_RUNTIME=linux-musl-x64; echo 'Building x64'; fi \
    && if [ "$DOCKER_ARCH" = "arm64" ] || [ "$TARGETPLATFORM" = "linux/arm64" ]; then export DN_RUNTIME=linux-musl-arm64; echo 'Build ARM'; fi \
    && test -n "$DN_RUNTIME" \
    && dotnet publish /src/Sockseek.Cli/Sockseek.Cli.csproj -c Release -r "$DN_RUNTIME" -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained=true -o /out \
    && rm -f /out/*.pdb

FROM ghcr.io/linuxserver/baseimage-alpine:3.20 AS app

ENV TZ=Etc/GMT

RUN ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone

RUN \
  echo "**** install runtime packages ****" && \
  apk --no-cache add \
    icu-libs \
    libgcc \
    libstdc++ \
    zlib && \
  echo "**** cleanup ****" && \
  rm -rf \
    /root/.cache \
    /tmp/*

ENV DOCKER_MODS=linuxserver/mods:universal-cron \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0

COPY docker/root/ /

COPY --from=build /out/ /usr/bin/
