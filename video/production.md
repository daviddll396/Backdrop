# Backdrop launch film production

## Format and direction

Make a 33-second film at 1920 x 1080 and 30 fps (990 frames) in Remotion. Pin `remotion` and every `@remotion/*` package to 4.0.534. Keep the film project inside `video/`; it does not change the Windows app.

Use the app palette: charcoal `#18191C`, panel `#202125`, text `#EBECEF`, and the warm accents in the generated sample screens. Use Segoe UI and short captions. The finished composition must be visible in the opening. Keep movement restrained and make every point clear with audio muted. Use a few quiet transition sounds, no voiceover or music.

Use straight cuts between scenes. The cut from the result to the app is a real app screenshot; do not add a simulated focus blur, cursor, or click. Do not show a made-up Explorer menu or claim that Explorer visibility was verified. The app README confirms that image processing runs on the device. End with the GitHub repository, not a Store badge or installer claim.

## Source assets

The three phone screens are generated from the app's code-drawn sample pages (Overview, Activity, Spending). The group, single-image, grain, and dots renders use those same pages through `BackdropRenderer`. They are demo assets and contain no personal images.

| Film asset | Source and use |
| --- | --- |
| `app-main.png` | Actual installed app client-area capture. Only the native window frame is cropped; controls and content are unchanged. The preview is marked SAMPLE. |
| `input-1.png`, `input-2.png`, `input-3.png` | 800 x 1600 sample pages. Use as the source cards. |
| `single.png` | Backdrop output from `input-1.png`, displayed at its portrait ratio. |
| `multi.png`, `sample-composition.png` | Backdrop output from all three sample pages. |
| `background-grain.png`, `background-dots.png` | Backdrop outputs using Soft grain and Dots with the same three pages. |
| `background-editor.png` | Client-area crop of the actual Background dialog with Pattern and Soft grain selected. Only window chrome is cropped. |
| `app-icon.png` | The app's installed icon, shown in the closing card. |

Do not use `portrait one.png`, `landscape.png`, or `render-preview.png` from the self-check fixture. They are test shapes. Crop only window chrome or empty margins. Do not change controls or invent an app state.

## Motion references

Adapt only the motion from the installed Shotcraft examples. Keep Backdrop's own images, type, and palette.

1. `spotlight-hero-card`: `C:/Users/ASUS/.codex/skills/video-shotcraft/references/shots/opening/spotlight-hero-card.md`. Use a restrained light and one shallow rise on the finished composition. Skip amber outlines and 3D annotations.
2. `list-reveal`: `C:/Users/ASUS/.codex/skills/video-shotcraft/references/shots/ui-entrance/list-reveal.md`. Bring the three sample cards into the workspace in order and hold them long enough to read.
3. Between scenes, use clean straight cuts. No blur or other focus-handoff effect is present in the approved edit.

## Timeline

| Frames | Time | Picture and motion | On-screen text |
| --- | --- | --- | --- |
| 0-119 | 0-4 s | Spotlight settles on `sample-composition.png`. Hold the full canvas for at least 45 frames. | `Backdrop` / `Images, composed.` |
| 120-239 | 4-8 s | Show `input-1.png` as a portrait card, then cut to `single.png` at its full aspect ratio. | `One image.` |
| 240-419 | 8-14 s | Reveal the three source cards in order. Cut to `multi.png`, then `sample-composition.png`; hold the finished group. | `Or several. One PNG.` |
| 420-599 | 14-20 s | Straight cut to the real `app-main.png` client-area capture. Keep the 960 x 720 image at about 1000 pixels wide so the preview, settings, and Create PNG action remain legible. | `Preview as you edit.` |
| 600-749 | 20-25 s | Show the actual Pattern / Soft grain dialog, then compare the grain and dots outputs side by side. | `Set the mood.` |
| 750-869 | 25-29 s | Return to the finished composition. After it settles, show the local-device caption. | `Made on your device.` |
| 870-989 | 29-33 s | Close with the app icon, name, composition, and repository. Hold the final card for at least 45 frames. | `Explore on GitHub` / `github.com/daviddll396/Backdrop` |

Keep captions at least 125 pixels from the left and right edges and at least 75 pixels from the top and bottom edges where the layout permits. These gutters keep text readable at phone size. Composition images may use more of the canvas, but keep their full boundaries visible and do not crop a source photo to imply that Backdrop crops it.

## Acceptance and delivery

Inspect frames 0, 90, 175, 310, 390, 500, 675, 805, 925, and 975. Check that the images match the real app output, the complete composition is visible, labels are readable, edges are clean, and text has enough contrast. Watch the full film once muted and once with the final sound effects.

Render an H.264 MP4 at 1920 x 1080 and 30 fps. Keep the Remotion source and a compact contact sheet of the inspected frames. Confirm the output has 990 frames, has no missing assets or black gaps, and shows the correct repository URL. Do not describe an unverified Explorer menu, Store release, or signed installer as released.
