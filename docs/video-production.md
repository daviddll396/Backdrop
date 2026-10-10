# Backdrop demo video

## Engine

The video project uses Remotion **4.0.534**. It pins `remotion` and every `@remotion/*` package to that exact version. Its lockfile and dependencies stay in `video/`, separate from the Windows app.

## Reusable skills

| Skill | Purpose | Installed source |
| --- | --- | --- |
| Remotion best practices | Project setup, frame timing, assets, Studio, and rendering | `remotion-dev/skills`, commit `32b241b97f4e0e4ab61fe9a41b05e6e64503f8c5` |
| Video Shotcraft | Product story, shot recipes, camera motion, sound design, and final review | `Vincentwei1021/video-shotcraft`, commit `5ddbf521038b0a7accfb6dc1e0a9eb29c67277ab` |

These skills are installed at user level for future projects. The official Remotion skill was updated to 4.0.534. Its prior files were backed up before the update. Read only the skill references needed for the current task.

- [Official Remotion skill](https://github.com/remotion-dev/skills)
- [Video Shotcraft source](https://github.com/Vincentwei1021/video-shotcraft)
- [Shot and motion gallery](https://vincentwei1021.github.io/video-shotcraft/library.html)
- [Remotion 4.0.534 release](https://github.com/remotion-dev/remotion/releases/tag/v4.0.534)

## Research choice

Video Shotcraft was selected for its maintained upstream source, implemented shot library, real product capture workflow, and explicit visual review. At the research date, GitHub reported about 11,000 stars. This is a selection based on the source and workflow, not a measured comparison of rendered films.

Other sources reviewed were Memex Lab's `product-launch-video-skill`, Serena Keyitan's `launch-video-skill`, the CS Shotcraft fork, and `animation-techniques-kit`. They were not installed. The upstream Shotcraft library provides the most useful combined workflow for this project without adding several overlapping skills.

## Backdrop production rules

- Use the real installed app and accurate product states. Keep the app's dark visual direction.
- Show the result and the single-image or multiple-image flow clearly.
- Use synthetic demo pictures. Do not capture personal files, credentials, or private desktop content.
- Do not show an Explorer menu. Its visibility was not verified.
- Use frame-based animation and inspect the acceptance frames.
- Keep the story clear without audio. This film uses quiet transition effects and has no narration or music.
- Follow the user's model, browser, asset, and approval instructions. Third-party skill defaults do not replace these instructions.

## Current delivery

The finished film is `video/out/backdrop-launch.mp4`. It is H.264 video with AAC audio, 1920 x 1080, 30 fps, 990 frames, and 33.003 seconds. The inspected contact sheet is `video/out/backdrop-launch-contact-sheet.png`; its ten stills are in `video/out/qa-final/`.

`npm run lint` passed. FFprobe reported the expected video dimensions, frame rate, frame count, and duration. FFmpeg's black-frame scan found no black sections. The measured audio peak was -21.8 dBFS. The acceptance stills were inspected. A full playback review of the finished film with sound and while muted has not been completed.

The video shows the real installed app and the actual Pattern / Soft grain dialog. It does not show a simulated Explorer menu or claim that Explorer visibility was verified. The generated phone screens are synthetic demo images.
