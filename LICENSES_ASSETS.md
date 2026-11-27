# Asset sources

| Files | Source | Usage |
| --- | --- | --- |
| `Assets/Resources/ProductionArt/*.fbx` | Original Zombie Survival Run clothing shapes, gear and motion. Base body topology, morphs and remapped [vertex weights](https://github.com/makehumancommunity/makehuman/blob/a8bc2d54ff0ac92e78ff71431b1023eda42bf482/makehuman/data/rigs/default_weights.mhw) from [MakeHuman Community](https://github.com/makehumancommunity/makehuman/blob/a8bc2d54ff0ac92e78ff71431b1023eda42bf482/LICENSE.md) under CC0 1.0. Adapted `shoes02` and `shoes05` meshes from the [CC0 MakeHuman system assets](https://www.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html). | Derived meshes and rigged poses shipped in the game. |
| `CharacterAtlas.png` | Original clothing and gear tiles with recolored CC0 skin and shoe textures from the [MakeHuman system assets](https://www.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html). | Shared texture for the nine figures. |
| `CharacterAtlas_normal.png`, `CharacterAtlas_emission.png` | Original maps authored in `Tools/Art/build_textures.py`. | Shared surface detail and masked boss emission. |
| `ParticleSoft.png` | Original maps authored in `Tools/Art/build_textures.py`. | Shipped in the game source. |
| `Highway.png`, `Highway_normal.png`, `Highway_surface.png`, `RoadReflection.png` | Original road, worn paint, drains and registered surface maps authored in `Tools/Art/build_highway.py`. | Shared bridge material. |
| `Horizon.jpg` | OpenAI ImageGen output based on an original text prompt for this game. | Environment texture. |
| `Horizon_Industrial.jpg`, `Horizon_Evacuation.jpg`, `UI/campaign_cover_*.png` | Color grades and crops of the project's existing skyline and portrait artwork, prepared in `Tools/Art/build_campaign_art.py`. | Chapter atmosphere and campaign menu. |
| `track_chapter_*.obj` | Original work authored in `Tools/Art/build_chapter_props.py`. | Three roadside landmarks. |
| `zsr_city_*.obj`, `city_facades.png`, `city_facades_emission.png` | Original facade geometry and textures authored in `Tools/Art/build_city_meshes.py` and `build_city_facades.py`. | Street buildings and background city layers. |
| `track_*.obj`, `TrackPropAtlas.png` | Original guardrail, weathering and other meshes adapted from the existing Lux props in `Tools/Art/build_track_props.py`. | Shared finish for obstacles, guardrails and gate pylons. |
| `salvage_token.obj`, `SalvageToken.png`, `SalvageToken_normal.png` | Original brass token, beacon stamp and relief maps authored in `Tools/Art/build_salvage_token.py`. | Collectible currency. |
| `TrackGateLabels.png` | Original sign artwork rasterized with DejaVu Sans Condensed Bold under the [DejaVu font license](https://dejavu-fonts.github.io/License.html). | Six bonus labels. The repository includes the atlas image. |
| `Assets/Resources/UI/ZSRMenuFont.ttf` | Unmodified DejaVu Sans Condensed. [Font license](docs/design/DejaVu-LICENSE.txt). | Bundled game interface font with Cyrillic coverage. |
| `Assets/Resources/UI/ZSRButtonFont.ttf` | Subset of DejaVu Sans Condensed Bold, including Cyrillic and UI symbols. [Font license](docs/design/DejaVu-LICENSE.txt). | Bundled button captions. |
| `Assets/Resources/UI/btn_*.png`, `Assets/Resources/UI/panel.png` | Original rounded surfaces authored in `Tools/Art/build_menu_art.py`. | Nine-slice menu panels and buttons. |
| `Assets/Resources/UI/menu_symbols.png` | Original field-kit and navigation pictograms drawn in `Tools/Art/build_menu_art.py`. | Shared transparent canvas atlas. |
| `lux_weapon_rifle.obj`, `lux_weapon_rifle_steel.obj` | Original game meshes authored in `Tools/Art/build_rifle.py`. | Two shared materials, existing grip and muzzle attachments. |
| `combat_*.obj` | Original drone, rotor, grenade and medkit authored in `Tools/Art/build_combat_props.py`. | Combat props using the shared track atlas. |
| `Assets/Resources/Audio/*.ogg` | Original synthesized score and effects composed in `Tools/Art/build_audio.py`. | Two looping music cues and six gameplay sounds. |
| `docs/design/*-blender.jpg`, `docs/design/*-blender.gif` | Blender renders of the shipped meshes and textures. | Character, motion, and portrait composition reviews. |

The existing Polyfork license and source notes in the repository continue to apply to their listed external assets.
