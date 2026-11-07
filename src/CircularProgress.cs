using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ShapePath = System.Windows.Shapes.Path;

namespace ClearWin
{
    public class CircularProgress : Grid
    {
        private ShapePath _arc;
        private TextBlock _pctText;

        public CircularProgress(double size, double thickness)
        {
            Width = Height = size;
            VerticalAlignment = VerticalAlignment.Center;
            HorizontalAlignment = HorizontalAlignment.Center;

            var bg = new Ellipse
            {
                Width = size - thickness,
                Height = size - thickness,
                Stroke = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255)),
                StrokeThickness = thickness
            };
            Children.Add(bg);

            _arc = new ShapePath
            {
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,
                SnapsToDevicePixels = true
            };
            Children.Add(_arc);

            _pctText = new TextBlock
            {
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Colors.White)
            };
            Children.Add(_pctText);
        }

        public bool ShowCenterText
        {
            get { return _pctText.Visibility == Visibility.Visible; }
            set { _pctText.Visibility = value ? Visibility.Visible : Visibility.Collapsed; }
        }

        public void SetProgress(double percent, Color color)
        {
            var grad = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(1, 1) };
            grad.GradientStops.Add(new GradientStop(color, 0.0));
            grad.GradientStops.Add(new GradientStop(Color.FromRgb(34, 211, 238), 1.0));
            _arc.Stroke = grad;
            _pctText.Text = string.Format("{0:F0}%", percent);

            double sz = Width;
            double thick = _arc.StrokeThickness;
            double radius = (sz - thick) / 2.0;
            double cx = sz / 2.0;

            double startAngle = -90.0 * Math.PI / 180.0;
            double sweepAngle = 360.0 * Math.Min(percent, 99.99) / 100.0;
            double endAngle = (-90.0 + sweepAngle) * Math.PI / 180.0;

            var start = new Point(cx + radius * Math.Cos(startAngle), cx + radius * Math.Sin(startAngle));
            var end = new Point(cx + radius * Math.Cos(endAngle), cx + radius * Math.Sin(endAngle));

            if (percent < 0.1) { _arc.Data = null; return; }

            var seg = new ArcSegment
            {
                Point = end,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = percent > 50.0
            };

            var fig = new PathFigure { StartPoint = start, IsClosed = false };
            fig.Segments.Add(seg);
            _arc.Data = new PathGeometry(new PathFigure[] { fig });
        }
    }
}
