using Windows.System;

namespace Keep_Awake;

public static class KeyMapper
{
    public record KeyInfo(string DisplayName, byte ArduinoCode, bool IsSafe, string Group);

    private const byte KEY_RETURN    = 0xB0;
    private const byte KEY_ESC       = 0xB1;
    private const byte KEY_BACKSPACE = 0xB2;
    private const byte KEY_TAB       = 0xB3;
    private const byte KEY_CAPS_LOCK = 0xC1;
    private const byte KEY_F1        = 0xC2;
    private const byte KEY_F2        = 0xC3;
    private const byte KEY_F3        = 0xC4;
    private const byte KEY_F4        = 0xC5;
    private const byte KEY_F5        = 0xC6;
    private const byte KEY_F6        = 0xC7;
    private const byte KEY_F7        = 0xC8;
    private const byte KEY_F8        = 0xC9;
    private const byte KEY_F9        = 0xCA;
    private const byte KEY_F10       = 0xCB;
    private const byte KEY_F11       = 0xCC;
    private const byte KEY_F12       = 0xCD;
    private const byte KEY_F13       = 0xF0;
    private const byte KEY_F14       = 0xF1;
    private const byte KEY_F15       = 0xF2;
    private const byte KEY_RIGHT_ARR = 0xD7;
    private const byte KEY_LEFT_ARR  = 0xD8;
    private const byte KEY_DOWN_ARR  = 0xD9;
    private const byte KEY_UP_ARR    = 0xDA;
    private const byte KEY_HOME      = 0xD2;
    private const byte KEY_PAGE_UP   = 0xD3;
    private const byte KEY_DELETE    = 0xD4;
    private const byte KEY_END       = 0xD5;
    private const byte KEY_PAGE_DOWN = 0xD6;

    private static readonly Dictionary<VirtualKey, KeyInfo> _map = new()
    {
        // ── Safe function keys ──
        [VirtualKey.F13] = new("F13", KEY_F13, true,  "Safe Keys"),
        [VirtualKey.F14] = new("F14", KEY_F14, true,  "Safe Keys"),
        [VirtualKey.F15] = new("F15", KEY_F15, true,  "Safe Keys"),

        // ── Function keys ──
        [VirtualKey.F1]  = new("F1",  KEY_F1,  false, "Function Keys"),
        [VirtualKey.F2]  = new("F2",  KEY_F2,  false, "Function Keys"),
        [VirtualKey.F3]  = new("F3",  KEY_F3,  false, "Function Keys"),
        [VirtualKey.F4]  = new("F4",  KEY_F4,  false, "Function Keys"),
        [VirtualKey.F5]  = new("F5",  KEY_F5,  false, "Function Keys"),
        [VirtualKey.F6]  = new("F6",  KEY_F6,  false, "Function Keys"),
        [VirtualKey.F7]  = new("F7",  KEY_F7,  false, "Function Keys"),
        [VirtualKey.F8]  = new("F8",  KEY_F8,  false, "Function Keys"),
        [VirtualKey.F9]  = new("F9",  KEY_F9,  false, "Function Keys"),
        [VirtualKey.F10] = new("F10", KEY_F10, false, "Function Keys"),
        [VirtualKey.F11] = new("F11", KEY_F11, false, "Function Keys"),
        [VirtualKey.F12] = new("F12", KEY_F12, false, "Function Keys"),

        // ── Letters ──
        [VirtualKey.A] = new("A", (byte)'a', false, "Letters"),
        [VirtualKey.B] = new("B", (byte)'b', false, "Letters"),
        [VirtualKey.C] = new("C", (byte)'c', false, "Letters"),
        [VirtualKey.D] = new("D", (byte)'d', false, "Letters"),
        [VirtualKey.E] = new("E", (byte)'e', false, "Letters"),
        [VirtualKey.F] = new("F", (byte)'f', false, "Letters"),
        [VirtualKey.G] = new("G", (byte)'g', false, "Letters"),
        [VirtualKey.H] = new("H", (byte)'h', false, "Letters"),
        [VirtualKey.I] = new("I", (byte)'i', false, "Letters"),
        [VirtualKey.J] = new("J", (byte)'j', false, "Letters"),
        [VirtualKey.K] = new("K", (byte)'k', false, "Letters"),
        [VirtualKey.L] = new("L", (byte)'l', false, "Letters"),
        [VirtualKey.M] = new("M", (byte)'m', false, "Letters"),
        [VirtualKey.N] = new("N", (byte)'n', false, "Letters"),
        [VirtualKey.O] = new("O", (byte)'o', false, "Letters"),
        [VirtualKey.P] = new("P", (byte)'p', false, "Letters"),
        [VirtualKey.Q] = new("Q", (byte)'q', false, "Letters"),
        [VirtualKey.R] = new("R", (byte)'r', false, "Letters"),
        [VirtualKey.S] = new("S", (byte)'s', false, "Letters"),
        [VirtualKey.T] = new("T", (byte)'t', false, "Letters"),
        [VirtualKey.U] = new("U", (byte)'u', false, "Letters"),
        [VirtualKey.V] = new("V", (byte)'v', false, "Letters"),
        [VirtualKey.W] = new("W", (byte)'w', false, "Letters"),
        [VirtualKey.X] = new("X", (byte)'x', false, "Letters"),
        [VirtualKey.Y] = new("Y", (byte)'y', false, "Letters"),
        [VirtualKey.Z] = new("Z", (byte)'z', false, "Letters"),

        // ── Digits ──
        [VirtualKey.Number0] = new("0", (byte)'0', false, "Digits"),
        [VirtualKey.Number1] = new("1", (byte)'1', false, "Digits"),
        [VirtualKey.Number2] = new("2", (byte)'2', false, "Digits"),
        [VirtualKey.Number3] = new("3", (byte)'3', false, "Digits"),
        [VirtualKey.Number4] = new("4", (byte)'4', false, "Digits"),
        [VirtualKey.Number5] = new("5", (byte)'5', false, "Digits"),
        [VirtualKey.Number6] = new("6", (byte)'6', false, "Digits"),
        [VirtualKey.Number7] = new("7", (byte)'7', false, "Digits"),
        [VirtualKey.Number8] = new("8", (byte)'8', false, "Digits"),
        [VirtualKey.Number9] = new("9", (byte)'9', false, "Digits"),

        // ── Navigation ──
        [VirtualKey.Up]       = new("↑ Up",      KEY_UP_ARR,    false, "Navigation"),
        [VirtualKey.Down]     = new("↓ Down",    KEY_DOWN_ARR,  false, "Navigation"),
        [VirtualKey.Left]     = new("← Left",    KEY_LEFT_ARR,  false, "Navigation"),
        [VirtualKey.Right]    = new("→ Right",   KEY_RIGHT_ARR, false, "Navigation"),
        [VirtualKey.Home]     = new("Home",      KEY_HOME,      false, "Navigation"),
        [VirtualKey.End]      = new("End",       KEY_END,       false, "Navigation"),
        [VirtualKey.PageUp]   = new("Page Up",   KEY_PAGE_UP,   false, "Navigation"),
        [VirtualKey.PageDown] = new("Page Down", KEY_PAGE_DOWN, false, "Navigation"),
        [VirtualKey.Delete]   = new("Delete",    KEY_DELETE,    false, "Navigation"),

        // ── Other ──
        [VirtualKey.Space]       = new("Space",     (byte)' ',    false, "Other"),
        [VirtualKey.Tab]         = new("Tab",       KEY_TAB,      false, "Other"),
        [VirtualKey.Enter]       = new("Enter",     KEY_RETURN,   false, "Other"),
        [VirtualKey.Back]        = new("Backspace", KEY_BACKSPACE,false, "Other"),
        [VirtualKey.CapitalLock] = new("Caps Lock", KEY_CAPS_LOCK,false, "Other"),
    };

    private static readonly string[] _groupOrder =
        ["Safe Keys", "Function Keys", "Letters", "Digits", "Navigation", "Other"];

    public static KeyInfo? Get(VirtualKey key) =>
        _map.TryGetValue(key, out var info) ? info : null;

    public static KeyInfo? GetByCode(byte code) =>
        _map.Values.FirstOrDefault(v => v.ArduinoCode == code);

    public static IReadOnlyList<KeyInfo> GetAll() =>
        _map.Values
            .OrderBy(k => Array.IndexOf(_groupOrder, k.Group))
            .ThenBy(k => k.ArduinoCode)
            .ToList();

    public static byte DefaultCode => KEY_F15;
}
