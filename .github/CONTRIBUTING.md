# Contributing

Thanks for helping. English or Japanese is fine everywhere. / 日本語でも大丈夫です。

## Issues

- Use a template: **Bug report**, **Feature request** or **Mediation network request**.
- For bugs, paste the report from **Integration Status > Copy report**. It never contains your IDs.
- Never post real App IDs, ad unit IDs, API keys or account details.
- Search existing issues first.

## Pull requests

1. Open an issue first for anything bigger than a small fix, so we can agree on the approach.
2. Branch from `main`. One topic per PR.
3. Fill in the pull request template.
4. Keep Unity-independent logic in `Editor/Core` and cover it with NUnit tests in `Tests/Editor`.
5. Commit the `.meta` file for every new file and folder.
6. User-facing changes: add a line under `## [Unreleased]` in `CHANGELOG.md`, and update both
   `README.md` and `README.ja.md`.

## Merge rules

- `main` is protected. Nobody pushes to it directly; every change goes through a PR.
- **Only the maintainer (@haru-sudo) merges, by hand, after reviewing.**
- Auto-merge is not used. Bots and AI agents must never merge, approve, or enable auto-merge.
- A new push to a PR dismisses earlier approvals; all review threads must be resolved before merging.

## Releases

The maintainer bumps `version` in `package.json`, moves `Unreleased` in `CHANGELOG.md` to the new
version, and tags the release (`v1.2.3`).
