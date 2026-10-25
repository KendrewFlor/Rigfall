# Models, sound and music

All of Rigfall's art and audio is generated from source code in this repository. There are no downloaded models, textures, samples or sound packs.

## 3D models: one recipe, two outputs

```
blender/rigkit.py          a tiny low-poly modelling kit (box, cylinder, cone, sphere, wedge, roof, beam, pipe)
blender/rigfall_models.py  107 model recipes written with it (heroes, weapons, enemies, bosses, crews, buildings, props)
        │
        ├── blender/build_models.py   → Assets/Rigfall/Resources/Models/<name>.json  (what the game loads)
        └── blender/rigfall_blender.py → real Blender objects → .blend / .fbx / .glb   (to look at, edit, render)
```

- **Conventions** (Unity's): metres, +Y up, models face +Z, Euler rotations applied Z, then X, then Y. Every face is flat-shaded with its own normal and a vertex colour; the game's `Rigfall/Lit` shader lights them, so there are no textures.
- **Animated parts** are separate parts with their own pivot, which code moves directly: the pumpjack's walking `beam`, a tower's `top` (turns toward targets), the drone's `rotors`. **Anchors** mark points such as a hero's `hand` (where the weapon goes) and a tower's `muzzle`.
- **Upgrades** have their own model when one exists: `building-derrick-tower@flare-stack` is used for the Flare Stack upgrade; otherwise the base model is kept.

Commands:

```sh
python3 blender/build_models.py            # write the JSON meshes
python3 blender/build_models.py --check    # CI: committed meshes match the recipes, triangle budgets respected
python3 blender/build_models.py --stats    # triangle counts per model
python3 tools/render_previews.py           # contact sheets in docs/images (numpy + Pillow)

# In Blender 4.x (no add-ons needed):
blender --background --python blender/rigfall_blender.py -- --build --save out/rigfall_models.blend
blender --background --python blender/rigfall_blender.py -- --build hero-driller building-pumpjack --fbx out/fbx --glb out/glb
blender out/rigfall_models.blend --background --python blender/rigfall_blender.py -- --to-json
```

`--to-json` writes models you edited by hand in Blender back into `Resources/Models`. After that, `build_models.py --check` will report them as different from their recipes; either move the change into the recipe or keep the hand edit and update the check.

The Blender script uses only Blender's own Python API (`bpy`, `bmesh`) and was written against Blender 4.x. It hasn't been run inside Blender as part of this repository's checks (CI has no Blender), so treat it as a starting point if your Blender version differs.

## Sound effects, ambience and music

`tools/synth_audio.py` synthesises everything with numpy: oscillators, filtered noise, FM bells and a Karplus-Strong plucked string.

- **34 sound effects**: swings, hits, perforating-gun shots, mud spray, seismic lobs, explosions, coins, building and upgrading, UI clicks, wheel ticks and wins, the Blowout Brilliant shimmer, the night horn (a two-tone rig horn), dawn bells, boss roars, and more.
- **4 ambience loops**: desert wind, swamp insects and frogs, arctic wind, sea waves with creaking steel.
- **9 music tracks**, each composed from a fixed seed: a scale, a chord progression, drum and bass patterns, and a motif that repeats with variations (A A' B A''). Every track is rendered as a seamless loop.

| Track | Where it plays |
|---|---|
| Shift Change | Main menu |
| Pumpjack Waltz | Day on the Permian Pad |
| Board Road Blues | Day in the Delta Swamp |
| Ice Pad Hymn | Day on the Arctic Slope |
| Helideck at Dawn | Day offshore |
| Kick Alarm | Night |
| Uncontrolled Flow | Boss nights |
| Sunrise Over the Crown | Dawn and the reward wheel |
| Plugged and Abandoned | Defeat |

```sh
pip install numpy
python3 tools/synth_audio.py                     # everything
python3 tools/synth_audio.py --only bgm_night    # one file
python3 tools/synth_audio.py --check             # every expected file exists and is a valid WAV
```

Output is 16-bit mono WAV at 22.05 kHz. Unity compresses it on import.

## Fonts

`Assets/Rigfall/Resources/Fonts` holds Saira Stencil One (titles) and Chakra Petch Regular and Bold (text), copied from the google/fonts repository with their SIL Open Font License files. `tools/validate_data.py` checks that every non-ASCII character the UI shows exists in Chakra Petch (when `fonttools` is installed), because a WebGL build has no system fonts to fall back on.
