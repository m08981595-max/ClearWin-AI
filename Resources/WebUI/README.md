# ClearWin SmartAgent Motion v3

Adds a locally animated SVG SmartAgent character to the top bar and main engine card. No external CDN, video, GIF, API, or additional package is required.

## Reactions
- Idle: gentle floating and occasional blinking.
- Sending/running: faster orbit, scanning eyes and active antenna.
- Done: green glow and smile, driven by the UI's actual completed phase.
- Error: restrained red alert expression.

The character state is wired to `state.aiActivity.phase` and engine log updates in `app.js`. It uses CSS/SVG so it is lightweight and can respond immediately to engine state without a separate media decoder. This is a vector animation rather than a GIF/video.

## Install
Back up the current UI first. Replace `index.html`, `style.css`, and `app.js` together in the frontend folder loaded by WebView2, then rebuild/relaunch ClearWin. Verify the animated character responds to an actual engine command, including failure/no-bridge behavior.

## Validation
JavaScript syntax is checked with Node.js. Static HTML checks confirm the two character mounts and SVG gradient IDs are present. Full interaction testing still needs the C# WebView2 host.
