# TeamsIO logo assets

The person-and-open-ring concept was approved for TeamsIO. The amber head represents presence, with a teal torso and ring on a charcoal tile. The solid taskbar icon is distinct from the purple Microsoft Teams mark.

- `teamsio-icon.png`: production source, generated with the built-in image-generation tool.
- `TeamsIO.ico`: Windows icon containing 16, 20, 24, 32, 40, 48, 64, 128 and 256 pixel frames.
- `teamsio-logo-concept.png`: approved logo and wordmark presentation reference.

Regenerate the ICO on Windows with `./tools/New-AppIcon.ps1`. Conversion resizes and encodes the production PNG; it does not redesign the artwork.

Final generation brief: preserve one amber circular head and teal shoulders/torso within a thick open teal ring, widen the gap around the torso for small sizes, and use a full-bleed charcoal square background. No text, extra people, purple, or letter tile. Built-in image generation was used; the CLI/API fallback was not used.
