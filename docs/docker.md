# Docker

> [!WARNING]
> This Docker workflow is still a secondary headless/container path. It is not the primary desktop distribution mechanism for Sockseek UI.
>
> The checked-in `Dockerfile` now builds against the repository's `net10.0` target, but the default compose stack is intentionally minimal: it starts the linuxserver base image with cron support and does **not** automatically start `sockseek daemon`.

A Docker container for running Sockseek can be built from this repository. The image supports linux x86/ARM.

## What the current compose stack does

`docker compose up -d` builds the image and starts the container's init/cron environment.

It currently:

- mounts `./config` to `/config`
- mounts `./data` to `/data`
- publishes `127.0.0.1:48721:48721` for provider login callbacks such as Spotify PKCE
- does **not** start the Sockseek HTTP/SignalR daemon automatically
- does **not** publish the daemon API port `5030` by default

That makes the current compose file suitable for:

- interactive CLI use via `docker compose exec`
- scheduled cron-driven CLI jobs inside the container

It is **not** yet a reviewed one-command daemon deployment.

## Build and start the container

```shell
git clone https://github.com/fiso64/sockseek
cd sockseek
docker compose up -d
```

## Run the CLI inside the container

```shell
docker compose exec sockseek sh
sockseek --help
```

The compose stack mounts two directories relative to where `docker-compose.yml` is located:

* `/config` (host `./config`) - put your `sockseek.conf` here, then run `sockseek -c /config ...`
* `/data` (host `./data`) - use this as the download directory, for example `sockseek -p /data ...`

## Daemon / remote mode in Docker

If you want to experiment with the daemon in Docker, start it manually inside the container and add your own `5030` port mapping first. For example:

```shell
docker compose exec sockseek sockseek daemon --server-ip 0.0.0.0 --server-port 5030 -c /config
```

Then connect a client to `http://127.0.0.1:5030` only after you have explicitly published that port in your compose override or local edits.

Because this path has not had the same Sprint 0 review as the CLI/container flow, treat it as manual advanced usage rather than a polished default deployment.

## File Permissions

If you are running Docker on a **Linux Host** you should specify `user:group` permissions of the user who owns the **configuration and data directory** on the host to avoid [docker file permission problems.](https://ikriv.com/blog/?p=4698) These can be specified using the [environmental variables **PUID** and **PGID**.](https://docs.linuxserver.io/general/understanding-puid-and-pgid)

To get the UID and GID for the current user run these commands from a terminal:

* `id -u` -- prints UID
* `id -g` -- prints GID

Replace these with the corresponding variable (`PUID` `PGID`) in `docker-compose.yml`.


## Cron

One or more Sockseek commands can be run on a schedule using [cron](https://en.wikipedia.org/wiki/Cron) built into the container.

To create a schedule make a new file on the host `./config/crontabs/abc` and use it with the standard [crontab](https://en.wikipedia.org/wiki/Cron#Overview) syntax.

Make sure to restart the container after any changes to the cron file are made.

Example => Run Sockseek every Sunday at 1am, search for missing tracks from the specified Spotify playlist

```
# min   hour    day     month   weekday command
0 1 * * 0 sockseek https://open.spotify.com/playlist/6sf1WR5grXGJ6dET -c /config -p /data --index-path /data/index.sockseek --spotify-id 123456 --spotify-secret 123456
```

[crontab.guru](https://crontab.guru/) could be used to help with the scheduling expression.
