#!/bin/bash
# Ralph Wiggum - Long-running AI agent loop
# Usage: ./ralph.sh [--tool amp|claude|codex] [max_iterations]

set -e

# Parse arguments
TOOL="amp"  # Default to amp for backwards compatibility
MAX_ITERATIONS=10

while [[ $# -gt 0 ]]; do
  case $1 in
    --tool)
      TOOL="$2"
      shift 2
      ;;
    --tool=*)
      TOOL="${1#*=}"
      shift
      ;;
    *)
      # Assume it's max_iterations if it's a number
      if [[ "$1" =~ ^[0-9]+$ ]]; then
        MAX_ITERATIONS="$1"
      fi
      shift
      ;;
  esac
done

# Validate tool choice
if [[ "$TOOL" != "amp" && "$TOOL" != "claude" && "$TOOL" != "codex" ]]; then
  echo "Error: Invalid tool '$TOOL'. Must be 'amp', 'claude', or 'codex'."
  exit 1
fi
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PRD_FILE="$SCRIPT_DIR/prd.json"
PROGRESS_FILE="$SCRIPT_DIR/progress.txt"
ARCHIVE_DIR="$SCRIPT_DIR/archive"
LAST_BRANCH_FILE="$SCRIPT_DIR/.last-branch"
CODEX_LAST_MESSAGE_FILE="$SCRIPT_DIR/.codex-last-message.txt"
CODEX_PROMPT_FILE="$SCRIPT_DIR/.codex-iteration-prompt.md"
PROJECT_ROOT="$(git -C "$SCRIPT_DIR" rev-parse --show-toplevel 2>/dev/null || echo "$SCRIPT_DIR")"

file_fingerprint() {
  local file="$1"
  if [ -f "$file" ]; then
    cksum "$file" | awk '{print $1 ":" $2}'
  else
    echo "missing"
  fi
}

current_story_state() {
  if [ -f "$PRD_FILE" ]; then
    jq -r '.userStories | map(select(.passes != true)) | sort_by(.priority) | .[0] | "\(.id // "none")|\(.title // "")|\(.passes // false)"' "$PRD_FILE" 2>/dev/null || echo "none||false"
  else
    echo "none||false"
  fi
}

write_codex_prompt() {
  local next_story
  next_story="$(current_story_state)"
  local story_id="${next_story%%|*}"
  local remainder="${next_story#*|}"
  local story_title="${remainder%%|*}"

  cat > "$CODEX_PROMPT_FILE" <<EOF
# Ralph Agent Instructions

You are an autonomous Codex agent working on a software project.

## Runtime Context

- Project root: \`$PROJECT_ROOT\`
- Ralph workspace: \`$SCRIPT_DIR\`
- PRD file: \`$PRD_FILE\`
- Progress log: \`$PROGRESS_FILE\`
- Current target story: \`$story_id\` - $story_title

Read and update the files at those exact paths. Do not assume the PRD or progress log live in the current working directory.

## Your Task

1. Read the PRD at \`$PRD_FILE\`
2. Read the progress log at \`$PROGRESS_FILE\` (check \`## Codebase Patterns\` first)
3. Check that you're on the correct branch from PRD \`branchName\`. If not, check it out or create it from \`main\`
4. Pick the highest priority user story where \`passes: false\`
5. Implement that single user story
6. Run quality checks appropriate for the project (typecheck, lint, test, build as needed)
7. Update nearby \`AGENTS.md\` files if you discover reusable patterns
8. If checks pass, commit all changes with message: \`feat: [Story ID] - [Story Title]\`
9. Update the PRD to set \`passes: true\` for the completed story
10. Append your progress to \`$PROGRESS_FILE\`

## Progress Report Format

APPEND to \`$PROGRESS_FILE\` (never replace, always append):

\`\`\`md
## [Date/Time] - [Story ID]
- What was implemented
- Files changed
- Quality checks run and result
- Learnings for future iterations:
  - Patterns discovered
  - Gotchas encountered
  - Useful context
---
\`\`\`

The learnings section is critical. It helps future iterations avoid repeated mistakes and understand the codebase faster.

## Consolidate Patterns

If you discover a reusable pattern that future iterations should know, add it to the \`## Codebase Patterns\` section at the top of \`$PROGRESS_FILE\` (create it if needed).

Only add patterns that are general and reusable, not story-specific details.

## Update AGENTS.md Files

Before committing, check whether any edited directories or their parents contain an \`AGENTS.md\` file. If you discovered reusable knowledge that would help future work in that area, add it there.

Good additions:
- Non-obvious module conventions
- File relationships that must stay in sync
- Area-specific testing approaches
- Environment or configuration requirements

Do not add:
- Story-specific implementation notes
- Temporary debugging notes
- Information already captured well in \`$PROGRESS_FILE\`

## Quality Requirements

- Do not commit broken code
- Keep changes focused and minimal
- Follow existing code patterns
- Run the checks needed to validate the specific story

## Browser Testing

For any story that changes UI, use an available browser-testing skill or tool if one exists in the environment. If none is available, note in \`$PROGRESS_FILE\` that manual browser verification is still required.

## Stop Condition

After completing a user story, check whether all stories have \`passes: true\`.

If all stories are complete and passing, reply with:

\`\`\`xml
<promise>COMPLETE</promise>
\`\`\`

If stories remain, end normally so the next Ralph iteration can continue.

## Important

- Work on one story per iteration
- Keep the repository in a usable state between iterations
- Read the \`## Codebase Patterns\` section in \`$PROGRESS_FILE\` before starting
- A successful iteration must update \`$PROGRESS_FILE\` and usually update \`$PRD_FILE\`
EOF
}

# Archive previous run if branch changed
if [ -f "$PRD_FILE" ] && [ -f "$LAST_BRANCH_FILE" ]; then
  CURRENT_BRANCH=$(jq -r '.branchName // empty' "$PRD_FILE" 2>/dev/null || echo "")
  LAST_BRANCH=$(cat "$LAST_BRANCH_FILE" 2>/dev/null || echo "")
  
  if [ -n "$CURRENT_BRANCH" ] && [ -n "$LAST_BRANCH" ] && [ "$CURRENT_BRANCH" != "$LAST_BRANCH" ]; then
    # Archive the previous run
    DATE=$(date +%Y-%m-%d)
    # Strip "ralph/" prefix from branch name for folder
    FOLDER_NAME=$(echo "$LAST_BRANCH" | sed 's|^ralph/||')
    ARCHIVE_FOLDER="$ARCHIVE_DIR/$DATE-$FOLDER_NAME"
    
    echo "Archiving previous run: $LAST_BRANCH"
    mkdir -p "$ARCHIVE_FOLDER"
    [ -f "$PRD_FILE" ] && cp "$PRD_FILE" "$ARCHIVE_FOLDER/"
    [ -f "$PROGRESS_FILE" ] && cp "$PROGRESS_FILE" "$ARCHIVE_FOLDER/"
    echo "   Archived to: $ARCHIVE_FOLDER"
    
    # Reset progress file for new run
    echo "# Ralph Progress Log" > "$PROGRESS_FILE"
    echo "Started: $(date)" >> "$PROGRESS_FILE"
    echo "---" >> "$PROGRESS_FILE"
  fi
fi

# Track current branch
if [ -f "$PRD_FILE" ]; then
  CURRENT_BRANCH=$(jq -r '.branchName // empty' "$PRD_FILE" 2>/dev/null || echo "")
  if [ -n "$CURRENT_BRANCH" ]; then
    echo "$CURRENT_BRANCH" > "$LAST_BRANCH_FILE"
  fi
fi

# Initialize progress file if it doesn't exist
if [ ! -f "$PROGRESS_FILE" ]; then
  echo "# Ralph Progress Log" > "$PROGRESS_FILE"
  echo "Started: $(date)" >> "$PROGRESS_FILE"
  echo "---" >> "$PROGRESS_FILE"
fi

echo "Starting Ralph - Tool: $TOOL - Max iterations: $MAX_ITERATIONS"
echo "Project root: $PROJECT_ROOT"
echo "Ralph workspace: $SCRIPT_DIR"

for i in $(seq 1 $MAX_ITERATIONS); do
  echo ""
  echo "==============================================================="
  echo "  Ralph Iteration $i of $MAX_ITERATIONS ($TOOL)"
  echo "==============================================================="

  BEFORE_PROGRESS="$(file_fingerprint "$PROGRESS_FILE")"
  BEFORE_PRD="$(file_fingerprint "$PRD_FILE")"
  BEFORE_STORY="$(current_story_state)"
  COMPLETION_TEXT=""

  # Run the selected tool with the ralph prompt
  if [[ "$TOOL" == "amp" ]]; then
    OUTPUT=$(cat "$SCRIPT_DIR/prompt.md" | amp --dangerously-allow-all 2>&1 | tee /dev/stderr) || true
  elif [[ "$TOOL" == "claude" ]]; then
    # Claude Code: use --dangerously-skip-permissions for autonomous operation, --print for output
    OUTPUT=$(claude --dangerously-skip-permissions --print < "$SCRIPT_DIR/CLAUDE.md" 2>&1 | tee /dev/stderr) || true
    COMPLETION_TEXT="$OUTPUT"
  else
    # Codex: use non-interactive exec mode rooted at the project root.
    write_codex_prompt
    rm -f "$CODEX_LAST_MESSAGE_FILE"
    OUTPUT=$(codex exec \
      --dangerously-bypass-approvals-and-sandbox \
      --skip-git-repo-check \
      -C "$PROJECT_ROOT" \
      -o "$CODEX_LAST_MESSAGE_FILE" \
      - < "$CODEX_PROMPT_FILE" 2>&1 | tee /dev/stderr) || true

    # Prefer the agent's final message when checking completion, since stdout may include CLI noise.
    if [[ -f "$CODEX_LAST_MESSAGE_FILE" ]]; then
      OUTPUT="$(cat "$CODEX_LAST_MESSAGE_FILE")"$'\n'"$OUTPUT"
      COMPLETION_TEXT="$(cat "$CODEX_LAST_MESSAGE_FILE")"
    fi
  fi

  AFTER_PROGRESS="$(file_fingerprint "$PROGRESS_FILE")"
  AFTER_PRD="$(file_fingerprint "$PRD_FILE")"
  AFTER_STORY="$(current_story_state)"
  
  # Check for completion signal
  if [[ "$TOOL" == "amp" && "$OUTPUT" == *"<promise>COMPLETE</promise>"* ]]; then
    echo ""
    echo "Ralph completed all tasks!"
    echo "Completed at iteration $i of $MAX_ITERATIONS"
    exit 0
  fi

  if [[ "$TOOL" != "amp" && "$COMPLETION_TEXT" == *"<promise>COMPLETE</promise>"* ]]; then
    echo ""
    echo "Ralph completed all tasks!"
    echo "Completed at iteration $i of $MAX_ITERATIONS"
    exit 0
  fi

  if [[ "$TOOL" == "codex" ]]; then
    if [[ "$BEFORE_PROGRESS" == "$AFTER_PROGRESS" && "$BEFORE_PRD" == "$AFTER_PRD" ]]; then
      echo ""
      echo "Codex iteration made no durable updates to prd.json or progress.txt."
      echo "Stopping early to avoid a blind loop."
      echo "Prompt used: $CODEX_PROMPT_FILE"
      exit 1
    fi

    if [[ "$BEFORE_STORY" == "$AFTER_STORY" && "$BEFORE_PROGRESS" == "$AFTER_PROGRESS" ]]; then
      echo ""
      echo "Codex did not advance the current story or append progress."
      echo "Stopping early to avoid repeating the same iteration."
      echo "Prompt used: $CODEX_PROMPT_FILE"
      exit 1
    fi
  fi
  
  echo "Iteration $i complete. Continuing..."
  sleep 2
done

echo ""
echo "Ralph reached max iterations ($MAX_ITERATIONS) without completing all tasks."
echo "Check $PROGRESS_FILE for status."
exit 1
