// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

// Writes tosu's lazer offsets file (https://github.com/tosuapp/tosu, packages/tosu/src/assets/offsets.json) for a published build.
// tosu reads lazer's memory through field offsets of ppy's builds; our classes have other fields, so the offsets are worked out here
// from the published assemblies themselves: they're loaded into this process (same runtime, so the same field layout as in the game)
// and ClrMD reads the field offsets of the loaded types.
//
// Usage: TosuOffsets <published game folder> <tosu's offsets.json (which classes/fields to fill)> <output file> [version]
// The version defaults to the one tosu reads from osu!.deps.json ("osu!/2026.1020.1-lazer" -> "2026.1020.1").

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Diagnostics.Runtime;

if (args.Length < 3)
{
    Console.Error.WriteLine("Usage: TosuOffsets <published game folder> <template offsets.json> <output file> [version]");
    return 2;
}

string gameDir = Path.GetFullPath(args[0]);
var template = JsonNode.Parse(File.ReadAllText(args[1]))!.AsObject();
string outputPath = args[2];
string version = args.Length > 3 ? args[3] : readTosuVersion(gameDir);

AssemblyLoadContext.Default.Resolving += (context, name) =>
{
    string path = Path.Combine(gameDir, name.Name + ".dll");
    return File.Exists(path) ? context.LoadFromAssemblyPath(path) : null;
};

// The game's own assemblies (the runtime's are already there; native libraries aren't assemblies).
var loadedNames = new HashSet<string>(AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetName().Name!), StringComparer.OrdinalIgnoreCase);
var assemblies = new List<Assembly>();

foreach (string dll in Directory.GetFiles(gameDir, "*.dll"))
{
    string name = Path.GetFileNameWithoutExtension(dll);
    if (loadedNames.Contains(name) || name.StartsWith("System.", StringComparison.Ordinal) || name.StartsWith("Microsoft.", StringComparison.Ordinal))
        continue;

    try
    {
        assemblies.Add(AssemblyLoadContext.Default.LoadFromAssemblyPath(dll));
    }
    catch (BadImageFormatException)
    {
    }
}

Type? findType(string fullName) => assemblies.Select(a => a.GetType(fullName, false)).FirstOrDefault(t => t != null);

// Every type is looked up first so its MethodTable exists before the snapshot.
var types = new Dictionary<string, Type>();

foreach (var (typeName, _) in template)
{
    if (template[typeName] is not JsonObject)
        continue;

    var type = findType(typeName);
    if (type == null)
        Console.WriteLine($"warning: type {typeName} isn't in this build; left out");
    else
        types[typeName] = type;
}

var gameType = findType("osu.Desktop.OsuGameDesktop") ?? throw new InvalidOperationException("osu.Desktop.OsuGameDesktop not found");

var output = new JsonObject { ["OsuVersion"] = version };
int missing = 0;

using (var dataTarget = DataTarget.CreateSnapshotAndAttach(Environment.ProcessId))
{
    var runtime = dataTarget.ClrVersions.Single().CreateRuntime();

    foreach (var (typeName, fieldsNode) in template)
    {
        if (fieldsNode is not JsonObject templateFields || !types.TryGetValue(typeName, out var type))
            continue;

        var clrType = runtime.GetTypeByMethodTable((ulong)type.TypeHandle.Value) ?? throw new InvalidOperationException($"ClrMD can't see {typeName}");
        var fields = new JsonObject();

        foreach (var (fieldName, _) in templateFields)
        {
            int? offset = fieldOffset(clrType, fieldName);

            if (offset == null)
            {
                Console.WriteLine($"warning: {typeName}.{fieldName} isn't in this build; left out");
                missing++;
                continue;
            }

            fields[fieldName] = offset;
        }

        output[typeName] = fields;
    }

    // tosu tells the game object apart by the first 8 bytes of its MethodTable (flags and base size).
    output["GameBaseVtable"] = Marshal.ReadInt64(gameType.TypeHandle.Value);

    // tosu finds the game by scanning for ScalingContainerTargetDrawSize (1024x768, after two true bools) and reading externalLinkOpener a few bytes before it.
    var osuGame = runtime.GetTypeByMethodTable((ulong)gameType.TypeHandle.Value)!;
    int? drawSize = fieldOffset(osuGame, "<ScalingContainerTargetDrawSize>k__BackingField");
    int? linkOpener = fieldOffset(osuGame, "externalLinkOpener");

    if (drawSize != null && linkOpener != null)
    {
        int delta = drawSize.Value - 4 - linkOpener.Value;
        int[] tosuDeltas = [0x24, 0x28, 0x2c, 0x20, 0x30, 0x1c, 0x34];

        if (tosuDeltas.Contains(delta))
            Console.WriteLine($"externalLinkOpener is 0x{delta:x} before the scan pattern (tosu tries that)");
        else
        {
            Console.Error.WriteLine($"error: externalLinkOpener is 0x{delta:x} before the scan pattern; tosu only tries {string.Join(", ", tosuDeltas.Select(d => $"0x{d:x}"))}, so it won't find the game");
            return 1;
        }
    }
}

File.WriteAllText(outputPath, output.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
Console.WriteLine($"Wrote offsets for {version} to {outputPath} ({missing} field(s) missing)");
return 0;

// Offset from the object's start as tosu counts it (after the MethodTable pointer for classes), including fields of base classes.
// A private field can have the same name in a class and its base (SubmittingPlayer's and Player's <api>k__BackingField):
// the class's own comes last in the object, and that's the one tosu means.
static int? fieldOffset(ClrType type, string name)
{
    var field = type.Fields.Where(f => f.Name == name).MaxBy(f => f.Offset);
    return field == null ? null : field.Offset + (type.IsValueType ? 0 : IntPtr.Size);
}

// Same as tosu's getOsuVersion(): the "osu!/<version>-<suffix>" library in osu!.deps.json, up to the dash.
static string readTosuVersion(string gameDir)
{
    using var deps = JsonDocument.Parse(File.ReadAllText(Path.Combine(gameDir, "osu!.deps.json")));
    string key = deps.RootElement.GetProperty("libraries").EnumerateObject().Select(p => p.Name).First(n => n.StartsWith("osu!/", StringComparison.Ordinal));
    int dash = key.IndexOf('-');

    if (dash < 0)
        throw new InvalidOperationException($"{key} has no -suffix; tosu would read its version wrong (publish with -p:Version=<version>-lazer)");

    return key[(key.IndexOf('/') + 1)..dash];
}

// The class the top-level statements above are compiled into (named, for the code file checks).
internal partial class Program
{
}
