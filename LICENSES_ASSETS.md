# Asset sources

| Files | Source | Usage |
| --- | --- | --- |
| `Assets/Resources/ProductionArt/*.fbx` | Zombie Survival Run gear, skin weights and motion, with head topology and morphs adapted from [MakeHuman Community](https://github.com/makehumancommunity/makehuman/blob/a8bc2d54ff0ac92e78ff71431b1023eda42bf482/LICENSE.md) under CC0 1.0. Max and Eva include adapted `male_casualsuit04` and `female_casualsuit01` trousers from the [CC0 MakeHuman system assets](https://www.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html). | Derived meshes and rigged poses shipped in the game. |
| `CharacterAtlas.png` | Original gear tiles and recolored CC0 face and trouser textures from the [MakeHuman system assets](https://www.makehumancommunity.org/assets/assetpacks/makehuman_system_assets.html). | Shared texture for the nine figures. |
| `CharacterAtlas_normal.png` | Tangent-space surface detail authored in `Tools/Art/build_textures.py`. | Shared normal map for the nine figures. |
| `Highway.png`, `Highway_normal.png`, `ParticleSoft.png` | Original maps authored in `Tools/Art/build_textures.py`. | Shipped in the game source. |
| `Horizon.jpg` | OpenAI ImageGen output based on an original text prompt for this game. | Environment texture. |
| `zsr_city_*.obj`, `city_facades.png`, `city_facades_emission.png` | Original facade geometry and textures authored in `Tools/Art/build_city_meshes.py` and `build_city_facades.py`. | Street buildings and background city layers. |
| `track_*.obj`, `TrackPropAtlas.png` | Original weathering and meshes adapted from the existing Lux props in `Tools/Art/build_track_props.py`. | Shared finish for obstacles and gate pylons. |
| `TrackGateLabels.png` | Original sign artwork rasterized with DejaVu Sans Condensed Bold under the [DejaVu font license](https://dejavu-fonts.github.io/License.html). | Six bonus labels. The repository includes the atlas image. |
| `docs/design/*-blender.jpg` | Blender renders of the shipped meshes and textures. | Character and portrait composition reviews. |

The existing Polyfork license and source notes in the repository continue to apply to their listed external assets.
