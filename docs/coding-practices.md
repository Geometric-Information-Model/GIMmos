# Coding Practices

Add future practices deliberately and keep examples close to the rule they illustrate.

## 1. Document reusable functions and classes

Public functions and classes always get a terse XML comment. An internal or private
function that is called from more than one place gets one too. A throwaway local
helper gets a single terse line.

Say what it does, what it is for, and what the parameters are. Leave inner workings
out unless a caller needs them to use it safely. Include an important failure or
side effect when that changes how to call it.

```csharp
/// <summary>Reads a CAD file into GimData.</summary>
/// <param name="path">Location of the source file.</param>
/// <returns>The loaded GimData.</returns>
GimData Read(string path);

// Formats the source path for the log line.
string FormatPath(string path);
```

When an implementation follows an interface contract, use `/// <inheritdoc />`
instead of duplicating the interface description.

## 2. Break up long functions

A long function gets occasional one-line comments that mark what each section does,
so a reader can skim it. A weird or non-obvious choice gets a one-line explanation.

```csharp
// Open the source and reject a path that does not exist.

// Convert supported types and leave unsupported files untouched.
```

## 3. Cite ADRs, not plans

A code comment does not cite a plan or a plan step. A design choice cites an ADR:

```csharp
// MSVC Debug runtime redistribution, as per GIMmos - ADR 0002
```

A readme or guide is fine when that document is the one that matters. The reference
must be readable on its own.
