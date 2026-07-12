using Panda3D.Core;
using Panda3D.Egg;

namespace Panda3D.ModelDeps;

/// <summary>
/// panda-model-deps [--model-root DIR ...] &lt;model-file&gt; ...
///
/// Loads each model through the Panda bindings and prints its texture dependencies as TSV:
///     &lt;model&gt;\t&lt;texture-abspath&gt;\t&lt;EXISTS|MISSING&gt;
///
/// Egg files are parsed by <see cref="EggData"/> (never touches the image data, so a missing
/// texture can't fail the read, and .egg.pz is read directly via the VFS). Every other format
/// (bam, obj, gltf, flt, dae, … — anything with a loader plugin) goes through the Loader with
/// default options (texture images deferred), then the graph is walked for textures. Texture
/// references are resolved against the supplied --model-root dirs (in order), then the model's
/// own directory.
/// </summary>
static class Program
{
    static int Main(string[] args)
    {
        var roots = new List<string>();
        var models = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--model-root" when i + 1 < args.Length:
                    roots.Add(args[++i]);
                    break;
                case "-h" or "--help":
                    Console.Error.WriteLine("usage: panda-model-deps [--model-root DIR ...] <model-file> ...");
                    Console.Error.WriteLine("  prints TSV lines: <model>\\t<texture-abspath>\\t<EXISTS|MISSING>");
                    return 0;
                default:
                    models.Add(args[i]);
                    break;
            }
        }
        if (models.Count == 0)
        {
            Console.Error.WriteLine("panda-model-deps: no model files given");
            return 2;
        }

        int errors = 0;
        foreach (var model in models)
        {
            try
            {
                foreach (var texRef in TextureRefsOf(model))
                {
                    var (abs, exists) = Resolve(texRef, model, roots);
                    Console.WriteLine($"{model}\t{abs}\t{(exists ? "EXISTS" : "MISSING")}");
                }
            }
            catch (Exception ex)
            {
                // Don't abort the batch — report and keep going so other models still resolve.
                Console.Error.WriteLine($"panda-model-deps: {model}: {ex.Message}");
                errors++;
            }
        }
        return errors == 0 ? 0 : 1;
    }

    // The Loader is the flexible default — it handles bam, obj, gltf, flt, dae and any format
    // Panda has a loader plugin for. Only actual egg text gets the EggData fast-path (pure parse,
    // no texture-image loading), which the Loader's egg2pg route would otherwise trigger.
    static IEnumerable<string> TextureRefsOf(string model) =>
        model.EndsWith(".egg", StringComparison.OrdinalIgnoreCase) ||
        model.EndsWith(".egg.pz", StringComparison.OrdinalIgnoreCase)
            ? EggTextureRefs(model)
            : LoaderTextureRefs(model);

    // Egg: pure parse via EggData, then collect every used EggTexture's filename.
    static List<string> EggTextureRefs(string model)
    {
        // Absolute: EggData.Read resolves a relative name via the model-path, not cwd.
        var fn = Filename.FromOsSpecific(Path.GetFullPath(model));
        var egg = new EggData();
        egg.SetEggFilename(fn);          // so relative texture refs are anchored to the egg
        if (!egg.Read(fn))
            throw new Exception("could not read egg");

        var textures = new EggTextureCollection();
        textures.FindUsedTextures(egg);   // non-destructive walk

        var refs = new List<string>();
        for (int i = 0, n = textures.GetNumTextures(); i < n; i++)
            refs.Add(FilenameString(textures.GetTexture(i).GetFullpath()));
        return refs;
    }

    // Loader path (bam + any loader-supported format): load with default options (texture
    // images deferred for bam), walk the graph for textures.
    static List<string> LoaderTextureRefs(string model)
    {
        var node = Loader.GetGlobalPtr().LoadSync(Filename.FromOsSpecific(Path.GetFullPath(model)))
                   ?? throw new Exception("could not load model");
        var textures = new NodePath(node).FindAllTextures();

        var refs = new List<string>();
        for (int i = 0, n = textures.GetNumTextures(); i < n; i++)
            refs.Add(FilenameString(textures.GetTexture(i).GetFullpath()));
        return refs;
    }

    // Panda Filenames are unix-style internally; that string is directly usable on POSIX and
    // combinable via Path on Windows.
    static string FilenameString(Filename fn) => fn.GetFullpath();

    // Resolve a (usually relative) texture reference against the model roots, then the model dir.
    static (string abs, bool exists) Resolve(string texRef, string model, List<string> roots)
    {
        if (string.IsNullOrEmpty(texRef))
            return (texRef, false);
        if (Path.IsPathRooted(texRef) || (texRef.Length > 1 && texRef[1] == ':'))
            return (Path.GetFullPath(texRef), File.Exists(texRef));

        string? first = null;
        foreach (var root in roots)
        {
            var cand = Path.GetFullPath(Path.Combine(root, texRef));
            first ??= cand;
            if (File.Exists(cand)) return (cand, true);
        }
        var modelDir = Path.GetDirectoryName(Path.GetFullPath(model)) ?? ".";
        var byModel = Path.GetFullPath(Path.Combine(modelDir, texRef));
        first ??= byModel;
        return (first, File.Exists(byModel) ? true : File.Exists(first));
    }
}
