#!/usr/bin/env bash
set -euo pipefail

usage() {
  cat <<'EOF'
Usage:
  ./tool/ai_helper.sh review [--helper gemini|claude] [--ref <git-range>]
  ./tool/ai_helper.sh ask [--helper gemini|claude] "<question>"

Defaults:
  helper = gemini
  review = staged diff, else working tree diff, else HEAD~1..HEAD

Environment overrides:
  HELPER_DEFAULT   Default helper (gemini|claude)
  GEMINI_MODEL     Optional Gemini model
  CLAUDE_MODEL     Optional Claude model
EOF
}

repo_root=$(git rev-parse --show-toplevel 2>/dev/null || true)
if [[ -z "${repo_root}" ]]; then
  echo "Not inside a git repository." >&2
  exit 1
fi
cd "$repo_root"

mode="${1:-}"
if [[ -z "$mode" ]]; then
  usage
  exit 1
fi
shift || true

helper="${HELPER_DEFAULT:-gemini}"
ref_range=""
question=""

while [[ $# -gt 0 ]]; do
  case "$1" in
    --helper)
      helper="$2"
      shift 2
      ;;
    --ref)
      ref_range="$2"
      shift 2
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      if [[ "$mode" == "ask" ]]; then
        if [[ -n "$question" ]]; then
          question+=" "
        fi
        question+="$1"
      else
        echo "Unknown argument: $1" >&2
        usage
        exit 1
      fi
      shift
      ;;
  esac
done

case "$helper" in
  gemini|claude) ;;
  *) echo "Unsupported helper: $helper" >&2; exit 1 ;;
esac

need_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "Missing command: $1" >&2; exit 1; }
}

run_gemini() {
  local prompt="$1"
  local input_file="$2"
  local args=(--approval-mode plan --skip-trust)
  if [[ -n "${GEMINI_MODEL:-}" ]]; then
    args+=(--model "$GEMINI_MODEL")
  fi
  if [[ -n "$input_file" ]]; then
    gemini "${args[@]}" -p "$prompt" < "$input_file"
  else
    gemini "${args[@]}" -p "$prompt"
  fi
}

run_claude() {
  local prompt="$1"
  local input_file="$2"
  local args=(--print --permission-mode plan)
  if [[ -n "${CLAUDE_MODEL:-}" ]]; then
    args+=(--model "$CLAUDE_MODEL")
  fi
  if [[ -n "$input_file" ]]; then
    claude "${args[@]}" "$prompt" --add-dir "$repo_root" < "$input_file"
  else
    claude "${args[@]}" "$prompt" --add-dir "$repo_root"
  fi
}

run_helper() {
  local prompt="$1"
  local input_file="$2"
  if [[ "$helper" == "gemini" ]]; then
    need_cmd gemini
    run_gemini "$prompt" "$input_file"
  else
    need_cmd claude
    run_claude "$prompt" "$input_file"
  fi
}

review_mode() {
  local tmp diff_target diff_mode
  tmp=$(mktemp)
  trap 'rm -f "$tmp"' EXIT

  if [[ -n "$ref_range" ]]; then
    diff_mode="range:$ref_range"
    git diff --stat "$ref_range" > "$tmp"
    printf '\n--- DIFF ---\n' >> "$tmp"
    git diff --no-ext-diff --unified=3 "$ref_range" >> "$tmp"
  elif ! git diff --cached --quiet; then
    diff_mode="staged"
    git diff --cached --stat > "$tmp"
    printf '\n--- DIFF ---\n' >> "$tmp"
    git diff --cached --no-ext-diff --unified=3 >> "$tmp"
  elif ! git diff --quiet; then
    diff_mode="working-tree"
    git diff --stat > "$tmp"
    printf '\n--- DIFF ---\n' >> "$tmp"
    git diff --no-ext-diff --unified=3 >> "$tmp"
  elif git rev-parse --verify HEAD~1 >/dev/null 2>&1; then
    diff_mode="last-commit"
    git diff --stat HEAD~1..HEAD > "$tmp"
    printf '\n--- DIFF ---\n' >> "$tmp"
    git diff --no-ext-diff --unified=3 HEAD~1..HEAD >> "$tmp"
  else
    echo "No diff available to review." >&2
    exit 1
  fi

  cat >> "$tmp" <<EOF

--- REPO STATUS ---
$(git status --short --branch)
EOF

  local prompt
  prompt=$(cat <<EOF
You are performing a read-only code review for the Sockseek repository.

Focus on:
- correctness and regressions
- breaking API/CLI behavior
- active sprint scope discipline
- test coverage gaps
- security/privacy/license concerns

Use the provided diff and status only. Do not propose broad rewrites. Return:
1. Critical issues
2. Medium issues
3. Nice-to-have notes
4. Verdict (approve / approve with follow-ups / block)

Review target: $diff_mode
EOF
)

  run_helper "$prompt" "$tmp"
}

ask_mode() {
  if [[ -z "$question" ]]; then
    echo "Missing question." >&2
    usage
    exit 1
  fi

  local prompt
  prompt=$(cat <<EOF
You are a read-only engineering helper for the Sockseek repository at $repo_root.

Answer concisely and concretely. Prefer actionable guidance over theory. Respect current repository state and avoid suggesting provider playback or other changes that conflict with the repo's AGENTS.md/product docs.

Question:
$question
EOF
)

  run_helper "$prompt" ""
}

case "$mode" in
  review) review_mode ;;
  ask) ask_mode ;;
  *) usage; exit 1 ;;
esac
