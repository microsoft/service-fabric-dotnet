---
name: document
description: |
  Document public top-level types in a given scope.
  Use when asked to add or complete XML documentation comments for a type, namespace, or project.
  Don't use from a sub-agent.
argument-hint: "(type|namespace|project)"
---

<!-- The `TODO.md` steps are shared by the `document`, `finish` and `test` skills. Keep them identical until a human chooses
a sharing mechanism. -->

- **Read `.github/copilot-instructions.md` before doing anything else**.
- **Identify top-level `public` types in the target scope**.
  - _Include all types, regardless of their number_. User will stop processing if needed.
  - _Include every public top-level type in a file_. Each public top-level type needs to be documented.
  - _`{target}` means `{namespace-name}.{type-name} in {relative-file-path}`_.
- **Sort target types from the most generic to the most specific** to enable use of `<inheritdoc/>`.
  - _Place base types/interfaces before types derived from/implementing them_.
  - _Place generics before specializations_. E.g. place `IComparable<T>` extension classes before extension classes for types
    implementing `IComparable<T>`.
  - _Place providers before consumers_. E.g. place `IEnumerable<T>` before `Enumerable` that uses it.
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
  - _Print start note_: `[{finished + 1}/{total}] Documenting {target} ...`
  - _Run the `iterator` subagent_; use this prompt:
    ```md
    Ensure `{target}` has complete, accurate, compiler-verified XML documentation comments, including the type itself
    and all of its accessible members. API, functional, code style changes are out of scope.
    ```
  - _`{iterator-output}` is the verdict on the first line of the `iterator` response_, without report text or file path.
  - _Don't pause for `iterator` responses that need human review_. User will review them later.
  - _Print finish note_: `[{finished + 1}/{total}] Finished {target}: {iterator-output}.`
  - _Update the `TODO.md`_.
    - Don't re-read the `TODO.md` file.
    - Edit `TODO.md`, replacing `- [ ] {target}` with `- [x] {target}: {iterator-output}.`
  - _Go to the next target_.
