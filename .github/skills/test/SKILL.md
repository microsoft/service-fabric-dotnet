---
name: test
description: "Test public and internal types in a given scope."
argument-hint: "(type|namespace|project)"
---

<!-- The `TODO.md` steps are shared by the `document`, `finish` and `test` skills. Keep them identical until a human chooses
a sharing mechanism. -->

- **Read `.github/copilot-instructions.md` before doing anything else**.
- **Identify top-level `public` and `internal` types in the target scope**.
  - _Include all types, regardless of their number_. User will stop processing if needed.
  - _Include every top-level type in a file_. Each top-level type needs to be tested separately.
  - _Exclude compiler-generated types, types without concrete methods_. They don't need to be tested.
  - _`{target}` means `{namespace-name}.{type-name} in {relative-file-path}`_.
  - _Sort target types alphabetically, by file path then by type name_.
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
- **Repeat for each target, _one at a time_**.
  - _Print start note_: `[{finished + 1}/{total}] Testing {target} ...`
  - _Run the `iterator` subagent_; use this prompt:
    ```md
    Test `{target}`.
    Bug fixes, testability improvements in `{target}` are out of scope.
    Restructure existing, eliminate redundant tests as needed.
    ```
  - _`{iterator-output}` is the verdict on the first line of the `iterator` response_, without report text or file path.
  - _Don't pause for `iterator` responses that need human review_. User will review them later.
  - _Print finish note_: `[{finished}/{total}] Finished {target}: {iterator-output}.`
  - _Update the `TODO.md`_.
    - Don't re-read the `TODO.md` file.
    - Edit `TODO.md`, replacing `- [ ] {target}` with `- [x] {target}: {iterator-output}.`
  - _Go to the next target_.
