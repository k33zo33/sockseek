FROM ubuntu:24.04

ENV DEBIAN_FRONTEND=noninteractive

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        ca-certificates \
        curl \
        libasound2t64 \
        libfontconfig1 \
        libgl1 \
        libice6 \
        libsm6 \
        libx11-6 \
        libxcursor1 \
        libxext6 \
        libxi6 \
        libxinerama1 \
        libxrandr2 \
        libxrender1 \
        tar \
        vlc \
        xauth \
        xvfb \
    && rm -rf /var/lib/apt/lists/* \
    && useradd --create-home --shell /bin/bash sockseek

COPY run-smoke.sh /usr/local/bin/run-sockseek-package-smoke
RUN chmod 0755 /usr/local/bin/run-sockseek-package-smoke

USER sockseek
ENTRYPOINT ["/usr/local/bin/run-sockseek-package-smoke"]
