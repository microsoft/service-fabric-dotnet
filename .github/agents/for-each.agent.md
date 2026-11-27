---
description: "Runs the `iterator` on each target in a list, tracking progress in `TODO.md`."
tools: [agent, edit, read]
agents: ["iterator"]
model: "Claude Sonnet 5.5"
modelPolicy: "required"
include-custom-instructions: true
---

- **Understand `.github/copilot-instructions.md` before doing anything else**.
  This repository requires unique knowledge you don't possess; you won't know what you don't know until you read them.

- **Extract the following from your prompt**.
  - _`{target-prompt}` is the text at the start of your prompt, before the `Targets` list_.
  - _`{target}` is a single target from the `Targets` list_.

- **Ensure `TODO.md` exists in the repo root**.
  - _Don't use a built-in todo-list tool_. It cannot handle large lists efficiently.
  - _Use this file template_. Add one line for each target.
    ```md
    {target-prompt}
    - [ ] {target}
    ```
  - _If `TODO.md` already exists, check the prompt stored in it_
    - If the stored prompt is the same as `{target-prompt}`, resume from the first unfinished target.
    - If the stored prompt is different, stop and return both prompts without changing `TODO.md`.
      The user needs to decide what to do.

- **Repeat for each target, _one at a time_**.
  - _Run the `iterator` subagent_ with `{target-prompt}` as the prompt, replacing `{target}` in it with the current
    target.
  - _`{iterator-output}` is the verdict on the first line of the `iterator` response_, without report text or file path.
  - _Don't pause for `iterator` responses that need human review_. User will review them later.
  - _Update the `TODO.md`_.
    - Don't re-read the `TODO.md` file.
    - Edit `TODO.md`, replacing `- [ ] {target}` with `- [x] {target}: {iterator-output}.`
  - _Go to the next target_.

- **Return the final `TODO.md` verbatim**.
