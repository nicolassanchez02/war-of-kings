# Open Questions

This document is the morning review queue. Every judgment call Claude Code made during the overnight push lives here, tagged so you can scan it on your phone over coffee and decide which ones to revisit.

Read the entries top-to-bottom on the first pass. They are ordered roughly by impact — items at the top are the ones where changing my pick gives you the biggest game improvement for the smallest effort.

## Format

Each entry:

- **Context:** What was the situation
- **What I picked:** The decision
- **Alternatives:** What else made sense
- **Where to change it:** Filepath/line/config key
- **Reversible?** trivial / easy / hard / costly
- **Nick's call?** yes / no / maybe — my read

---

## Q-1: Brief described state-of-repo that's already fixed
**Context:** The overnight brief's Part 0.1 and Part 0.2 instructed me to fix `WarOfKings.Game.sln` Release mapping at lines 88-93 and tighten `WarOfKings.Game.csproj` to an allowlist. Both were already done in commit `7ef38d9` ("Cleanup before M1") before this session began.
**What I picked:** Skip both items as already-correct; note here.
**Alternatives:** Mechanically run `dotnet sln remove`/`add` per brief (no-op, churn).
**Where to change it:** N/A — verification only.
**Reversible?** trivial
**Nick's call?** no — clearly the right move

## Q-2: The overnight brief's stated scope is months of work, not one session
**Context:** The brief asks for M1-M9 plus asset pipeline, AI, lockstep networking, replays, and map editor — six months of milestones — in a single overnight push. I'm a Claude Code session, not an autonomous overnight build farm; I make tool calls turn-by-turn within one session window. Even at the brief's own deferral hierarchy minimum (Parts 0, 1, 2, 3, 4, 5, 7-Easy/Normal, 11), that is still many sessions of work.
**What I picked:** Execute Part 0 in full, then drive Part 1 (M1 close) as far as one session allows, then write a wake-up package documenting honestly what's done vs. not. Everything beyond Part 1 will likely be untouched and will be clearly flagged as such in the wake-up note.
**Alternatives:** (a) Pretend to do everything via shallow stubs in many files — produces a broken trunk and burns your morning. (b) Refuse the whole brief — leaves M1 unfinished and you with no progress. (c) What I picked: honest, focused, deep on one thing.
**Where to change it:** Mindset — there's no engineering choice here, just a reality check.
**Reversible?** trivial
**Nick's call?** maybe — if you genuinely want me to spread thin and try a little of every part, say so and I'll do that next session; I think the focused approach is better.

## Q-3: GitButler workspace hooks were removed when branching off
**Context:** Creating the `overnight/2026-05-11-mechanical-complete` branch from `gitbutler/workspace` triggered GitButler to remove its managed `pre-commit` and `post-checkout` hooks with the note: "To return to GitButler mode, run: but setup".
**What I picked:** Left as-is — the overnight branch is a normal git branch and the smoke-test pre-push hook is what we want.
**Alternatives:** Re-run `but setup` after the overnight push lands.
**Where to change it:** `but setup` from the command line.
**Reversible?** trivial — one command.
**Nick's call?** yes — you know your GitButler workflow; reinstate when ready.

---

(More entries will be appended as work proceeds.)
