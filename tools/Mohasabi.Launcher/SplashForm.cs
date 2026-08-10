using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Mohasabi.Launcher;

/// <summary>
/// Écran de démarrage de Mohasabi — « Moteur comptable » : logo en fondu,
/// réseau technique de modules convergeant vers le cœur du système, puis
/// séquence d'états d'initialisation et tagline finale. Aucun montant fictif,
/// aucune barre de progression. La fenêtre s'ouvre en fondu, reste visible
/// le temps du démarrage de l'API locale (minimum ~1,8 s), puis se referme.
/// </summary>
internal sealed class SplashForm : Form
{
    private const float FadeInMs = 300f;
    private const float MinShowMs = 1750f;
    private const float FadeOutMs = 280f;

    private readonly Func<bool> _startup;
    private readonly Image? _logo;
    private readonly string _version;
    private readonly float _dpi;

    private readonly Font _nameFont;
    private readonly Font _subtitleFont;
    private readonly Font _stateFont;
    private readonly Font _taglineFont;
    private readonly Font _nodeFont;
    private readonly Font _versionFont;

    private readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 16 };
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    private Task<bool>? _startupTask;
    private bool _fading;
    private float _fadeStartMs;

    private readonly Node[] _nodes;
    private readonly PointF _hub;
    private readonly int _baseWidth = 520;
    private readonly int _baseHeight = 350;

    private static readonly float[] StateStarts =
    {
        500f, 670f, 840f, 1010f, 1180f, 1350f, 1520f,
    };

    private static readonly string[] States =
    {
        "Initialisation du moteur comptable",
        "Vérification de la base de données",
        "Chargement du moteur de facturation",
        "Configuration de la TVA",
        "Vérification de l'intégrité",
        "Sécurisation des données locales",
        "Système prêt",
    };

    private const float TaglineStartMs = 1580f;
    private const string Tagline = "Comptabilité • Précision • Sécurité";

    /// <summary>True si le démarrage de l'API a réussi (déterminé en arrière-plan).</summary>
    public bool StartupSucceeded { get; private set; }

    private sealed record Node(string Label, PointF Pos, float StartMs);

    public SplashForm(string version, string logoPath, Func<bool> startup)
    {
        _version = version;
        _startup = startup;

        try
        {
            if (File.Exists(logoPath))
            {
                _logo = Image.FromFile(logoPath);
            }
        }
        catch
        {
            _logo = null;
        }

        using (var g = CreateGraphics())
        {
            _dpi = Math.Max(1f, g.DpiX / 96f);
        }

        _hub = new PointF(S(260f), S(214f));
        _nodes = new[]
        {
            new Node("Clients", new PointF(S(100f), S(186f)), 380f),
            new Node("Factures", new PointF(S(180f), S(176f)), 470f),
            new Node("TVA", new PointF(S(260f), S(172f)), 560f),
            new Node("Paiements", new PointF(S(340f), S(176f)), 650f),
            new Node("Rapports", new PointF(S(420f), S(186f)), 740f),
        };

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        ClientSize = new Size(S(_baseWidth), S(_baseHeight));
        DoubleBuffered = true;
        BackColor = Color.White;
        Region = new Region(RoundedRect(new Rectangle(0, 0, Width, Height), S(22)));

        _nameFont = NewFont(30f, FontStyle.Bold);
        _subtitleFont = NewFont(14f, FontStyle.Regular);
        _stateFont = NewFont(13f, FontStyle.Regular);
        _taglineFont = NewFont(12f, FontStyle.Regular);
        _nodeFont = NewFont(9.5f, FontStyle.Regular);
        _versionFont = NewFont(11f, FontStyle.Regular);

        _animTimer.Tick += (_, _) => OnTick();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        _startupTask = Task.Run(() =>
        {
            try
            {
                return _startup();
            }
            catch
            {
                return false;
            }
        });
        _animTimer.Start();
    }

    private void OnTick()
    {
        var t = (float)_clock.ElapsedMilliseconds;

        if (_startupTask is not null && _startupTask.IsCompleted)
        {
            StartupSucceeded = _startupTask.Result;
        }

        if (_startupTask is not null && _startupTask.IsCompleted && t >= MinShowMs)
        {
            if (!_fading)
            {
                _fading = true;
                _fadeStartMs = t;
            }

            var fadeElapsed = t - _fadeStartMs;
            Opacity = Math.Max(0f, 1f - fadeElapsed / FadeOutMs);
            if (fadeElapsed >= FadeOutMs)
            {
                _animTimer.Stop();
                Close();
                return;
            }
        }
        else
        {
            Opacity = Math.Min(1f, t / FadeInMs);
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var w = ClientSize.Width;
        var h = ClientSize.Height;
        var t = (float)_clock.ElapsedMilliseconds;

        DrawBackground(g, w, h);
        DrawDecorativeGlow(g, w);
        DrawLogoAndIdentity(g, w, t);
        DrawNetwork(g, w, t);
        DrawStatus(g, w, t);
        DrawTagline(g, w, t);
        DrawVersion(g, w);
    }

    private void DrawLogoAndIdentity(Graphics g, int w, float t)
    {
        var fade = EaseOut(Math.Clamp((t - 60f) / 300f, 0f, 1f));
        var scale = 0.92f + 0.08f * EaseOut(Math.Clamp((t - 60f) / 340f, 0f, 1f));

        using var nameFormat = new StringFormat { Alignment = StringAlignment.Center };

        if (_logo is not null)
        {
            var size = (int)(S(92f) * scale);
            var alpha = (int)(255f * fade);
            var attributes = new ImageAttributes();
            var matrix = new ColorMatrix { Matrix33 = fade };
            attributes.SetColorMatrix(matrix);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(
                _logo,
                new Rectangle((w - size) / 2, S(32), size, size),
                0, 0, _logo.Width, _logo.Height,
                GraphicsUnit.Pixel,
                attributes);
            attributes.Dispose();
        }

        // Nom + sous-titre (fondu progressif).
        var nameAlpha = (int)(255f * EaseOut(Math.Clamp((t - 140f) / 220f, 0f, 1f)));
        using var nameBrush = new SolidBrush(Color.FromArgb(nameAlpha, 15, 90, 55));
        g.DrawString("Mohasabi", _nameFont, nameBrush, new RectangleF(0, S(128f), w, S(36f)), nameFormat);

        var subAlpha = (int)(255f * EaseOut(Math.Clamp((t - 220f) / 220f, 0f, 1f)));
        using var subtitleBrush = new SolidBrush(Color.FromArgb(subAlpha, 107, 114, 128));
        g.DrawString("Assistant comptable", _subtitleFont, subtitleBrush, new RectangleF(0, S(166f), w, S(20f)), nameFormat);
    }

    private void DrawNetwork(Graphics g, int w, float t)
    {
        var cx = w / 2f;

        // Lignes + paquets voyageant vers le cœur du système.
        foreach (var node in _nodes)
        {
            var p = EaseOut(Math.Clamp((t - node.StartMs) / 350f, 0f, 1f));
            if (p <= 0f)
            {
                continue;
            }

            var end = Lerp(node.Pos, _hub, p);
            using (var linePen = new Pen(Color.FromArgb((int)(200f * p), 21, 115, 71), S(1.4f)))
            {
                linePen.StartCap = LineCap.Round;
                linePen.EndCap = LineCap.Round;
                g.DrawLine(linePen, node.Pos, end);
            }

            // Paquet lumineux qui converge vers le hub.
            var packet = Lerp(node.Pos, _hub, p);
            using (var packetBrush = new SolidBrush(Color.FromArgb((int)(235f * p), 15, 90, 55)))
            {
                g.FillEllipse(packetBrush, packet.X - S(2.5f), packet.Y - S(2.5f), S(5f), S(5f));
            }
        }

        // Pastilles + libellés des modules.
        foreach (var node in _nodes)
        {
            var nodeAlpha = Math.Clamp((t - node.StartMs - 220f) / 180f, 0f, 1f);
            if (nodeAlpha <= 0f)
            {
                continue;
            }

            using var dotBrush = new SolidBrush(Color.FromArgb((int)(225f * nodeAlpha), 21, 115, 71));
            g.FillEllipse(dotBrush, node.Pos.X - S(3.5f), node.Pos.Y - S(3.5f), S(7f), S(7f));

            using var labelFormat = new StringFormat { Alignment = StringAlignment.Center };
            using var labelBrush = new SolidBrush(Color.FromArgb((int)(225f * nodeAlpha), 100, 116, 106));
            g.DrawString(node.Label, _nodeFont, labelBrush, new RectangleF(node.Pos.X - S(44f), node.Pos.Y + S(9f), S(88f), S(14f)), labelFormat);
        }

        // Cœur du système : anneaux concentriques discrets + pulsation douce.
        var pulse = t > 1250f ? MathF.Sin(t / 320f) : 0f;
        using (var ring1 = new Pen(Color.FromArgb((int)(55f + 25f * Math.Max(0f, pulse)), 21, 115, 71), S(1.1f)))
        {
            g.DrawEllipse(ring1, cx - S(11f), _hub.Y - S(11f), S(22f), S(22f));
        }

        using (var ring2 = new Pen(Color.FromArgb((int)(28f + 18f * Math.Max(0f, pulse)), 21, 115, 71), S(0.9f)))
        {
            g.DrawEllipse(ring2, cx - S(17f), _hub.Y - S(17f), S(34f), S(34f));
        }

        using var coreBrush = new SolidBrush(Color.FromArgb(235, 15, 90, 55));
        g.FillEllipse(coreBrush, _hub.X - S(4.5f), _hub.Y - S(4.5f), S(9f), S(9f));
    }

    private void DrawStatus(Graphics g, int w, float t)
    {
        var state = -1;
        for (var i = 0; i < States.Length; i++)
        {
            if (t >= StateStarts[i])
            {
                state = i;
            }
        }

        if (state < 0)
        {
            return;
        }

        var alpha = Math.Clamp((t - StateStarts[state]) / 110f, 0f, 1f);
        if (state < States.Length - 1)
        {
            var nextStart = StateStarts[state + 1];
            var outAlpha = Math.Clamp((t - (nextStart - 110f)) / 110f, 0f, 1f);
            alpha *= 1f - outAlpha;
        }

        var isReady = state == States.Length - 1;
        var statusColor = isReady
            ? Color.FromArgb((int)(255f * alpha), 15, 90, 55)
            : Color.FromArgb((int)(255f * alpha), 51, 65, 58);

        using var stateFormat = new StringFormat { Alignment = StringAlignment.Center };
        using var stateBrush = new SolidBrush(statusColor);
        var textRect = new RectangleF(0, S(252f), w, S(20f));

        // Mesure du texte pour aligner l'indicateur juste à gauche de l'état.
        var textSize = g.MeasureString(States[state], _stateFont);
        var indicatorX = w / 2f - textSize.Width / 2f - S(16f);

        var dotPulse = isReady ? 1f : 0.55f + 0.45f * MathF.Sin(t / 160f);
        using (var dotBrush = new SolidBrush(Color.FromArgb((int)(200f * alpha * dotPulse), 21, 115, 71)))
        {
            g.FillEllipse(dotBrush, indicatorX - S(5f), S(258f) + S(4f) - S(5f), S(10f), S(10f));
        }

        g.DrawString(States[state], _stateFont, stateBrush, textRect, stateFormat);
    }

    private void DrawTagline(Graphics g, int w, float t)
    {
        var alpha = EaseOut(Math.Clamp((t - TaglineStartMs) / 260f, 0f, 1f));
        if (alpha <= 0f)
        {
            return;
        }

        using var format = new StringFormat { Alignment = StringAlignment.Center };
        using var brush = new SolidBrush(Color.FromArgb((int)(255f * alpha), 21, 115, 71));
        g.DrawString(Tagline, _taglineFont, brush, new RectangleF(0, S(278f), w, S(18f)), format);
    }

    private void DrawVersion(Graphics g, int w)
    {
        using var format = new StringFormat { Alignment = StringAlignment.Center };
        using var brush = new SolidBrush(Color.FromArgb(156, 163, 175));
        g.DrawString($"Version {_version}", _versionFont, brush, new RectangleF(0, S(316f), w, S(16f)), format);
    }

    private void DrawBackground(Graphics g, int w, int h)
    {
        using var brush = new LinearGradientBrush(
            new Rectangle(0, 0, w, h),
            Color.White,
            Color.FromArgb(243, 249, 246),
            LinearGradientMode.Vertical);
        g.FillRectangle(brush, 0, 0, w, h);

        // Fine barre d'accent verte en haut.
        using var accent = new SolidBrush(Color.FromArgb(21, 115, 71));
        g.FillRectangle(accent, 0, 0, w, S(5));
    }

    private void DrawDecorativeGlow(Graphics g, int w)
    {
        var cx = w / 2f;
        using var glow = new GraphicsPath();
        glow.AddEllipse(cx - S(70), S(36), S(140), S(140));
        using var brush = new PathGradientBrush(glow)
        {
            CenterColor = Color.FromArgb(26, 21, 115, 71),
            SurroundColors = new[] { Color.FromArgb(0, 21, 115, 71) },
        };
        g.FillPath(brush, glow);
    }

    private Font NewFont(float size, FontStyle style)
        => new("Segoe UI", size * _dpi, style, GraphicsUnit.Pixel);

    private int S(int v) => (int)MathF.Round(v * _dpi);

    private float S(float v) => v * _dpi;

    private static PointF Lerp(PointF a, PointF b, float p)
        => new(a.X + (b.X - a.X) * p, a.Y + (b.Y - a.Y) * p);

    private static float EaseOut(float p)
        => 1f - MathF.Pow(1f - p, 3f);

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        var d = radius * 2;
        path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animTimer.Dispose();
            _logo?.Dispose();
            _nameFont.Dispose();
            _subtitleFont.Dispose();
            _stateFont.Dispose();
            _taglineFont.Dispose();
            _nodeFont.Dispose();
            _versionFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
