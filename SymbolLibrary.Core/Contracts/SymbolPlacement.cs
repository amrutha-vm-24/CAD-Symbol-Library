// ─────────────────────────────────────────────────────────────────────────────
//  Symbol Library – required interface
//
//  Copy this file into your solution unchanged (you may change the namespace).
//  Your panel and your drawing area must communicate only through these types,
//  so please do not rename members or change their meaning.
// ─────────────────────────────────────────────────────────────────────────────

namespace SymbolLibrary.Contracts;

/// <summary>
/// Describes the symbol being dragged from the panel onto the drawing area.
/// Your panel creates this when the user starts dragging a thumbnail.
/// </summary>
/// <param name="SymbolId">An id for the symbol, unique within the panel.</param>
/// <param name="Name">Display name, e.g. "Door Single 900".</param>
/// <param name="SourceFile">Absolute path of the DXF file that contains the symbol.</param>
/// <param name="BlockName">
/// Name of a block inside <paramref name="SourceFile"/> if the symbol is one block of a file
/// that holds several, or null when the whole file is the symbol (the normal case).
/// </param>
/// <param name="Units">Drawing units of the source file, e.g. "Millimeters" (from the DXF $INSUNITS header).</param>
/// <param name="Width">Width of the symbol in drawing units (bounding box).</param>
/// <param name="Height">Height of the symbol in drawing units (bounding box).</param>
public sealed record SymbolPlacement(
    string SymbolId,
    string Name,
    string SourceFile,
    string? BlockName,
    string Units,
    double Width,
    double Height);

/// <summary>Use this data format name for drag-and-drop between the panel and the drawing area.</summary>
public static class SymbolDragFormat
{
    public const string Name = "SymbolLibrary.Placement";
}
