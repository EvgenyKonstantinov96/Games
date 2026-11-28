# Asset sources

| Files | Source | Usage |
| --- | --- | --- |
| `Assets/Resources/ProductionArt/*.fbx` | Original Zombie Survival Run gear and motion. Base body topology, morphs and remapped [vertex weights](https://github.com/makehumancommunity/makehuman/blob/a8bc2d54ff0ac92e78ff71431b1023eda42bf482/makehuman/data/rigs/default_weights.mhw) from [MakeHuman Community](https://github.com/makehumancommunity/makehuman/blob/a8bc2d54ff0ac92e78ff71431b1023eda42bf482/LICENSE.md) under CC0 1.0. Adapted `shoes02`, `shoes05`, `male_casualsuit01`–`05`, `female_casualsuit01`, `short01`, `ponytail01`, `eyes/low-poly` and `teeth_base` from the [CC0 MakeHuman system assets](https://www.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html). | Fitted, reweighted and reduced meshes, three distance levels per model and rigged poses shipped in the game. |
| `CharacterAtlas.png` | Original gear tiles and recolored CC0 skin, footwear, garment, hair and eye textures from the [MakeHuman system assets](https://www.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html). | Shared 2048×2048 texture for the nine figures; each hero now has a dedicated 768×768 skin region. |
| Sewn insignia in `CharacterAtlas.png` slots 46–48 | Original hazard, scout chevrons and security shield drawn in `Tools/Art/build_textures.py`. | Fitted sleeve/chest patches on the shipped skinned models; no extra material. |
| `CharacterAtlas_normal.png`, `CharacterAtlas_emission.png`, `CharacterAtlas_surface.png` | Maps prepared in `Tools/Art/build_textures.py`; includes adapted CC0 garment normal maps from the same system pack. | Shared surface detail, metallic/smoothness and masked boss emission. |
| `ParticleSoft.png` | Original maps authored in `Tools/Art/build_textures.py`. | Shipped in the game source. |
| `Highway.png`, `Highway_normal.png`, `Highway_surface.png`, `RoadReflection.png` | Original road, worn paint, drains and registered surface maps authored in `Tools/Art/build_highway.py`. | Shared bridge material. |
| `Horizon.jpg` | OpenAI ImageGen output based on an original text prompt for this game. | Environment texture. |
| `docs/design/target-character-sheet.png` | Original OpenAI ImageGen output prepared for the game's target design. | Artistic reference sheet; not a screenshot or a render of the shipped models. |
| `Horizon_Industrial.jpg`, `Horizon_Evacuation.jpg`, `UI/campaign_cover_*.png` | Color grades and crops of the project's existing skyline and portrait artwork, prepared in `Tools/Art/build_campaign_art.py`. | Chapter atmosphere and campaign menu. |
| `track_chapter_*.obj` | Original work authored in `Tools/Art/build_chapter_props.py`. | Three roadside landmarks. |
| `zsr_city_*.obj`, `city_facades.png`, `city_facades_emission.png` | Original facade geometry and textures authored in `Tools/Art/build_city_meshes.py` and `build_city_facades.py`. | Street buildings and background city layers. |
| `track_*.obj`, `TrackPropAtlas.png` | Original guardrail, weathering and other meshes adapted from the existing Lux props in `Tools/Art/build_track_props.py`. | Shared finish for obstacles, guardrails and gate pylons. |
| `salvage_token.obj`, `SalvageToken.png`, `SalvageToken_normal.png` | Original brass token, beacon stamp and relief maps authored in `Tools/Art/build_salvage_token.py`. | Collectible currency. |
| `TrackGateLabels.png` | Original sign artwork rasterized with DejaVu Sans Condensed Bold under the [DejaVu font license](https://dejavu-fonts.github.io/License.html). | Six bonus labels. The repository includes the atlas image. |
| `Assets/Resources/UI/ZSRMenuFont.ttf` | Unmodified DejaVu Sans Condensed. [Font license](docs/design/DejaVu-LICENSE.txt). | Bundled game interface font with Cyrillic coverage. |
| `Assets/Resources/UI/ZSRButtonFont.ttf` | Subset of DejaVu Sans Condensed Bold, including Cyrillic and UI symbols. [Font license](docs/design/DejaVu-LICENSE.txt). | Bundled button captions. |
| `Assets/Resources/UI/btn_*.png`, `Assets/Resources/UI/panel.png` | Original rounded surfaces authored in `Tools/Art/build_menu_art.py`. | Nine-slice menu panels and buttons. |
| `Tools/Art/Source/survivors-menu.png`, `Assets/Resources/UI/survivors_cover.png`, `hero_*.png`, `dialogue_face_*.png` | Original OpenAI ImageGen output for this game, cropped in `Tools/Art/build_production_menu_art.py`. | Menu illustration and pre-run dialogue avatars; the source image is included for reproducibility. |
| `Assets/Resources/UI/menu_symbols.png` | Original field-kit and navigation pictograms drawn in `Tools/Art/build_menu_art.py`. | Shared transparent canvas atlas. |
| `lux_weapon_rifle.obj`, `lux_weapon_rifle_steel.obj` | Original game meshes authored in `Tools/Art/build_rifle.py`. | Two shared materials, existing grip and muzzle attachments. |
| `lux_weapon_{pistol,shotgun,minigun}*.obj` | Original beveled meshes authored in `Tools/Art/build_arsenal.py`. | Three distinct weapon models with existing grip and barrel attachment axes. |
| `track_maintenance.obj`, `track_streetlamp.obj` | Original meshes authored in `Tools/Art/build_street_dressing.py`. | Joined roadside details using the shared track atlas. |
| `combat_*.obj`, `CombatAtlas.png`, `CombatAtlas_normal.png`, `CombatAtlas_surface.png` | Original medical hardcase, fragmentation grenade, white quadcopter, rotors and shared material maps authored in `Tools/Art/build_combat_props.py`. | In-game field kit with its own 512×512 material atlas. |
| `Assets/Resources/UI/kit_{medkit,grenade,drone}.png` | Blender renders of those original shipped meshes, produced by `Tools/Art/render_fieldkit.py`. | Transparent item illustrations shared by the HUD and upgrades menu. |
| `Assets/Resources/Audio/*.ogg` | Original synthesized score and effects composed in `Tools/Art/build_audio.py`. | Two looping music cues, six combat sounds and four synthesized bite, claw, acid and spit sounds. The damage sounds are authored in `Tools/Art/build_damage_audio.py`. |
| `docs/design/*-blender.jpg`, `docs/design/*-blender.gif` | Blender renders of the shipped meshes and textures. | Character, motion, and portrait composition reviews. |
| `docs/design/field-kit-hud-layout.jpg`, `docs/design/field-kit-hud-strip.png` | Reconstruction of managed UI layout using the game's own artwork, produced by `Tools/Art/preview_interface.py`. | Layout review only; not a Unity screenshot. |
| `docs/design/arsenal-milestones-layout.jpg` | Reconstruction of managed UI layout with the four accepted weapon images, produced by `Tools/Art/preview_interface.py`. | Arsenal unlock review only; not a Unity screenshot. |

The existing Polyfork license and source notes in the repository continue to apply to their listed external assets.
