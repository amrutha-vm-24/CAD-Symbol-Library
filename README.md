# CAD Symbol Library

## Overview

CAD Symbol Library is a WPF-based application developed using C# and .NET 8. It allows users to browse DXF symbols, search for symbols, preview a sample floor plan, and place multiple symbols onto the drawing using drag and drop.

## Project Structure

The solution contains three projects:

1. **SymbolLibrary.Core**
   - Class library targeting .NET 8.
   - Contains DXF reading and symbol-related core functionality.
   - Uses the `netDxf.netstandard` library.

2. **SymbolLibrary.Wpf**
   - WPF class library targeting .NET 8.
   - Contains drawing preview and rendering functionality.
   - Handles supported DXF entities and symbol rendering.

3. **SymbolLibrary.TestHost**
   - WPF desktop application.
   - Provides the user interface for browsing symbols and displaying the sample floor plan.

## Features

- Select a symbol folder from the local system.
- Recursively load DXF symbol files from the selected folder.
- Display symbols grouped by categories.
- Search symbols by name with case-insensitive filtering.
- Display a sample DXF floor plan.
- Support rendering of LINE, CIRCLE, ARC, LWPOLYLINE, TEXT, MTEXT, and INSERT entities.
- Support nested block references and block transformations.
- Drag and drop multiple symbols onto the drawing.
- Place symbols at the mouse drop position.
- Zoom the drawing around the mouse cursor.
- Keep the drawing and placed symbols aligned during zooming.

## Technologies Used

- C#
- .NET 8
- WPF
- XAML
- netDxf.netstandard 3.0.1
- Visual Studio 2022

## Prerequisites

- Visual Studio 2022
- .NET 8 SDK
- Windows operating system

## How to Run

1. Open `SymbolLibrary.TestHost` as the startup project.
2. Build the solution.
3. Run the application.
4. Select the `03_Sample_Symbols` folder when prompted.
5. Select `Sample_Floor_Plan.dxf` from the `04_Sample_Drawing` folder.
6. Browse the symbol categories or use the search box.
7. Drag and drop symbols onto the floor plan.
8. Use the mouse wheel to zoom around the cursor position.

## Architecture

The solution follows a three-project architecture:

- **Core:** Handles DXF data and symbol-related logic independently of WPF.
- **WPF:** Handles rendering and drawing presentation.
- **TestHost:** Hosts the application UI and connects the symbol library with the drawing preview.

## Notes

- The application uses user-selected folders and files rather than fixed local file paths.
- The supplied sample DXF files are used for testing.
- The application is intended for symbol browsing, previewing, and placement. DXF export or saving is not included.
