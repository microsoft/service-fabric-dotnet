---
name: test
description: |
  Test public and internal types in a given scope.
  Don't use from a sub-agent.
argument-hint: "(type|namespace|project)"
---

- **Read `.github/copilot-instructions.md` before doing anything else**.
- **Identify top-level `public` and `internal` types in the target scope**.
  - _Include all types, regardless of their number_. User will stop processing if needed.
  - _Include every top-level type in a file_. Each top-level type needs to be tested separately.
  - _Exclude compiler-generated types, types without concrete methods_. They don't need to be tested.
  - _`{target}` means `{namespace-name}.{type-name} in {relative-file-path}`_.
  - _Sort target types alphabetically, by file path then by type name_.
- **Run the `for-each` subagent** with this prompt; list the sorted targets, one per line.
  ```md
  Test `{target}`.
  Bug fixes, testability improvements in `{target}` are out of scope.
  Restructure existing, eliminate redundant tests as needed.

  Targets:
  {targets}
  ```
