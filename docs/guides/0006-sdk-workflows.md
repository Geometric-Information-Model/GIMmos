# GIMtransfer SDK workflows

`GimTransfer` exposes a small public surface around `GimData`, GIM JSON, and
GLB conversion. Current package/runtime prerequisites are covered in the
[package consumer guide](0005-package-consumer.md). Format routing is
summarized in the [format specification](../specs/formats/README.md).

## API boundary

Examples in this guide use public APIs. The main public entry points are:

- `FileAccess.ReadGimDataFromFile(path)` reads a file into `GimData`, inferring
  its type from the extension.
- `FileAccess.SharpGLTFReadGLBToGimData(path)` reads a GLB file into `GimData`.
- `FileAccess.FileToJsonAsync(...)` reads a source file and returns GIM JSON
  text.
- `FileAccess.JsonToFileAsync(...)` reads GIM JSON text and writes a selected
  output format.
- `JsonManager.ReadGimJson(jsonText)` parses GIM JSON into `JsonGimObject`.
- `FileAccess.GetGimDataAsGLB64(data, scale)` and
  `FileAccess.GetGimDataAsByteArrayGLB(data, scale)` create GLB in memory.
- `FileAccess.ConvertJSONToGLBArray(json, outputFileName, scale)` writes a GLB
  to the GIM temp directory and returns its path.
- `FileAccess.WriteToSvgFileAsync(data, fileName)` writes SVG and returns its
  path.

Several useful-looking methods are not public SDK entry points:
`ReadToGimData`, `BinaryToGimData`, `WriteFromGimData`,
`WriteFromGimDataAsync`, `GetGLTF`, `GimDataToJson`, and `GimJsonToGimData`
are internal or private. Some repository tests and UI code access internal
members through friend-assembly permissions. That material is repository-only,
not package-consumer usage.

## Read a model

For a file on disk, use the public reader that matches the workflow:

```csharp
using GIMcompress;
using GIMtransfer;

GimData data = FileAccess.ReadGimDataFromFile(@"C:\models\part.step");
GimData glbData = FileAccess.SharpGLTFReadGLBToGimData(@"C:\models\part.glb");
```

`ReadGimDataFromFile` derives the file type from the final dot-separated
component of `filePath` and passes it unchanged. Dedicated dispatch checks are
case-sensitive for extensions such as `json`, `gml`, and `kml`; use lower-case
extensions such as `.json`, `.gml`, and `.kml`. The GLB-specific reader accepts
a path; it does not accept GLB bytes.

## Convert through JSON

> **Known limitation:** `FileToJsonAsync` can hang for GML or KML when a
> `readProgressStatus` is supplied. Its post-read `WaitForStart()` can loop
> forever because those readers do not start that status. Do not treat this
> workflow as safe for those formats.

`FileToJsonAsync` returns JSON text. It also writes a copy under the GIM temp
directory; the returned string is not that path.

```csharp
using GIMtransfer;
using GimUtilities;

var readProgress = new GimProgressStatus();
var writeProgress = new GimProgressStatus();
var options = new GimOptions();

string json = await FileAccess.FileToJsonAsync(
    @"C:\models\part.step",
    "step",
    "part",
    readProgress,
    writeProgress,
    options);
```

`JsonToFileAsync` accepts JSON text and returns the output path for ordinary
file formats. Its `readProgressStatus` parameter is currently ignored:
`JsonManager.ReadGimJson` produces no read progress. Only the supplied
`writeProgressStatus` is used by the output stage. With `convertToFileType`
set to `"json"`, it returns JSON text and also writes a temporary `.json` file.

```csharp
string outputPath = await FileAccess.JsonToFileAsync(
    json,
    "glb",
    "part",
    null,
    new GimProgressStatus(),
    new GimOptions());
```

Use a file type without its leading dot. For methods that append extensions,
`fileName` must be extensionless: pass `"part"`, not `"part.glb"`, or the
result can become `part.glb.glb`. `GimOptions` is required by both methods;
the current implementation reads its fields without a null check.
`scale` applies to the conversion transform. A positive `binaryUnitToBe` value
triggers unit-based polygon rounding and clears embedded binary data.
`visualisationUnitToBe` selects rounding for generated visual geometry.
Treat these as conversion semantics, not as a promise that every source
format has identical unit behavior.

## Read and create GIM JSON

`JsonManager.ReadGimJson` accepts a JSON string, not a file path:

```csharp
using GIMcompress;
using GIMtransfer;

string jsonText = File.ReadAllText(@"C:\models\part.json");
JsonGimObject document = JsonManager.ReadGimJson(jsonText);
```

GIM JSON has a root `Version` string with up to three numeric components.
Missing or unparsable versions use the legacy reader. The current supported
ceiling is `1.2.0`; a greater version throws `NotImplementedException` after
the streaming version sniff and before a full document parse. Readers stamp
the canonical reader version on `JsonGimObject.Version`; they do not copy the
root string verbatim. `Children` is initialized, while `Joints` and
`Materials` may be null. See the
[JSON reader reference](../../GIMtransfer/JsonManager/README.md) for dispatch
details.

For a `GimData` to JSON conversion, use `FileToJsonAsync`. The lower-level
`GimDataToJson` helper is internal and is not part of an external consumer
workflow.

## Create and handle GLB

Create GLB bytes or Base64 without a filesystem round trip:

```csharp
byte[] glbBytes = FileAccess.GetGimDataAsByteArrayGLB(data);
string glbBase64 = FileAccess.GetGimDataAsGLB64(data);

File.WriteAllBytes(@"C:\models\part.glb", glbBytes);
```

The numeric `scale` parameter on `GetGimDataAsGLB64` is currently unused.
`GetGimDataAsByteArrayGLB` passes `scale` into the GLB writer and applies it
to generated vertex positions. `ConvertJSONToGLBArray` uses the latter path,
adds JSON-object handling, writes under the GIM temp directory, and returns
the resulting path. Its `outputFileName` is used as a name, not as a general
destination path.

`GetObjectVisualisationFromJson(jsonContent, scale)` also returns GLB bytes.
Its `GimUnitSize` argument selects polygon rounding before GLB creation; it is
not the same as the numeric linear scale used by
`GetGimDataAsByteArrayGLB`.

`CompressGlbFileInPlace(path)` is best effort. It returns without a result when
the input is absent, unreadable, or compression does not produce a smaller
file, and logs warnings for read/write failures. A successful compressed file
uses `EXT_meshopt_compression`; use this method only when downstream viewers
support that extension. The optional compressor and its threshold are
described in the [package consumer guide](0005-package-consumer.md).

## Output and temporary paths

Most format conversions use:

```text
%TEMP%\GIM\
```

`FileAccess.GetTempFolder()` creates and returns that directory.
`FileAccess.GetTempPath("part.bin")` returns the corresponding full path and
creates the directory if needed. `GetGltfTempFolder()` and
`GetObjTempFolder()` create format-specific subdirectories used by the
two-argument temp-path helper.

Core JSON, binary, Assimp, and OCCT conversion paths construct output below
the GIM temp directory from extensionless `fileName` and append an extension.
They do not provide a public cleanup or caller-selected destination parameter.
Move or copy returned files before application-specific cleanup if they must
survive the temp directory lifecycle.

SVG differs: `WriteToSvgFileAsync` uses the system temp root when `fileName`
has no directory, and writes to the supplied directory when it does. It
replaces the supplied filename extension with `.svg`.

## Progress

Progress is exposed through `GimProgressStatus`, not through an event or
delegate callback. For `FileToJsonAsync`, create separate read and write
instances, pass them to the method, and poll `PercentageDone` or
`PercentageLeft`:

```csharp
var readProgress = new GimProgressStatus();
var writeProgress = new GimProgressStatus();
Task<string> conversion = FileAccess.FileToJsonAsync(
    sourcePath, "step", "part", readProgress, writeProgress, new GimOptions());

while (!conversion.IsCompleted)
{
    Console.WriteLine(
        $"read {readProgress.PercentageDone:0}% / " +
        $"write {writeProgress.PercentageDone:0}%");
    await Task.Delay(100);
}

string json = await conversion;
```

The operation creates its progress plan. `GimProgressStatus.Create` can be
called only once unless `Reset` is used, so do not pre-create a plan for a
status passed to a conversion. Status names and totals vary by reader/writer;
some paths report no read stages, so zero progress is possible even while
work continues. Progress is advisory, not a completion or cancellation
contract. `JsonToFileAsync` has no read progress; poll its write status only.

## Errors

Await conversion tasks and handle parse, file, native-dependency, and format
errors at the application boundary:

```csharp
try
{
    JsonGimObject document = JsonManager.ReadGimJson(jsonText);
}
catch (NotImplementedException)
{
    // Input uses a schema newer than this package supports.
}
catch (Newtonsoft.Json.JsonReaderException)
{
    // Input is not valid JSON for the selected reader.
}
```

`ReadGimDataFromFile` catches exceptions from `ReadToGimData` and rethrows a
generic `"Invalid file"` error. A failed binary (`.bin`) read can instead return
`null` in Release builds because `BinaryToGimData` suppresses its exception;
callers should handle both outcomes and should not rely on the original
exception type. `WriteToSvgFileAsync` and the conversion methods can surface
filesystem, parser, native-loader, or backend exceptions through the returned
task. `CompressGlbFileInPlace` is the exception: its missing-input and
compression failures are non-throwing, best-effort outcomes.

## Lifecycle and concurrency

- Some conversion stages use `Task.Run`, but async method names do not mean
  every stage immediately yields to the thread pool. `JsonToFileAsync` parses
  and transforms JSON synchronously before awaiting its write stage.
- `WriteToSvgFileAsync` generates SVG synchronously before awaiting its file
  write. These APIs expose no `CancellationToken`.
- No public conversion session, shutdown, or temp-file cleanup lifecycle is
  defined. Keep returned data or copy returned files before cleanup.
- A `GimProgressStatus` instance is mutable and designed for one operation.
  Use one read status and one write status per concurrent conversion.
- `GimData` is mutable. Do not mutate the same instance while another
  conversion reads it. Some internal GLB and scene workflows use parallel
  processing, so callers should pass independent input objects when running
  operations concurrently.
- Temp output names derive from `fileName`; concurrent operations using the
  same name can target the same file. Use unique names when outputs overlap.

These points describe observed current behavior, not a broader thread-safety
guarantee.
