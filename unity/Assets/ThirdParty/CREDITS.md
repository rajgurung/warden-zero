# Third-party assets

Everything below is CC0 1.0 (public domain dedication): free for commercial use, no
attribution required. Credited anyway. Downloaded on 2026-10-10.

## Poly Haven (https://polyhaven.com, licence: https://polyhaven.com/license, CC0)

| Asset | Type | Author | Used as | In the project |
| --- | --- | --- | --- | --- |
| forrest_ground_01 | texture 1k (diffuse, GL normal) | Rob Tuytel | jungle forest floor (terrain layer) | `Assets/Art/Jungle/Ground` |
| leafy_grass | texture 1k | Charlotte Baglioni | grassy clearings (terrain layer) | `Assets/Art/Jungle/Ground` |
| mud_forest | texture 1k | eye-candy.xyz | trail and stream banks (terrain layer) | `Assets/Art/Jungle/Ground` |
| mossy_rock | texture 1k | Rob Tuytel | boulders, steep slopes, far mountains | `Assets/Art/Jungle/Rock` |
| tree_bark_03 | texture 1k | Rob Tuytel | broadleaf tree bark | `Assets/Art/Jungle/Bark` |
| palm_bark | texture 1k | Charlotte Baglioni | palm trunks | `Assets/Art/Jungle/Bark` |
| fern_02 | model textures 1k (diffuse, alpha) | Rob Tuytel (scan), Rico Cilliers (model) | fern frond cards | `unity/ArtSource/Jungle`, `Assets/Art/Jungle/Cards/fern_atlas.png` |
| kloofendal_48d_partly_cloudy_puresky | HDRI 2k | Greg Zaal, Jarod Guest (sky edits) | sky, ambient light and reflections | `Assets/Art/Jungle/Sky` |

Authors as listed by the Poly Haven API at download time (`api.polyhaven.com/info/<id>`).

## ambientCG (https://ambientcg.com, licence: https://docs.ambientcg.com/license/, CC0)

| Asset | Type | Used as | In the project |
| --- | --- | --- | --- |
| LeafSet022, LeafSet023, LeafSet024 | leaf atlases 1K (colour, opacity) | leaves composed into leaf-cluster cards and the big heart-leaf card | `unity/ArtSource/Jungle`, `Assets/Art/Jungle/Cards` |
| Foliage001 | grass blade atlas 1K | grass tufts | `unity/ArtSource/Jungle`, `Assets/Art/Jungle/Cards/grass_tuft.png` |
| Foliage008 | long blade atlas 1K | palm frond leaflets | `unity/ArtSource/Jungle`, `Assets/Art/Jungle/Cards/palm_frond.png` |

The cards, tree meshes, rocks, canopy texture and water ripples are generated from these
by `Assets/Editor/JungleArt.cs`, `JungleFlora.cs` and `SceneBuilder.Jungle.cs`.

## Made for this project

- Chopper, rotor, parachute canopy and checkpoint beacon: generated with Tripo from our own
  art (`Assets/Art/Campaign/TRIPO_LOG.md`).
- Drop sounds (rotor and turbine, cabin mix, freefall wind, canopy snap and flutter, landing
  thud and roll, jungle insects and birds): synthesised at runtime by
  `Assets/Scripts/SynthAudio.cs`. No recordings are used.
