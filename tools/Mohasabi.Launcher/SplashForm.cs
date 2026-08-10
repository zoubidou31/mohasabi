using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Windows.Forms;

namespace Mohasabi.Launcher;

/// <summary>
/// Écran de démarrage de Mohasabi — identité visuelle abstraite « l'information
/// métier qui s'ordonne » : un jeu de modules en douceur s'aligne sur une ligne
/// de structure pendant que l'identité (logo, nom, sous-titre, version) se
/// révèle. Un indicateur de chargement discret clôt la séquence. Aucun montant
/// fictif, aucune barre de progression chiffrée, aucun texte saisi lettre à
/// lettre : uniquement des fondus, des échelles et des révélations posés.
/// La fenêtre s'ouvre en fondu, reste visible le temps du démarrage de l'API
/// locale (minimum ~1,75 s), puis se referme en fondu vers l'application.
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
    private readonly Font _versionFont;

    private readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 16 };
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    private Task<bool>? _startupTask;
    private bool _fading;
    private float _fadeStartMs;

    private static readonly int[] TileHeights = { 20, 26, 30, 26, 20 };
    private const float TileWidth = 34f;
    private const float TileGap = 10f;
    private static readonly int[] TileOrder = { 2, 1, 3, 0, 4 };

    private const float LogoStartMs = 80f;
    private const float NameStartMs = 180f;
    private const float SubtitleStartMs = 260f;
    private const float VersionStartMs = 800f;
    private const float MotifStartMs = 200f;
    private const float MotifStaggerMs = 110f;
    private const float LoadingStartMs = 500f;
    private const float LoadingDurationMs = 1150f;

    private readonly float _motifTotalWidth;

    /// <summary>True si le démarrage de l'API a réussi (déterminé en arrière-plan).</summary>
    public bool StartupSucceeded { get; private set; }

    public SplashForm(string version, string logoPath, Func<bool> startup)
    {
        _version = version;
        _startup = startup;
        _motifTotalWidth = TileWidth * TileHeights.Length + TileGap * (TileHeights.Length - 1);

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

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        ClientSize = new Size(S(520), S(350));
        DoubleBuffered = true;
        BackColor = Color.White;
        Region = new Region(RoundedRect(new Rectangle(0, 0, Width, Height), S(22)));

        _nameFont = NewFont(30f, FontStyle.Bold);
        _subtitleFont = NewFont(14f, FontStyle.Regular);
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
        DrawMotif(g, w, t);
        DrawLogoAndIdentity(g, w, t);
        DrawVersion(g, w, t);
        DrawLoading(g, w, t);
    }

    /// <summary>Logo puis identité : logo en fondu + échelle, nom révélé en douceur,
    /// sous-titre en fondu. Ordre fixe, jamais de saisie lettre à lettre.</summary>
    private void DrawLogoAndIdentity(Graphics g, int w, float t)
    {
        var fade = EaseOut(Math.Clamp((t - LogoStartMs) / 300f, 0f, 1f));
        var scale = 0.92f + 0.08f * EaseOut(Math.Clamp((t - LogoStartMs) / 340f, 0f, 1f));

        using var nameFormat = new StringFormat { Alignment = StringAlignment.Center };

        if (_logo is not null)
        {
            var size = (int)(S(88f) * scale);
            var alpha = (int)(255f * fade);
            var attributes = new ImageAttributes();
            var matrix = new ColorMatrix { Matrix33 = fade };
            attributes.SetColorMatrix(matrix);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(
                _logo,
                new Rectangle((w - size) / 2, S(28), size, size),
                0, 0, _logo.Width, _logo.Height,
                GraphicsUnit.Pixel,
                attributes);
            attributes.Dispose();
        }

        // Nom : fondu discret, léger zoom vertical et retour en place (pas de frappe).
        var nameP = EaseOut(Math.Clamp((t - NameStartMs) / 240f, 0f, 1f));
        var nameScale = 0.985f + 0.015f * nameP;
        var nameDy = (1f - nameP) * S(4f);
        var nameRect = new RectangleF(
            0,
            S(122f) + nameDy,
            w,
            S(36f) * nameScale);
        using (var nameBrush = new SolidBrush(Color.FromArgb((int)(255f * nameP), 15, 90, 55)))
        {
            g.DrawString("Mohasabi", _nameFont, nameBrush, nameRect, nameFormat);
        }

        var subAlpha = (int)(255f * EaseOut(Math.Clamp((t - SubtitleStartMs) / 220f, 0f, 1f)));
        using (var subtitleBrush = new SolidBrush(Color.FromArgb(subAlpha, 107, 114, 128)))
        {
            g.DrawString("Assistant comptable", _subtitleFont, subtitleBrush, new RectangleF(0, S(160f), w, S(20f)), nameFormat);
        }
    }

    /// <summary>Module discret : cinq éléments doux (l'écosystème de Mohasabi) qui
    /// s'alignent symétriquement autour du centre, puis une ligne de structure se
    /// dessine sous eux — l'information métier qui s'ordonne.</summary>
    private void DrawMotif(Graphics g, int w, float t)
    {
        var cx = w / 2f;
        var x0 = cx - S(_motifTotalWidth / 2f);
        var baselineY = S(230f);

        for (var i = 0; i < TileHeights.Length; i++)
        {
            var order = TileOrder[i];
            var start = MotifStartMs + order * MotifStaggerMs;
            var p = EaseOut(Math.Clamp((t - start) / 260f, 0f, 1f));
            if (p <= 0f)
            {
                continue;
            }

            var scale = 0.88f + 0.12f * p;
            var tileW = S(TileWidth) * scale;
            var tileH = S(TileHeights[i]) * scale;
            var x = x0 + S(TileWidth) * i + (S(TileWidth) - tileW) / 2f;
            var y = baselineY - tileH;

            using var brush = new SolidBrush(Color.FromArgb((int)(40f * p), 21, 115, 71));
            FillRoundedRect(g, brush, x, y, tileW, tileH, S(4f));
        }

        // Ligne de structure : se déploie depuis le centre après l'alignement des modules.
        var lineStart = MotifStartMs + 4f * MotifStaggerMs + 200f;
        var lineP = EaseOut(Math.Clamp((t - lineStart) / 320f, 0f, 1f));
        if (lineP > 0f)
        {
            var half = S(_motifTotalWidth / 2f) * lineP;
            using var pen = new Pen(Color.FromArgb((int)(26f * lineP), 21, 115, 71), S(1.4f))
            {
                StartCap = LineCap.Round,
                EndCap = LineCap.Round,
            };
            g.DrawLine(pen, cx - half, baselineY, cx + half, baselineY);
        }
    }

    /// <summary>Version affichée exactement comme publiée (ex. « Version 1.0.1 »).</summary>
    private void DrawVersion(Graphics g, int w, float t)
    {
        var alpha = (int)(255f * EaseOut(Math.Clamp((t - VersionStartMs) / 240f, 0f, 1f)));
        if (alpha <= 0)
        {
            return;
        }

        using var format = new StringFormat { Alignment = StringAlignment.Center };
        using var brush = new SolidBrush(Color.FromArgb(alpha, 156, 163, 175));
        g.DrawString($"Version {_version}", _versionFont, brush, new RectangleF(0, S(244f), w, S(18f)), format);
    }

    /// <summary>Indicateur de chargement discret : piste douce remplie en fondu
    /// progressif, point lumineux en tête. Aucune barre de progression chiffrée.</summary>
    private void DrawLoading(Graphics g, int w, float t)
    {
        var trackW = S(190f);
        var trackH = S(3f);
        var trackY = S(310f);
        var x = w / 2f - trackW / 2f;

        using (var trackBrush = new SolidBrush(Color.FromArgb(48, 231, 235, 233)))
        {
            FillRoundedRect(g, trackBrush, x, trackY, trackW, trackH, trackH / 2f);
        }

        var p = EaseOut(Math.Clamp((t - LoadingStartMs) / LoadingDurationMs, 0f, 1f));
        if (p <= 0f)
        {
            return;
        }

        var fillW = Math.Max(trackH, trackW * p);
        using (var fillBrush = new SolidBrush(Color.FromArgb(235, 21, 115, 71)))
        {
            FillRoundedRect(g, fillBrush, x, trackY, fillW, trackH, trackH / 2f);
        }

        // Pointe discrète en tête du remplissage.
        var tipX = x + fillW;
        var tipR = S(3.4f);
        using (var tipBrush = new SolidBrush(Color.FromArgb(110, 21, 115, 71)))
        {
            g.FillEllipse(tipBrush, tipX - tipR, trackY + trackH / 2f - tipR, tipR * 2f, tipR * 2f);
        }
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

    private static void FillRoundedRect(Graphics g, Brush brush, float x, float y, float width, float height, float radius)
    {
        var d = radius * 2f;
        using var path = new GraphicsPath();
        path.AddArc(x, y, d, d, 180, 90);
        path.AddArc(x + width - d, y, d, d, 270, 90);
        path.AddArc(x + width - d, y + height - d, d, d, 0, 90);
        path.AddArc(x, y + height - d, d, d, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }

    private Font NewFont(float size, FontStyle style)
        => new("Segoe UI", size * _dpi, style, GraphicsUnit.Pixel);

    private int S(int v) => (int)MathF.Round(v * _dpi);

    private float S(float v) => v * _dpi;

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
            _versionFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
