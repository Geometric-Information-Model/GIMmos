# GimData

`GimData` is the in-memory model used for GIM 3D CAD data. This specification
covers its graph structure, placement rules, element contracts, and safe
mutation practices. It does not describe proprietary serialization, compression,
or cryptography.

## Model at a glance

`GimData` contains:

- one indexed list of `GimElement` values;
- named `GimObject` entry points;
- an optional parallel list of property sets with object-side links;
- joints;
- project and unit metadata.

Element identity is an index, not an object reference. The index graph is the
contract that connects document structure, repeated instances, and geometry.

## Indexed graph

### Element list and child references

- `GimData.Elements` is one sequential, zero-based list.
- Index `0` is conventionally the single `D_root` element.
- `GimElement.Elements` contains child indices into the same list.
- Resolve a child through `gimData.Elements[childIndex]`.

Insertion, deletion, and other reordering can change indices. Every operation
that changes the list must update all affected references before the data is
used again.

### Parent references and instances

Parents are represented on their children. Each `GimElement` has `Locals`; each
`GimLocal.Parent` is an index into `GimData.Elements`.

A child can have multiple locals. Each local represents one instance under one
parent. If a parent contains the same child index more than once, the child
needs a matching number of locals for that parent. Locals retain their
per-parent order, which is used by child/local lookup helpers.

Each local also stores placement: position (`Px`, `Py`, `Pz`), a 3×3 basis
(`Xx` through `Zz`), and handedness. Reusing an element does not imply reusing
its placement.

### Objects

`GimObject.Element` is another index into `GimData.Elements`. Objects are named
entry points and may target only document elements: root, folder, or part.
`GimObject.name` is non-null; empty string means unnamed.

## Property sets

`GimData.PropertySets` is an optional list of `GimPropertySet` values. Each set
maps string keys to `GimPropertyValue` values tagged as `String` or `Double`.
Objects link to sets through nullable `GimObject.PropertySetIndex`; several
objects may share one set.

- A string-valued `name` or `type` entry takes precedence over the matching
  `GimObject` value; otherwise the object value remains the fallback.
- Property links belong to objects, not elements. Element operations never
  shift property-set indices.
- Treat a set shared by multiple objects as immutable. Unlinked sets are
  allowed and remain part of the model.
- File-global properties belong to the set linked from the root object.

## Document and representation hierarchy

The usual hierarchy is:

```text
document → representation → geometry
```

Document elements include root, folder, part, and link-like structures.
Representations include B-rep and polygon representations.

Typical B-rep structure:

```text
part → R_BRep → solid → shell → face → wire → curve
```

A 3D curve may have an optional `Seam` child. A seam marks topology; it is not
itself a curve. Polygon representations normally contain polygon, polyline, and
vertex elements.

These relationships are structural constraints, not a complete list of every
element type or every valid specialized child.

## Element contracts

`GimElement.ElementType` determines which value arrays and child relationships
apply. `EntityHelpers` supplies expected counts for `ElementInts`,
`ElementDoubles`, and `ElementBytes` where counts are fixed, plus predicates
such as `IsDocument`, `IsRepresentation`, `IsFace`, `IsCurve`, `IsPoly`, and
`IsBrep`.

`IsSeam()` identifies the topological `Seam` marker. It returns true for
`GimElementType.Seam`, not for 3D or 2D curve types. Use it when distinguishing
a seam child from curve geometry.

Keep an element's representation consistent wherever that element type appears.
The exact array layout is an implementation contract and is intentionally not
reproduced here.

## Builders and adapters

Use `ElementBuilders` to construct elements from semantic parameters and
`ElementAdapters` to read existing elements through named properties and typed
ranges. This keeps array layout and validation in one place.

New code should use builders and adapters. Direct manipulation of
`ElementInts`, `ElementDoubles`, `ElementBytes`, or `Locals` is legacy practice
and should be limited to compatibility work, migration code, or serialization
code that must handle raw values.

## Units and transforms

`GimData` exposes `UnitCurr`, `UnitToBe`, and `GimUnits`. `GimObject` can also
carry a `GimUnitSize`. A `GimUnitSize` combines a unit (`GimUnit`) with a
preferred size (`GimSize`); its `GetRoundingStep()` reports the corresponding
step in millimetres.

Unit metadata and numeric conversion are related but separate concerns. Keep
coordinates, curve dimensions, radii, and local positions in one consistent
scale within a data set. When a conversion operation requests a target unit or
scale, verify that the operation applies it to both geometry and placement as
required. Do not assume changing metadata alone converts stored values.

Local basis values describe orientation and local position. Applying a
transformation must preserve the parent relationship and the intended handedness.

## Usage rules

1. **Single root:** `GimData` has exactly one root element.
2. **Part is atomic:** a part must not contain document-type children; root,
   part, and folder elements must not be children of a part.
3. **Representation before geometry:** faces and curves must not be direct
   children of a part. Place a valid representation between the part and its
   geometry.
4. **Object targets:** `GimObject` references target only root, folder, or part
   elements.
5. **Representation consistency:** an element type has the same value-array
   contract regardless of parent. Context may change which child types are
   valid.
6. **Index safety:** update `Elements`, `Locals.Parent`, `Objects.Element`, and
   every other index reference together when indices change.
7. **Promotion and demotion:** only `GimObject` entry points can be promoted or
   demoted.
8. **Representation placement:** a representation is a child of a part and may
   contain only valid children for that representation.
9. **Seams:** a 3D curve has at most one `Seam` child. A seam may have zero or
   one 2D curve child for p-curve geometry; without one, it is a straight-edge
   marker associated with its 3D curve.

### Enum stability

Every `GimElementType` member must declare an explicit integer value. Never
depend on implicit enum sequencing. New types use a documented free slot and
must remain within the currently supported range. Changing an existing value
breaks consumers that persist or exchange element types.

## Mutation safety

- Clone data before mutating a value obtained for read-only or shared use.
- Use `AddChildToParent` or an equivalent operation that adds the child index
  and the matching local together.
- Before deleting elements, remove or rehome references from parents, locals,
  and objects.
- After deletion or insertion, remap every surviving index. Do not assume
  indices after the changed position are still valid.
- Preserve one local per parent occurrence, including repeated child indices.
- Do not mutate a `GimData` instance while another conversion or traversal reads
  it. Use independent instances for concurrent work.

## Compatibility

Do not change the int, double, byte, or boolean structure of an existing
element type for legacy data. If the model must evolve, introduce a compatible
versioned serialization path so older data remains readable. Keep proprietary
binary readers and writers opaque; this document defines the in-memory model
only. Serialization evolution follows the [read-tolerant versioning
ADR](../../adr/0003-read-tolerant-versioning.md): versioned readers, not
versioned writers.

## Local implementation pointers

| Concern | Path |
| --- | --- |
| Data list and structural operations | `GIMCompress/entities/GimData.cs` |
| Element, child, and local access | `GIMCompress/entities/GimElement.cs` |
| Local parent and basis values | `GIMCompress/entities/GimLocal.cs` |
| Object entry points | `GIMCompress/entities/GimObject.cs` |
| Counts and type predicates | `GIMCompress/EntityHelpers.cs` |
| Element construction | `GIMCompress/entities/ElementBuilders/` |
| Element access adapters | `GIMCompress/entities/ElementAdapters/` |
| Property values and sets | `GIMCompress/entities/GimPropertyValue.cs`, `GIMCompress/entities/GimPropertySet.cs` |
| Object construction (incl. property-set link) | `GIMCompress/entities/GimObjectFactory.cs` |
