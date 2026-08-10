using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows.Forms;

namespace Mohasabi.Launcher;

/// <summary>
/// Lanceur de Mohasabi : démarre l'API locale, puis affiche l'application dans une
/// fenêtre native Windows hébergeant WebView2. Aucun navigateur externe n'est ouvert.
/// </summary>
internal static class Program
{
    private const string AppUserModelId = "Mohasabi";
    private const string MutexName = "MohasabiInstanceMutex";
    private const string UpdatePendingFile = "update-pending";
    private const int MaxCrashes = 3;

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private static MainForm? _form;
    private static System.Windows.Forms.Timer? _watchTimer;
    private static Process? _api;
    private static int _port;
    private static string _apiToken = "";
    private static string _dataDir = "";
    private static string _markerPath = "";
    private static string _cleanExitMarkerPath = "";
    private static string _apiExe = "";
    private static string _manifestUrl = "";
    private static int _crashCount;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(
        [MarshalAs(UnmanagedType.LPWStr)] string appID);

    [STAThread]
    private static int Main(string[] args)
    {
        using var mutex = new Mutex(true, MutexName, out var createdNew);
        if (!createdNew)
        {
            return 0;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        // Apparition autonome dans la barre des tâches (icône Mohasabi, groupe dédié).
        try
        {
            SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
        }
        catch
        {
            // Non bloquant.
        }

        var exeDir = AppContext.BaseDirectory;
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mohasabi");
        _dataDir = Path.Combine(appData, "data");
        _markerPath = Path.Combine(appData, UpdatePendingFile);
        _cleanExitMarkerPath = Path.Combine(appData, "clean-exit.marker");
        _manifestUrl = ReadManifestUrl(Path.Combine(exeDir, "launcher.json"));

        // Nettoie un marqueur de mise à jour restant (ex. fermeture forcée précédente).
        if (File.Exists(_markerPath))
        {
            try
            {
                File.Delete(_markerPath);
            }
            catch
            {
                // Ignoré.
            }
        }

        _apiExe = Path.Combine(exeDir, "app", "Mohasabi.Api.exe");
        if (!File.Exists(_apiExe))
        {
            MessageBox.Show(
                $"Le serveur de Mohasabi est introuvable :\n{_apiExe}",
                "Mohasabi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        Directory.CreateDirectory(_dataDir);
        Directory.CreateDirectory(Path.Combine(_dataDir, "uploads"));
        Directory.CreateDirectory(Path.Combine(appData, "logs"));
        Directory.CreateDirectory(Path.Combine(appData, "webview2"));

        // Jeton d'authentification éphémère : régénéré à chaque session, il protège
        // l'API locale contre toute requête provenant d'un autre processus local ou
        // d'une page web (protection CSRF / appels externes).
        _apiToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        _form = new MainForm(Path.Combine(exeDir, "mohasabi.ico"), _apiToken);
        _form.FormClosing += (_, _) => StopApi();

        _port = ChoosePort();

        // Écran de démarrage (activable) : animé pendant que l'API locale démarre en
        // arrière-plan ; se referme en fondu quand l'API est prête (min ~1,5 s).
        var splashEnabled = IsSplashEnabled(appData);
        bool apiReady;
        if (splashEnabled)
        {
            var versionText = GetVersionText();
            var splash = new SplashForm(versionText, Path.Combine(exeDir, "mohasabi.png"), () => StartApi(_port));
            Application.Run(splash);
            apiReady = splash.StartupSucceeded;
            splash.Dispose();
        }
        else
        {
            apiReady = StartApi(_port);
        }

        if (!apiReady)
        {
            MessageBox.Show(
                "Impossible de démarrer le serveur Mohasabi.",
                "Mohasabi",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return 1;
        }

        _watchTimer = new System.Windows.Forms.Timer { Interval = 2000 };
        _watchTimer.Tick += (_, _) => WatchApi();

        _form.Shown += (_, _) =>
        {
            _watchTimer?.Start();
            _form.InitializeWebViewAsync(
                Path.Combine(appData, "webview2"),
                $"http://127.0.0.1:{_port}/");
        };

        Application.Run(_form);

        // Le formulaire est fermé : nettoyage final.
        StopApi();
        _form?.DisposeWebView();
        MarkCleanExit();
        return 0;
    }

    /// <summary>Lit la préférence « écran de démarrage » (activé par défaut).</summary>
    private static bool IsSplashEnabled(string appData)
    {
        var settingsPath = Path.Combine(appData, "settings.json");
        if (!File.Exists(settingsPath))
        {
            return true;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
            var splash = FindProperty(doc.RootElement, "splashEnabled");
            return !splash.HasValue || splash.Value.GetBoolean();
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Recherche une propriété JSON sans tenir compte de la casse.</summary>
    private static JsonElement? FindProperty(JsonElement element, string name)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals(name))
            {
                return property.Value;
            }
        }
        return null;
    }

    /// <summary>Marque un arrêt propre : permet à l'API de détecter une interruption à la prochaine session.</summary>
    private static void MarkCleanExit()
    {
        try
        {
            var dir = Path.GetDirectoryName(_cleanExitMarkerPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.WriteAllText(_cleanExitMarkerPath, DateTime.UtcNow.ToString("O"));
        }
        catch
        {
            // Non bloquant.
        }
    }
    private static string GetVersionText()
    {
        var assembly = Assembly.GetEntryAssembly();
        var info = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrEmpty(info))
        {
            var plus = info.IndexOf('+');
            return plus > 0 ? info[..plus] : info;
        }

        return assembly?.GetName().Version?.ToString(3) ?? "1.0.1";
    }

    private static string ReadManifestUrl(string configPath)
    {
        if (!File.Exists(configPath))
        {
            return "";
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(configPath));
            var manifest = FindProperty(doc.RootElement, "manifestUrl");
            return manifest.HasValue ? manifest.Value.GetString() ?? "" : "";
        }
        catch
        {
            return "";
        }
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static bool IsPortInUse(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return false;
        }
        catch
        {
            return true;
        }
    }

    private static int ChoosePort()
    {
        // Port par défaut stable si disponible, sinon un port libre.
        return IsPortInUse(5299) ? GetFreePort() : 5299;
    }

    private static bool StartApi(int port)
    {
        _port = port;

        var psi = new ProcessStartInfo
        {
            FileName = _apiExe,
            WorkingDirectory = Path.GetDirectoryName(_apiExe) ?? AppContext.BaseDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        psi.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        psi.Environment["API_TOKEN"] = _apiToken;
        psi.Environment["ConnectionStrings__DefaultConnection"] = $"Data Source={Path.Combine(_dataDir, "mohasabi.db")}";
        psi.Environment["Storage__UploadsPath"] = Path.Combine(_dataDir, "uploads");
        psi.Environment["Serilog__File__Path"] = Path.Combine(
            Path.GetDirectoryName(_markerPath) ?? _dataDir, "logs", "mohasabi-.log");
        if (!string.IsNullOrEmpty(_manifestUrl))
        {
            psi.Environment["Update__ManifestUrl"] = _manifestUrl;
        }

        try
        {
            _api = Process.Start(psi);
        }
        catch (Exception ex)
        {
            _form?.ShowServerError($"Impossible de démarrer le serveur : {ex.Message}");
            return false;
        }

        if (_api is null)
        {
            return false;
        }

        // Attend que l'API réponde.
        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            if (_api.HasExited)
            {
                _crashCount++;
                return false;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/api/version");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiToken);
                using var response = Http.Send(request);
                if (response.IsSuccessStatusCode)
                {
                    _crashCount = 0;
                    return true;
                }
            }
            catch
            {
                // Pas encore prêt.
            }

            System.Threading.Thread.Sleep(500);
        }

        return true;
    }

    private static void WatchApi()
    {
        if (_api == null || !_api.HasExited)
        {
            return;
        }

        _api = null;

        // Mise à jour : le marqueur a été posé par l'API ; on ferme la fenêtre,
        // l'installateur (déjà lancé) prend le relais et relance Mohasabi.
        if (File.Exists(_markerPath))
        {
            try
            {
                File.Delete(_markerPath);
            }
            catch
            {
                // Ignoré.
            }

            _form?.Close();
            return;
        }

        _crashCount++;
        if (_crashCount > MaxCrashes)
        {
            var retry = MessageBox.Show(
                "Le serveur Mohasabi s'est arrêté de façon répétée.\n\nVoulez-vous le redémarrer ?",
                "Mohasabi",
                MessageBoxButtons.RetryCancel,
                MessageBoxIcon.Warning);
            if (retry == DialogResult.Retry)
            {
                _crashCount = 0;
                _port = ChoosePort();
                if (StartApi(_port))
                {
                    _form?.Navigate($"http://127.0.0.1:{_port}/");
                }
            }
            else
            {
                _form?.Close();
            }

            return;
        }

        _port = ChoosePort();
        if (StartApi(_port))
        {
            _form?.Navigate($"http://127.0.0.1:{_port}/");
        }
    }

    private static void StopApi()
    {
        if (_api != null && !_api.HasExited)
        {
            try
            {
                _api.Kill();
                _api.WaitForExit(3000);
            }
            catch
            {
                // Ignoré.
            }
        }

        _api = null;
    }
}
