using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ClearWin
{
    // =====================================================================
    // GlassScrollRail — frosted-glass vertical scroll rail (overlay strip).
    //
    // Overlay a glass rail on a ScrollViewer's Grid cell:
    //
    //   var sv = new ScrollViewer { ... };
    //   Grid.SetRow(sv, 0); grid.Children.Add(sv);
    //   var rail = GlassScrollRail.Attach(sv, GlassScrollRail.RailMode.Main);
    //   Grid.SetRow(rail, 0); grid.Children.Add(rail);
    //
    // RailMode picks which edge the rail bar hugs:
    //   Main    → opposite side of the sidebar (right in LTR, left in RTL)
    //   Sidebar → outer edge of the sidebar   (left in LTR, right in RTL)
    //
    // If the content has nothing to scroll, the rail is fully invisible
    // AND fully non-interactive (does not steal input).
    //
    // Pure WPF, no dependencies, C# 5 / net462 safe.
    // =====================================================================
    public class GlassScrollRail : Control
    {
        /// <summary>Which edge of the host the glass rail hugs.</summary>
        public enum RailMode
        {
            Main,
            Sidebar
        }

        public static readonly DependencyProperty RailModeProperty =
            DependencyProperty.Register("RailMode", typeof(RailMode), typeof(GlassScrollRail),
                new PropertyMetadata(RailMode.Main, OnGeometryChanged));

        public RailMode Mode
        {
            get { return (RailMode)GetValue(RailModeProperty); }
            set { SetValue(RailModeProperty, value); }
        }

        // --------------------- Dependency properties ---------------------
        public static readonly DependencyProperty RailWidthProperty =
            DependencyProperty.Register("RailWidth", typeof(double), typeof(GlassScrollRail),
                new PropertyMetadata(10.0, OnGeometryChanged));

        public static readonly DependencyProperty ThumbMinHeightProperty =
            DependencyProperty.Register("ThumbMinHeight", typeof(double), typeof(GlassScrollRail),
                new PropertyMetadata(36.0, OnGeometryChanged));

        public static readonly DependencyProperty RailPaddingProperty =
            DependencyProperty.Register("RailPadding", typeof(double), typeof(GlassScrollRail),
                new PropertyMetadata(8.0, OnGeometryChanged));

        public static readonly DependencyProperty AutoHideIdleMsProperty =
            DependencyProperty.Register("AutoHideIdleMs", typeof(int), typeof(GlassScrollRail),
                new PropertyMetadata(3000, OnAutoHideIdleMsChanged));

        public double RailWidth
        {
            get { return (double)GetValue(RailWidthProperty); }
            set { SetValue(RailWidthProperty, value); }
        }

        public double ThumbMinHeight
        {
            get { return (double)GetValue(ThumbMinHeightProperty); }
            set { SetValue(ThumbMinHeightProperty, value); }
        }

        public double RailPadding
        {
            get { return (double)GetValue(RailPaddingProperty); }
            set { SetValue(RailPaddingProperty, value); }
        }

        public int AutoHideIdleMs
        {
            get { return (int)GetValue(AutoHideIdleMsProperty); }
            set { SetValue(AutoHideIdleMsProperty, value); }
        }

        // --------------------- Static glass resources ---------------------
        private static readonly SolidColorBrush _hitBrush = new SolidColorBrush(Colors.Transparent);
        private static readonly LinearGradientBrush _railBrush;
        private static readonly LinearGradientBrush _thumbBrush;
        private static readonly Pen _railPen;

        static GlassScrollRail()
        {
            _railBrush = new LinearGradientBrush();
            _railBrush.StartPoint = new Point(0.0, 0.0);
            _railBrush.EndPoint = new Point(0.0, 1.0);
            _railBrush.GradientStops.Add(new GradientStop(Color.FromArgb(24, 255, 255, 255), 0.0));
            _railBrush.GradientStops.Add(new GradientStop(Color.FromArgb(64, 255, 255, 255), 0.5));
            _railBrush.GradientStops.Add(new GradientStop(Color.FromArgb(24, 255, 255, 255), 1.0));

            _thumbBrush = new LinearGradientBrush();
            _thumbBrush.StartPoint = new Point(0.0, 0.0);
            _thumbBrush.EndPoint = new Point(0.0, 1.0);
            _thumbBrush.GradientStops.Add(new GradientStop(Color.FromArgb(150, 255, 255, 255), 0.0));
            _thumbBrush.GradientStops.Add(new GradientStop(Color.FromArgb(80, 255, 255, 255), 1.0));

            _railPen = new Pen(new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)), 1.0);
        }

        // --------------------- State ---------------------
        private ScrollViewer _host;
        private bool _draggingThumb;
        private double _dragStartY;
        private double _dragStartOffset;
        private bool _hover;
        private DispatcherTimer _idleTimer;

        public GlassScrollRail()
        {
            Focusable = false;
            SnapsToDevicePixels = true;
            ClipToBounds = true;
            MouseEnter += delegate { _hover = true; InvalidateVisual(); };
            MouseLeave += delegate { _hover = false; InvalidateVisual(); };
        }

        // =================================================================
        // Attach / Detach
        // =================================================================
        /// <summary>
        /// Creates a glass rail for the given ScrollViewer and returns it
        /// as an overlay element. Add the returned control to the same
        /// Grid cell as the ScrollViewer (the rail hovers over it and is
        /// invisible + non-interactive when there is nothing to scroll).
        /// </summary>
        public static GlassScrollRail Attach(ScrollViewer host, RailMode mode)
        {
            if (host == null) throw new ArgumentNullException("host");
            GlassScrollRail rail = new GlassScrollRail { Mode = mode };
            rail._host = host;
            host.ScrollChanged += rail.OnHostScrollChanged;

            rail._idleTimer = new DispatcherTimer();
            rail._idleTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, rail.AutoHideIdleMs));
            rail._idleTimer.Tick += rail.OnIdleTick;

            rail.Opacity = 1.0;
            rail.Wake();
            rail.InvalidateVisual();
            return rail;
        }

        public void Detach()
        {
            if (_idleTimer != null)
            {
                _idleTimer.Stop();
                _idleTimer.Tick -= OnIdleTick;
                _idleTimer = null;
            }
            if (_host != null)
            {
                _host.ScrollChanged -= OnHostScrollChanged;
                _host = null;
            }
            _draggingThumb = false;
            _hover = false;
            try { if (IsMouseCaptured) ReleaseMouseCapture(); }
            catch (Exception) { }
            BeginAnimation(OpacityProperty, null);
            Opacity = 1.0;
            InvalidateVisual();
        }

        /// <summary>Force re-render (call after external layout changes on the host).</summary>
        public void Refresh()
        {
            InvalidateVisual();
        }

        // =================================================================
        // DP change callbacks
        // =================================================================
        private static void OnGeometryChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((GlassScrollRail)d).InvalidateVisual();
        }

        private static void OnAutoHideIdleMsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            GlassScrollRail rail = (GlassScrollRail)d;
            if (rail._idleTimer == null) return;
            rail._idleTimer.Stop();
            rail._idleTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(0, (int)e.NewValue));
            if ((int)e.NewValue > 0) rail._idleTimer.Start();
        }

        protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            if (e.Property == FlowDirectionProperty)
                InvalidateVisual();
        }

        // =================================================================
        // Geometry
        // =================================================================
        // Which side of the control the rail bar hugs (mode + RTL aware).
        private bool IsRightEdge()
        {
            bool rtl = (FlowDirection == FlowDirection.RightToLeft);
            return (Mode == RailMode.Main) != rtl;
        }

        // Full rail + thumb geometry. Returns false when nothing is
        // scrollable — the rail then stays fully invisible and non-interactive.
        private bool GetMetrics(out double railX, out double thumbY, out double thumbH, out double thumbW)
        {
            railX = 0.0; thumbY = 0.0; thumbH = 0.0; thumbW = 0.0;
            if (_host == null) return false;

            double scrollable = Math.Max(0.0, _host.ScrollableHeight);
            double viewport = Math.Max(0.0, _host.ViewportHeight);
            if (scrollable <= 0.0) return false;

            double trackLen = Math.Max(0.0, ActualHeight - 2.0 * RailPadding);
            if (trackLen <= 0.0) return false;

            thumbW = RailWidth;
            thumbH = trackLen * viewport / (viewport + scrollable);
            if (thumbH < ThumbMinHeight) thumbH = ThumbMinHeight;
            if (thumbH > trackLen) thumbH = trackLen;

            double ratio = _host.VerticalOffset / scrollable;
            if (ratio < 0.0) ratio = 0.0;
            if (ratio > 1.0) ratio = 1.0;
            thumbY = RailPadding + ratio * (trackLen - thumbH);

            // Main rail hugs the side opposite the sidebar; the sidebar rail
            // hugs the sidebar's outer edge. (In this app RTL ⇒ sidebar right.)
            bool rightEdge = IsRightEdge();
            railX = rightEdge ? Math.Max(0.0, ActualWidth - RailWidth) : 0.0;
            return true;
        }

        // Maps 1px of track/drag distance to content pixels of scroll.
        private double PixelsPerTrackPixel()
        {
            if (_host == null) return 1.0;
            double scrollable = Math.Max(0.0, _host.ScrollableHeight);
            double trackLen = Math.Max(0.0, ActualHeight - 2.0 * RailPadding);
            if (scrollable <= 0.0 || trackLen <= 0.0) return 1.0;

            double viewport = Math.Max(0.0, _host.ViewportHeight);
            double thumbH = trackLen * viewport / (viewport + scrollable);
            if (thumbH < ThumbMinHeight) thumbH = ThumbMinHeight;
            if (thumbH >= trackLen) return 1.0;
            return scrollable / (trackLen - thumbH);
        }

        private void ScrollHostTo(double offset)
        {
            if (_host == null) return;
            double max = Math.Max(0.0, _host.ScrollableHeight);
            if (offset < 0.0) offset = 0.0;
            if (offset > max) offset = max;
            _host.ScrollToVerticalOffset(offset);
        }

        // =================================================================
        // Idle auto-hide (fade out after N ms of inactivity)
        // =================================================================
        private void OnIdleTick(object sender, EventArgs e)
        {
            _idleTimer.Stop();
            AnimateOpacity(0.0, 400);
        }

        private void Wake()
        {
            if (_idleTimer == null) return;
            if (AutoHideIdleMs > 0)
            {
                _idleTimer.Stop();
                _idleTimer.Start();
            }
            if (Opacity < 0.999)
                AnimateOpacity(1.0, 150);
        }

        private void AnimateOpacity(double to, int ms)
        {
            if (ms <= 0) { Opacity = to; return; }
            DoubleAnimation anim = new DoubleAnimation(Opacity, to, new Duration(TimeSpan.FromMilliseconds(ms)));
            anim.FillBehavior = FillBehavior.HoldEnd;
            BeginAnimation(OpacityProperty, anim);
        }

        // =================================================================
        // Input
        // =================================================================
        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);
            if (_host == null) return;

            double y = e.GetPosition(this).Y;
            double railX, thumbY, thumbH, thumbW;
            if (!GetMetrics(out railX, out thumbY, out thumbH, out thumbW))
            {
                e.Handled = true;
                return;
            }

            if (y >= thumbY - 4.0 && y <= thumbY + thumbH + 4.0)
            {
                _draggingThumb = true;
                _dragStartY = y;
                _dragStartOffset = _host.VerticalOffset;
                CaptureMouse();
                InvalidateVisual();
            }
            else
            {
                // Click on rail → jump one thumb-length toward the click.
                double pxPerTrack = PixelsPerTrackPixel();
                double dir = (y < thumbY) ? -1.0 : 1.0;
                ScrollHostTo(_host.VerticalOffset + dir * thumbH * pxPerTrack);
            }
            e.Handled = true;
            Wake();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_draggingThumb || _host == null) return;

            double y = e.GetPosition(this).Y;
            double dy = y - _dragStartY;
            double pxPerTrack = PixelsPerTrackPixel();
            ScrollHostTo(_dragStartOffset + dy * pxPerTrack);
        }

        protected override void OnMouseUp(MouseButtonEventArgs e)
        {
            base.OnMouseUp(e);
            if (_draggingThumb)
            {
                _draggingThumb = false;
                ReleaseMouseCapture();
                InvalidateVisual();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_host == null) return;

            double pixelsPerNotch = _host.ViewportHeight / 5.0;
            if (pixelsPerNotch < 40.0) pixelsPerNotch = 40.0;

            int dir = (e.Delta > 0) ? -1 : 1;
            ScrollHostTo(_host.VerticalOffset + dir * pixelsPerNotch);
            e.Handled = true;
            Wake();
        }

        // =================================================================
        // Host synchronization
        // =================================================================
        private void OnHostScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            Wake();
            InvalidateVisual();
        }

        // =================================================================
        // Rendering
        // =================================================================
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            double railX, thumbY, thumbH, thumbW;
            if (!GetMetrics(out railX, out thumbY, out thumbH, out thumbW))
                return; // nothing to scroll → invisible + non-interactive

            // Hit area: a narrow strip around the rail edge only, so the rest
            // of the cell (buttons, clicks, wheel over content) stays
            // fully interactive. Transparent ≠ null → hittable.
            const double hitPad = 6.0;
            double hitW = RailWidth + 2.0 * hitPad;
            double hitX = IsRightEdge() ? Math.Max(0.0, ActualWidth - hitW) : 0.0;
            dc.DrawRectangle(_hitBrush, null, new Rect(hitX, 0.0, hitW, ActualHeight));

            double trackTop = RailPadding;
            double trackLen = Math.Max(0.0, ActualHeight - 2.0 * RailPadding);
            double railR = Math.Max(2.0, RailWidth / 2.0);

            // Frosted-glass rail bar
            dc.DrawRoundedRectangle(_railBrush, _railPen,
                new Rect(railX, trackTop, RailWidth, trackLen), railR, railR);

            // Thumb (+ soft halo when hovered or dragging)
            double thumbR = Math.Max(2.0, thumbW / 2.0);
            bool glow = _draggingThumb || _hover;
            if (glow)
            {
                double haloX = Math.Max(0.0, Math.Min(railX - 3.0, Math.Max(0.0, ActualWidth - (thumbW + 6.0))));
                SolidColorBrush halo = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255));
                dc.DrawRoundedRectangle(halo, null,
                    new Rect(haloX, Math.Max(0.0, thumbY - 3.0), thumbW + 6.0, thumbH + 6.0),
                    Math.Max(thumbR + 3.0, 3.0), Math.Max(thumbR + 3.0, 3.0));
            }

            byte thumbAlpha = (byte)(glow ? 160 : 90);
            Pen thumbPen = new Pen(new SolidColorBrush(Color.FromArgb(thumbAlpha, 255, 255, 255)), 1.0);
            dc.DrawRoundedRectangle(_thumbBrush, thumbPen,
                new Rect(railX, thumbY, thumbW, thumbH), thumbR, thumbR);
        }
    }
}