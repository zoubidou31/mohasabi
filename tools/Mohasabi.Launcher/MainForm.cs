using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Mohasabi.Launcher;

/// <summary>
/// Fenêtre principale de Mohasabi : héberge l'application dans un contrôle WebView2
/// (runtime Microsoft Edge WebView2) — aucun navigateur externe n'est utilisé.
/// </summary>
internal sealed class MainForm : Form
{
    private readonly WebView2 _webView = new();
    private readonly Label _loading;

    public MainForm(string iconPath)
    {
        Text = "Mohasabi";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1280, 820);
        MinimumSize = new Size(960, 600);
        ShowInTaskbar = true;
        ShowIcon = true;
        Icon = LoadIcon(iconPath);

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
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
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
            _webView.CoreWebView2?.Navigate(url);
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
}
