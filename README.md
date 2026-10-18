# OWO Twitch API Integration

A Windows desktop app that turns Twitch channel activity into haptic feedback on an [OWO](https://owogame.com/) suit. Connect your Twitch account, assign a sensation to each event, and the suit fires when viewers follow, subscribe, raid, cheer bits, redeem channel points, or start a hype train.

The latest release is **v1.3**. That version drops the built-in sensation editor. Create sensations in the official [OWO Sensations Creator](https://owo-game.gitbook.io/owo-api/tools/sensations-creator) and import the `.owo` files they export.

## Download

Grab the latest build from the [Releases](https://github.com/Merkel-User/OWO-Twitch-API-Integration/releases) page. The complete program is there — you do not need to compile anything.

You will also need:

- An [OWO](https://owogame.com/) suit and the [My OWO](https://owogame.com/downloads/) app running on the same network
- A Twitch account for the channel you want to connect
- The official [Sensations Creator](https://owogame.com/downloads/) if you want to make or edit sensations

## Getting started

1. Download and run the latest release.
2. Start the My OWO app and wait until this program connects to the suit.
3. Sign in with Twitch from the app. Your browser will open Twitch's authorization page, then return you here.
4. Create sensations in the official Sensations Creator and export them as `.owo` files. Leave **baked** unchecked — baked exports are meant for Game Auth, not for this app.
5. Import those `.owo` files in this program, then assign them to Twitch events in the UI.

## Importing sensations

v1.3 does not include a sensation editor. Use OWO's official tool instead:

1. Download the [Sensations Creator](https://owogame.com/downloads/).
2. Design a sensation and feel it on the suit from that tool.
3. Export it as an individual `.owo` file, with muscles included if you want them locked to specific zones.
4. Import the file here. It will show up in the event dropdowns so you can map it to a follow, redeem, bit amount, and so on.

You can keep adding or replacing `.owo` files as you go; the event lists pick up new imports without a restart.

## Twitch events

The app listens for eight Twitch EventSub topics and maps each to a sensation you choose:

| Event | How the sensation is chosen |
| --- | --- |
| Follow | A single dropdown selection. Repeat follows from the same user are ignored for the session. |
| Raid | A single dropdown selection. |
| Subscribe, gift sub, resub | A single dropdown selection shared by all three. |
| Hype train start | A single dropdown selection. |
| Bits / cheers | Either an exact bit-amount lookup table, or a single sensation whose intensity scales with the cheer size. |
| Channel point redeem | Matched by reward title against a list of name-to-sensation pairs. |

Each event type has its own on/off toggle, so you can leave follows enabled while muting raids, for example.

**Scaling bits mode.** Instead of mapping exact amounts, you can set a minimum bit threshold and let the intensity grow with the cheer. The ratio of bits to the threshold is capped at 25x, and the sensation's base intensity is interpolated up toward 100 across that range.

## During a stream

The Twitch connection is built to stay up for a long session:

- Access tokens refresh automatically before they expire
- A keepalive watchdog reconnects if Twitch's heartbeat stops
- The app follows Twitch's reconnect URL when the EventSub session migrates
- Stale subscriptions from earlier disconnects are cleaned up so you do not hit Twitch's subscription limit after a restart

A debug toggle writes extra EventSub traffic to a log file when you need to diagnose a missed event.

## License

MIT — see [LICENSE](LICENSE). Copyright (c) 2024 RevoForge.
