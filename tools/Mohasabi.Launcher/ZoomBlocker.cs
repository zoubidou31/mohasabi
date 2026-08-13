using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Mohasabi.Launcher;

/// <summary>
/// Bloque le zoom au niveau du système (hooks bas niveau), avant que n'importe quelle
/// fenêtre — y compris la sous-fenêtre de rendu d'Edge hébergée par WebView2 — ne le reçoive.
///  - WH_MOUSE_LL : supprime Ctrl + Molette (verticale/horizontale) → pas de zoom,
///    mais la molette seule (sans Ctrl) passe → le scroll reste fonctionnel.
///  - WH_KEYBOARD_LL : supprime Ctrl + (Plus / Moins / 0) → pas de zoom clavier.
/// Les raccourcis applicatifs (Ctrl+S/K/F/P/N, Alt+←/→, Échap) ne sont pas concernés
/// (ils ne figurent pas dans la liste des touches de zoom).
/// Le blocage n'agit que lorsque la fenêtre Mohasabi est au premier plan.
/// </summary>
internal sealed class ZoomBlocker : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MOUSEHWHEEL = 0x020E;
    private const int WM_KEYDOWN = 0x0100;
    private const int MK_CONTROL = 0x0008;
    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_OEM_PLUS = 0xBB;    // Ctrl + "=" / "+"
    private const int VK_OEM_MINUS = 0xBD;   // Ctrl + "-"
    private const int VK_ADD = 0x6B;         // Pavé numérique "+"
    private const int VK_SUBTRACT = 0x6D;    // Pavé numérique "-"
    private const int VK_0 = 0x30;           // Ctrl + "0"

    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    private readonly Func<IntPtr> _getForegroundHandle;
    private IntPtr _mouseHook = IntPtr.Zero;
    private IntPtr _keyboardHook = IntPtr.Zero;
    private readonly LowLevelMouseProc _mouseCallback;
    private readonly LowLevelKeyboardProc _keyboardCallback;

    public ZoomBlocker(Func<IntPtr> getForegroundHandle)
    {
        _getForegroundHandle = getForegroundHandle;
        _mouseCallback = MouseHookProc;
        _keyboardCallback = KeyboardHookProc;

        IntPtr module = GetModuleHandle(Process.GetCurrentProcess().MainModule!.ModuleName);
        _mouseHook = SetWindowsHookEx(WH_MOUSE_LL, _mouseCallback, module, 0);
        _keyboardHook = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardCallback, module, 0);
    }

    private static bool IsCtrlDown() =>
        (GetKeyState(VK_CONTROL) & 0x8000) != 0 ||
        (GetKeyState(VK_LCONTROL) & 0x8000) != 0 ||
        (GetKeyState(VK_RCONTROL) & 0x8000) != 0;

    private static bool IsZoomKey(int vk) =>
        vk is VK_OEM_PLUS or VK_ADD or VK_OEM_MINUS or VK_SUBTRACT or VK_0;

    private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg is WM_MOUSEWHEEL or WM_MOUSEHWHEEL)
            {
                // Pour WM_MOUSEWHEEL, le bit MK_CONTROL est dans le mot de poids fort de wParam.
                bool ctrl = (((int)wParam >> 16) & MK_CONTROL) != 0;
                if (ctrl && GetForegroundWindow() == _getForegroundHandle())
                {
                    return 1; // supprimé → aucun zoom
                }
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (int)wParam == WM_KEYDOWN)
        {
            int vk = (int)lParam & 0xFFFF; // bits 0-15 = code virtuel
            if (IsZoomKey(vk) && IsCtrlDown() && GetForegroundWindow() == _getForegroundHandle())
            {
                return 1; // supprimé → aucun zoom
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_mouseHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_mouseHook);
        }

        if (_keyboardHook != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_keyboardHook);
        }

        _mouseHook = _keyboardHook = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }
}
