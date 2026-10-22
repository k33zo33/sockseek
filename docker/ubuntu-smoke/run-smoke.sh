#!/usr/bin/env bash
set -euo pipefail

archive=${1:?Usage: run-sockseek-package-smoke <package.tar.gz> [evidence.json]}
evidence_path=${2:-}
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

assert_process_listens_only_on_loopback() {
    local process_id=$1
    local listeners
    listeners=$(ss -ltnpH | awk -v process_marker="pid=$process_id," '$0 ~ process_marker { print $4 }')
    if [[ -z "$listeners" ]]; then
        echo "Process $process_id has no TCP listener to validate." >&2
        exit 1
    fi

    while IFS= read -r endpoint; do
        case "$endpoint" in
            127.*|\[::1\]:*) ;;
            *)
                echo "Process $process_id exposed a non-loopback TCP listener: $endpoint" >&2
                exit 1
                ;;
        esac
    done <<<"$listeners"
}

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

session_token=$(tr -d '-' </proc/sys/kernel/random/uuid)
account_username="package-smoke-${session_token:0:12}"
account_password="$(tr -d '-' </proc/sys/kernel/random/uuid)$(tr -d '-' </proc/sys/kernel/random/uuid)"
daemon_data_dir="$work_dir/daemon-data"
secret_dir="$daemon_data_dir/secrets"
mkdir -p "$daemon_data_dir"

start_daemon() {
    SockseekServer__ConfigDir="$daemon_data_dir" \
    SockseekServer__SessionToken="$session_token" \
        "$package_dir/daemon/Sockseek.Server" >>"$daemon_log" 2>&1 &
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
}

: >"$daemon_log"
start_daemon
echo "Packaged daemon health: $health"
assert_process_listens_only_on_loopback "$daemon_pid"

anonymous_status=$(curl --silent --output /dev/null --write-out '%{http_code}' \
    http://127.0.0.1:5030/api/v1/system/info)
if [[ "$anonymous_status" != '401' ]]; then
    echo "Protected API returned HTTP $anonymous_status without a session token." >&2
    exit 1
fi

account_response=$(
    printf '{"username":"%s","password":"%s"}' "$account_username" "$account_password" \
        | curl --fail --silent --show-error \
            --request PUT \
            --header "Authorization: Bearer $session_token" \
            --header 'Content-Type: application/json' \
            --data-binary @- \
            http://127.0.0.1:5030/api/v1/soulseek/account
)
if [[ "$account_response" != *"\"username\":\"$account_username\""* ]] \
    || [[ "$account_response" != *'"isConfigured":true'* ]] \
    || [[ "$account_response" != *'"credentialStatus":"configured"'* ]] \
    || [[ "$account_response" == *"$account_password"* ]] \
    || [[ "$account_response" == *'secretReference'* ]]; then
    echo 'Packaged Soulseek account configure response was invalid or exposed secret data.' >&2
    exit 1
fi

test "$(stat -c '%a' "$secret_dir")" = '700'
test "$(stat -c '%a' "$secret_dir/master.key")" = '600'
mapfile -t secret_files < <(find "$secret_dir" -maxdepth 1 -type f -name '*.secret')
if [[ "${#secret_files[@]}" -ne 1 ]]; then
    echo "Expected one encrypted Soulseek secret payload, found ${#secret_files[@]}." >&2
    exit 1
fi
test "$(stat -c '%a' "${secret_files[0]}")" = '600'
if grep --binary-files=text --fixed-strings --quiet -- "$account_password" "${secret_files[0]}"; then
    echo 'Packaged Soulseek secret payload contains the plaintext password.' >&2
    exit 1
fi
account_password=''

kill "$daemon_pid"
wait "$daemon_pid" 2>/dev/null || true
daemon_pid=''
start_daemon
assert_process_listens_only_on_loopback "$daemon_pid"

account_response=$(curl --fail --silent --show-error \
    --header "Authorization: Bearer $session_token" \
    http://127.0.0.1:5030/api/v1/soulseek/account)
if [[ "$account_response" != *"\"username\":\"$account_username\""* ]] \
    || [[ "$account_response" != *'"isConfigured":true'* ]] \
    || [[ "$account_response" != *'"credentialStatus":"configured"'* ]]; then
    echo 'Packaged Soulseek account was not restored from the secret store after daemon restart.' >&2
    exit 1
fi

account_response=$(curl --fail --silent --show-error \
    --request DELETE \
    --header "Authorization: Bearer $session_token" \
    http://127.0.0.1:5030/api/v1/soulseek/account)
if [[ "$account_response" != *'"isConfigured":false'* ]] \
    || [[ "$account_response" != *'"credentialStatus":"not_configured"'* ]] \
    || find "$secret_dir" -maxdepth 1 -type f -name '*.secret' -print -quit | grep --quiet .; then
    echo 'Packaged Soulseek account disconnect did not remove local account state.' >&2
    exit 1
fi
account_response=''
echo 'Packaged daemon passed secret-backed account restart restore, cleanup, and Unix permission smoke.'

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
assert_process_listens_only_on_loopback "$desktop_daemon_pid"
desktop_anonymous_status=$(curl --silent --output /dev/null --write-out '%{http_code}' \
    "http://$desktop_daemon_endpoint/api/v1/system/info")
if [[ "$desktop_anonymous_status" != '401' ]]; then
    echo "Desktop child daemon protected API returned HTTP $desktop_anonymous_status without a session token." >&2
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

if [[ -n "$evidence_path" ]]; then
    # Values come from the pinned runtime image and uname, not package or user input.
    . /etc/os-release
    architecture=$(uname -m)
    printf '%s\n' \
        '{' \
        '  "schemaVersion": 1,' \
        "  \"distributionId\": \"$ID\"," \
        "  \"distributionVersion\": \"$VERSION_ID\"," \
        "  \"architecture\": \"$architecture\"," \
        '  "checks": {' \
        '    "archiveExtracted": true,' \
        '    "requiredPayloadPresent": true,' \
        '    "executableModes": true,' \
        '    "daemonHealth": true,' \
        '    "daemonProtectedApiRejectsAnonymous": true,' \
        '    "daemonSoulseekAccountLifecycle": true,' \
        '    "daemonSoulseekAccountRestartRestore": true,' \
        '    "daemonSoulseekSecretRemovedAfterDisconnect": true,' \
        '    "secretPermissions": true,' \
        '    "secretEncryptedAtRest": true,' \
        '    "desktopStartedPackagedDaemon": true,' \
        '    "desktopDaemonHealth": true,' \
        '    "desktopDaemonProtectedApiRejectsAnonymous": true,' \
        '    "loopbackOnly": true,' \
        '    "installUninstall": true,' \
        '    "userDataPreserved": true' \
        '  }' \
        '}' >"$evidence_path"
fi

echo 'Ubuntu 24.04 exact package smoke passed.'
