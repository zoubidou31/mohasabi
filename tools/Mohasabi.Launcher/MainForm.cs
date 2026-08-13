using System.Drawing;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Mohasabi.Launcher;

/// <summary>
/// Fenêtre principale de Mohasabi : héberge l'application dans un contrôle WebView2
/// (runtime Microsoft Edge WebView2) — aucun navigateur externe n'est utilisé.
/// La fenêtre gère elle-même sa taille, son emplacement et sa persistance (Desktop).
/// </summary>
internal sealed class MainForm : Form
{
    private readonly ZoomBlockingWebView2 _webView = new();
    private readonly Label _loading;
    private readonly string _apiToken;
    private readonly string _windowSettingsPath;
    private readonly ZoomBlocker _zoomBlocker;
    private bool _pendingMaximized;

    /// <summary>Source unique de vérité de l'état de la fenêtre Desktop (géré par le Launcher).</summary>
    private sealed class WindowSettings
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsMaximized { get; set; }
        public string? LastMonitor { get; set; }
    }

    public MainForm(string iconPath, string apiToken)
    {
        _apiToken = apiToken;
        Text = "Mohasabi";
        StartPosition = FormStartPosition.Manual;
        MinimumSize = new Size(920, 580);
        ShowInTaskbar = true;
        ShowIcon = true;
        MaximizeBox = true;
        MinimizeBox = true;
        Icon = LoadIcon(iconPath);

        // Emplacement de persistance de l'état de la fenêtre (hors React / localStorage).
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mohasabi");
        try
        {
            Directory.CreateDirectory(appData);
        }
        catch
        {
            // Non bloquant.
        }

        _windowSettingsPath = Path.Combine(appData, "window.json");

        // Bloque le zoom (Ctrl+Molette / Ctrl+Plus-Moins-0) au niveau système, avant
        // que WebView2 ne le traite. N'exige aucun rétablissement de ZoomFactor → pas de scintillement.
        _zoomBlocker = new ZoomBlocker(() => Handle);

        // Applique la taille/emplacement sauvegardés ou un défaut adaptatif (~90% écran).
        Bounds = ComputeBounds();
        if (_pendingMaximized)
        {
            WindowState = FormWindowState.Maximized;
        }

        FormClosing += (_, _) => SaveWindowSettings();

        _loading = new Label
        {
            Text = "Démarrage de Mohasabi…",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 13f),
            ForeColor = SystemColors.GrayText,
        };
        Controls.Add(_loading);

        _webView.Dock = DockStyle.Fill;
        _webView.Visible = false;
        _webView.CoreWebView2InitializationCompleted += (_, e) =>
        {
            if (e.IsSuccess && _webView.CoreWebView2 != null)
            {
                // Empêche tout zoom utilisateur (Ctrl+Molette, Ctrl+/-, Ctrl+0) : l'UI
                // s'adapte via le CSS responsive, jamais via un zoom artificiel.
                _webView.CoreWebView2.Settings.IsZoomControlEnabled = false;
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.ZoomFactor = 1.0;
                _webView.CoreWebView2.NewWindowRequested += OnNewWindowRequested;
            }
        };
        _webView.NavigationCompleted += (_, e) =>
        {
            if (e.IsSuccess && !_webView.Visible)
            {
                _loading.Visible = false;
                _webView.Visible = true;
            }
        };
        Controls.Add(_webView);
        _webView.BringToFront();
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        // Aucune ouverture vers un navigateur externe : toute fenêtre demandée par
        // l'application reste dans la fenêtre Mohasabi.
        e.Handled = true;
        if (!string.IsNullOrWhiteSpace(e.Uri) && _webView.CoreWebView2 != null)
        {
            try
            {
                _webView.CoreWebView2.Navigate(e.Uri);
            }
            catch
            {
                // Ignoré.
            }
        }
    }

    /// <summary>Calcule la géométrie de démarrage : sauvegardée+valide, dernier écran, ou 90% écran principal.</summary>
    private Rectangle ComputeBounds()
    {
        var saved = LoadWindowSettings();
        if (saved is not null)
        {
            var r = new Rectangle(saved.X, saved.Y, saved.Width, saved.Height);
            if (IsOnAnyScreen(r))
            {
                _pendingMaximized = saved.IsMaximized;
                return r;
            }

            // Hors écran : tente l'écran où l'utilisateur était, sinon l'écran principal.
            var last = string.IsNullOrEmpty(saved.LastMonitor)
                ? null
                : Screen.AllScreens.FirstOrDefault(s => s.DeviceName == saved.LastMonitor);
            if (last is not null)
            {
                _pendingMaximized = saved.IsMaximized;
                return FitToScreen(last);
            }
        }

        _pendingMaximized = false;
        return FitToScreen(Screen.PrimaryScreen ?? Screen.AllScreens[0]);
    }

    /// <summary>Rectangle de ~90% de la zone de travail, centré, sans chevaucher les barres système.</summary>
    private static Rectangle FitToScreen(Screen screen)
    {
        var wa = screen.WorkingArea;
        int w = (int)(wa.Width * 0.9);
        int h = (int)(wa.Height * 0.9);
        int x = wa.X + (wa.Width - w) / 2;
        int y = wa.Y + (wa.Height - h) / 2;
        return new Rectangle(x, y, w, h);
    }

    /// <summary>Vrai si au moins une partie du rectangle est visible sur un écran connecté.</summary>
    private static bool IsOnAnyScreen(Rectangle r)
    {
        foreach (var s in Screen.AllScreens)
        {
            if (Rectangle.Intersect(r, s.WorkingArea) is { Width: > 0, Height: > 0 })
            {
                return true;
            }
        }

        return false;
    }

    private WindowSettings? LoadWindowSettings()
    {
        try
        {
            if (File.Exists(_windowSettingsPath))
            {
                var settings = JsonSerializer.Deserialize<WindowSettings>(File.ReadAllText(_windowSettingsPath));
                if (settings is not null && settings.Width >= 320 && settings.Height >= 240)
                {
                    return settings;
                }
            }
        }
        catch
        {
            // Ignoré : on retombe sur le défaut.
        }

        return null;
    }

    private void SaveWindowSettings()
    {
        WindowSettings settings;
        if (WindowState == FormWindowState.Maximized)
        {
            var rb = RestoreBounds;
            settings = new WindowSettings
            {
                X = rb.X,
                Y = rb.Y,
                Width = rb.Width,
                Height = rb.Height,
                IsMaximized = true,
            };
        }
        else
        {
            settings = new WindowSettings
            {
                X = Bounds.X,
                Y = Bounds.Y,
                Width = Bounds.Width,
                Height = Bounds.Height,
                IsMaximized = false,
            };
        }

        try
        {
            settings.LastMonitor = Screen.FromHandle(Handle).DeviceName;
        }
        catch
        {
            // Non bloquant.
        }

        try
        {
            File.WriteAllText(
                _windowSettingsPath,
                JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Non bloquant.
        }
    }

    private static Icon LoadIcon(string iconPath)
    {
        try
        {
            if (File.Exists(iconPath))
            {
                using var fs = File.OpenRead(iconPath);
                return new Icon(fs);
            }
        }
        catch
        {
            // Ignoré : icône système par défaut.
        }

        return SystemIcons.Application;
    }

    /// <summary>Initialise WebView2 puis charge l'application locale. À appeler sur le thread UI.</summary>
    public async void InitializeWebViewAsync(string userDataFolder, string url)
    {
        try
        {
            var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);
            await _webView.EnsureCoreWebView2Async(environment);

            if (_webView.CoreWebView2 != null)
            {
                // Expose le jeton d'authentification de l'API locale au code front-end,
                // qui l'ajoute à chaque requête (en-tête Authorization). Le jeton reste
                // valable pour toutes les navigations futures de la fenêtre.
                await _webView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(
                    $"window.__MOHASABI_API_TOKEN__ = {JsonSerializer.Serialize(_apiToken)};");
                _webView.CoreWebView2.Navigate(url);
            }
        }
        catch (Exception ex)
        {
            _loading.Text = "Impossible de démarrer l'interface Mohasabi." + Environment.NewLine +
                            Environment.NewLine +
                            ex.Message +
                            Environment.NewLine +
                            Environment.NewLine +
                            "Vérifiez que le Microsoft Edge WebView2 Runtime est installé, " +
                            "puis relancez Mohasabi.";
        }
    }

    public void Navigate(string url)
    {
        if (_webView.CoreWebView2 != null)
        {
            try
            {
                _webView.CoreWebView2.Navigate(url);
            }
            catch
            {
                // Ignoré.
            }
        }
    }

    public void ShowServerError(string message)
    {
        _loading.Text = message;
    }

    public void DisposeWebView()
    {
        try
        {
            _webView.Dispose();
        }
        catch
        {
            // Ignoré.
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _zoomBlocker?.Dispose();
        }

        base.Dispose(disposing);
    }
}
