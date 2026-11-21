# Codex instructions — Zombie Survival Run

## Current state (verified 2026-11-21)
The Unity source project has now been uploaded to the `main` branch: `Assets/`, `Packages/`, `ProjectSettings/` exist. The previous claim that this repository contains only documentation is obsolete.

Read [Codex handoff](docs/CODEX_HANDOFF_RU.md), [100 independent Unity checks](docs/CODEX_AUDIT_100_RU.md), and [Issue #1](https://github.com/EvgenyKonstantinov96/Games/issues/1) before editing. Work from the latest main and inspect actual files; do not assume earlier snapshots are current.

Remaining prerequisites to inspect: `Assets/Scenes/ZSR_Main.unity` is not committed (editor setup can generate it); `Assets/Resources/ProductionArtSettings.asset` is not committed; Polyfork licence source lists exist but downloaded rigged GLB/FBX files have not been verified. The approved reference picture provided in the user's ChatGPT conversation is not yet present as `docs/design/approved_concept.png`; obtain it for pixel-level visual acceptance.

Target: Unity 6000.3.28f1, Android Portrait, URP. Code and file existence alone do not prove a playable build.

## Rules
- Audit and **fix** the existing game, not replace it with a new prototype. Follow the artistic reference and handoff specification.
- After each failed test, fix the smallest correct code/asset issue and rerun the test from a clean start. Record evidence and the commit hash.
- Keep pre-existing features: 15 levels, weapons, coins, health, bonuses, bosses, upgrades, persistence and progression.
- Never count procedural primitive humans/zombies as completed professional art. Real rigged GLB/FBX prefabs and a reproducible import pipeline are required for full visual acceptance.
- Detect missing ProductionArtSettings/prefabs and report accurately; do not silently mark a fallback to old Max as visual success.
- Do not overclaim Unity compilation, PlayMode, Android tests, realistic animation, frame rate or screenshot comparison if Unity/GPU/devices/reference are unavailable. Mark affected T-IDs `BLOCKED`, not `PASS`.
- Keep asset license sources and attribution in `LICENSES_ASSETS.md`; never redistribute restricted third-party assets in the public repository.
- Prefer incremental commits and one clear PR with code changes, evidence and remaining blockers.
- Preserve the four accepted unique arsenal weapon icons unless a verified bug requires changes.
- The older [PR #2](https://github.com/EvgenyKonstantinov96/Games/pull/2) documents the **pre-import** state; its `100 BLOCKED` outcome is stale and must not be used as current acceptance.
