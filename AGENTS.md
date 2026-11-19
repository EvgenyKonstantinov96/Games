# Codex instructions — Zombie Survival Run

## Current state
This repository currently contains only documentation. The real Unity source project MUST be imported before any code or in-engine audit. Do not claim tests were run otherwise.

## Goal
Audit and improve Zombie Survival Run according to the approved artistic concept. See [100 checks](docs/CODEX_AUDIT_100_RU.md). Target Unity version: 6000.3.28f1 Android Portrait.

## Rules
- After each failed test, fix the smallest correct code/asset issue and rerun the test from a clean start. Record evidence and the commit hash.
- Keep pre-existing game features: 15 levels, weapons, coins, health, bonuses, bosses, progression.
- No primitive human/zombie placeholders as success; actual GLB/FBX prefabs must be resolved with reproducible import pipeline. Never silently fall back to old Max when Polyfork content is absent.
- Do not overclaim visual inspection or realistic animation if Unity/GPU is unavailable. Report BLOCKED.
- Keep asset license sources and attribution in LICENSES_ASSETS.md; never redistribute restricted original third-party assets in public repository.
- Prefer incremental commits and one clear pull request with change evidence.
- Do not redesign accepted four weapon icons unless required by a verified bug.
