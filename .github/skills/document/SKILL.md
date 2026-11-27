---
name: document
description: |
  Document public top-level types in a given scope.
  Use when asked to add or complete XML documentation comments for a type, namespace, or project.
  Don't use from a sub-agent.
argument-hint: "(type|namespace|project)"
---

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
- **Run the `for-each` subagent** with this prompt; list the sorted targets, one per line.
  ```md
  Ensure `{target}` has complete, accurate, compiler-verified XML documentation comments, including the type itself
  and all of its accessible members. API, functional, code style changes are out of scope.

  Targets:
  {targets}
  ```
