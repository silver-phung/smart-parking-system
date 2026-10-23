#!/bin/bash
set -euo pipefail

repo_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
helper_project="$repo_root/scripts/SharedDatabase/SharedDatabase.csproj"

if ! command -v dotnet >/dev/null 2>&1; then
    printf '%s\n' 'Hay cai .NET 10 SDK, mo lai terminal va chay lai script.' >&2
    exit 1
fi

exec dotnet run --project "$helper_project" -- --root "$repo_root" "$@"
