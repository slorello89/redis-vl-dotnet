#!/usr/bin/env bash

set -euo pipefail

mapfile -t example_projects < <(find examples -name '*.csproj' -print | sort)

if [[ ${#example_projects[@]} -eq 0 ]]; then
  echo "No example projects found under examples/." >&2
  exit 1
fi

for project in "${example_projects[@]}"; do
  echo "Building ${project}"
  dotnet build "${project}"
done
