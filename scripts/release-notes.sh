#!/usr/bin/env bash
#
# Prints the CHANGELOG.md section for one version, for use as GitHub release
# notes. Exits non-zero if there is no section for it, so a tag pushed without a
# changelog entry fails the release before anything is built.
#
# Usage: ./scripts/release-notes.sh 0.7.0
set -euo pipefail

VERSION="${1:-}"
if [[ -z "$VERSION" ]]; then
  echo "Usage: $(basename "$0") <version>" >&2
  exit 2
fi

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CHANGELOG="$ROOT_DIR/CHANGELOG.md"

# index() rather than a regex because the brackets in "## [0.7.0]" need escaping
# that does not survive being embedded in a shell string.
NOTES="$(awk -v header="## [$VERSION]" '
  index($0, header) == 1 { found = 1; next }
  found && index($0, "## [") == 1 { exit }
  found && index($0, "[") == 1 { exit }
  found { print }
' "$CHANGELOG")"

if [[ -z "${NOTES//[[:space:]]/}" ]]; then
  echo "CHANGELOG.md has no '## [$VERSION]' section." >&2
  exit 1
fi

printf '%s\n' "$NOTES"
