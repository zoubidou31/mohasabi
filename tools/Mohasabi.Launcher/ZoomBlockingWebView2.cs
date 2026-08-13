using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace Mohasabi.Launcher;

/// <summary>
/// WebView2 qui bloque le zoom au niveau Win32, avant que le moteur de rendu ne le traite :
///  - Ctrl + Molette (verticale/horizontale) : supprimé → pas de zoom, mais la molette seule
///    (sans Ctrl) passe normalement → le scroll reste fonctionnel.
///  - Ctrl + (Plus / Moins / 0) : supprimé → pas de zoom clavier.
/// Les raccourcis applicatifs (Ctrl+S/K/F/P/N, Alt+←/→, Échap) ne sont pas concernés.
/// Ce blocage est indépendant du runtime WebView2 installé (contrairement à
/// IsZoomControlEnabled qui peut être contourné par le traitement de la molette au niveau
/// du contrôle). Aucun rétablissement de ZoomFactor n'est nécessaire → pas de scintillement.
/// </summary>
internal sealed class ZoomBlockingWebView2 : WebView2
{
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WM_MOUSEHWHEEL = 0x020E;
    private const int WM_KEYDOWN = 0x0100;
    private const int VK_CONTROL = 0x11;
    private const int VK_LCONTROL = 0xA2;
    private const int VK_RCONTROL = 0xA3;
    private const int VK_OEM_PLUS = 0xBB;   // Ctrl + "=" / "+"
    private const int VK_OEM_MINUS = 0xBD;  // Ctrl + "-" 
    private const int VK_ADD = 0x6B;        // Pavé numérique "+"
    private const int VK_SUBTRACT = 0x6D;   // Pavé numérique "-"
    private const int VK_0 = 0x30;          // Ctrl + "0"

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    private static bool IsCtrlDown()
    {
        // Le bit de poids fort (0x8000) indique que la touche est enfoncée.
        return (GetKeyState(VK_CONTROL) & 0x8000) != 0
            || (GetKeyState(VK_LCONTROL) & 0x8000) != 0
            || (GetKeyState(VK_RCONTROL) & 0x8000) != 0;
    }

    private static bool IsKeyboardZoomKey(int vk) =>
        vk is VK_OEM_PLUS or VK_ADD or VK_OEM_MINUS or VK_SUBTRACT or VK_0;

    protected override void WndProc(ref Message m)
    {
        if (m.Msg is WM_MOUSEWHEEL or WM_MOUSEHWHEEL)
        {
            // Molette seule (sans Ctrl) : on laisse passer → scroll normal.
            // Ctrl + Molette : on avale le message → aucun zoom.
            if (IsCtrlDown())
            {
                m.Result = IntPtr.Zero;
                return;
            }
        }
        else if (m.Msg == WM_KEYDOWN)
        {
            // Défense supplémentaire pour le zoom clavier (Ctrl++ / Ctrl+- / Ctrl+0),
            // sans toucher aux autres raccourcis.
            if (IsCtrlDown() && IsKeyboardZoomKey((int)m.WParam & 0xFFFF))
            {
                m.Result = IntPtr.Zero;
                return;
            }
        }

        base.WndProc(ref m);
    }
}
