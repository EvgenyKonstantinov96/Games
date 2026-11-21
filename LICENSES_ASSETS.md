# Asset sources

| Files | Source | Usage |
| --- | --- | --- |
| `Assets/Resources/ProductionArt/*.fbx` | Original mesh surfaces, skin weights and keyframe poses authored for Zombie Survival Run in `Tools/Art/build_characters.py`. | Shipped in the game source. No third-party character models or motion capture. |
| `CharacterAtlas.png`, `Highway.png`, `Highway_normal.png`, `ParticleSoft.png` | Original texture inputs authored in `Tools/Art/build_textures.py`. | Shipped in the game source. |
| `Horizon.jpg` | OpenAI ImageGen output based on an original text prompt for this game. | Shipped as an environment texture. |
| `docs/design/character-lineup-blender.jpg` | Blender render of the original FBX source skins and poses. | Visual reference for the shipped character assets. |

The existing Polyfork license and source notes in the repository continue to apply to their listed external assets.
