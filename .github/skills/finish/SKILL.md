---
name: finish
description: "Make sure changes in the current branch pass review."
---

<!-- The `TODO.md` steps are shared by the `document`, `finish` and `test` skills. Keep them identical until a human chooses
a sharing mechanism. -->

- **Read `.github/copilot-instructions.md` before doing anything else**.
- **Identify files modified in the current branch**.
  - _Run `git diff --name-only --diff-filter=d $(git merge-base origin/HEAD HEAD)`_.
    It includes committed and uncommitted changes to tracked files and excludes deleted files.
  - _Run `git ls-files --others --exclude-standard --full-name :/`_.
    It includes untracked files, which `git diff` omits, and excludes ignored files.
    `:/` and `--full-name` list files in the whole repo with root-relative paths, matching `git diff` from any directory.
  - _Combine both lists without duplicates_.
  - _Exclude `TODO.md` and `*-needs-human-review.md` files_. They are produced by this workflow.
  - _Include all files, regardless of their number_. User will stop processing if needed.
  - _`{target}` means the relative file path_.
- **Ensure `TODO.md` exists in the repo root**.
  - _Don't use a built-in todo-list tool_. It cannot handle large lists efficiently.
  - _Use this file template_. Add one line for each target.
    ```md
    Executing `{user-request}`
    - [ ] {target}
    ```
  - _If `TODO.md` already exists, check the prompt stored in it_
    - If the stored prompt is the same, resume from the first unfinished target.
    - If the stored prompt is different, stop and ask what to do.
- **When resuming, reconcile `TODO.md` with target files**.
  The `iterator` commits changes during the run and the user keeps working between runs, so the stored list may be stale.
  - _Append `- [ ] {target}` for target files missing from `TODO.md`_.
  - _Reopen flagged entries for target files_, i.e. modified again after being reverted or deleted.
    Replace `- [x] {target}: ...` lines ending with ` ⚠️ No longer modified.` with `- [ ] {target}`.
  - _Flag unflagged entries for files missing from target files_, i.e. reverted or deleted.
    Replace `- [ ] {target}` with `- [x] {target}: ⚠️ No longer modified.` Append ` ⚠️ No longer modified.` to
    `- [x] {target}: ...` lines not already ending with it.
- **Repeat for each target, _one at a time_**.
  - _Print start note_: `[{finished + 1}/{total}] Finishing {target} ...`
  - _Run the `iterator` subagent_; use this prompt:
    ```md
    Make sure changes in `{target}` pass review.
    ```
  - _`{iterator-output}` is the verdict on the first line of the `iterator` response_, without report text or file path.
  - _Don't pause for `iterator` responses that need human review_. User will review them later.
  - _Print finish note_: `[{finished}/{total}] Finished {target}: {iterator-output}.`
  - _Update the `TODO.md`_.
    - Don't re-read the `TODO.md` file.
    - Edit `TODO.md`, replacing `- [ ] {target}` with `- [x] {target}: {iterator-output}.`
  - _Go to the next target_.
