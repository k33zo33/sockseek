#!/usr/bin/env bash
set -euo pipefail

archive=${1:?Usage: run-sockseek-package-smoke <package.tar.gz>}
work_dir=$(mktemp -d)
package_dir="$work_dir/package"
daemon_log="$work_dir/daemon.log"
desktop_log="$work_dir/desktop.log"
daemon_pid=''
desktop_pid=''

cleanup() {
    if [[ -n "$desktop_pid" ]] && kill -0 "$desktop_pid" 2>/dev/null; then
        kill "$desktop_pid" 2>/dev/null || true
        wait "$desktop_pid" 2>/dev/null || true
    fi
    if [[ -n "$daemon_pid" ]] && kill -0 "$daemon_pid" 2>/dev/null; then
        kill "$daemon_pid" 2>/dev/null || true
        wait "$daemon_pid" 2>/dev/null || true
    fi
    rm -rf "$work_dir"
}
trap cleanup EXIT

mkdir -p "$package_dir" "$work_dir/home" "$work_dir/runtime"
tar -xzf "$archive" -C "$package_dir"

for required_file in \
    Sockseek.Desktop \
    daemon/Sockseek.Server \
    install.sh \
    uninstall.sh \
    LICENSE \
    THIRD-PARTY-NOTICES \
    SECURITY.md \
    docs/beta-limitations.md \
    release-metadata.json \
    sbom.spdx.json \
    closed-beta-tester-instructions.md; do
    test -f "$package_dir/$required_file"
done

test -x "$package_dir/Sockseek.Desktop"
test -x "$package_dir/daemon/Sockseek.Server"
test -x "$package_dir/install.sh"
test -x "$package_dir/uninstall.sh"

export HOME="$work_dir/home"
export XDG_CONFIG_HOME="$HOME/.config"
export XDG_DATA_HOME="$HOME/.local/share"
export XDG_CACHE_HOME="$HOME/.cache"
export XDG_RUNTIME_DIR="$work_dir/runtime"
chmod 0700 "$XDG_RUNTIME_DIR"

"$package_dir/daemon/Sockseek.Server" >"$daemon_log" 2>&1 &
daemon_pid=$!
health=''
for _ in $(seq 1 30); do
    if ! kill -0 "$daemon_pid" 2>/dev/null; then
        cat "$daemon_log"
        echo 'Packaged daemon exited before health became ready.' >&2
        exit 1
    fi
    if health=$(curl --fail --silent --show-error http://127.0.0.1:5030/health 2>/dev/null); then
        break
    fi
    sleep 1
done
if [[ "$health" != *'"status":"ok"'* ]]; then
    cat "$daemon_log"
    echo 'Packaged daemon did not return a healthy response.' >&2
    exit 1
fi
echo "Packaged daemon health: $health"
kill "$daemon_pid"
wait "$daemon_pid" 2>/dev/null || true
daemon_pid=''

xvfb-run -a "$package_dir/Sockseek.Desktop" >"$desktop_log" 2>&1 &
desktop_pid=$!
desktop_health=''
desktop_daemon_pid=''
desktop_daemon_endpoint=''
for _ in $(seq 1 30); do
    if ! kill -0 "$desktop_pid" 2>/dev/null; then
        cat "$desktop_log"
        echo 'Packaged Desktop exited during startup.' >&2
        exit 1
    fi

    desktop_daemon_pid=$(pgrep -f -- "$package_dir/daemon/Sockseek.Server" | head -n 1 || true)
    if [[ -n "$desktop_daemon_pid" ]]; then
        desktop_daemon_endpoint=$(
            ss -ltnpH \
                | awk -v process_marker="pid=$desktop_daemon_pid," \
                    '$0 ~ process_marker && $4 ~ /^127[.]0[.]0[.]1:/ { print $4; exit }')
        if [[ -n "$desktop_daemon_endpoint" ]] \
            && desktop_health=$(curl --fail --silent --show-error "http://$desktop_daemon_endpoint/health" 2>/dev/null); then
            break
        fi
    fi
    sleep 1
done
if [[ "$desktop_health" != *'"status":"ok"'* ]]; then
    cat "$desktop_log"
    ps -ef
    ss -ltnp
    echo 'Packaged Desktop did not start its local daemon.' >&2
    exit 1
fi
echo "Packaged Desktop local daemon health ($desktop_daemon_endpoint): $desktop_health"
kill "$desktop_pid"
wait "$desktop_pid" 2>/dev/null || true
desktop_pid=''

mkdir -p "$work_dir/data"
"$package_dir/install.sh" "$work_dir/install" "$work_dir/bin"
test -x "$work_dir/install/Sockseek.Desktop"
test -L "$work_dir/bin/sockseek-desktop"
"$package_dir/uninstall.sh" "$work_dir/install" "$work_dir/bin" "$work_dir/data"
test ! -e "$work_dir/install"
test ! -e "$work_dir/bin/sockseek-desktop"
test -d "$work_dir/data"

echo 'Ubuntu 24.04 exact package smoke passed.'
