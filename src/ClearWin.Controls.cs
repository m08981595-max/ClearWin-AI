using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ClearWin
{
    // ===================== REUSABLE UI FRAMEWORK =====================
    // All controls are Code-Only WPF, C# 5 compatible
    // Replaces repetitive card/UI construction across all pages

    // === GLASS CARD: Semi-transparent card with subtle border ===
    public static class UI
    {
        // Cached frozen brushes/effects - prevents 529+ allocations per tick (GC/flicker fix)
        private static LinearGradientBrush _cachedGlassFill;
        private static LinearGradientBrush _cachedGlassBorder;
        private static DropShadowEffect _cachedGlassShadow;

        // Glass fill: translucent dark-purple/blue frosted body with subtle top reflection highlight
        public static LinearGradientBrush GlassFill()
        {
            if (_cachedGlassFill != null) return _cachedGlassFill;
            var b = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(0, 1),
                GradientStops = {
                    new GradientStop(Color.FromArgb(45, 4, 4, 8), 0),
                    new GradientStop(Color.FromArgb(65, 5, 5, 9), 0.32),
                    new GradientStop(Color.FromArgb(75, 8, 8, 16), 0.62),
                    new GradientStop(Color.FromArgb(85, 4, 4, 10), 1)
                }
            };
            if (b.CanFreeze) b.Freeze();
            _cachedGlassFill = b;
            return b;
        }

        // Thin translucent cyber purple/blue glass edge
        public static LinearGradientBrush GlassBorder()
        {
            if (_cachedGlassBorder != null) return _cachedGlassBorder;
            var b = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
                GradientStops = {
                    new GradientStop(Color.FromArgb(110, 139, 92, 246), 0),
                    new GradientStop(Color.FromArgb(50, 59, 130, 246), 0.5),
                    new GradientStop(Color.FromArgb(90, 139, 92, 246), 1)
                }
            };
            if (b.CanFreeze) b.Freeze();
            _cachedGlassBorder = b;
            return b;
        }

        // Soft light shadow for glass bubbles with glowing purple tint
        public static DropShadowEffect GlassShadow()
        {
            if (_cachedGlassShadow != null) return _cachedGlassShadow;
            var e = new DropShadowEffect { Color = Color.FromRgb(139, 92, 246), BlurRadius = 14, ShadowDepth = 2, Opacity = 0.12, Direction = 315 };
            if (e.CanFreeze) e.Freeze();
            _cachedGlassShadow = e;
            return e;
        }

        public static Border GlassCard(double radius, Color? accentColor, Thickness margin)
        {
            double rr = Math.Max(22, Math.Min(32, radius + 10));
            var card = new Border();
            card.CornerRadius = new CornerRadius(rr);
            card.Background = GlassFill();
            card.BorderThickness = new Thickness(1);
            card.Margin = margin;
            card.ClipToBounds = true;
            card.BorderBrush = GlassBorder();
            card.MouseEnter += (s, e) =>
            {
                card.RenderTransform = new TranslateTransform(0, -2);
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));
            };
            card.MouseLeave += (s, e) =>
            {
                card.RenderTransform = new TranslateTransform(0, 0);
                card.BorderBrush = GlassBorder();
            };
            return card;
        }

        // === LIQUID BACKDROP: unified pure-black window base (V20 - gradient + bubbles removed) ===
        public static Grid LiquidBackdrop()
        {
            var g = new Grid();
            g.Background = new SolidColorBrush(Colors.Black);
            return g;
        }

        public static Ellipse Bubble(double size, Color tint, double alpha, double left, double top)
        {
            var e = new Ellipse();
            e.Width = size;
            e.Height = size;
            var bg = new RadialGradientBrush();
            bg.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(alpha * 255), tint.R, tint.G, tint.B), 0));
            bg.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(alpha * 40), tint.R, tint.G, tint.B), 0.55));
            bg.GradientStops.Add(new GradientStop(Color.FromArgb(0, tint.R, tint.G, tint.B), 1));
            e.Fill = bg;
            Canvas.SetLeft(e, left);
            Canvas.SetTop(e, top);
            return e;
        }

        // === MODERN PILL BUTTON: accent action button with hover/click feedback ===
        public static Border PillButton(string text, Color accent, double fontSize)
        {
            var b = new Border {
                Background = GlassFill(),
                BorderBrush = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
                    GradientStops = {
                        new GradientStop(Color.FromArgb(160, accent.R, accent.G, accent.B), 0),
                        new GradientStop(Color.FromArgb(80, 255, 255, 255), 0.5),
                        new GradientStop(Color.FromArgb(120, accent.R, accent.G, accent.B), 1)
                    }
                },
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(20, 9, 20, 9),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var t = Text(text, fontSize, accent, true);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            b.Child = t;
            b.MouseEnter += (s, e) => { b.Background = new SolidColorBrush(Color.FromArgb(90, accent.R, accent.G, accent.B)); b.RenderTransform = new TranslateTransform(0, -1); };
            b.MouseLeave += (s, e) => { b.Background = GlassFill(); b.RenderTransform = new TranslateTransform(0, 0); };
            return b;
        }

        // === GHOST BUTTON: subtle secondary action ===
        public static Border GhostButton(string text, double fontSize)
        {
            var b = new Border {
                Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(24),
                Padding = new Thickness(20, 9, 20, 9),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var t = Text(text, fontSize, Color.FromArgb(225, 255, 255, 255), false);
            t.HorizontalAlignment = HorizontalAlignment.Center;
            b.Child = t;
            b.MouseEnter += (s, e) => { b.Background = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255)); b.BorderBrush = new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)); };
            b.MouseLeave += (s, e) => { b.Background = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)); b.BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)); };
            return b;
        }

        // Hover lift animation for any Border card
        public static void AddHover(Border card)
        {
            Brush origBg = card.Background;
            Brush origBorder = card.BorderBrush;
            TransformGroup tg = new TransformGroup();
            ScaleTransform st = new ScaleTransform(1.0, 1.0, card.ActualWidth / 2, card.ActualHeight / 2);
            tg.Children.Add(st);
            card.RenderTransform = tg;
            card.RenderTransformOrigin = new Point(0.5, 0.5);
            card.MouseEnter += (s, e) =>
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnim(1.015, 150));
                st.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnim(1.015, 150));
                card.Background = new SolidColorBrush(Theme.CardHover);
                card.BorderBrush = new SolidColorBrush(Color.FromArgb(170, 255, 255, 255));
            };
            card.MouseLeave += (s, e) =>
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnim(1.0, 200));
                st.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnim(1.0, 200));
                card.Background = origBg;
                card.BorderBrush = origBorder;
            };
        }

        // === SECTION HEADER: Title bar with accent stripe ===
        public static StackPanel SectionHeader(string title, Color accentColor, string subtitle)
        {
            var sp = new StackPanel();
            sp.Margin = new Thickness(0, 0, 0, 16);

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var stripe = new Border();
            stripe.Width = 4;
            stripe.Height = 24;
            stripe.CornerRadius = new CornerRadius(2);
            stripe.Background = new SolidColorBrush(accentColor);
            stripe.Margin = new Thickness(0, 0, 12, 0);
            stripe.VerticalAlignment = VerticalAlignment.Center;
            row.Children.Add(stripe);
            row.Children.Add(Text(title, 20, Theme.TextPrimary, true));
            sp.Children.Add(row);

            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = Text(subtitle, 12, Theme.TextSecondary, false);
                sub.Margin = new Thickness(16, 4, 0, 0);
                sp.Children.Add(sub);
            }
            return sp;
        }

        // === METRIC CARD: Large number + label + optional sparkline ===
        public static Border MetricCard(string icon, string label, string value, Color accent, double radius)
        {
            var card = GlassCard(radius, accent, new Thickness(0));
            var sp = new StackPanel();
            sp.Margin = new Thickness(16, 14, 16, 14);

            // Icon + Label row
            var hdr = new StackPanel { Orientation = Orientation.Horizontal };
            var ico = Text(icon, 14, accent, false);
            ico.FontFamily = new FontFamily("Segoe MDL2 Assets");
            ico.Margin = new Thickness(0, 0, 8, 0);
            ico.VerticalAlignment = VerticalAlignment.Center;
            hdr.Children.Add(ico);
            hdr.Children.Add(Text(label, 11, Theme.TextMuted, false));
            sp.Children.Add(hdr);

            // Value
            var val = Text(value, 28, Theme.TextPrimary, true);
            val.Margin = new Thickness(0, 6, 0, 2);
            sp.Children.Add(val);

            // Sparkline area (caller populates)
            var spark = new Grid();
            spark.Height = 32;
            spark.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(spark);

            card.Child = sp;
            AddHover(card);
            return card;
        }

        // === STATUS BADGE: Pill with colored dot ===
        public static StackPanel StatusBadge(string text, Color dotColor)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            var dot = new Border();
            dot.Width = 7;
            dot.Height = 7;
            dot.CornerRadius = new CornerRadius(3.5);
            dot.Background = new SolidColorBrush(dotColor);
            dot.Margin = new Thickness(0, 0, 6, 0);
            dot.VerticalAlignment = VerticalAlignment.Center;
            var effect = new DropShadowEffect();
            effect.Color = dotColor;
            effect.BlurRadius = 6;
            effect.ShadowDepth = 0;
            effect.Opacity = 0.6;
            dot.Effect = effect;
            sp.Children.Add(dot);
            sp.Children.Add(Text(text, 11, dotColor, true));
            return sp;
        }

        // === AI AGENT CARD: For each specialized agent ===
        public static Border AIAgentCard(string name, string iconGlyph, Color accent, double impact, string status, string lastAction)
        {
            var card = GlassCard(14, accent, new Thickness(0));
            var sp = new StackPanel();
            sp.Margin = new Thickness(14, 12, 14, 12);

            // Header row: Icon + Name + Status badge
            var hdr = new DockPanel();
            var nameTxt = Text(name, 13, Theme.TextPrimary, true);
            DockPanel.SetDock(nameTxt, Dock.Left);
            hdr.Children.Add(nameTxt);
            var badge = StatusBadge(status, Theme.Green);
            badge.HorizontalAlignment = HorizontalAlignment.Right;
            hdr.Children.Add(badge);
            sp.Children.Add(hdr);

            // Icon centered
            var icoBorder = new Border();
            icoBorder.Width = 40;
            icoBorder.Height = 40;
            icoBorder.CornerRadius = new CornerRadius(20);
            icoBorder.HorizontalAlignment = HorizontalAlignment.Center;
            icoBorder.Margin = new Thickness(0, 10, 0, 8);
            icoBorder.Background = new SolidColorBrush(Color.FromArgb(30, accent.R, accent.G, accent.B));
            var icoTxt = Text(iconGlyph, 18, accent, false);
            icoTxt.FontFamily = new FontFamily("Segoe MDL2 Assets");
            icoTxt.HorizontalAlignment = HorizontalAlignment.Center;
            icoTxt.VerticalAlignment = VerticalAlignment.Center;
            icoBorder.Child = icoTxt;
            sp.Children.Add(icoBorder);

            // Impact value
            var impactTxt = Text(string.Format("{0:F0}%", impact), 24, accent, true);
            impactTxt.HorizontalAlignment = HorizontalAlignment.Center;
            impactTxt.Margin = new Thickness(0, 0, 0, 2);
            sp.Children.Add(impactTxt);
            sp.Children.Add(Text("التأثير", 9, Theme.TextMuted, false));

            // Last action (if any)
            if (!string.IsNullOrEmpty(lastAction))
            {
                sp.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Theme.BorderClr), Margin = new Thickness(0, 8, 0, 8) });
                string shortAction = lastAction.Length > 30 ? lastAction.Substring(0, 28) + ".." : lastAction;
                sp.Children.Add(Text(shortAction, 10, Theme.TextSecondary, false));
            }

            card.Child = sp;
            AddHover(card);
            return card;
        }

        // === TIMELINE ENTRY: For activity timeline ===
        public static StackPanel TimelineEntry(string time, string action, bool success, Color iconClr)
        {
            var sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };

            // Left: Time
            var timeSp = new StackPanel();
            timeSp.Width = 50;
            timeSp.Margin = new Thickness(0, 0, 12, 0);
            timeSp.Children.Add(Text(time, 12, Theme.TextSecondary, false));
            sp.Children.Add(timeSp);

            // Center: Dot + Line
            var dotCol = new StackPanel();
            dotCol.Width = 20;
            dotCol.HorizontalAlignment = HorizontalAlignment.Center;
            var dot = new Border();
            dot.Width = 10;
            dot.Height = 10;
            dot.CornerRadius = new CornerRadius(5);
            dot.Background = new SolidColorBrush(iconClr);
            dot.HorizontalAlignment = HorizontalAlignment.Center;
            var dotEffect = new DropShadowEffect();
            dotEffect.Color = iconClr;
            dotEffect.BlurRadius = 8;
            dotEffect.ShadowDepth = 0;
            dotEffect.Opacity = 0.5;
            dot.Effect = dotEffect;
            dotCol.Children.Add(dot);
            sp.Children.Add(dotCol);

            // Right: Action text
            var actSp = new StackPanel();
            actSp.Margin = new Thickness(8, 0, 0, 0);
            actSp.Children.Add(Text(action, 12, Theme.TextPrimary, false));
            sp.Children.Add(actSp);

            return sp;
        }

        // === TERMINAL BOX: Console-style log viewer ===
        public static Border TerminalBox(double height, Thickness margin)
        {
            var card = GlassCard(10, Theme.Accent, margin);
            var sp = new StackPanel { Margin = new Thickness(0) };

            // Header bar
            var hdr = new DockPanel();
            hdr.Background = new SolidColorBrush(Color.FromArgb(46, 96, 200, 255));
            hdr.Margin = new Thickness(0);
            var hdrTxt = Text("  \u25CF  \u25CF  \u25CF    AI Console", 13, Theme.Accent, true);
            DockPanel.SetDock(hdrTxt, Dock.Left);
            hdr.Children.Add(hdrTxt);
            sp.Children.Add(hdr);

            // Terminal area
            var termBorder = new Border();
            termBorder.Background = new SolidColorBrush(Color.FromArgb(30, 8, 20, 44));
            termBorder.Height = height;
            termBorder.ClipToBounds = true;
            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            var logPanel = new StackPanel();
            logPanel.Margin = new Thickness(14, 10, 14, 10);
            scroll.Content = logPanel;
            termBorder.Child = scroll;
            sp.Children.Add(termBorder);

            card.Child = sp;
            return card;
        }

        // === LINE CHART: Simple chart with area fill ===
        public static Border ChartCard(string title, Color accent, double height, Thickness margin)
        {
            var card = GlassCard(14, accent, margin);
            var sp = new StackPanel();
            sp.Margin = new Thickness(14, 12, 14, 12);

            sp.Children.Add(Text(title, 13, Theme.TextPrimary, true));

            var chartArea = new Border();
            chartArea.Height = height;
            chartArea.Background = new SolidColorBrush(Color.FromArgb(15, accent.R, accent.G, accent.B));
            chartArea.CornerRadius = new CornerRadius(8);
            chartArea.Margin = new Thickness(0, 8, 0, 4);
            chartArea.ClipToBounds = true;
            sp.Children.Add(chartArea);

            card.Child = sp;
            return card;
        }

        // === DECISION TREE NODE ===
        public static Border DecisionNode(string text, Color accent, bool isActive)
        {
            var node = new Border();
            node.CornerRadius = new CornerRadius(10);
            node.Background = isActive
                ? new SolidColorBrush(Color.FromArgb(50, accent.R, accent.G, accent.B))
                : new SolidColorBrush(Theme.CardClr);
            node.BorderBrush = new SolidColorBrush(isActive ? accent : Theme.BorderClr);
            node.BorderThickness = new Thickness(isActive ? 2 : 1);
            node.Margin = new Thickness(0, 0, 0, 4);
            node.Padding = new Thickness(14, 8, 14, 8);
            if (isActive)
            {
                var glow = new DropShadowEffect();
                glow.Color = accent;
                glow.BlurRadius = 12;
                glow.ShadowDepth = 0;
                glow.Opacity = 0.3;
                node.Effect = glow;
            }
            node.Child = Text(text, 12, isActive ? Theme.TextPrimary : Theme.TextSecondary, isActive);
            return node;
        }

        // === GOAL SELECTOR BUTTON ===
        public static Border GoalButton(string icon, string label, bool isSelected, Color accent)
        {
            var btn = new Border();
            btn.CornerRadius = new CornerRadius(12);
            btn.Cursor = Cursors.Hand;
            btn.Margin = new Thickness(0, 0, 0, 6);
            btn.Padding = new Thickness(14, 10, 14, 10);
            btn.Background = isSelected
                ? new SolidColorBrush(Color.FromArgb(40, accent.R, accent.G, accent.B))
                : new SolidColorBrush(Theme.CardClr);
            btn.BorderBrush = new SolidColorBrush(isSelected ? accent : Theme.BorderClr);
            btn.BorderThickness = new Thickness(1.5);
            if (isSelected)
            {
                var glow = new DropShadowEffect();
                glow.Color = accent;
                glow.BlurRadius = 10;
                glow.ShadowDepth = 0;
                glow.Opacity = 0.25;
                btn.Effect = glow;
            }

            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var ico = Text(icon, 16, accent, false);
            ico.Margin = new Thickness(0, 0, 10, 0);
            ico.VerticalAlignment = VerticalAlignment.Center;
            ico.FontFamily = new FontFamily("Segoe MDL2 Assets");
            row.Children.Add(ico);
            row.Children.Add(Text(label, 13, isSelected ? Theme.TextPrimary : Theme.TextSecondary, isSelected));
            btn.Child = row;

            AddHover(btn);
            return btn;
        }

        // === PROGRESS BAR: Horizontal ===
        public static Border ProgressBar(double value, double maxVal, Color accent, double width, double height)
        {
            var track = new Border();
            track.Width = width;
            track.Height = height;
            track.CornerRadius = new CornerRadius(height / 2);
            track.Background = new SolidColorBrush(Color.FromArgb(25, accent.R, accent.G, accent.B));
            double ratio = maxVal > 0 ? Math.Max(0, Math.Min(1, value / maxVal)) : 0;
            var fill = new Border();
            fill.Height = height;
            fill.Width = Math.Max(height, width * ratio);
            fill.HorizontalAlignment = HorizontalAlignment.Left;
            fill.CornerRadius = new CornerRadius(height / 2);
            fill.Background = new SolidColorBrush(accent);
            track.Child = fill;
            return track;
        }

        // === STAT ROW: Label + Value side by side ===
        public static DockPanel StatRow(string label, string value, Color valueClr)
        {
            var dp = new DockPanel();
            dp.Margin = new Thickness(0, 3, 0, 3);
            var val = Text(value, 13, valueClr, true);
            DockPanel.SetDock(val, Dock.Right);
            dp.Children.Add(val);
            dp.Children.Add(Text(label, 11, Theme.TextMuted, false));
            return dp;
        }

        // === DIVIDER LINE ===
        public static Border Divider()
        {
            return new Border
            {
                Height = 1,
                Background = new SolidColorBrush(Theme.BorderClr),
                Margin = new Thickness(0, 10, 0, 10)
            };
        }

        // === SPACER ===
        public static UIElement Spacer(double height)
        {
            return new Border { Height = height, Background = Theme.TransparentBrush };
        }

        // === TEXT HELPER ===
        public static TextBlock Text(string content, double size, Color color, bool bold)
        {
            var tb = new TextBlock();
            tb.Text = content ?? "";
            tb.FontSize = size;
            tb.Foreground = new SolidColorBrush(color);
            tb.FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal;
            tb.FontFamily = new FontFamily("Segoe UI");
            tb.TextWrapping = TextWrapping.NoWrap;
            return tb;
        }

        public static TextBlock TextWrapped(string content, double size, Color color, bool bold)
        {
            var tb = Text(content, size, color, bold);
            tb.TextWrapping = TextWrapping.Wrap;
            return tb;
        }

        // === ICON TEXT (Segoe MDL2 Assets) ===
        public static TextBlock Icon(string glyph, double size, Color color)
        {
            var tb = new TextBlock();
            tb.Text = glyph;
            tb.FontSize = size;
            tb.Foreground = new SolidColorBrush(color);
            tb.FontFamily = new FontFamily("Segoe MDL2 Assets");
            tb.VerticalAlignment = VerticalAlignment.Center;
            return tb;
        }

        // === RING GAUGE: Circular progress ring ===
        public static Border RingGauge(double value, double maxVal, Color accent, double size, string centerText, double fontSize)
        {
            var container = new Border();
            container.Width = size;
            container.Height = size;
            container.HorizontalAlignment = HorizontalAlignment.Center;
            container.Margin = new Thickness(0, 4, 0, 4);

            var grid = new Grid();

            // Background ring
            var bgRing = new Ellipse();
            bgRing.Width = size;
            bgRing.Height = size;
            bgRing.Stroke = new SolidColorBrush(Color.FromArgb(30, accent.R, accent.G, accent.B));
            bgRing.StrokeThickness = 4;
            bgRing.Fill = null;
            grid.Children.Add(bgRing);

            // Foreground arc (simulated with polyline)
            double ratio = maxVal > 0 ? value / maxVal : 0;
            double angle = ratio * 360;
            var arc = new Polyline();
            arc.Stroke = new SolidColorBrush(accent);
            arc.StrokeThickness = 4;
            arc.StrokeStartLineCap = PenLineCap.Round;
            arc.StrokeEndLineCap = PenLineCap.Round;
            double cx = size / 2, cy = size / 2, r = (size - 4) / 2;
            int steps = Math.Max(3, (int)(angle / 10));
            for (int i = 0; i <= steps; i++)
            {
                double a = -90 + (angle * i / steps);
                double rad = a * Math.PI / 180;
                arc.Points.Add(new Point(cx + r * Math.Cos(rad), cy + r * Math.Sin(rad)));
            }
            grid.Children.Add(arc);

            // Center text
            if (!string.IsNullOrEmpty(centerText))
            {
                var txt = Text(centerText, fontSize, Theme.TextPrimary, true);
                txt.HorizontalAlignment = HorizontalAlignment.Center;
                txt.VerticalAlignment = VerticalAlignment.Center;
                grid.Children.Add(txt);
            }

            container.Child = grid;
            return container;
        }

        // === GLOW BUTTON ===
        public static Border GlowButton(string label, Color accent, double fontSize)
        {
            var btn = new Border();
            btn.CornerRadius = new CornerRadius(16);
            btn.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            btn.BorderBrush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0), EndPoint = new Point(1, 1),
                GradientStops = {
                    new GradientStop(Color.FromArgb(150, accent.R, accent.G, accent.B), 0),
                    new GradientStop(Color.FromArgb(50, 255, 255, 255), 0.5),
                    new GradientStop(Color.FromArgb(110, accent.R, accent.G, accent.B), 1)
                }
            };
            btn.BorderThickness = new Thickness(1);
            btn.Cursor = Cursors.Hand;
            btn.Padding = new Thickness(18, 8, 18, 8);
            btn.Child = Text(label, fontSize, accent, true);
            btn.RenderTransformOrigin = new Point(0.5, 0.5);
            TransformGroup tg = new TransformGroup();
            ScaleTransform st = new ScaleTransform(1.0, 1.0);
            tg.Children.Add(st);
            btn.RenderTransform = tg;
            btn.MouseEnter += (s, e) =>
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnim(1.04, 120));
                st.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnim(1.04, 120));
                btn.Background = new SolidColorBrush(Color.FromArgb(70, accent.R, accent.G, accent.B));
            };
            btn.MouseLeave += (s, e) =>
            {
                st.BeginAnimation(ScaleTransform.ScaleXProperty, CreateAnim(1.0, 180));
                st.BeginAnimation(ScaleTransform.ScaleYProperty, CreateAnim(1.0, 180));
                btn.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255));
            };
            return btn;
        }

        // === RADAR CHART: 6-axis spider chart ===
        public static Border RadarChart(double[] values, string[] labels, Color accent, double size)
        {
            var container = new Border();
            container.Width = size;
            container.Height = size;
            container.HorizontalAlignment = HorizontalAlignment.Center;

            var canvas = new Canvas();
            canvas.Width = size;
            canvas.Height = size;

            double cx = size / 2, cy = size / 2, r = (size - 20) / 2;
            int axes = Math.Min(values.Length, labels.Length);
            if (axes < 3) axes = 3;

            // Draw grid rings
            for (int ring = 1; ring <= 3; ring++)
            {
                double rr = r * ring / 3;
                var ringLine = new Polyline();
                ringLine.Stroke = new SolidColorBrush(Color.FromArgb(25, accent.R, accent.G, accent.B));
                ringLine.StrokeThickness = 1;
                for (int i = 0; i <= axes; i++)
                {
                    double a = -90 + (360.0 * (i % axes) / axes);
                    double rad = a * Math.PI / 180;
                    ringLine.Points.Add(new Point(cx + rr * Math.Cos(rad), cy + rr * Math.Sin(rad)));
                }
                canvas.Children.Add(ringLine);
            }

            // Draw axes
            for (int i = 0; i < axes; i++)
            {
                double a = -90 + (360.0 * i / axes);
                double rad = a * Math.PI / 180;
                var axisLine = new Line();
                axisLine.X1 = cx;
                axisLine.Y1 = cy;
                axisLine.X2 = cx + r * Math.Cos(rad);
                axisLine.Y2 = cy + r * Math.Sin(rad);
                axisLine.Stroke = new SolidColorBrush(Color.FromArgb(20, accent.R, accent.G, accent.B));
                axisLine.StrokeThickness = 1;
                canvas.Children.Add(axisLine);
            }

            // Draw data polygon
            var dataPoly = new Polyline();
            dataPoly.Stroke = new SolidColorBrush(accent);
            dataPoly.StrokeThickness = 2;
            var fillPoly = new Polygon();
            fillPoly.Fill = new SolidColorBrush(Color.FromArgb(30, accent.R, accent.G, accent.B));
            fillPoly.Stroke = null;

            for (int i = 0; i < axes; i++)
            {
                double a = -90 + (360.0 * i / axes);
                double rad = a * Math.PI / 180;
                double val = i < values.Length ? Math.Max(0, Math.Min(1, values[i])) : 0;
                double pr = r * val;
                Point p = new Point(cx + pr * Math.Cos(rad), cy + pr * Math.Sin(rad));
                dataPoly.Points.Add(p);
                fillPoly.Points.Add(p);
            }
            canvas.Children.Add(fillPoly);
            canvas.Children.Add(dataPoly);

            // Draw data dots
            for (int i = 0; i < axes; i++)
            {
                double a = -90 + (360.0 * i / axes);
                double rad = a * Math.PI / 180;
                double val = i < values.Length ? Math.Max(0, Math.Min(1, values[i])) : 0;
                double pr = r * val;
                var dot = new Ellipse();
                dot.Width = 6;
                dot.Height = 6;
                dot.Fill = new SolidColorBrush(accent);
                Canvas.SetLeft(dot, cx + pr * Math.Cos(rad) - 3);
                Canvas.SetTop(dot, cy + pr * Math.Sin(rad) - 3);
                canvas.Children.Add(dot);
            }

            // Labels
            for (int i = 0; i < axes; i++)
            {
                double a = -90 + (360.0 * i / axes);
                double rad = a * Math.PI / 180;
                double lr = r + 14;
                var lbl = Text(labels[i], 9, Theme.TextSecondary, false);
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                Canvas.SetLeft(lbl, cx + lr * Math.Cos(rad) - 20);
                Canvas.SetTop(lbl, cy + lr * Math.Sin(rad) - 8);
                canvas.Children.Add(lbl);
            }

            container.Child = canvas;
            return container;
        }

        // === LINE CHART WITH AREA FILL ===
        public static Border SparklineChart(double[] data, Color accent, double width, double height)
        {
            var border = new Border();
            border.Width = width;
            border.Height = height;
            border.ClipToBounds = true;

            var canvas = new Canvas();
            canvas.Width = width;
            canvas.Height = height;

            if (data.Length < 2)
            {
                border.Child = canvas;
                return border;
            }

            // Find range
            double min = double.MaxValue, max = double.MinValue;
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] < min) min = data[i];
                if (data[i] > max) max = data[i];
            }
            double range = max - min;
            if (range < 0.001) range = 1;

            // Area fill polygon
            var fill = new Polygon();
            fill.Fill = new SolidColorBrush(Color.FromArgb(20, accent.R, accent.G, accent.B));
            fill.Stroke = null;
            double padY = 4;
            fill.Points.Add(new Point(0, height));
            for (int i = 0; i < data.Length; i++)
            {
                double x = (i / (double)(data.Length - 1)) * width;
                double y = padY + ((max - data[i]) / range) * (height - padY * 2);
                fill.Points.Add(new Point(x, y));
            }
            fill.Points.Add(new Point(width, height));
            canvas.Children.Add(fill);

            // Line
            var line = new Polyline();
            line.Stroke = new SolidColorBrush(accent);
            line.StrokeThickness = 1.5;
            line.StrokeStartLineCap = PenLineCap.Round;
            line.StrokeEndLineCap = PenLineCap.Round;
            line.StrokeLineJoin = PenLineJoin.Round;
            for (int i = 0; i < data.Length; i++)
            {
                double x = (i / (double)(data.Length - 1)) * width;
                double y = padY + ((max - data[i]) / range) * (height - padY * 2);
                line.Points.Add(new Point(x, y));
            }
            canvas.Children.Add(line);

            // End dot
            if (data.Length > 0)
            {
                double lx = width;
                double ly = padY + ((max - data[data.Length - 1]) / range) * (height - padY * 2);
                var dot = new Ellipse();
                dot.Width = 5;
                dot.Height = 5;
                dot.Fill = new SolidColorBrush(accent);
                Canvas.SetLeft(dot, lx - 2.5);
                Canvas.SetTop(dot, ly - 2.5);
                canvas.Children.Add(dot);
            }

            border.Child = canvas;
            return border;
        }

        // === DECISION TREE VISUAL ===
        public static StackPanel DecisionTree(string[] nodes, int activeIndex, Color accent)
        {
            var sp = new StackPanel();
            for (int i = 0; i < nodes.Length; i++)
            {
                bool active = (i <= activeIndex);
                bool current = (i == activeIndex);
                sp.Children.Add(DecisionNode(nodes[i], accent, current));
                if (i < nodes.Length - 1)
                {
                    // Arrow between nodes
                    var arrow = Text("\u2193", 16, active ? accent : Theme.TextMuted, false);
                    arrow.HorizontalAlignment = HorizontalAlignment.Center;
                    arrow.Margin = new Thickness(0, -2, 0, -2);
                    sp.Children.Add(arrow);
                }
            }
            return sp;
        }

        // === UTILITY: Create DoubleAnimation ===
        public static System.Windows.Media.Animation.DoubleAnimation CreateAnim(double to, int ms)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation();
            anim.To = to;
            anim.Duration = new Duration(TimeSpan.FromMilliseconds(ms));
            anim.EasingFunction = new CubicEase();
            return anim;
        }

        public static System.Windows.Media.Animation.DoubleAnimation CreateAnim(double from, double to, int ms)
        {
            var anim = new System.Windows.Media.Animation.DoubleAnimation();
            anim.From = from;
            anim.To = to;
            anim.Duration = new Duration(TimeSpan.FromMilliseconds(ms));
            anim.EasingFunction = new CubicEase();
            return anim;
        }

        // === UTILITY: Create ColorAnimation ===
        public static System.Windows.Media.Animation.ColorAnimation CreateColorAnim(Color to, int ms)
        {
            var anim = new System.Windows.Media.Animation.ColorAnimation();
            anim.To = to;
            anim.Duration = new Duration(TimeSpan.FromMilliseconds(ms));
            return anim;
        }
    }
}
