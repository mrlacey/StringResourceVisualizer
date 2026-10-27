---
mode: agent
description: Generates a pull request description for String Resource Visualizer by analyzing the current branch's changes and filling in the pull request template.
---

# Create PR Description

## Context

Pull request template: `.github/PULL_REQUEST_TEMPLATE.md`. This repository has none at the moment, so use the default structure under `## Output`. If the file exists, read it (for example `Get-Content .github/PULL_REQUEST_TEMPLATE.md` in PowerShell from the repository root) and follow it exactly instead.

## Task

Analyze the current branch's git diff and commit history relative to the remote base branch (`origin/main`, or the base that the user names, for example `mrlacey/main` for a pull request to the original repository), then produce a fully filled-in PR description that conforms to the template.

## Steps

1. Identify the exact section structure, checkbox labels, and required ordering: from the template when it exists, otherwise from the default structure under `## Output`.
2. Run `git diff origin/main...HEAD --stat`, `git diff origin/main...HEAD`, and `git log origin/main...HEAD --oneline` to determine exactly what changed in the current branch.
3. Fill `## Proposed Changes` with a concise 3–6 sentence summary of the concrete behavior, UI, option, or documentation changes visible in the diff.
4. Set `## Types of changes` checkboxes strictly from evidence in commits and diff:
   - mark bugfix only when existing behavior is corrected
   - mark new feature only when new user-facing capability is introduced
   - mark breaking change only when existing behavior, settings, or supported Visual Studio versions are intentionally changed
5. Evaluate `## Checklist` strictly from branch evidence:
   - the test project covers only `ResourceAdornmentManager.FormatDisplayText`: mark the tests item only if a passing run is visible from evidence, and never claim that the tests cover the adornments, the options page or the package load
   - mark the build item only if a successful build is visible from evidence; otherwise leave it unchecked and add a brief inline note
   - mark the Experimental instance item only if the user says that the change was verified there
   - mark documentation-related items only if `README.md` and `CHANGELOG.md`, or XML comments were changed in the diff
6. Replace all placeholder/template text with branch-specific content and include `## Further comments` only when the change is large or architecturally significant.

## Guidelines

- Be factual and precise – base every statement on the actual diff and commits, not assumptions.
- Keep the **Proposed Changes** section concise but informative (3–6 sentences max).
- For checklist items that cannot be determined from the diff alone, leave them unchecked and add a short inline note.
- Do not invent issue numbers or links unless they appear in commit messages or branch names.
- Use plain English; avoid vague filler phrases like "various improvements".
- Do not start the summary with "This branch adds".
- Use only en dashes (`–`) for dashes; never use em dashes (`—`).
- Always wrap names and versions into backticks (for example, `ResourceAdornmentManager`) when referring to them in the description.
- Preserve all original template headings, checkbox syntax, and section order exactly.

## Output

A single fenced markdown code block containing the fully filled-out PR description, ready to copy and paste directly into GitHub. Do not escape any markdown syntax inside the block. Match the structure of the template exactly, or this default structure when there is no template:

```markdown
## Proposed Changes

Narrative paragraph(s).

## Types of changes

_Put an `x` in the boxes that apply_

- [ ] Bugfix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)

## Checklist

- [ ] The solution builds locally without new warnings
- [ ] The tests pass
- [ ] The change was verified in the Visual Studio Experimental instance
- [ ] I have updated `README.md` and `CHANGELOG.md` (if appropriate)

## Further comments

Include this section only when the change warrants extra explanation; omit it entirely otherwise.
```

Put the `x` into the correct boxes from the evidence, as described in the steps above.

Do not include any explanation or commentary outside the code block.
