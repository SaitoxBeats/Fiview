# Fiview

Fiview is a lightweight Windows image viewer focused on one thing: opening images fast.

It was built as an alternative to heavier default viewers, especially on slower CPUs, where opening a simple image can feel unnecessarily delayed. Fiview keeps the interface minimal, avoids expensive redraw work, and uses a fast preview pipeline for large images.

## Highlights

- Fast image opening with a simple WinForms interface.
- Smooth keyboard navigation with left and right arrows.
- Support for `.jpg`, `.jpeg`, `.png`, `.bmp`, `.gif`, and `.webp`.
- Lightweight preview mode for images larger than `1000x1000`.
- Automatic switch to full quality when the user zooms in.
- Automatic return to compressed preview when the image goes back to fit view.
- Drag and drop support.
- Right-click menu with open, fit image, and quit actions.
- Single-instance behavior: opening another image sends it to the existing Fiview window.
- Dark window styling.

## Why It Feels Fast

Fiview does not resize and recreate the displayed bitmap on every interaction. Instead, it draws through a custom canvas and only swaps image quality when it makes sense:

- Large image opens as a compressed preview.
- Viewport rendering stays light.
- Zoom requests the original full-quality image.
- Returning to fit view restores the compressed preview.
- Previous and next images are preloaded in the background.

This keeps the first visual response quick while still preserving full-quality inspection when needed.

## Informal Speed Comparison

Test scenario reported on a low-end CPU:

| Viewer | Time to Open the Same Photo |
| --- | ---: |
| Windows Photos | About 10 seconds |
| Fiview | Practically instant |

This is not a lab benchmark, but it reflects the goal of the project: make opening an image feel immediate, even on weaker hardware.

## Controls

| Action | Shortcut / Input |
| --- | --- |
| Next image | `Right Arrow` |
| Previous image | `Left Arrow` |
| Zoom | `Ctrl + Mouse Wheel` |
| Fit image | `F` or right-click menu |
| Pan image | Left mouse drag |
| Open image | Right-click menu or drag and drop |

## Build

Requirements:

- Windows
- .NET 9 SDK

Build in Release mode:

```powershell
dotnet build .\FiviewSolution.sln -c Release
```

Run the built app:

```powershell
.\Fiview\bin\Release\net9.0-windows\Fiview.exe
```

Open a specific image:

```powershell
.\Fiview\bin\Release\net9.0-windows\Fiview.exe "C:\path\to\image.webp"
```

## Project Structure

| File | Purpose |
| --- | --- |
| `Fiview/Program.cs` | App startup and single-instance routing. |
| `Fiview/imgView_Form.cs` | Main image viewer form, loading, navigation, cache, and quality switching. |
| `Fiview/ImageCanvas.cs` | Custom image rendering surface with zoom, pan, fit, and preview/full-quality switching. |
| `Fiview/SingleInstanceServer.cs` | Named pipe server used to send new image paths to the already-running app. |
| `Fiview/init_Form.cs` | Initial window shown when no image path is provided. |

## Notes

Fiview is still evolving. The current priority is speed, low memory pressure, and a clean viewing experience. Future improvements could include configurable quality thresholds, animated GIF controls, rotation, metadata display, and shell integration.
