# Panda3D.ModelDeps

`panda-model-deps` — a small, self-contained tool that loads a Panda3D model through the engine's
own bindings and lists the **texture files it references**, so a build can make model→bam
compilation dependency-aware (a changed texture re-triggers the bam).

It handles any format the engine can load:

- **egg / egg.pz** — parsed with `EggData` (pure parse; never loads image data, so a missing
  texture can't fail it, and `.egg.pz` is read directly via the VFS).
- **bam, obj, gltf, flt, dae, …** — loaded via the `Loader` with default options (texture images
  deferred), then the scene graph is walked for textures.

Texture references are resolved against `--model-root` search dirs (in priority order), then the
model's own directory. Output is TSV, one line per referenced texture:

```
<model>\t<texture-abspath>\t<EXISTS|MISSING>
```

## Usage

```
panda-model-deps [--model-root DIR ...] <model-file> ...
```

## How it's shipped

A **NativeAOT** build produces a single self-contained native executable per RID — no .NET
runtime, no side libraries (Panda is statically linked, built against the lean/AOT `Panda3D.Interop`
variant + `Panda3D.Runtime`'s static libs). The `Panda3D.ModelDeps` NuGet packs them
`Panda3D.Tools`-style under `tools/<rid>/` and its `buildTransitive` targets expose the host-RID
path as **`$(PandaModelDeps)`** (with the Unix exec bit restored on restore).

## Building

```bash
./pack.sh                      # host RID -> ./artifacts/Panda3D.ModelDeps.<version>.nupkg
./pack.sh linux-x64 win-x64 osx-x64 osx-arm64   # CI: all RIDs
```

Versioned independently of consumers (e.g. Panda3D.Framework's build pipeline, which references
this package and passes `$(PandaModelDeps)` to its texture-dependency step).
