# Backdrop design record

## Direction

Backdrop is a small Windows image tool. The design keeps attention on the finished composition. It uses a charcoal window, a large preview, a short settings panel, and one clear **Create PNG** action.

## Layout and color

- The window opens at 960 × 720 logical pixels. Its minimum client size is 800 × 600.
- The left side uses about two thirds of the work area for the preview. The right side contains composition controls and the ordered image list.
- The preview sits inside a charcoal inset stage. The full canvas stays visible at its true aspect ratio; the stage does not crop or stretch it.
- The header names the app and holds preference and shortcut actions. The footer shows the current status and the primary action.
- Window background: `#18191C`.
- Card background: `#202125`.
- Input background: `#2B2C31`.
- Preview background: `#121316`.
- Main text: `#EBECEF`. Supporting text: `#9EA0A7`.
- The primary button uses a light neutral fill with dark text. Other controls use the charcoal palette. Focus and selection use a low-contrast gray highlight.
- Buttons have soft rounded corners. Their fill changes on hover, press, and disabled states. Focus has a visible outline.
- Canvas ratio keeps the native keyboard-accessible drop-down. The closed field and its arrow use the dark palette. The open list shows a check beside the selected ratio.
- Layout uses three keyboard-accessible radio choices: Auto, Row, and Grid. Numeric fields keep native text entry, keyboard arrows, bounds, and value-change events. Their spinner buttons use dark surfaces with plus and minus marks.
- Segoe UI provides the text hierarchy. Visible labels and accessible control names identify each setting.

## Composition behavior

Pictures keep their original aspect ratio. Backdrop fits and centers each picture inside its cell; it does not crop. Auto uses a picture's own ratio for one picture, an equal-height row for two to four portrait pictures, and a grid for other groups. The grid centers its final row. Auto row ratios stay between 0.5:1 and 3:1 so that a very long row remains a useful canvas.

The output long edge is 640 to 4096 pixels. Wide 16:9 is the default, so older preference files keep their existing 1920 × 1080 output. Square and portrait canvases use the same long-edge setting. Preview images are limited to a 1000-pixel long edge.

When no source images are selected, the preview shows three code-drawn sample app screens: overview, activity, and spending detail. They use the same render, ratio, layout, padding, and shadow settings as a real composition. The badge and caption mark them as samples; they contain no real account data or external branding.

The default background samples colors from the source images. If a source has enough saturated color, the palette skips dominant neutral samples. Monochrome sources keep a gray palette. The preview header opens a small editor for a solid color, a custom horizontal gradient, or a pattern over one base color. Patterns use deterministic soft grain or spaced dots. Color values are opaque six-digit hex values. The editor updates the preview while it is open. Cancel restores its starting values; Apply keeps the changes for the session. Only **Save preferences** writes them to the settings file. Old preference files default to the automatic gradient.

A separable Gaussian blur draws a soft shadow under each picture. Background drawing shares the same output renderer for sample previews, live previews, and PNG files.

## Interaction and feedback

Adding or dropping files appends them to the ordered list. The app accepts no more than nine supported images and rejects duplicate paths. **Move up**, **Move down**, **Alt+Up**, and **Alt+Down** change the composition order.

Preview work waits 200 milliseconds after the last edit. A new edit cancels older preview work. One preview render runs at a time, and an older result is discarded if a newer request exists. Image loading keeps the 40-megapixel per-file and 80-megapixel per-selection limits.

Preferences save only when the user chooses **Save preferences**. PNG creation runs off the UI thread and does not allow a second create action at the same time. The completion status gives the actual output dimensions and direct actions to open the image or folder. A command started without a console shows a short, non-activating completion window.

## Compatibility and output safety

New settings use JSON enum strings. Missing ratio and layout fields default to Wide 16:9 and Auto. Missing background fields default to the automatic gradient, so old settings remain valid. Unknown enum values or invalid hex colors make the preferences file invalid; Backdrop uses the safe defaults and shows a warning.

Generated files go beside the first selected image. Backdrop creates a new file and adds a number when a name is already taken. It leaves input files unchanged.

## Check record

The built-in self-check verifies preset dimensions, Auto source ratios, row ordering, centered grid rows, legacy settings, invalid enum fallback, source preservation, output-name collisions, image limits, cancellation, preference round trips, and the sample preview's Auto row ratio. It also captures the sample preview at 960 × 720 and 800 × 600. Explorer integration needs a separate manual check.

Background checks cover legacy defaults, strict hex validation, saved settings, solid and gradient colors, deterministic grain and dots, and the shared preview and file renderer.
