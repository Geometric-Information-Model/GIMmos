---
name: general-gimdata-skill
description: GimData structure and usage rules for the GIM 3D CAD format. Use when creating, editing, or validating GimData, GIMbin, or element hierarchies (parts, representations, geometry).
---

# General GimData Skill

Apply this skill whenever you work with **GimData**, GIMbin, or GIM element hierarchies (creating, editing, moving elements, changing structure, serialization, or validation).

## Authoritative rules

**Follow the usage rules in [docs/specs/gimdata/README.md](../../../docs/specs/gimdata/README.md)** (section **Usage rules**). That document is the single source of truth. Summary below.

## Summary of usage rules

1. **Backwards compatibility:** Do not change GimElement type structures (element int count, double count, byte count). Introduce a new GIMbin version instead so legacy files remain readable.
2. **Part is atomic:** No document type (root, part, folder) may be a child of a part.
3. **Single root:** GimData must have exactly one root element.
4. **Representation before geometry:** Geometric elements (faces, curves) must not be direct children of parts; there must be a representation (B-rep, polyrep, etc.) in between.
5. **GimObject targets:** GimObject references may only point to root, folder, or part elements.
6. **Element representation consistency:** An element type’s representation (element ints, doubles, bytes) must be consistent regardless of parent; sub-elements may vary by context.
7. **Index safety:** Any operation that updates indices (insertions, deletions, moves) must consider all references in the GimData (Elements, Locals.parent, Objects.element, etc.).
8. **Promotion/demotion:** Only Objects can be promoted or demoted.
9. **Representation placement and children:** A representation may only be a child of a part and may only be parent of its valid child types (e.g. polyrep → polygons/polygonal elements; brep → solid, shell, face, wire, curve, etc.).
10. **Enum integer stability:** Every `GimElementType` member must use an explicit `= N` value; new types take documented free slots — never implicit sequencing (see `docs/specs/gimdata/README.md` enum integer stability rule).

## Do not

- Change element type structures (int/double/byte counts) for existing types; create a new GIMbin version instead.
- Create invalid structures: part with document children, multiple roots, geometry directly under part, Objects pointing to non-document elements, representations with wrong parent or wrong child types, or inconsistent element representation by parent.
- Perform index updates (insertions, deletions, moves) without updating all affected references in the GimData.
- Promote or demote anything other than Objects.
- Add `GimElementType` members without an explicit integer value or without reserving a documented free slot.

When in doubt, refer to **docs/specs/gimdata/README.md** (Usage rules and structure sections).

## Builder and Adapter pattern

For creating and reading GimElements, prefer **ElementBuilders** and **ElementAdapters** over direct array manipulation:

- **Builders** (`ElementBuilders/Curves/`, etc.) construct GimElements from semantic parameters via fluent interfaces.
- **Adapters** (`ElementAdapters/Curves/`, etc.) wrap existing GimElements and provide named properties and typed `ArraySegment` access.

Direct manipulation of `elementDoubles[]` / `elementInts[]` / `Locals` is legacy. New code should use the builder/adapter pattern. GIMbin serialization code is exempt.
