# Keep Awake

A Windows app that uses an Arduino as a USB HID keyboard to periodically press a key and prevent your PC from going to sleep or locking without modifying any Windows power settings.

Settings (key, interval, schedule) are stored on the Arduino in EEPROM and survive USB reconnects and reboots.

---

## Requirements

- Windows 10 or Windows 11
- A compatible Arduino board (see below)
- Arduino IDE 1.8+ or Arduino IDE 2.x

---

## Supported Arduino Boards

The sketch uses the `Keyboard` library, which requires native USB HID support. The following boards are supported:

| Board               | Chip        |
| ------------------- | ----------- |
| Arduino Leonardo    | ATmega32U4  |
| Arduino Micro       | ATmega32U4  |
| SparkFun Pro Micro  | ATmega32U4  |
| Arduino Due         | AT91SAM3X8E |
| Arduino Zero        | ATSAMD21G18 |
| Arduino MKR series  | ATSAMD21G18 |
| Arduino Nano 33 IoT | ATSAMD21G18 |
| Arduino Nano 33 BLE | nRF52840    |

**Not supported:** Arduino Uno, Nano (328P), and Mega 2560. These do not have native USB HID capability.

---

## Arduino Setup

### 1. Open the sketch

Open `Arduino Code\KeepAwakeArduino.ino` in the Arduino IDE.

### 2. Select your board and port

1. Connect your Arduino via USB.
2. In the Arduino IDE, go to **Tools → Board** and select your board (e.g. _Arduino Leonardo_).
3. Go to **Tools → Port** and select the COM port for your Arduino.

### 3. Upload the sketch

Click **Upload** (the right-arrow button). The IDE will compile and flash the sketch.

After a successful upload, open the **Serial Monitor** (**Tools → Serial Monitor**) at **9600 baud**. You should see:

```
Ready
```

The Arduino is now ready. Disconnect from the Serial Monitor before starting the Windows app. Only one application can use the COM port at a time.

### Default settings (stored in EEPROM)

| Setting  | Default    |
| -------- | ---------- |
| Key      | F15        |
| Interval | 60 seconds |
| Enabled  | Yes        |

Settings are updated by the Windows app and persist across power cycles.

---

## Windows App Installation

### Option A — Installer (recommended)

1. Go to the [Releases](../../releases) page and download the latest `KeepAwake-Setup-x.x.x.exe`.
2. Double-click it and follow the wizard.

No administrator access required — the app installs for the current user only. After installation, **Keep Awake** appears in the Start menu.

To uninstall, go to **Settings → Apps → Keep Awake → Uninstall**.

### Option B — Build from source

**Prerequisites:** .NET 8 SDK, Windows App SDK workload

```powershell
git clone https://github.com/evantrowbridge/Arduino-Keep-Awake.git
cd Arduino-Keep-Awake
dotnet build "Keep Awake/Keep Awake.csproj" -c Debug
```

Or open `Keep Awake.slnx` in Visual Studio 2022 and press **F5**.

---

## App Settings

### Mode

| Mode              | Description                                                                     |
| ----------------- | ------------------------------------------------------------------------------- |
| **Always Active** | The toggle controls keep-awake directly.                                        |
| **Schedule**      | Keep-awake is automatically enabled/disabled on the configured weekly schedule. |

### Keep Awake

A toggle that enables or disables key presses on the Arduino. Only visible in _Always Active_ mode. The Arduino remembers its enabled state across reconnects.

### Schedule

A list of weekly time windows during which keep-awake is active. Each entry has a day, a start time, and an end time. Times are in 15-minute increments.

- Use **Add** to create a new entry.
- Use the pencil icon to edit an existing entry.
- Use the trash icon to remove an entry.

Entries are sorted automatically by day then start time.

### Interval

How often the Arduino presses the key. Ranges from 1 second to 12 hours. Shorter intervals are more reliable for aggressive screensaver/lock policies; longer intervals are less disruptive.

### Key

The key the Arduino presses. **F13, F14, and F15 are recommended** these keys have no standard function in Windows or common applications and will not interfere with anything.

Other keys (F1–F12, letters, digits, navigation) are available but marked with a warning since they may trigger actions in active applications.

### Start with Windows

Launches Keep Awake automatically at login. The app starts minimised to the system tray.

### Minimize to tray on close

When enabled, closing the window sends the app to the system tray instead of exiting. Right-click the tray icon for options, or left-click to reopen the window. To fully exit, use **Exit** from the tray context menu.

---

## GitHub Actions — Release Setup

The workflow builds a self-contained EXE and packages it with Inno Setup automatically.

To create a release, push a tag:

```bash
git tag v1.0.0
git push origin v1.0.0
```

The workflow builds and publishes the installer automatically. You can also trigger a release manually from the **Actions** tab using **workflow_dispatch** and entering a version number.
