# Repository Guidelines

## Project Structure & Module Organization

`specification.md` defines the Japanese product requirements. The C# console application is in `src/TokikosOrder/`; `tokikosOrder.sh` provides a Bash and ImageMagick alternative. Add future tests under a dedicated test project and update this guide when structure or tooling changes.

## Build, Test, and Development Commands

Build and run the application with the .NET 10 SDK:

- `dotnet build src/TokikosOrder/TokikosOrder.csproj` builds the program.
- `dotnet run --project src/TokikosOrder -- 210 148 12 3 3 350 ./cover.jpg` generates a sample cover layout.
- `./tokikosOrder.sh 210 148 12 3 3 350 ./cover.jpg` generates the same layout with ImageMagick 6 or 7.

The arguments are cover height, cover width, spine width, trim width, bleed-frame width, resolution, and optional output path. Without the output path, output goes to `./tokikosOrder_output.jpg`.

## Coding Style & Naming Conventions

Use four-space indentation, nullable reference types, and descriptive English identifiers. Keep input dimensions in millimeters until converting to pixels using 350 or 600 DPI. Run `dotnet format src/TokikosOrder/TokikosOrder.csproj` before submitting style changes.

## Testing Guidelines

No test project or coverage target is configured yet. When adding tests, cover layout dimensions and colored regions at both resolutions, the default output path, invalid inputs, and inaccessible output paths. Use the standard `dotnet test` command.

## Commit & Pull Request Guidelines

Git history is not available in this checkout, so no existing commit convention can be confirmed. Use short, imperative commit subjects (for example, `Add layout dimension validation`). Pull requests should explain the behavior change, list validation performed, and include a sample output image when image generation or rendering changes.

## Specification & Configuration

Treat `specification.md` as the source of truth for input/output behavior and the colored cover, spine, and bleed regions. Preserve its distinction between the outer image dimensions and the interior region coordinates when implementing calculations. Keep generated output and local configuration out of version control unless explicitly needed as fixtures.
