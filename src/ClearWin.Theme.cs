using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace ClearWin
{
    public static class Theme
    {
        // === Premium Cyber-Dark Purple & Neon Blue theme — Professional & Fitting for Project Power ===
        public static Color Background = Colors.Black; // V20: explicit pure black (#000000)
        public static Color Sidebar = Color.FromArgb(255, 0, 0, 0); // V20: unified pure black
        public static Color Content = Color.FromArgb(255, 0, 0, 0);
        public static Color CardClr = Color.FromArgb(140, 8, 8, 12);
        public static Color CardGlass = Color.FromArgb(180, 5, 5, 8);
        public static Color BorderClr = Color.FromArgb(90, 10, 10, 16);
        public static Color BorderLight = Color.FromArgb(140, 139, 92, 246);
        public static Color Accent = Color.FromRgb(139, 92, 246);
        public static Color AccentHover = Color.FromRgb(167, 139, 250);
        public static Color Blue = Color.FromRgb(59, 130, 246);
        public static Color BlueHover = Color.FromRgb(96, 165, 250);
        public static Color Green = Color.FromRgb(34, 197, 94);
        public static Color Warning = Color.FromRgb(245, 158, 11);
        public static Color Error = Color.FromRgb(239, 68, 68);
        public static Color TextPrimary = Color.FromRgb(237, 233, 254);
        public static Color TextSecondary = Color.FromRgb(156, 163, 175);
        public static Color TextMuted = Color.FromRgb(107, 114, 128);
        public static Color Purple = Color.FromRgb(139, 92, 246);
        public static Color PurpleLight = Color.FromRgb(167, 139, 250);
        public static Color Cyan = Color.FromRgb(6, 182, 212);
        public static Color Pink = Color.FromRgb(236, 72, 153);
        public static Color Disabled = Color.FromRgb(75, 85, 99);
        public static Color Panel2 = Color.FromArgb(255, 3, 3, 4);
        public static Color SubCard = Color.FromArgb(110, 10, 10, 16);
        public static Color HoverBg = Color.FromArgb(60, 139, 92, 246);
        public static Color Glow = Color.FromArgb(40, 139, 92, 246);
        public static Color GlowBlue = Color.FromArgb(40, 59, 130, 246);
        public static Color CardHover = Color.FromArgb(160, 26, 21, 53);
        public static Color SelectedNav = Color.FromArgb(160, 139, 92, 246);
        public static Color NavHover = Color.FromArgb(80, 139, 92, 246);
        public static Color SearchBg = Color.FromArgb(240, 0, 0, 0);

        public static Brush BgBrush = FreezeBrush(new SolidColorBrush(Background));
        public static Brush SidebarBrush = FreezeBrush(new SolidColorBrush(Sidebar));
        public static Brush ContentBrush = FreezeBrush(new SolidColorBrush(Content));
        public static Brush CardBrush = FreezeBrush(new SolidColorBrush(CardClr));
        public static Brush CardGlassBrush = FreezeBrush(new SolidColorBrush(CardGlass));
        public static Brush BorderBrush = FreezeBrush(new SolidColorBrush(BorderClr));
        public static Brush AccentBrush = FreezeBrush(new SolidColorBrush(Accent));
        public static Brush BlueBrush = FreezeBrush(new SolidColorBrush(Blue));
        public static Brush GreenBrush = FreezeBrush(new SolidColorBrush(Green));
        public static Brush WarningBrush = FreezeBrush(new SolidColorBrush(Warning));
        public static Brush ErrorBrush = FreezeBrush(new SolidColorBrush(Error));
        public static Brush CyanBrush = FreezeBrush(new SolidColorBrush(Cyan));
        public static Brush PurpleBrush = FreezeBrush(new SolidColorBrush(Purple));
        public static Brush TextBrush = FreezeBrush(new SolidColorBrush(TextPrimary));
        public static Brush TextSecBrush = FreezeBrush(new SolidColorBrush(TextSecondary));
        public static Brush TextMutedBrush = FreezeBrush(new SolidColorBrush(TextMuted));
        public static Brush TransparentBrush = FreezeBrush(new SolidColorBrush(Colors.Transparent));
        public static Brush Panel2Brush = FreezeBrush(new SolidColorBrush(Panel2));
        public static Brush SubCardBrush = FreezeBrush(new SolidColorBrush(SubCard));
        public static Brush HoverBrush = FreezeBrush(new SolidColorBrush(HoverBg));
        public static Brush MutedWhiteBrush = FreezeBrush(new SolidColorBrush(Color.FromArgb(136, 255, 255, 255)));
        // Pre-frozen gradients for Health page (avoid per-tick new LinearGradientBrush alloc)
        // Health temp gradient varies by color — freeze known variants; disk gradient is fixed.
        public static LinearGradientBrush DiskHealthGradient = FreezeBrush(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 0),
            GradientStops = { new GradientStop(Color.FromRgb(34,197,94),0), new GradientStop(Purple,1) }
        });
        public static LinearGradientBrush TempGreenGradient = FreezeBrush(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 0),
            GradientStops = { new GradientStop(Green,0), new GradientStop(Color.FromArgb(180, Green.R, Green.G, Green.B),1) }
        });
        public static LinearGradientBrush TempWarningGradient = FreezeBrush(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 0),
            GradientStops = { new GradientStop(Warning,0), new GradientStop(Color.FromArgb(180, Warning.R, Warning.G, Warning.B),1) }
        });
        public static LinearGradientBrush TempErrorGradient = FreezeBrush(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 0),
            GradientStops = { new GradientStop(Error,0), new GradientStop(Color.FromArgb(180, Error.R, Error.G, Error.B),1) }
        });
        // Translucent variants — avoid per-tick `new SolidColorBrush(Color.FromArgb(..))` allocations
        public static Brush GreenAlpha38Brush = FreezeBrush(new SolidColorBrush(Color.FromArgb(38, Green.R, Green.G, Green.B)));
        public static Brush AccentAlpha38Brush = FreezeBrush(new SolidColorBrush(Color.FromArgb(38, Accent.R, Accent.G, Accent.B)));
        public static Brush PurpleAlpha38Brush = FreezeBrush(new SolidColorBrush(Color.FromArgb(38, Purple.R, Purple.G, Purple.B)));
        public static Brush WarningAlpha38Brush = FreezeBrush(new SolidColorBrush(Color.FromArgb(38, Warning.R, Warning.G, Warning.B)));
        public static Brush ErrorAlpha38Brush = FreezeBrush(new SolidColorBrush(Color.FromArgb(38, Error.R, Error.G, Error.B)));
        public static Brush WarningAlpha15Brush = FreezeBrush(new SolidColorBrush(Color.FromArgb(15, Warning.R, Warning.G, Warning.B)));
        public static Brush WarningAlpha30Brush = FreezeBrush(new SolidColorBrush(Color.FromArgb(30, Warning.R, Warning.G, Warning.B)));
        // Fast Color -> frozen Brush mapping (no allocation for known palette entries)
        public static Brush BrushFor(Color c)
        {
            if (c == Green) return GreenBrush;
            if (c == Warning) return WarningBrush;
            if (c == Error) return ErrorBrush;
            if (c == Accent) return AccentBrush;
            if (c == Blue) return BlueBrush;
            if (c == Cyan) return CyanBrush;
            if (c == Purple) return PurpleBrush;
            if (c == TextPrimary) return TextBrush;
            if (c == TextSecondary) return TextSecBrush;
            if (c == TextMuted) return TextMutedBrush;
            if (c == Background) return BgBrush;
            // translucent variants
            if (c.A == 38)
            {
                if (c.R == Green.R && c.G == Green.G && c.B == Green.B) return GreenAlpha38Brush;
                if (c.R == Accent.R && c.G == Accent.G && c.B == Accent.B) return AccentAlpha38Brush;
                if (c.R == Purple.R && c.G == Purple.G && c.B == Purple.B) return PurpleAlpha38Brush;
                if (c.R == Warning.R && c.G == Warning.G && c.B == Warning.B) return WarningAlpha38Brush;
                if (c.R == Error.R && c.G == Error.G && c.B == Error.B) return ErrorAlpha38Brush;
            }
            if (c.A == 15 && c.R == Warning.R && c.G == Warning.G && c.B == Warning.B) return WarningAlpha15Brush;
            if (c.A == 30 && c.R == Warning.R && c.G == Warning.G && c.B == Warning.B) return WarningAlpha30Brush;
            if (c.A == 136 && c.R == 255 && c.G == 255 && c.B == 255) return MutedWhiteBrush;
            var b = new SolidColorBrush(c); if (b.CanFreeze) b.Freeze(); return b;
        }

        private static Brush FreezeBrush(Brush b) { if (b.CanFreeze) b.Freeze(); return b; }
        private static LinearGradientBrush FreezeBrush(LinearGradientBrush b) { if (b.CanFreeze) b.Freeze(); return b; }
        private static DropShadowEffect FreezeEffect(DropShadowEffect e) { if (e.CanFreeze) e.Freeze(); return e; }

        public static LinearGradientBrush AccentGradient = FreezeBrush(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            GradientStops = { new GradientStop(Accent, 0), new GradientStop(Color.FromRgb(56, 140, 230), 1) }
        });
        public static LinearGradientBrush BlueGradient = FreezeBrush(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            GradientStops = { new GradientStop(Blue, 0), new GradientStop(Color.FromRgb(40, 96, 178), 1) }
        });
        public static LinearGradientBrush GreenGradient = FreezeBrush(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
            GradientStops = { new GradientStop(Green, 0), new GradientStop(Color.FromRgb(22, 101, 52), 1) }
        });
        public static LinearGradientBrush SurfaceGradient = FreezeBrush(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
            GradientStops = { new GradientStop(Color.FromArgb(120, 170, 210, 245), 0), new GradientStop(Color.FromArgb(95, 140, 185, 235), 1) }
        });

        public static DropShadowEffect CardShadow = FreezeEffect(new DropShadowEffect
        {
            Color = Color.FromRgb(139, 92, 246), BlurRadius = 24, ShadowDepth = 0, Opacity = 0.24
        });
        public static DropShadowEffect GlowShadow = FreezeEffect(new DropShadowEffect
        {
            Color = Color.FromRgb(139, 92, 246), BlurRadius = 32, ShadowDepth = 0, Opacity = 0.32
        });

        // === Modern nav (sidebar) ===
        public static LinearGradientBrush NavActiveGradient = FreezeBrush(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 0),
            GradientStops = { new GradientStop(Color.FromArgb(130, 139, 92, 246), 0), new GradientStop(Color.FromArgb(26, 139, 92, 246), 1) }
        });
        public static LinearGradientBrush SidebarGrad = FreezeBrush(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
            GradientStops = { new GradientStop(Color.FromArgb(255, 0, 0, 0), 0), new GradientStop(Color.FromArgb(255, 0, 0, 0), 0.55), new GradientStop(Color.FromArgb(255, 0, 0, 0), 1) }
        });
        public static LinearGradientBrush EdgeAccent = FreezeBrush(new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
            GradientStops = { new GradientStop(Color.FromArgb(0, 96, 200, 255), 0), new GradientStop(Color.FromArgb(150, 96, 200, 255), 0.5), new GradientStop(Color.FromArgb(0, 96, 200, 255), 1) }
        });
    }

    public static class ThemeColors
    {
        public static readonly Color[] KpiColors = new[] {
            Color.FromRgb(80, 170, 255),
            Color.FromRgb(34, 197, 94),
            Color.FromRgb(96, 200, 255),
            Color.FromRgb(250, 204, 21),
            Color.FromRgb(239, 68, 68),
            Color.FromRgb(148, 130, 246)
        };
        public static readonly Color[] ChartColors = new[] {
            Color.FromRgb(80, 170, 255),
            Color.FromRgb(96, 200, 255),
            Color.FromRgb(34, 197, 94),
            Color.FromRgb(6, 182, 212),
            Color.FromRgb(250, 204, 21)
        };
        public static readonly Color[] HealthColors = new[] {
            Color.FromRgb(34, 197, 94),
            Color.FromRgb(250, 204, 21),
            Color.FromRgb(239, 68, 68)
        };
    }
}
