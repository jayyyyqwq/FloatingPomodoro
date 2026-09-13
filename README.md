# FloatingPomodoro

A small always-on-top WPF widget: a Pomodoro timer with inline settings and
quick YouTube Music controls, docked anywhere on screen.

## What it is

A borderless, transparent, draggable **450×104** window that stays on top of
everything else. No taskbar clutter, no separate settings dialog. The layout is
deliberately wide rather than square — music on the left, pomodoro on the right,
split by a vertical divider:

```text
┌──────────────────────────────────────────────────┐
│ ┌────┐ Shake It Off (Taylor's V… ┃              │
│ │art │ Taylor Swift              ┃  WORK     ⚙  │
│ └────┘ ━━━━━━━━━───────  0:56/3:39┃    25:00     │
│        ⏮   ⏸   ⏭    YTM          ┃[Start][Reset]│
└──────────────────────────────────────────────────┘
```

**Timer** (right panel)

- Alternates WORK (red) / REST (green) countdowns, configurable in minutes.
- Start/Pause, Reset.
- Plays a chime (falls back to a system beep) when a phase ends.
- The mode colour drives *everything* on the widget, music panel included.

**Music** (left panel) — live, not just buttons

- Album artwork, track title, and artist for whatever is currently playing.
- Progress bar with elapsed / total time, updated once a second.
- **Click the progress bar to seek.**
- Play/pause button reflects real playback state — shows ⏸ while playing and
  ▶ while paused, and follows along when you change it from the browser.
- `YTM` opens `https://music.youtube.com` to pick a different playlist.

**Settings** (⚙ button)

- Overlays the whole card with a single row: work/rest minutes and Done. The ⚙
  stays visible so you can click it again to cancel; Done saves and resets.

## How it's built

| Piece | File |
| --- | --- |
| Window UI (XAML) | [MainWindow.xaml](MainWindow.xaml) |
| Window logic / event wiring | [MainWindow.xaml.cs](MainWindow.xaml.cs) |
| Countdown state machine | [TimerService.cs](TimerService.cs) |
| Live media session (title/art/position/state) | [Services/MediaSessionService.cs](Services/MediaSessionService.cs) |
| Media-key fallback + browser launch | [Services/MediaController.cs](Services/MediaController.cs) |
| Config (durations, opacity, sound toggle) | [Models/PomodoroConfig.cs](Models/PomodoroConfig.cs) |
| Track DTO | [Models/TrackInfo.cs](Models/TrackInfo.cs) |
| Alert sounds | [Assets/chime.wav](Assets/chime.wav), [Assets/beep.wav](Assets/beep.wav) |

Music reads and controls the **Windows global media session**
(`GlobalSystemMediaTransportControls`), the same mechanism behind the volume-key
media flyout. That is what makes title, artwork, position and true play/pause
state available — plain media keys are write-only and report nothing back. When
no session exists the transport buttons fall back to sending global media keys
via `keybd_event`.

Two things worth knowing:

- **It follows the current system media session, not YouTube Music specifically.**
  The session only identifies itself as `"Chrome"`, so any browser audio (a
  YouTube video, for example) will appear in the panel too. There is no reliable
  way to distinguish them.
- **No API keys, no browser extension, no YouTube account/auth** is involved.

**Stack:** .NET 6, WPF (`net6.0-windows10.0.19041.0`), Windows 10 1809+ only.

## Installing it as an app

The app ships as a **self-contained** build — the .NET runtime is bundled
inside the .exe, so it runs on any Windows 10 1809+ machine with nothing
installed. Copy the folder to another PC and it just works.

```powershell
cd D:\widgit-selfdev\FloatingPomodoro
dotnet publish -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o publish
```

That produces `publish\FloatingPomodoro.exe` (~72 MB, compressed from ~150 MB)
plus an `Assets\` folder next to it. **`Assets\` must stay beside the .exe** —
the chime is loaded from disk at runtime, not embedded.

Then create shortcuts pointing at it:

```powershell
$exe = "D:\widgit-selfdev\FloatingPomodoro\publish\FloatingPomodoro.exe"
$shell = New-Object -ComObject WScript.Shell
foreach ($dir in @([Environment]::GetFolderPath('Desktop'), [Environment]::GetFolderPath('Programs'))) {
    $lnk = $shell.CreateShortcut((Join-Path $dir "FloatingPomodoro.lnk"))
    $lnk.TargetPath = $exe
    $lnk.WorkingDirectory = Split-Path $exe
    $lnk.Save()
}
```

**Pinning to the taskbar:** launch it, then right-click its taskbar button →
*Pin to taskbar*. (Or find it in Start and right-click → *Pin to taskbar*.)

**Note:** the .exe has no custom icon yet, so Windows shows the generic blank
application icon in the taskbar and Start menu. To fix that, add an
`Assets\app.ico` and set `<ApplicationIcon>Assets\app.ico</ApplicationIcon>`
in [FloatingPomodoro.csproj](FloatingPomodoro.csproj).

The `publish\` folder lives inside the project. `dotnet clean` won't touch it,
but a `git clean -xdf` would — move it to `%LOCALAPPDATA%\Programs\` and repoint
the shortcuts if you want it fully out of harm's way.

## Running it from source

Requires the **.NET 6 SDK** (not just the runtime) — grab it from
<https://dotnet.microsoft.com/download/dotnet/6.0> if `dotnet --version`
doesn't print something starting with `6.`.

```powershell
cd D:\widgit-selfdev\FloatingPomodoro
dotnet restore
dotnet run
```

Or open `FloatingPomodoro.csproj` in Visual Studio / Rider and hit Run.

The widget appears near the top of the screen; drag it anywhere by clicking
and holding the card background (not the buttons).

There's also [verify_build.ps1](verify_build.ps1), a quick script that just
runs `dotnet build` against the project and prints whether it succeeded.

## Testing it

There's no automated test project yet — everything below is manual. Check
each item after any change:

### Timer

1. Launch the app — it should show `WORK` in red and `25:00`.
2. Click **Start** — countdown ticks down every second, button becomes
   **Pause**.
3. Click **Pause** — countdown stops; **Start** resumes it from where it
   left off.
4. Click **Reset** — returns to `WORK` / `25:00`, stopped.
5. Let a phase hit `00:00` (or temporarily set a 1-minute duration via
   Settings to test faster) — it should auto-switch to `REST` (green), play
   the chime/beep, and start counting down the rest duration automatically.

### Settings

6. Click **⚙** — timer view is replaced by the settings view, pre-filled
   with current durations.
7. Change the numbers, click **Done** — timer resets using the new
   durations, view returns to the timer.
8. Click **⚙** again then **⚙** a second time (no Done) — should return to
   the timer view without applying any edits.
9. Enter non-numeric text and click **Done** — should show an "Invalid
   Input" warning and not close the settings view.

### Music panel

10. Open `https://music.youtube.com` in Chrome/Edge and play a song — the
    panel should fill in with artwork, title, artist, and a progress bar that
    advances once a second.
11. Click the play/pause button — playback toggles in the browser tab, the
    glyph flips between ⏸ and ▶, and focus stays on the widget.
12. Pause/resume *from the browser instead* — the widget's glyph must follow
    within about a second.
13. Click `⏭` / `⏮` — title, artist and **artwork** all update. Watch the
    artwork specifically: players briefly report a placeholder (the browser
    icon) before the real cover, and the widget must end up on the real cover.
14. Click partway along the progress bar — playback jumps to that point
    rather than the window starting to drag.
15. Click `YTM` — default browser opens `music.youtube.com`.
16. Close all playing tabs — panel falls back to "Nothing playing" with an
    empty-art placeholder, and the transport buttons still don't crash.

### Window behavior / regression check

17. Confirm the window is 450 wide × 104 tall, has no title bar/border, is
    see-through, stays on top, and still drags by the card background.
18. Confirm the WORK↔REST colour switch recolors *everything* — divider,
    progress fill, artwork outline, track title/artist, position text and
    transport glyphs — not just the timer controls.
19. Play a track with a very long title and confirm it truncates with an
    ellipsis instead of resizing or overflowing the window.

### Automating this

Synthetic clicks are best driven through **UI Automation** (find by
`AutomationId`, then `InvokePattern.Invoke()`); every button carries its
`x:Name` as an AutomationId. The progress bar is the exception — a `Border`
hit-area isn't in the UIA tree, so seeking needs a real `mouse_event` click at
a computed coordinate (call `SetProcessDPIAware` first, or your coordinates
land in the wrong place under display scaling).

For *verifying* that a click really reached the player, read the media session
back independently from PowerShell rather than trusting the widget's own UI:

```powershell
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$m = [System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
  $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
  $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' }
function Await($t, $rt) { $x = $m[0].MakeGenericMethod($rt).Invoke($null, @($t)); $x.Wait(-1) | Out-Null; $x.Result }
[Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType=WindowsRuntime] | Out-Null
$mgr = Await ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager]::RequestAsync()) ([Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager])
$s = $mgr.GetCurrentSession()
"$($s.GetPlaybackInfo().PlaybackStatus)  $($s.GetTimelineProperties().Position)"
```

### Adding real automated tests later

If/when a test project is added, the pieces actually worth unit testing are:

- **`TimerService`** ([TimerService.cs](TimerService.cs)) — mode switching,
  reset behavior, tick formatting. No Win32 calls, fully testable as-is.
- **`MediaSessionService`** ([Services/MediaSessionService.cs](Services/MediaSessionService.cs))
  — put an interface over the session object so the position extrapolation
  (`Position + elapsed`, clamped at `EndTime`, frozen while paused) and the
  track-identity dedupe can be tested without WinRT.

The WinRT calls themselves and the `keybd_event` P/Invoke aren't unit-testable
and stay manual checks (items 10–16 above).
