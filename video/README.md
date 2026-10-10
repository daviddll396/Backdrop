# Backdrop launch film

This folder contains the editable Remotion source for the 33-second Backdrop
launch film. It uses generated sample screens and real screenshots of the
Windows application. It does not show an Explorer menu or claim that Explorer
visibility was verified.

## Render

Use Node.js, then install the pinned dependencies and start the preview:

```powershell
npm ci
npm run dev
```

For the licensed transition sounds, download these Mixkit files into
`public/audio/` before rendering:

- `swoosh-quick.mp3` — <https://assets.mixkit.co/active_storage/sfx/166/166-preview.mp3>
- `transition-soft.mp3` — <https://assets.mixkit.co/active_storage/sfx/2608/2608-preview.mp3>

They are covered by the [Mixkit Sound Effects Free License](https://mixkit.co/license/modal/sfxFree/).
The MP3 files are ignored by Git because the license allows use in a video but
does not allow redistributing the sound files on their own. The rendered MP4
includes the low-volume cues.

Run the type and lint checks, then render the 1,920 × 1,080, 30 fps film:

```powershell
npm run lint
npx remotion render src/index.ts BackdropLaunch out/backdrop-launch.mp4 --codec h264
```

The checked cut is 990 frames. The source composition and still assets remain
editable in this project.
