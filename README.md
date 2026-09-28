# OWO Twitch API Integration

A Unity desktop application that turns Twitch channel activity into haptic feedback on an [OWO](https://owogame.com/) haptic suit. It bundles two things: a visual editor for authoring OWO sensations, and a Twitch EventSub client that fires those sensations when viewers follow, subscribe, raid, cheer bits, redeem channel points, or start a hype train.

## Features

### Twitch event handling

The app subscribes to eight Twitch EventSub topics over a WebSocket connection and maps each to a sensation you choose in the UI:

| Event | EventSub topic | How the sensation is chosen |
| --- | --- | --- |
| Follow | `channel.follow` (v2) | A single dropdown selection. Repeat follows from the same user are ignored for the session. |
| Raid | `channel.raid` | A single dropdown selection. |
| Subscribe, gift sub, resub | `channel.subscribe`, `channel.subscription.gift`, `channel.subscription.message` | A single dropdown selection shared by all three. |
| Hype train start | `channel.hype_train.begin` (v2) | A single dropdown selection. |
| Bits / cheers | `channel.cheer` | Either an exact bit-amount lookup table, or a single sensation whose intensity scales with the cheer size. |
| Channel point redeem | `channel.channel_points_custom_reward_redemption.add` | Matched by reward title against a list of name-to-sensation pairs. |

Each event type has an individual on/off toggle, so you can leave follows enabled while muting raids, for example.

**Scaling bits mode.** Instead of mapping exact amounts, you can set a minimum bit threshold and let the intensity grow with the cheer. The ratio of bits to the threshold is capped at 25x, and the sensation's base intensity is interpolated up toward 100 across that range.

### Sensation editor

The editor lets you build OWO sensations without writing them by hand:

- **Micro sensations** are the building blocks. Sliders control frequency, duration, intensity, ramp-up, ramp-down, and exit delay. You pick which muscles fire, either as the whole front/back groups or individually (pectorals, dorsals, arms, lumbars, abdominals), with an optional per-muscle intensity override.
- **Full sensations** are built by appending micro sensations together in sequence. The app joins their raw strings with `&` and saves the result as a new file.
- **Test playback** sends any sensation straight to the suit so you can feel it before assigning it to an event.
- **`.owo` import** converts sensation files exported from OWO's own tools. Each file is cleaned up with a regex pass and filed as a full sensation or a micro sensation depending on whether it contains a `&`.

Sensations live as JSON on disk in two folders, and the UI dropdowns rescan those folders once per second so new files appear without a restart:

```
Assets/OWO/MicroSensation Events/*.json   # single-block micro sensations
Assets/OWO/Sensation Events/*.json        # composed, multi-block sensations
```

Each file is a thin wrapper around OWO's sensation string format:

```json
{
  "data": "100,1,25,1,1,0|0%100,1%100"
}
```

## Requirements

- **Unity** — the scripts target Unity's UI stack, so you need a Unity Editor install to open the project.
- **Windows** — `OwoFileConversion.cs` uses `System.Windows.Forms.OpenFileDialog`, and `HttpListener` binds a local port, so the app is Windows-only as written.
- **An OWO suit and the OWO desktop app** running on the same network. Connection is established through `OWO.AutoConnect()` with a 60 second timeout.
- **A Twitch application** registered at the [Twitch Developer Console](https://dev.twitch.tv/console/apps), with `http://localhost:12345/callback/` added as an OAuth redirect URL.

### Unity packages and plugins

These are referenced by the scripts but not vendored in this repository:

- [OWO Unity SDK](https://github.com/owogame) — `OWOGame` namespace, provides `OWO`, `Sensation`, `SensationsFactory`, and `Muscle`.
- [NativeWebSocket](https://github.com/endel/NativeWebSocket) — WebSocket transport for Twitch EventSub.
- [Newtonsoft Json for Unity](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@latest) — used for EventSub message parsing and settings serialization.
- TextMeshPro — all UI text, dropdowns, and input fields.
- [Michsky UI — Shift](https://assetstore.unity.com/packages/tools/gui/shift-ui-hd-ui-kit-115553) — supplies `SplashScreenManager` and `TimedEvent` for the startup screen.

## Setup

1. Clone the repository into the `Assets` folder of a Unity project, or copy the `.cs` files into an existing project alongside the packages listed above.
2. Create the two sensation folders if they do not exist:
   ```
   Assets/OWO/MicroSensation Events
   Assets/OWO/Sensation Events
   ```
3. Add your Twitch credentials. `TwitchIntegration.cs` reads them from a `TwitchSecret` class that is deliberately kept out of version control. Create `TwitchSecret.cs` yourself:
   ```csharp
   public static class TwitchSecret
   {
       public const string clientId = "your_twitch_client_id";
       public const string clientSecret = "your_twitch_client_secret";
   }
   ```
4. Build the Unity scene, wire the serialized UI references on the `TwitchManager` and `OwoSensationBuilderAndTester` components, and press Play.

> **A note on the client secret.** The authorization code flow used here requires a client secret, which means the secret ships inside any build you distribute. If you plan to hand this app to other streamers, migrate to the implicit or device code flow so each user authorizes without a shared secret. `LocalSever.cs` contains a partial implicit-flow implementation that captures the token from the URL fragment.

## How it works

```
 Twitch                        This app                         OWO suit
   │                               │                                │
   │  1. OAuth authorize           │                                │
   │◄──────────────────────────────┤  browser opens id.twitch.tv    │
   │  redirect w/ code             │                                │
   ├──────────────────────────────►│  HttpListener :12345/callback/ │
   │  2. code → access token       │                                │
   │◄─────────────────────────────►│  POST /oauth2/token            │
   │  3. GET /helix/users          │                                │
   │◄─────────────────────────────►│  resolve broadcaster ID        │
   │  4. EventSub WebSocket        │                                │
   │◄─────────────────────────────►│  wss://eventsub.wss.twitch.tv  │
   │  5. POST 8 subscriptions      │                                │
   │◄──────────────────────────────┤  helix/eventsub/subscriptions  │
   │  6. notification              │                                │
   ├──────────────────────────────►│  look up sensation JSON        │
   │                               ├───────────────────────────────►│
   │                               │  OWO.Send(Sensation.Parse(...))│
```

The connection is designed to survive a long stream:

- **Token refresh** happens automatically five minutes before expiry, and the refreshed token is validated against `id.twitch.tv/oauth2/validate`.
- **Keepalive watchdog** tracks the `keepalive_timeout_seconds` Twitch sends in the welcome message and tears down the socket if heartbeats stop arriving.
- **Reconnect handling** follows the `session_reconnect` URL when Twitch migrates the session, and reconnects from scratch on an unexpected close.
- **Stale subscription cleanup** deletes any subscriptions left in `websocket_disconnected` state before creating new ones, so you do not hit Twitch's subscription limit after repeated restarts.

Because Unity's API is single-threaded, HTTP callbacks and WebSocket messages arrive on background threads and are marshalled back through a `ConcurrentQueue` that drains in `Update()`.

## Settings and data

| What | Where |
| --- | --- |
| Event dropdown selections, redeem pairs, bit pairs | Unity `PlayerPrefs`, under the keys `SavedReddemList` and `SavedBitList` |
| Sensation definitions | `Assets/OWO/Sensation Events` and `Assets/OWO/MicroSensation Events` |
| Debug log | `<Application.dataPath>/TwitchOWOLogs.txt`, written only while debug mode is enabled |

Debug mode is toggled from the UI. When off, only errors reach the Unity console; when on, every non-keepalive EventSub message is appended to the log file.

## OAuth scopes

The authorization request asks for:

```
channel:read:subscriptions
moderator:read:followers
channel:read:redemptions
bits:read
channel:read:hype_train
```

## Project structure

| File | Role |
| --- | --- |
| `TwitchIntegration.cs` | The core. Handles OAuth, token refresh, the EventSub WebSocket, subscription management, event-to-sensation mapping, and settings persistence. |
| `OwoPrimitiveSensations.cs` | The sensation editor. Slider and muscle state, building and saving micro sensations, appending them into full sensations, loading files back into the UI. |
| `OWOTwitchConnection.cs` | Suit connection bootstrap with a 60 second timeout and a startup pulse so you know it worked. |
| `OwoFileConversion.cs` | Imports `.owo` files, normalizes them with a regex pass, and sorts them into the micro or full sensation folder. |
| `DropDownPopulator.cs` | Keeps the sensation dropdowns in sync with the files on disk, polling once per second. |
| `DynamicLog.cs` | Scrolling on-screen log panel. |
| `LocalSever.cs` | Standalone implicit-flow token listener, an alternative to the authorization code flow in `TwitchIntegration.cs`. |
| `FullSensationPlayClick.cs`, `SensationLoadClick.cs`, `SensationAppendclick.cs` | Small click handlers on the generated list items for playing, loading, and appending a sensation. |

> `LocalSever.cs` and `TwitchIntegration.cs` both bind `http://localhost:12345/callback/`. Only one of them should be active in a scene at a time.

## License

MIT — see [LICENSE](LICENSE). Copyright (c) 2024 RevoForge.
