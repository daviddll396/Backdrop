# Backdrop design record

## App icon

The app icon uses a charcoal tile, a warm gradient canvas, and an inset picture. The source is `assets/app-icon.png`. The Windows icon file is `assets/app-icon.ico`, with sizes from 16 to 256 pixels. The executable, main window, and installer use the same icon.

## App direction

Backdrop is a small Windows image tool. The design keeps attention on the finished composition. It uses a charcoal window, a large preview, a short settings panel, and one clear **Create PNG** action.

## Layout and color

- The window opens at 960 × 720 logical pixels. Its minimum client size is 800 × 600.
- The left side uses about two thirds of the work area for the preview. The right side contains composition controls and the ordered image list.
- The preview sits inside a charcoal inset stage. The full canvas stays visible at its true aspect ratio; the stage does not crop or stretch it.
- The header names the app and holds preference and shortcut actions. The footer shows the current status and the primary action.
- The preview, settings, and image-list panels use a charcoal surface without an outer border. The settings panel gives **Size (px)** a full row, then places **Padding (%)** beside **Shadow (%)**.
- The preview has a separate toolbar row for the **Background** editor and its full current mode. The sample or image-count label has its own row.
- An empty image list shows the centered hint **Add or drop up to 9 images.** The hint hides when the list contains images.
- Window background: `#18191C`.
- Card background: `#202125`.
- Input background: `#2B2C31`.
- Preview background: `#121316`.
- Main text: `#EBECEF`. Supporting text: `#9EA0A7`.
- The primary button uses a light neutral fill with dark text. Other controls use the charcoal palette. Focus and selection use a low-contrast gray highlight.
- Buttons have soft rounded corners. Their fill changes on hover, press, and disabled states. Focus has a visible outline.
- Canvas ratio keeps the native keyboard-accessible drop-down. The closed field and its arrow use the dark palette. The open list shows a check beside the selected ratio.
- Layout uses three keyboard-accessible radio choices: Auto, Row, and Grid. Numeric fields keep native text entry, keyboard arrows, bounds, and value-change events. Their spinner buttons use dark surfaces with up and down chevrons.
- The image list uses clear selected and focus states. Previous and next buttons show left and right chevrons. Their accessible names say which position they select. Alt+Up and Alt+Down also change the image order.
- Segoe UI provides the text hierarchy. Visible labels and accessible control names identify each setting.

## Composition behavior

Pictures keep their original aspect ratio. Backdrop fits and centers each picture inside its cell; it does not crop. Auto uses a picture's own ratio for one picture, an equal-height row for two to four portrait pictures, and a grid for other groups. The grid centers its final row. Auto row ratios stay between 0.5:1 and 3:1 so that a very long row remains a useful canvas.

The output long edge is 640 to 4096 pixels. Wide 16:9 is the default, so older preference files keep their existing 1920 × 1080 output. Square and portrait canvases use the same long-edge setting. Preview images are limited to a 1000-pixel long edge.

When no source images are selected, the preview shows three code-drawn sample app screens: overview, activity, and spending detail. They use the same render, ratio, layout, padding, and shadow settings as a real composition. The badge and caption mark them as samples; they contain no real account data or external branding.

The default background samples colors from the source images. If a source has enough saturated color, the palette skips dominant neutral samples. Dark colors move further toward white than bright colors do. This gives dark sources a lighter background. Monochrome sources keep a neutral gray palette. The preview toolbar opens an editor for a solid color, a custom horizontal gradient, or a pattern over one base color. The editor has eight quick-color buttons, an editable `#RRGGBB` field, and an advanced Windows color picker. The field rejects invalid hex values. Apply keeps the editor open until visible color values are valid. The toolbar shows the full current mode, such as **Automatic gradient** or **Soft-grain pattern**. Patterns use deterministic soft grain or spaced dots. The editor updates the preview while it is open. Cancel restores its starting values; Apply keeps the changes for the session. Only **Save preferences** writes them to the settings file. Old preference files default to the automatic gradient.

A separable Gaussian blur draws a soft shadow under each picture. Preview shadows use a mask with a maximum edge of 256 pixels. PNG shadows use a maximum edge of 384 pixels. The preview keeps the foreground at its full preview size. Background drawing shares the same output renderer for sample previews, live previews, and PNG files. Automatic and custom gradients use deterministic low-amplitude dithering to reduce visible color bands while keeping the selected endpoints and foreground pixels unchanged.

## Interaction and feedback

Adding or dropping files appends them to the ordered list. The app accepts no more than nine supported images and rejects duplicate paths. The left and right chevron buttons move a selected image to the previous or next position. **Alt+Up** and **Alt+Down** keep the same behavior.

Preview work waits 200 milliseconds after the last edit. A small spinner shows while an update is pending. The last good preview stays visible while new work runs. A new edit cancels older preview work. One preview render runs at a time, and an older result is discarded if a newer request exists. The preview cache stores reduced copies of the current selection. It drops copies that are no longer selected and reloads a file when its size or modified time changes. Backdrop checks the full source before it reduces the image. Image loading keeps the 40-megapixel per-file and 80-megapixel per-selection limits.

Preferences save only when the user chooses **Save preferences**. PNG creation runs off the UI thread and does not allow a second create action at the same time. The completion status gives the actual output dimensions and direct actions to open the image or folder. A command started without a console shows a short, non-activating completion window.

## Compatibility and output safety

New settings use JSON enum strings. Missing ratio and layout fields default to Wide 16:9 and Auto. Missing background fields default to the automatic gradient, so old settings remain valid. Unknown enum values or invalid hex colors make the preferences file invalid; Backdrop uses the safe defaults and shows a warning.

Generated files go beside the first selected image. Backdrop creates a new file and adds a number when a name is already taken. It leaves input files unchanged.

## Check record

The built-in self-check verifies preset dimensions, source ratios, image order, row and grid layout, preference loading, output names, image limits, and source preservation. It checks cancellation, cache reuse, cache refresh after a file change, invalid color text, automatic colors for dark monochrome images, spinner controls, and the live editor. It captures the main window at 960 by 720 and 800 by 600 pixels. Explorer integration needs a separate manual check.

Background checks cover legacy defaults, valid and invalid hex values, solid colors, gradients, patterns, and the shared preview and file renderer.
