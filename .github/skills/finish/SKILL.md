---
name: finish
description: "Make sure changes in the current branch pass review."
---

- **Read `.github/copilot-instructions.md` before doing anything else**.
- **Identify files modified in the current branch**.
  - _Run `git diff --name-only --diff-filter=d $(git merge-base origin/HEAD HEAD)`_.
    It includes committed and uncommitted changes to tracked files and excludes deleted files.
  - _Run `git ls-files --others --exclude-standard --full-name :/`_.
    It includes untracked files, which `git diff` omits, and excludes ignored files.
  - _Combine both lists without duplicates_.
  - _Exclude `TODO.md` and `*-needs-human-review.md` files_. They are produced by this workflow.
  - _Include all files, regardless of their number_. User will stop processing if needed.
  - _`{target}` means the relative file path_.
- **Run the `for-each` subagent** with this prompt; list the target files, one per line.
  ```md
  Make sure changes in `{target}` pass review.

  Targets:
  {targets}
  ```
