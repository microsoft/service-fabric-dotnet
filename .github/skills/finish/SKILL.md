---
name: finish
description: "Make sure changes in the current branch pass review."
---

- **Read `.github/copilot-instructions.md` before doing anything else**.
- **Identify files modified in the current branch**.
  - _Run `git diff --name-only --diff-filter=d $(git merge-base origin/HEAD HEAD)`_.
    It includes committed and uncommitted changes to tracked files and excludes deleted files.
  - _Run `git ls-files --others --exclude-standard`_.
    It includes untracked files, which `git diff` omits, and excludes ignored files.
  - _Combine both lists without duplicates_.
  - _Exclude `TODO.md` and `*-needs-human-review.md` files_. They are produced by this workflow.
  - _Include all files, regardless of their number_. User will stop processing if needed.
  - _`{target-file}` means the relative file path_.
- **Ensure `TODO.md` exists in the repo root**.
  - _Don't use a built-in todo-list tool_. It cannot handle large lists efficiently.
  - _Use this file template_. Add one line for each target file.
    ```md
    Executing `{user-request}`
    - [ ] {target-file}
    ```
  - _If `TODO.md` already exists, check the prompt stored in it_
    - If the stored prompt is the same, reconcile the stored list with target files and resume from the first unfinished file.
      The `iterator` commits changes during the run and the user keeps working between runs, so the stored list may be stale.
      - Append `- [ ] {target-file}` for target files missing from `TODO.md`.
      - Flag entries for files missing from target files, i.e. reverted or deleted.
        Replace `- [ ] {file}` with `- [x] {file}: ⚠️ No longer modified.` Append ` ⚠️ No longer modified.` to `- [x] {file}: ...` lines.
    - If the stored prompt is different, stop and ask what to do.
- **Repeat for each target file, _one at a time_**.
  - _Print start note_: `[{files-finished + 1}/{total-files}] Finishing {target-file} ...`
  - _Run the `iterator` subagent_; use this prompt:
    ```md
    Make sure changes in `{target-file}` pass review.
    ```
  - _`{iterator-output}` should be `✅ Looks Good` or `❓ Needs Human Review`_.
  - _Don't pause for `iterator` responses that need human review_. User will review them later.
  - _Print finish note_: `[{files-finished}/{total-files}] Finished {target-file}: {iterator-output}.`
  - _Update the `TODO.md`_.
    - Don't re-read the `TODO.md` file.
    - Edit `TODO.md`, replacing `- [ ] {target-file}` with `- [x] {target-file}: {iterator-output}.`
  - _Go to the next file_.
