---
inclusion: always
title: Function and Method Implementation Guidelines
---

# Implementation Guidelines for GGPK Project

This project is reverse-engineered and structured around binary bundles. To maintain correctness and performance, all function and method implementations **must** follow the documentation format extracted via Roslyn.

📄 **Reference**: [docs/Documentation.md](../../docs/Documentation.md)

## General Rules

- Use **function signatures** as provided in the documentation
- Always implement behavior consistent with the documented `<summary>` section or `///` XML comment
- If a method is declared as `readonly`, `unsafe`, `protected`, etc., preserve that modifier
- Use existing constructor logic as base unless explicitly overridden
- Avoid introducing unlogged side effects, even in caching or streaming methods
- Any method named `Read*`, `Write*`, or `Save*` must lock on their stream and maintain chunk-boundary correctness

## Property & Field Guidance

- All fields listed in the documentation must be treated as persistent members
- Use documented default values where applicable (e.g., `chunk_size = 256 * 1024`)
- Keep `readonly` and `const` modifiers untouched
- For `protected` or `internal` fields, limit usage to internal logic

## Method Implementation Directives

1. **Implement Summary Descriptions**

   For each method:
   - Parse its documented XML comment `<summary>` if available
   - Match behavior to this description exactly

2. **Match Signature**

   - Match parameter names and types exactly
   - Support overloading as declared
   - Include optional parameters where shown

3. **Respect Chunk Logic**

   For all methods dealing with reading chunks or byte arrays (e.g., `ReadChunks`, `ReadWithoutCache`):

   - Ensure buffer sizes match expected uncompressed sizes
   - Validate `offset` and `length` logic precisely
   - Call `EnsureNotDisposed()` before any stream access

4. **Dispose and Cache Management**

   - Implement `Dispose()` using documented lock/dispose patterns
   - `RemoveCache()` must nullify both `cachedContent` and `cacheTable`
   - Never expose unflushed buffers unless explicitly noted

5. **Validation and Error Handling**

   - Throw exceptions as declared (`ThrowHelper.Throw`, `ArgumentOutOfRangeException`, etc.)
   - Use `Debug.Assert` for developer-facing checks
   - Do not silently catch exceptions unless documented

## Special Handling for Unsafe Methods

- For methods marked `unsafe`, ensure pointer logic and memory rent/return cycles match existing code patterns
- Validate allocations via `GC.AllocateUninitializedArray` or `ArrayPool<byte>.Shared.Rent`

## Enum Handling

- Follow enum member names and numeric values exactly
- Enums like `Compressor`, `CompressionLevel` are critical for decompression logic
- Default to `Leviathan` and `Normal` unless context dictates otherwise

---

When creating new functionality, refer to existing `Read`, `Write`, `Extract`, and `Replace` methods for patterns. All new code should integrate cleanly with the chunked file and bundle system.
