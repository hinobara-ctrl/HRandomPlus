# Linux post-audit manual checks — v0.2.1

These checks are intentionally owner-run on a real Linux installation or the prepared VM. Automated coverage exists for every corrected edge case; this list only covers useful platform-level confirmation.

- [ ] Start HRandomPlus after lazer has produced a runtime log larger than 2 MiB. Confirm that the current map is found without selecting it again.
- [ ] With lazer detected, close lazer and reopen it using the same storage. Confirm that HRandomPlus reconnects and resolves the current map instead of retaining the previous session.
- [ ] Randomize and import a normal, resource-heavy lazer beatmapset. Confirm that the generated difficulty imports and its audio/resources remain available.
- [ ] Cancel or close HRandomPlus while a Wine-side helper operation is still running. Confirm that no helper process remains orphaned.
- [ ] If a real beatmapset contains resource names differing only by letter case, process it and confirm that both resources survive. Do not manufacture or modify a personal set solely for this check.

Not required manually: split UTF-8 byte sequences, malformed settings backup, duplicate Realm usage rows, traversal rejection, extraction-limit rejection, and cancellation propagation are deterministic regression tests in `HRandomPlus.Tests`.
