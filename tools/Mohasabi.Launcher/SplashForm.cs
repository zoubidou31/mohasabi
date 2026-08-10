using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Windows.Forms;

namespace Mohasabi.Launcher;

/// <summary>
/// Écran de démarrage de Mohasabi : logo, nom, sous-titre, version et une
/// animation « compteur + ligne de reçu » liée à la facturation — aucune barre
/// de progression. La fenêtre s'ouvre en fondu, reste visible le temps du
/// démarrage de l'API locale (minimum ~1,5 s), puis se referme en fondu.
/// </summary>
internal sealed class SplashForm : Form
{
    private const float FadeInMs = 260f;
    private const float MinShowMs = 1500f;
    private const float FadeOutMs = 280f;

    private readonly Func<bool> _startup;
    private readonly Image? _logo;
    private readonly string _version;
    private readonly Font _nameFont;
    private readonly Font _subtitleFont;
    private readonly Font _amountFont;
    private readonly Font _versionFont;
    private readonly System.Windows.Forms.Timer _animTimer = new() { Interval = 16 };
    private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

    private Task<bool>? _startupTask;
    private bool _fading;
    private float _fadeStartMs;
    private float _progress;
    private string _amount = "0,00 DA";

    /// <summary>True si le démarrage de l'API a réussi (déterminé en arrière-plan).</summary>
    public bool StartupSucceeded { get; private set; }

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

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        ShowInTaskbar = false;
        ClientSize = new Size(500, 330);
        DoubleBuffered = true;
        BackColor = Color.White;
        Region = new Region(RoundedRect(new Rectangle(0, 0, Width, Height), 20));

        _nameFont = new Font("Segoe UI", 32f, FontStyle.Bold, GraphicsUnit.Pixel);
        _subtitleFont = new Font("Segoe UI", 15f, FontStyle.Regular, GraphicsUnit.Pixel);
        _amountFont = new Font("Segoe UI", 25f, FontStyle.Bold, GraphicsUnit.Pixel);
        _versionFont = new Font("Segoe UI", 11f, FontStyle.Regular, GraphicsUnit.Pixel);

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
        var elapsed = (float)_clock.ElapsedMilliseconds;

        if (_startupTask is not null && _startupTask.IsCompleted)
        {
            StartupSucceeded = _startupTask.Result;
        }

        if (_startupTask is not null && _startupTask.IsCompleted && elapsed >= MinShowMs)
        {
            if (!_fading)
            {
                _fading = true;
                _fadeStartMs = elapsed;
            }

            var fadeElapsed = elapsed - _fadeStartMs;
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
            Opacity = Math.Min(1f, elapsed / FadeInMs);
        }

        _progress = Math.Clamp((elapsed - 200f) / 1100f, 0f, 1f);
        UpdateAmount();
        Invalidate();
    }

    private void UpdateAmount()
    {
        var p = Math.Clamp((_progress - 0.18f) / 0.72f, 0f, 1f);
        p = 1f - MathF.Pow(1f - p, 3f); // ease-out
        var value = 1234567.89 * p;
        _amount = value.ToString("N2", CultureInfo.GetCultureInfo("fr-FR")) + " DA";
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        var w = ClientSize.Width;
        var h = ClientSize.Height;

        DrawBackground(g, w, h);
        DrawDecorativeGlow(g, w);

        // Logo : apparition + léger scale-in.
        var scale = 0.92f + 0.08f * EaseOut(Math.Clamp(_progress / 0.3f, 0f, 1f));
        if (_logo is not null)
        {
            var logoSize = 108;
            var logoW = (int)(logoSize * scale);
            var logoH = (int)(logoSize * scale);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(
                _logo,
                (w - logoW) / 2,
                36 + (logoSize - logoH) / 2,
                logoW,
                logoH);
        }

        // Nom + sous-titre.
        using var nameFormat = new StringFormat { Alignment = StringAlignment.Center };
        using var nameBrush = new SolidBrush(Color.FromArgb(15, 90, 55));
        using var subtitleBrush = new SolidBrush(Color.FromArgb(107, 114, 128));
        g.DrawString("Mohasabi", _nameFont, nameBrush, new RectangleF(0, 142, w, 46), nameFormat);
        g.DrawString("Assistant comptable", _subtitleFont, subtitleBrush, new RectangleF(0, 190, w, 22), nameFormat);

        // Ligne de reçu qui « s'imprime » de gauche à droite.
        DrawReceiptLine(g, w, 226f);

        // Montant animé (compteur).
        using var amountFormat = new StringFormat { Alignment = StringAlignment.Center };
        using var amountBrush = new SolidBrush(Color.FromArgb(21, 115, 71));
        g.DrawString(_amount, _amountFont, amountBrush, new RectangleF(0, 246, w, 36), amountFormat);

        // Version.
        using var versionFormat = new StringFormat { Alignment = StringAlignment.Center };
        using var versionBrush = new SolidBrush(Color.FromArgb(156, 163, 175));
        g.DrawString($"Version {_version}", _versionFont, versionBrush, new RectangleF(0, 296, w, 18), versionFormat);
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
        g.FillRectangle(accent, 0, 0, w, 5);
    }

    private void DrawDecorativeGlow(Graphics g, int w)
    {
        var cx = w / 2f;
        using var glow = new GraphicsPath();
        glow.AddEllipse(cx - 70, 44, 140, 140);
        using var brush = new PathGradientBrush(glow)
        {
            CenterColor = Color.FromArgb(28, 21, 115, 71),
            SurroundColors = new[] { Color.FromArgb(0, 21, 115, 71) },
        };
        g.FillPath(brush, glow);
    }

    private void DrawReceiptLine(Graphics g, int w, float y)
    {
        const float half = 110f;
        var cx = w / 2f;
        var left = cx - half;
        var printed = half * 2f * Math.Clamp((_progress - 0.08f) / 0.62f, 0f, 1f);

        using var previewPen = new Pen(Color.FromArgb(209, 228, 216), 2f)
        {
            DashStyle = DashStyle.Dot,
        };
        g.DrawLine(previewPen, left, y, left + half * 2f, y);

        using var linePen = new Pen(Color.FromArgb(21, 115, 71), 2.5f)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round,
        };
        g.DrawLine(linePen, left, y, left + printed, y);

        // Tête d'impression (pulsation douce une fois la ligne terminée).
        var pulse = 1f + (printed >= half * 2f - 0.5f ? MathF.Sin(_clock.ElapsedMilliseconds / 150f) * 0.4f : 0f);
        using var headBrush = new SolidBrush(Color.FromArgb(15, 90, 55));
        g.FillEllipse(headBrush, left + printed - 4f * pulse, y - 4f * pulse, 8f * pulse, 8f * pulse);
    }

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
            _amountFont.Dispose();
            _versionFont.Dispose();
        }

        base.Dispose(disposing);
    }
}
