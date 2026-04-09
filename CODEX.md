# Ralph Agent Instructions

You are an autonomous Codex agent working on a software project.

## Your Task

1. Read the PRD at `prd.json` (in the same directory as this file)
2. Read the progress log at `progress.txt` (check `## Codebase Patterns` first)
3. Check that you're on the correct branch from PRD `branchName`. If not, check it out or create it from `main`
4. Pick the highest priority user story where `passes: false`
5. Implement that single user story
6. Run quality checks appropriate for the project (typecheck, lint, test, build as needed)
7. Update nearby `AGENTS.md` files if you discover reusable patterns
8. If checks pass, commit all changes with message: `feat: [Story ID] - [Story Title]`
9. Update the PRD to set `passes: true` for the completed story
10. Append your progress to `progress.txt`

## Progress Report Format

APPEND to `progress.txt` (never replace, always append):

```md
## [Date/Time] - [Story ID]
- What was implemented
- Files changed
- Quality checks run and result
- Learnings for future iterations:
  - Patterns discovered
  - Gotchas encountered
  - Useful context
---
```

The learnings section is critical. It helps future iterations avoid repeated mistakes and understand the codebase faster.

## Consolidate Patterns

If you discover a reusable pattern that future iterations should know, add it to the `## Codebase Patterns` section at the top of `progress.txt` (create it if needed).

Only add patterns that are general and reusable, not story-specific details.

## Update AGENTS.md Files

Before committing, check whether any edited directories or their parents contain an `AGENTS.md` file. If you discovered reusable knowledge that would help future work in that area, add it there.

Good additions:
- Non-obvious module conventions
- File relationships that must stay in sync
- Area-specific testing approaches
- Environment or configuration requirements

Do not add:
- Story-specific implementation notes
- Temporary debugging notes
- Information already captured well in `progress.txt`

## Quality Requirements

- Do not commit broken code
- Keep changes focused and minimal
- Follow existing code patterns
- Run the checks needed to validate the specific story

## Browser Testing

For any story that changes UI, use an available browser-testing skill or tool if one exists in the environment. If none is available, note in `progress.txt` that manual browser verification is still required.

## Stop Condition

After completing a user story, check whether all stories have `passes: true`.

If all stories are complete and passing, reply with:

```xml
<promise>COMPLETE</promise>
```

If stories remain, end normally so the next Ralph iteration can continue.

## Important

- Work on one story per iteration
- Keep the repository in a usable state between iterations
- Read the `## Codebase Patterns` section in `progress.txt` before starting
