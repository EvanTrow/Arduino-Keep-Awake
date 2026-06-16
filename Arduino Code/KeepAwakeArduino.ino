// KeepAwakeArduino.ino
// Arduino Leonardo sketch to keep a PC awake by pressing a configurable key.
// Communicates with the KeepAwake Windows app via serial (9600 baud).
//
// Serial commands:
//   isArduinoKeyboard   -> "true"  (device identification)
//   enable              -> enables key presses, saves to EEPROM
//   disable             -> disables key presses, saves to EEPROM
//   status              -> prints current enabled state and delay
//   getSettings         -> "SETTINGS:enabled=1,delay=60000,key=208"
//   setDelay <ms>       -> sets cycle delay in milliseconds
//   setKey <0-255>      -> sets key code (Arduino Keyboard.h value)
//
// Key codes (decimal):
//   F1-F12  = 194-205   F13=240  F14=241  F15=242
//   Letters a-z = 97-122  Digits 0-9 = 48-57
//   Enter=176  Escape=177  Backspace=178  Tab=179
//   Arrow Up=218  Down=217  Left=216  Right=215
// Note: F13-F15 use codes 240-242 (HID 0x68-0x6A).
//   Codes 206-208 resolve to PrintScreen/ScrollLock/Pause — do not use.

#include <Keyboard.h>
#include <EEPROM.h>

// ---- EEPROM Layout ----
// Bump magic to force re-initialisation — fixes F13-F15 key codes (were 0xCE-0xD0, now 0xF0-0xF2).
const uint32_t SETTINGS_MAGIC = 0xDEADBEF2;

struct Settings {
  uint32_t magic;
  uint8_t  enabled;       // 0 = disabled, 1 = enabled
  uint32_t cycleDelay;    // milliseconds between key presses (was unsigned long)
  uint8_t  keyCode;       // Arduino Keyboard.h key code byte
};

// ---- Runtime state ----
Settings     settings;
bool         keypressEnabled = true;
uint32_t     cycleDelay      = 60000UL;  // ms
uint8_t      activeKey       = 0xF2;     // KEY_F15

String       serialBuffer    = "";
uint32_t     nextCycleTime   = 0;
uint32_t     stepTime        = 0;
int          step            = 0;        // 0=waiting, 1=key held

const uint32_t HOLD_DURATION = 60UL;     // ms to hold the key

// ---- EEPROM helpers ----

void loadSettings() {
  EEPROM.get(0, settings);
  if (settings.magic != SETTINGS_MAGIC) {
    // First run or upgrade – write defaults
    settings.magic      = SETTINGS_MAGIC;
    settings.enabled    = 1;
    settings.cycleDelay = 60000UL;
    settings.keyCode    = 0xF2;  // KEY_F15
    EEPROM.put(0, settings);
  }
  keypressEnabled = (settings.enabled != 0);
  cycleDelay      = settings.cycleDelay;
  activeKey       = settings.keyCode;
}

void saveSettings() {
  settings.magic      = SETTINGS_MAGIC;
  settings.enabled    = keypressEnabled ? 1 : 0;
  settings.cycleDelay = cycleDelay;
  settings.keyCode    = activeKey;
  EEPROM.put(0, settings);
}

// ---- Setup ----

void setup() {
  Keyboard.begin();
  Serial.begin(9600);
  delay(2000);  // Allow USB CDC to enumerate
  loadSettings();
  Serial.println("Ready");
}

// ---- Serial command processor ----

void processSerial() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n' || c == '\r') {
      serialBuffer.trim();
      if (serialBuffer.length() > 0) {
        handleCommand(serialBuffer);
      }
      serialBuffer = "";
    } else {
      serialBuffer += c;
      if (serialBuffer.length() > 64) {
        serialBuffer = serialBuffer.substring(serialBuffer.length() - 64);
      }
    }
  }
}

void handleCommand(String cmd) {
  if (cmd.equalsIgnoreCase("isArduinoKeyboard")) {
    Serial.println("true");

  } else if (cmd.equalsIgnoreCase("enable")) {
    keypressEnabled = true;
    saveSettings();
    nextCycleTime = millis() + cycleDelay;
    Serial.println("Keypresses enabled");

  } else if (cmd.equalsIgnoreCase("disable")) {
    keypressEnabled = false;
    saveSettings();
    if (step == 1) {
      Keyboard.releaseAll();
      step = 0;
    }
    Serial.println("Keypresses disabled");

  } else if (cmd.equalsIgnoreCase("status")) {
    Serial.print("Keypresses ");
    Serial.println(keypressEnabled ? "enabled" : "disabled");
    Serial.print("Cycle delay (ms): ");
    Serial.println(cycleDelay);
    Serial.print("Key code: ");
    Serial.println(activeKey);

  } else if (cmd.equalsIgnoreCase("getSettings")) {
    // Single-line response for easy parsing by the Windows app
    Serial.print("SETTINGS:enabled=");
    Serial.print(keypressEnabled ? 1 : 0);
    Serial.print(",delay=");
    Serial.print(cycleDelay);
    Serial.print(",key=");
    Serial.println(activeKey);

  } else if (cmd.startsWith("setDelay ")) {
    String val = cmd.substring(9);
    val.trim();
    long v = val.toInt();
    if (v > 0) {
      cycleDelay = (uint32_t)v;
      saveSettings();
      nextCycleTime = millis() + cycleDelay;
      Serial.print("Cycle delay set to ");
      Serial.println(cycleDelay);
    } else {
      Serial.println("ERR: Invalid delay value");
    }

  } else if (cmd.startsWith("setKey ")) {
    String val = cmd.substring(7);
    val.trim();
    int v = val.toInt();
    if (v > 0 && v <= 255) {
      // Release old key if currently held
      if (step == 1) {
        Keyboard.release(activeKey);
        step = 0;
        nextCycleTime = millis() + cycleDelay;
      }
      activeKey = (uint8_t)v;
      saveSettings();
      Serial.print("Key set to ");
      Serial.println(activeKey);
    } else {
      Serial.println("ERR: Invalid key code (must be 1-255)");
    }

  } else {
    Serial.print("ERR: Unknown command: ");
    Serial.println(cmd);
  }
}

// ---- Non-blocking keyboard state machine ----

void handleKeyboardCycle() {
  uint32_t now = millis();

  if (!keypressEnabled) {
    if (step == 1) {
      Keyboard.releaseAll();
      step = 0;
    }
    return;
  }

  if (step == 0) {
    if (now >= nextCycleTime) {
      Keyboard.press(activeKey);
      step     = 1;
      stepTime = now;
    }
  } else if (step == 1) {
    if (now - stepTime >= HOLD_DURATION) {
      Keyboard.release(activeKey);
      Serial.println("Key Press");
      step          = 0;
      nextCycleTime = now + cycleDelay;
    }
  }
}

// ---- Main loop ----

void loop() {
  processSerial();
  handleKeyboardCycle();
  delay(0);  // Yield to USB stack
}
