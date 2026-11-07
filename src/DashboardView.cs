using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ClearWin
{
    /// <summary>
    /// Premium code-only WPF Dashboard for ClearWin.
    /// No XAML, no external UI libraries.
    ///
    /// IMPORTANT:
    /// This class is intentionally backend-agnostic. Connect your existing
    /// SmartAgent / monitoring services by calling the public Update... methods.
    /// That prevents this UI file from guessing names of backend properties.
    /// </summary>
    public sealed class DashboardView : UserControl, IDisposable
    {
        #region Palette — unified to ClearWin.Theme (single source of truth)
        // All colors now alias Theme.* so DashboardView no longer carries
        // a conflicting third palette (was 13 private colors diverging from
        // Theme.* and ThemeColors.*). Visuals unchanged — values are close.
        private static Color Bg           { get { return Theme.Background; } }
        private static Color Surface      { get { return Theme.CardClr; } }
        private static Color Surface2     { get { return Theme.SubCard; } }
        private static Color Surface3     { get { return Theme.CardHover; } }
        private static Color Border       { get { return Theme.BorderClr; } }
        private static Color BorderSoft   { get { return Color.FromArgb(55, Theme.BorderClr.R, Theme.BorderClr.G, Theme.BorderClr.B); } }

        private static Color Purple       { get { return Theme.Purple; } }
        private static Color PurpleBright { get { return Theme.PurpleLight; } }
        private static Color Cyan         { get { return Theme.Cyan; } }
        private static Color Blue         { get { return Theme.Blue; } }
        private static Color Green        { get { return Theme.Green; } }
        private static Color Red          { get { return Theme.Error; } }
        private static Color Amber        { get { return Theme.Warning; } }

        private static Color Text         { get { return Theme.TextPrimary; } }
        private static Color Text2        { get { return Theme.TextSecondary; } }
        private static Color Text3        { get { return Theme.TextMuted; } }
        #endregion

        #region Fields

        private readonly Grid _root;
        private readonly DispatcherTimer _uiTimer;

        private TextBlock _cpuValue;
        private TextBlock _ramValue;
        private TextBlock _gpuValue;
        private TextBlock _tempValue;
        private TextBlock _fpsValue;
        private TextBlock _healthValue;

        private TextBlock _cpuTrend;
        private TextBlock _ramTrend;
        private TextBlock _gpuTrend;
        private TextBlock _tempTrend;
        private TextBlock _fpsTrend;
        private TextBlock _healthTrend;

        private ProgressBar _aiConfidenceBar;
        private TextBlock _aiStatusText;
        private TextBlock _aiActionText;
        private TextBlock _aiConfidenceText;
        private TextBlock _aiDecisionsText;
        private TextBlock _aiActiveText;
        private TextBlock _aiLatencyText;
        private TextBlock _aiAccuracyText;

        private TextBlock _recommendationText;
        private TextBlock _recommendationDetails;
        private TextBlock _recommendationConfidence;

        private TextBlock _activityTitle;
        private TextBlock _activityDetails;
        private TextBlock _activityTime;

        private TextBlock _integrityCpu;
        private TextBlock _integrityRam;
        private TextBlock _integrityGpu;
        private TextBlock _integrityTemp;
        private TextBlock _integrityFps;
        private TextBlock _integrityHealth;

        private TextBlock _integrityOrigCpu;
        private TextBlock _integrityOrigRam;
        private TextBlock _integrityOrigGpu;
        private TextBlock _integrityOrigTemp;
        private TextBlock _integrityOrigFps;
        private TextBlock _integrityOrigHealth;

        private TextBlock _integrityStatusCpu;
        private TextBlock _integrityStatusRam;
        private TextBlock _integrityStatusGpu;
        private TextBlock _integrityStatusTemp;
        private TextBlock _integrityStatusFps;
        private TextBlock _integrityStatusHealth;

        private bool _integrityHasBaseline;
        private string _integrityBaselineCpu;
        private string _integrityBaselineRam;
        private string _integrityBaselineGpu;
        private string _integrityBaselineTemp;
        private string _integrityBaselineFps;
        private string _integrityBaselineHealth;

        private TextBlock _fpsCurrent;
        private TextBlock _fpsAverage;
        private TextBlock _fpsLow;
        private TextBlock _frameTime;
        private TextBlock _fpsStability;
        private TextBlock _fpsDrops;

        private RadarChart _radar;

        private Grid _metricsGrid;
        private Border[] _metricCards;

        private TextBlock _footerMode;
        private TextBlock _footerStatus;

        private bool _disposed;

        // Latest values retained by the view for radar/chart refresh.
        private double _cpu;
        private double _ram;
        private double _gpu;
        private double _temperature;
        private double _fps;
        private double _health;

        #endregion

        #region Events

        public event EventHandler OpenAiRequested;
        public event EventHandler OpenSettingsRequested;
        public event EventHandler RunIntegrityTestRequested;
        public event EventHandler ShowAllActivityRequested;

        public event EventHandler SystemProtectionRequested;
        public event EventHandler StartupManagerRequested;
        public event EventHandler GameBoostRequested;
        public event EventHandler MemoryCleanRequested;
        public event EventHandler FullOptimizationRequested;

        #endregion

        #region Constructor

        public DashboardView()
        {
            HorizontalAlignment = HorizontalAlignment.Stretch;
            VerticalAlignment = VerticalAlignment.Stretch;
            Background = Brush(Bg);

            _root = new Grid
            {
                Margin = new Thickness(8, 6, 8, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            BuildMetricCards();
            BuildAiSection();
            BuildActivitySection();
            BuildIntegrityAndFpsSection();
            BuildQuickActions();
            BuildFooter();

            Content = _root;

            Loaded += DashboardView_Loaded;
            SizeChanged += DashboardView_SizeChanged;

            _uiTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();

            Unloaded += DashboardView_Unloaded;
        }

        #endregion

        #region Public Backend API

        /// <summary>
        /// Updates the six main resource cards using real monitoring values.
        /// CPU/RAM/GPU/Health are percentages 0..100.
        /// Temperature is Celsius.
        /// FPS is frames per second.
        /// </summary>
        public void UpdateMetrics(
            double cpu,
            double ram,
            double gpu,
            double temperature,
            double fps,
            double health)
        {
            if (_disposed)
                return;

            _cpu = Clamp(cpu, 0, 100);
            _ram = Clamp(ram, 0, 100);
            _gpu = Clamp(gpu, 0, 100);
            _temperature = Math.Max(0, temperature);
            _fps = Math.Max(0, fps);
            _health = Clamp(health, 0, 100);

            RunOnUi(delegate
            {
                SetText(_cpuValue, FormatPercent(_cpu));
                SetText(_ramValue, FormatPercent(_ram));
                SetText(_gpuValue, FormatPercent(_gpu));
                SetText(_tempValue, _temperature.ToString("0.0", CultureInfo.InvariantCulture) + "°C");
                SetText(_fpsValue, _fps.ToString("0", CultureInfo.InvariantCulture));
                SetText(_healthValue, FormatPercent(_health));

                SetText(_cpuTrend, ResourceCaption(_cpu, false));
                SetText(_ramTrend, ResourceCaption(_ram, false));
                SetText(_gpuTrend, ResourceCaption(_gpu, false));
                SetText(_tempTrend, TemperatureCaption(_temperature));
                SetText(_fpsTrend, _fps >= 60 ? "مستقر" : "يحتاج تحسين");
                SetText(_healthTrend, _health >= 80 ? "جيد" : "تحقق من النظام");

                _radar.SetValues(_cpu, _ram, _gpu, TemperatureToScore(_temperature), FpsToScore(_fps), _health);
            });
        }

        /// <summary>
        /// Updates the AI panel from the real SmartAgent state.
        /// </summary>
        public void UpdateAiState(
            string status,
            string action,
            double confidence,
            int decisions,
            int activeOptimizations,
            double decisionLatencySeconds,
            double accuracyPercent)
        {
            if (_disposed)
                return;

            double safeConfidence = Clamp(confidence, 0, 100);
            double safeAccuracy = Clamp(accuracyPercent, 0, 100);

            RunOnUi(delegate
            {
                SetText(_aiStatusText, string.IsNullOrWhiteSpace(status) ? "Active" : status);
                SetText(_aiActionText, string.IsNullOrWhiteSpace(action) ? "لا توجد توصية حالياً" : action);
                SetText(_aiConfidenceText, safeConfidence.ToString("0.0", CultureInfo.InvariantCulture) + "%");
                SetText(_aiDecisionsText, decisions.ToString(CultureInfo.InvariantCulture));
                SetText(_aiActiveText, activeOptimizations.ToString(CultureInfo.InvariantCulture));
                SetText(_aiLatencyText, decisionLatencySeconds.ToString("0.0000", CultureInfo.InvariantCulture) + "s");
                SetText(_aiAccuracyText, safeAccuracy.ToString("0.0", CultureInfo.InvariantCulture) + "%");

                _aiConfidenceBar.Value = safeConfidence;

                _aiStatusText.Foreground = safeConfidence >= 80
                    ? Brush(Green)
                    : Brush(Amber);
            });
        }

        /// <summary>
        /// Updates the latest AI recommendation panel.
        /// </summary>
        public void UpdateRecommendation(
            string recommendation,
            string details,
            double confidence)
        {
            if (_disposed)
                return;

            RunOnUi(delegate
            {
                SetText(_recommendationText,
                    string.IsNullOrWhiteSpace(recommendation)
                        ? "لا توجد توصية"
                        : recommendation);

                SetText(_recommendationDetails,
                    string.IsNullOrWhiteSpace(details)
                        ? "النظام لا يحتاج إلى تدخل إضافي حالياً."
                        : details);

                SetText(
                    _recommendationConfidence,
                    "Ensemble confidence: " +
                    Clamp(confidence, 0, 100).ToString("0.0", CultureInfo.InvariantCulture) +
                    "%");
            });
        }

        /// <summary>
        /// Updates the activity log's latest entry.
        /// </summary>
        public void UpdateActivity(
            string title,
            string details,
            DateTime timestamp)
        {
            if (_disposed)
                return;

            RunOnUi(delegate
            {
                SetText(_activityTitle,
                    string.IsNullOrWhiteSpace(title) ? "لا توجد حركة" : title);

                SetText(_activityDetails,
                    string.IsNullOrWhiteSpace(details) ? "لا توجد بيانات نشاط حالياً." : details);

                SetText(_activityTime, timestamp.ToString("HH:mm:ss"));
            });
        }

        /// <summary>
        /// Updates the integrity comparison values.
        /// </summary>
        public void UpdateIntegrity(
            string cpu,
            string ram,
            string gpu,
            string temperature,
            string fps,
            string health)
        {
            if (_disposed)
                return;

            if (!_integrityHasBaseline)
            {
                _integrityBaselineCpu = cpu;
                _integrityBaselineRam = ram;
                _integrityBaselineGpu = gpu;
                _integrityBaselineTemp = temperature;
                _integrityBaselineFps = fps;
                _integrityBaselineHealth = health;
                _integrityHasBaseline = true;
            }

            RunOnUi(delegate
            {
                SetText(_integrityCpu, cpu);
                SetText(_integrityRam, ram);
                SetText(_integrityGpu, gpu);
                SetText(_integrityTemp, temperature);
                SetText(_integrityFps, fps);
                SetText(_integrityHealth, health);

                if (_integrityOrigCpu != null)
                {
                    SetText(_integrityOrigCpu, _integrityBaselineCpu);
                    SetText(_integrityOrigRam, _integrityBaselineRam);
                    SetText(_integrityOrigGpu, _integrityBaselineGpu);
                    SetText(_integrityOrigTemp, _integrityBaselineTemp);
                    SetText(_integrityOrigFps, _integrityBaselineFps);
                    SetText(_integrityOrigHealth, _integrityBaselineHealth);

                    UpdateIntegrityStatus(_integrityStatusCpu, cpu, _integrityBaselineCpu);
                    UpdateIntegrityStatus(_integrityStatusRam, ram, _integrityBaselineRam);
                    UpdateIntegrityStatus(_integrityStatusGpu, gpu, _integrityBaselineGpu);
                    UpdateIntegrityStatus(_integrityStatusTemp, temperature, _integrityBaselineTemp);
                    UpdateIntegrityStatus(_integrityStatusFps, fps, _integrityBaselineFps);
                    UpdateIntegrityStatus(_integrityStatusHealth, health, _integrityBaselineHealth);
                }
            });
        }

        private void UpdateIntegrityStatus(TextBlock block, string current, string original)
        {
            if (block == null)
                return;

            bool same = CompareTextValues(current, original);

            block.Text = same ? "● مطابقة" : "● مختلفة";
            block.Foreground = Brush(same ? Green : Amber);
        }

        /// <summary>
        /// Updates the FPS stabilizer panel.
        /// </summary>
        public void UpdateFpsStabilizer(
            double currentFps,
            double averageFps,
            double onePercentLow,
            double frameTimeMs,
            double stability,
            int dropCount)
        {
            if (_disposed)
                return;

            RunOnUi(delegate
            {
                SetText(_fpsCurrent, currentFps.ToString("0", CultureInfo.InvariantCulture));
                SetText(_fpsAverage, averageFps.ToString("0", CultureInfo.InvariantCulture));
                SetText(_fpsLow, onePercentLow.ToString("0", CultureInfo.InvariantCulture));
                SetText(_frameTime, frameTimeMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms");
                SetText(_fpsStability, Clamp(stability, 0, 100).ToString("0", CultureInfo.InvariantCulture) + "/100");
                SetText(_fpsDrops, Math.Max(0, dropCount).ToString(CultureInfo.InvariantCulture));
            });
        }

        /// <summary>
        /// Changes the footer mode/status.
        /// </summary>
        public void UpdateFooter(string mode, string status)
        {
            if (_disposed)
                return;

            RunOnUi(delegate
            {
                SetText(_footerMode, string.IsNullOrWhiteSpace(mode) ? "الوضع الذكي" : mode);
                SetText(_footerStatus, string.IsNullOrWhiteSpace(status) ? "المراقبة نشطة" : status);
            });
        }

        /// <summary>
        /// Stops the UI timer. Useful when the containing page is hidden.
        /// </summary>
        public void PauseUpdates()
        {
            if (_uiTimer != null)
                _uiTimer.Stop();
        }

        /// <summary>
        /// Starts the UI timer.
        /// </summary>
        public void ResumeUpdates()
        {
            if (!_disposed && _uiTimer != null)
                _uiTimer.Start();
        }

        #endregion

        #region Layout (responsive)

        private void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyResponsiveColumns();
        }

        private void DashboardView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ApplyResponsiveColumns();
        }

        private void ApplyResponsiveColumns()
        {
            if (_metricsGrid == null)
                return;

            double width = ActualWidth;

            int columns;
            if (width >= 1280) columns = 6;
            else if (width >= 900) columns = 3;
            else if (width >= 560) columns = 2;
            else columns = 1;

            while (_metricsGrid.ColumnDefinitions.Count < columns)
                _metricsGrid.ColumnDefinitions.Add(new ColumnDefinition());

            while (_metricsGrid.ColumnDefinitions.Count > columns)
                _metricsGrid.ColumnDefinitions.RemoveAt(_metricsGrid.ColumnDefinitions.Count - 1);

            for (int i = 0; i < columns; i++)
                _metricsGrid.ColumnDefinitions[i].Width = new GridLength(1, GridUnitType.Star);

            _metricsGrid.RowDefinitions.Clear();
            for (int i = 0; i < (6 + columns - 1) / columns; i++)
                _metricsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            LayoutMetricCards(columns);
        }

        #endregion

        #region Metrics

        private void BuildMetricCards()
        {
            _metricsGrid = new Grid
            {
                Margin = new Thickness(0, 0, 0, 8)
            };

            MetricCard cpu = CreateMetricCard("▣", "المعالج", "0%", "CPU", Blue);
            MetricCard ram = CreateMetricCard("▤", "الذاكرة", "0%", "RAM", PurpleBright);
            MetricCard gpu = CreateMetricCard("▰", "الرسوميات", "0%", "GPU", Green);
            MetricCard temp = CreateMetricCard("♨", "الحرارة", "0.0°C", "TEMP", Red);
            MetricCard fps = CreateMetricCard("FPS", "FPS", "0", "FRAME", Cyan);
            MetricCard health = CreateMetricCard("⌁", "الصحة", "0%", "HEALTH", Purple);

            _cpuValue = cpu.Value;
            _cpuTrend = cpu.Trend;

            _ramValue = ram.Value;
            _ramTrend = ram.Trend;

            _gpuValue = gpu.Value;
            _gpuTrend = gpu.Trend;

            _tempValue = temp.Value;
            _tempTrend = temp.Trend;

            _fpsValue = fps.Value;
            _fpsTrend = fps.Trend;

            _healthValue = health.Value;
            _healthTrend = health.Trend;

            _metricCards = new[]
            {
                cpu.Container,
                ram.Container,
                gpu.Container,
                temp.Container,
                fps.Container,
                health.Container
            };

            for (int i = 0; i < _metricCards.Length; i++)
                _metricsGrid.Children.Add(_metricCards[i]);

            Grid.SetRow(_metricsGrid, 0);
            _root.Children.Add(_metricsGrid);
        }

        private void LayoutMetricCards(int columns)
        {
            for (int i = 0; i < _metricCards.Length; i++)
            {
                Grid.SetColumn(_metricCards[i], i % columns);
                Grid.SetRow(_metricCards[i], i / columns);
            }
        }

        private MetricCard CreateMetricCard(
            string icon,
            string title,
            string value,
            string smallLabel,
            Color accent)
        {
            Border card = Card(Surface, accent, 13);
            card.Margin = new Thickness(3);
            card.Padding = new Thickness(12, 9, 12, 8);

            Grid layout = new Grid();
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });

            Grid top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border iconBox = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(9),
                Background = Brush(WithAlpha(accent, 24)),
                BorderBrush = Brush(WithAlpha(accent, 100)),
                BorderThickness = new Thickness(1)
            };

            TextBlock iconText = MakeText(icon, 12, Brush(accent), FontWeights.Bold);
            iconText.HorizontalAlignment = HorizontalAlignment.Center;
            iconText.VerticalAlignment = VerticalAlignment.Center;
            iconBox.Child = iconText;

            top.Children.Add(iconBox);

            TextBlock small = MakeText(smallLabel, 8, Brush(Text3), FontWeights.Bold);
            small.HorizontalAlignment = HorizontalAlignment.Right;
            small.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetColumn(small, 1);
            top.Children.Add(small);

            layout.Children.Add(top);

            TextBlock valueText = MakeText(value, 22, Brush(accent), FontWeights.SemiBold);
            valueText.Margin = new Thickness(0, 4, 0, 0);

            Grid.SetRow(valueText, 1);
            layout.Children.Add(valueText);

            StackPanel bottom = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                FlowDirection = FlowDirection.RightToLeft
            };

            TextBlock titleText = MakeText(title, 10, Brush(Text2), FontWeights.Normal);
            TextBlock trend = MakeText("—", 9, Brush(Text3), FontWeights.Normal);
            trend.Margin = new Thickness(8, 0, 0, 0);

            bottom.Children.Add(titleText);
            bottom.Children.Add(trend);

            Grid.SetRow(bottom, 2);
            layout.Children.Add(bottom);

            card.Child = layout;

            return new MetricCard
            {
                Container = card,
                Value = valueText,
                Trend = trend
            };
        }

        #endregion

        #region AI

        private void BuildAiSection()
        {
            Border ai = Card(Surface, Purple, 15);
            ai.Margin = new Thickness(3, 0, 3, 8);
            ai.Padding = new Thickness(18, 13, 18, 13);

            Grid aiLayout = new Grid();
            aiLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            aiLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
            aiLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // ---- Left: title + status + current state ----
            StackPanel content = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft
            };

            Grid titleLine = new Grid();
            titleLine.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleLine.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border active = new Border
            {
                Background = Brush(WithAlpha(Green, 25)),
                BorderBrush = Brush(WithAlpha(Green, 110)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(9, 2, 9, 2)
            };

            _aiStatusText = MakeText("Active", 10, Brush(Green), FontWeights.Bold);
            active.Child = _aiStatusText;

            titleLine.Children.Add(active);

            TextBlock title = MakeText("حالة الذكاء الاصطناعي", 15, Brush(Text), FontWeights.Bold);
            title.HorizontalAlignment = HorizontalAlignment.Right;

            // Make the AI header interactive: click to open the AI Agent page.
            var aiHeaderBtn = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(6, 2, 6, 2),
                Content = title
            };
            aiHeaderBtn.Click += delegate { Raise(OpenAiRequested); };
            Grid.SetColumn(aiHeaderBtn, 1);
            titleLine.Children.Add(aiHeaderBtn);

            content.Children.Add(titleLine);

            Grid state = new Grid
            {
                Margin = new Thickness(0, 12, 0, 0)
            };

            state.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            state.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            StackPanel stateLabel = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft
            };

            stateLabel.Children.Add(MakeText("الحالة الحالية", 9, Brush(Text3), FontWeights.Normal));

            _aiActionText = MakeText("لا توجد توصية", 15, Brush(Green), FontWeights.Bold);
            stateLabel.Children.Add(_aiActionText);

            Grid.SetColumn(stateLabel, 1);
            state.Children.Add(stateLabel);

            _aiConfidenceBar = new ProgressBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Height = 5,
                Width = 150,
                Margin = new Thickness(14, 4, 0, 0),
                Foreground = Brush(Green),
                Background = Brush(Surface3)
            };

            Grid.SetColumn(_aiConfidenceBar, 0);
            state.Children.Add(_aiConfidenceBar);

            content.Children.Add(state);

            Grid.SetColumn(content, 0);
            aiLayout.Children.Add(content);

            // ---- Center: confidence ring ----
            Grid visual = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            Ellipse outer = new Ellipse
            {
                Width = 104,
                Height = 104,
                Stroke = Brush(WithAlpha(Purple, 170)),
                StrokeThickness = 2,
                Fill = Brush(WithAlpha(Purple, 8))
            };

            Ellipse inner = new Ellipse
            {
                Width = 76,
                Height = 76,
                Stroke = Brush(WithAlpha(Cyan, 120)),
                StrokeThickness = 1,
                Fill = Brush(WithAlpha(Cyan, 7))
            };

            StackPanel ringText = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _aiConfidenceText = MakeText("0%", 19, Brush(PurpleBright), FontWeights.Bold);
            _aiConfidenceText.HorizontalAlignment = HorizontalAlignment.Center;

            TextBlock ringCaption = MakeText("الثقة", 9, Brush(Text3), FontWeights.Normal);
            ringCaption.HorizontalAlignment = HorizontalAlignment.Center;

            ringText.Children.Add(_aiConfidenceText);
            ringText.Children.Add(ringCaption);

            visual.Children.Add(outer);
            visual.Children.Add(inner);
            visual.Children.Add(ringText);

            Grid.SetColumn(visual, 1);
            aiLayout.Children.Add(visual);

            // ---- Right: recommendation + stats ----
            StackPanel right = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft
            };

            Grid recHead = new Grid();
            recHead.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            recHead.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            TextBlock recTitle = MakeText("آخر توصية", 15, Brush(Text), FontWeights.Bold);
            recHead.Children.Add(recTitle);

            Border recBadge = new Border
            {
                Background = Brush(WithAlpha(Cyan, 22)),
                BorderBrush = Brush(WithAlpha(Cyan, 110)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(9, 2, 9, 2),
                HorizontalAlignment = HorizontalAlignment.Right
            };

            TextBlock recBadgeText = MakeText("SMART", 8, Brush(Cyan), FontWeights.Bold);
            recBadge.Child = recBadgeText;

            Grid.SetColumn(recBadge, 1);
            recHead.Children.Add(recBadge);

            right.Children.Add(recHead);

            _recommendationText = MakeText("لا توجد توصية", 13, Brush(Green), FontWeights.SemiBold);
            _recommendationText.Margin = new Thickness(0, 6, 0, 2);
            right.Children.Add(_recommendationText);

            _recommendationDetails = MakeText(
                "النظام لا يحتاج إلى تدخل إضافي حالياً.",
                10,
                Brush(Text2),
                FontWeights.Normal);
            right.Children.Add(_recommendationDetails);

            _recommendationConfidence = MakeText(
                "Ensemble confidence: 0%",
                10,
                Brush(Cyan),
                FontWeights.Bold);
            _recommendationConfidence.Margin = new Thickness(0, 6, 0, 0);
            right.Children.Add(_recommendationConfidence);

            Grid stats = new Grid
            {
                Margin = new Thickness(0, 10, 0, 0)
            };

            for (int i = 0; i < 4; i++)
                stats.ColumnDefinitions.Add(new ColumnDefinition());

            _aiDecisionsText = AddAiStat(stats, 0, "0", "القرارات المنفذة");
            _aiActiveText = AddAiStat(stats, 1, "0", "التحسينات النشطة");
            _aiLatencyText = AddAiStat(stats, 2, "0s", "زمن القرار");
            _aiAccuracyText = AddAiStat(stats, 3, "0%", "دقة التنبؤ");

            right.Children.Add(stats);

            Grid.SetColumn(right, 2);
            aiLayout.Children.Add(right);

            ai.Child = aiLayout;

            Grid.SetRow(ai, 1);
            _root.Children.Add(ai);
        }

        private TextBlock AddAiStat(Grid parent, int column, string value, string title)
        {
            StackPanel stack = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft
            };

            TextBlock valueText = MakeText(value, 15, Brush(Text), FontWeights.Bold);
            TextBlock titleText = MakeText(title, 9, Brush(Text3), FontWeights.Normal);

            stack.Children.Add(valueText);
            stack.Children.Add(titleText);

            Grid.SetColumn(stack, column);
            parent.Children.Add(stack);

            return valueText;
        }

        #endregion

        #region Activity

        private void BuildActivitySection()
        {
            Border activity = Card(Surface2, BorderSoft, 13);
            activity.Margin = new Thickness(3, 0, 3, 8);
            activity.Padding = new Thickness(14, 9, 14, 9);

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            Border dot = new Border
            {
                Width = 8,
                Height = 8,
                CornerRadius = new CornerRadius(5),
                Background = Brush(Green),
                Margin = new Thickness(0, 5, 10, 0)
            };

            Grid.SetColumn(dot, 0);
            grid.Children.Add(dot);

            Grid detail = new Grid
            {
                VerticalAlignment = VerticalAlignment.Center
            };
            detail.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            detail.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            detail.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detail.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock title = MakeText("سجل النشاط", 12, Brush(Text), FontWeights.Bold);
            title.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(title, 0);
            detail.Children.Add(title);

            _activityTitle = MakeText("لا توجد حركة", 11, Brush(Green), FontWeights.SemiBold);
            _activityTitle.VerticalAlignment = VerticalAlignment.Center;
            _activityTitle.Margin = new Thickness(16, 0, 16, 0);
            _activityTitle.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(_activityTitle, 1);
            detail.Children.Add(_activityTitle);

            _activityDetails = MakeText("لا توجد بيانات نشاط حالياً.", 10, Brush(Text2), FontWeights.Normal);
            _activityDetails.VerticalAlignment = VerticalAlignment.Center;
            _activityDetails.Margin = new Thickness(16, 0, 16, 0);
            _activityDetails.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(_activityDetails, 2);
            detail.Children.Add(_activityDetails);

            _activityTime = MakeText("--:--:--", 9, Brush(Text3), FontWeights.Normal);
            _activityTime.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_activityTime, 3);
            detail.Children.Add(_activityTime);

            Grid.SetColumn(detail, 1);
            grid.Children.Add(detail);

            Button showAll = ActionButton("عرض الكل", 74, false);
            showAll.Height = 28;
            showAll.Click += delegate { Raise(ShowAllActivityRequested); };

            Grid.SetColumn(showAll, 2);
            grid.Children.Add(showAll);

            activity.Child = grid;

            Grid.SetRow(activity, 2);
            _root.Children.Add(activity);
        }

        #endregion

        #region Integrity + FPS

        private void BuildIntegrityAndFpsSection()
        {
            Grid row = new Grid
            {
                Margin = new Thickness(0, 0, 0, 8)
            };

            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(300) });

            Border radarCard = Card(Surface, BorderSoft, 15);
            radarCard.Margin = new Thickness(3);
            radarCard.Padding = new Thickness(14, 10, 14, 10);

            Grid radarLayout = new Grid();
            radarLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            radarLayout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(190) });

            StackPanel radarTitle = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                VerticalAlignment = VerticalAlignment.Center
            };

            Grid radarHeading = new Grid();
            radarHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            radarHeading.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Button test = ActionButton("تشغيل الاختبار", 104, true);
            test.Height = 28;
            test.Click += delegate { Raise(RunIntegrityTestRequested); };
            radarHeading.Children.Add(test);

            StackPanel headingText = new StackPanel
            {
                HorizontalAlignment = HorizontalAlignment.Right,
                FlowDirection = FlowDirection.RightToLeft
            };

            headingText.Children.Add(MakeText("رادار الأداء وسلامة القيم", 15, Brush(Text), FontWeights.Bold));
            headingText.Children.Add(MakeText("منظور 6 محاور + مقارنة الحالة بالمصدر القياسي", 9, Brush(Text3), FontWeights.Normal));

            Grid.SetColumn(headingText, 1);
            radarHeading.Children.Add(headingText);

            radarTitle.Children.Add(radarHeading);

            Grid tableHeader = CreateIntegrityHeader();
            tableHeader.Margin = new Thickness(0, 9, 0, 2);
            radarTitle.Children.Add(tableHeader);

            _integrityCpu = AddIntegrityRow(radarTitle, "المعالج", "0%", FormatValue(0));
            _integrityRam = AddIntegrityRow(radarTitle, "الذاكرة", "0%", FormatValue(1));
            _integrityGpu = AddIntegrityRow(radarTitle, "الرسوميات", "0%", FormatValue(2));
            _integrityTemp = AddIntegrityRow(radarTitle, "الحرارة", "0°C", FormatValue(3));
            _integrityFps = AddIntegrityRow(radarTitle, "FPS", "0", FormatValue(4));
            _integrityHealth = AddIntegrityRow(radarTitle, "الصحة", "0%", FormatValue(5));

            Grid.SetColumn(radarTitle, 0);
            radarLayout.Children.Add(radarTitle);

            _radar = new RadarChart
            {
                Height = 150,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            Grid.SetColumn(_radar, 1);
            radarLayout.Children.Add(_radar);

            radarCard.Child = radarLayout;

            Grid.SetColumn(radarCard, 0);
            row.Children.Add(radarCard);

            Border fps = Card(Surface, Cyan, 15);
            fps.Margin = new Thickness(3);
            fps.Padding = new Thickness(14, 10, 14, 10);

            StackPanel fpsContent = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft
            };

            TextBlock fpsTitle = MakeText("AI FPS Stabilizer", 14, Brush(Text), FontWeights.Bold);
            fpsContent.Children.Add(fpsTitle);

            fpsContent.Children.Add(MakeText("تحسين ثبات FPS", 10, Brush(Text3), FontWeights.Normal));

            Grid fpsGrid = new Grid
            {
                Margin = new Thickness(0, 8, 0, 0)
            };

            for (int i = 0; i < 3; i++)
                fpsGrid.ColumnDefinitions.Add(new ColumnDefinition());

            fpsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            fpsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _fpsCurrent = AddFpsRow(fpsGrid, 0, 0, "FPS الحالي", "0", Cyan);
            _fpsAverage = AddFpsRow(fpsGrid, 1, 0, "متوسط FPS", "0", Blue);
            _fpsLow = AddFpsRow(fpsGrid, 2, 0, "1% Low FPS", "0", Amber);
            _frameTime = AddFpsRow(fpsGrid, 0, 1, "Frame Time", "0.0 ms", PurpleBright);
            _fpsStability = AddFpsRow(fpsGrid, 1, 1, "الاستقرار", "0/100", Green);
            _fpsDrops = AddFpsRow(fpsGrid, 2, 1, "عدد الهبوط", "0", Purple);

            fpsContent.Children.Add(fpsGrid);

            fps.Child = fpsContent;

            Grid.SetColumn(fps, 1);
            row.Children.Add(fps);

            Grid.SetRow(row, 3);
            _root.Children.Add(row);
        }

        private string FormatValue(int index)
        {
            return "--";
        }

        private Grid CreateIntegrityHeader()
        {
            Grid grid = new Grid
            {
                FlowDirection = FlowDirection.RightToLeft
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            AddGridText(grid, "القيمة", 0, 9, Text3, FontWeights.Bold);
            AddGridText(grid, "الحالية", 1, 9, Text3, FontWeights.Bold);
            AddGridText(grid, "الأصلية", 2, 9, Text3, FontWeights.Bold);
            AddGridText(grid, "الحالة", 3, 9, Text3, FontWeights.Bold);

            return grid;
        }

        private TextBlock AddIntegrityRow(
            Panel parent,
            string name,
            string current,
            string original)
        {
            Grid grid = new Grid
            {
                Height = 23,
                FlowDirection = FlowDirection.RightToLeft
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            AddGridText(grid, name, 0, 10, Text2, FontWeights.Normal);

            TextBlock valueText = AddGridText(
                grid,
                current,
                1,
                10,
                Text,
                FontWeights.SemiBold);

            TextBlock origText = AddGridText(grid, original, 2, 10, Text3, FontWeights.Normal);

            bool same = CompareTextValues(current, original);
            TextBlock statusText = AddGridText(
                grid,
                same ? "● مطابقة" : "● مختلفة",
                3,
                9,
                same ? Green : Amber,
                FontWeights.Bold);

            if (name == "المعالج") { _integrityOrigCpu = origText; _integrityStatusCpu = statusText; }
            else if (name == "الذاكرة") { _integrityOrigRam = origText; _integrityStatusRam = statusText; }
            else if (name == "الرسوميات") { _integrityOrigGpu = origText; _integrityStatusGpu = statusText; }
            else if (name == "الحرارة") { _integrityOrigTemp = origText; _integrityStatusTemp = statusText; }
            else if (name == "FPS") { _integrityOrigFps = origText; _integrityStatusFps = statusText; }
            else if (name == "الصحة") { _integrityOrigHealth = origText; _integrityStatusHealth = statusText; }

            parent.Children.Add(grid);
            return valueText;
        }

        private static bool CompareTextValues(string current, string original)
        {
            if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(original))
                return true;

            if (current == "--" || current == "N/A" || original == "--" || original == "N/A")
                return true;

            double c = ParseNumber(current);
            double o = ParseNumber(original);

            if (double.IsNaN(c) || double.IsNaN(o))
                return true;

            return Math.Abs(c - o) < 0.6;
        }

        private static double ParseNumber(string text)
        {
            string s = text.Replace("%", "").Replace("°C", "").Replace(" ms", "").Trim();

            if (s == "--" || s == "N/A")
                return double.NaN;

            double v;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                return v;

            return double.NaN;
        }

        private TextBlock AddFpsRow(
            Panel parent,
            int column,
            int row,
            string title,
            string value,
            Color accent)
        {
            Border cell = new Border
            {
                Background = Brush(WithAlpha(Surface3, 130)),
                BorderBrush = Brush(BorderSoft),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(3),
                Padding = new Thickness(9, 7, 9, 7),
                MinHeight = 40
            };

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock label = MakeText(title, 10, Brush(Text3), FontWeights.Normal);
            label.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(label);

            TextBlock valueText = MakeText(value, 14, Brush(accent), FontWeights.Bold);
            valueText.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(valueText, 1);
            grid.Children.Add(valueText);

            cell.Child = grid;

            Grid.SetColumn(cell, column);
            Grid.SetRow(cell, row);
            parent.Children.Add(cell);

            return valueText;
        }

        #endregion

        #region Quick Actions

        private void BuildQuickActions()
        {
            Border container = Card(Surface, BorderSoft, 15);
            container.Margin = new Thickness(3, 0, 3, 8);
            container.Padding = new Thickness(14, 10, 14, 10);

            StackPanel root = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft
            };

            root.Children.Add(MakeText("إجراءات سريعة", 14, Brush(Text), FontWeights.Bold));
            root.Children.Add(MakeText("أدوات سريعة لتحسين النظام", 9, Brush(Text3), FontWeights.Normal));

            Grid grid = new Grid
            {
                Margin = new Thickness(0, 10, 0, 0)
            };

            for (int i = 0; i < 5; i++)
                grid.ColumnDefinitions.Add(new ColumnDefinition());

            Button protection = QuickAction("🛡", "حماية النظام", "تأمين واستقرار النظام", Green);
            Button startup = QuickAction("◉", "إدارة بدء التشغيل", "إدارة برامج بدء التشغيل", Purple);
            Button game = QuickAction("⌁", "تحسين الألعاب", "تحسين أداء الألعاب", Cyan);
            Button memory = QuickAction("▣", "تنظيف الذاكرة", "تحرير الذاكرة المؤقتة", Blue);
            Button full = QuickAction("🚀", "تحسين شامل", "تحسين النظام بالكامل", PurpleBright);

            protection.Click += delegate { Raise(SystemProtectionRequested); };
            startup.Click += delegate { Raise(StartupManagerRequested); };
            game.Click += delegate { Raise(GameBoostRequested); };
            memory.Click += delegate { Raise(MemoryCleanRequested); };
            full.Click += delegate { Raise(FullOptimizationRequested); };

            AddColumn(grid, protection, 0);
            AddColumn(grid, startup, 1);
            AddColumn(grid, game, 2);
            AddColumn(grid, memory, 3);
            AddColumn(grid, full, 4);

            root.Children.Add(grid);
            container.Child = root;

            Grid.SetRow(container, 4);
            _root.Children.Add(container);
        }

        private Button QuickAction(
            string icon,
            string title,
            string subtitle,
            Color accent)
        {
            Button button = new Button
            {
                Height = 58,
                Margin = new Thickness(3),
                Padding = new Thickness(10),
                Background = Brush(Surface2),
                BorderBrush = Brush(Border),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Stretch
            };

            Grid grid = new Grid
            {
                FlowDirection = FlowDirection.RightToLeft
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            Border iconBox = new Border
            {
                Width = 34,
                Height = 34,
                CornerRadius = new CornerRadius(10),
                Background = Brush(WithAlpha(accent, 24)),
                BorderBrush = Brush(WithAlpha(accent, 110)),
                BorderThickness = new Thickness(1),
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock iconText = MakeText(icon, 14, Brush(accent), FontWeights.Bold);
            iconText.HorizontalAlignment = HorizontalAlignment.Center;
            iconText.VerticalAlignment = VerticalAlignment.Center;
            iconBox.Child = iconText;

            grid.Children.Add(iconBox);

            StackPanel textStack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0),
                FlowDirection = FlowDirection.RightToLeft
            };

            TextBlock titleText = MakeText(title, 11, Brush(Text), FontWeights.SemiBold);
            TextBlock subText = MakeText(subtitle, 8, Brush(Text3), FontWeights.Normal);

            textStack.Children.Add(titleText);
            textStack.Children.Add(subText);

            Grid.SetColumn(textStack, 1);
            grid.Children.Add(textStack);

            button.Content = grid;
            return button;
        }

        #endregion

        #region Footer

        private void BuildFooter()
        {
            Border footer = new Border
            {
                Background = Brush(WithAlpha(Purple, 15)),
                BorderBrush = Brush(WithAlpha(Purple, 90)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(13),
                Margin = new Thickness(3, 0, 3, 0),
                Padding = new Thickness(14, 8, 14, 8)
            };

            Grid grid = new Grid
            {
                FlowDirection = FlowDirection.RightToLeft
            };

            grid.ColumnDefinitions.Add(new ColumnDefinition());
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel mode = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft
            };

            _footerMode = MakeText("الوضع الذكي", 12, Brush(PurpleBright), FontWeights.Bold);
            _footerStatus = MakeText("المراقبة نشطة", 9, Brush(Text3), FontWeights.Normal);

            mode.Children.Add(_footerMode);
            mode.Children.Add(_footerStatus);

            grid.Children.Add(mode);

            StackPanel rightCol = new StackPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                VerticalAlignment = VerticalAlignment.Center
            };

            TextBlock version = MakeText("ClearWin • Smart Optimization", 9, Brush(Text3), FontWeights.Normal);
            version.HorizontalAlignment = HorizontalAlignment.Right;
            rightCol.Children.Add(version);

            Button settingsBtn = ActionButton("الإعدادات", 78, false);
            settingsBtn.Height = 26;
            settingsBtn.Margin = new Thickness(0, 4, 0, 0);
            settingsBtn.Click += delegate { Raise(OpenSettingsRequested); };
            rightCol.Children.Add(settingsBtn);

            Grid.SetColumn(rightCol, 1);
            grid.Children.Add(rightCol);

            footer.Child = grid;

            Grid.SetRow(footer, 5);
            _root.Children.Add(footer);
        }

        #endregion

        #region Timer / Lifecycle

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (_disposed)
                return;

            // Intentionally lightweight.
            // Backend monitoring should push real values through UpdateMetrics()
            // rather than this UI timer polling expensive system APIs.
        }

        private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
        {
            PauseUpdates();
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;

            if (_uiTimer != null)
            {
                _uiTimer.Stop();
                _uiTimer.Tick -= UiTimer_Tick;
            }

            Unloaded -= DashboardView_Unloaded;
        }

        #endregion

        #region Helpers

        private static void RunOnUi(Action action)
        {
            if (action == null)
                return;

            try
            {
                Application app = Application.Current;

                if (app == null || app.Dispatcher == null)
                {
                    action();
                    return;
                }

                if (app.Dispatcher.CheckAccess())
                    action();
                else
                    app.Dispatcher.BeginInvoke(action);
            }
            catch
            {
                // UI update must never crash the optimizer.
            }
        }

        private static void SetText(TextBlock block, string value)
        {
            if (block != null)
                block.Text = value ?? string.Empty;
        }

        private static void Raise(EventHandler handler)
        {
            if (handler == null)
                return;

            try
            {
                handler(null, EventArgs.Empty);
            }
            catch
            {
                // UI event handlers must not bring down the Dashboard.
            }
        }

        private static TextBlock AddGridText(
            Grid grid,
            string text,
            int column,
            double size,
            Color color,
            FontWeight weight)
        {
            TextBlock block = MakeText(text, size, Brush(color), weight);
            block.VerticalAlignment = VerticalAlignment.Center;

            Grid.SetColumn(block, column);
            grid.Children.Add(block);

            return block;
        }

        private static void AddColumn(Grid grid, UIElement element, int column)
        {
            Grid.SetColumn(element, column);
            grid.Children.Add(element);
        }

        private static void AddCell(Grid grid, UIElement element, int column, int row)
        {
            Grid.SetColumn(element, column);
            Grid.SetRow(element, row);
            grid.Children.Add(element);
        }

        private static Border Card(
            Color background,
            Color borderColor,
            double radius)
        {
            return new Border
            {
                Background = Brush(background),
                BorderBrush = Brush(borderColor),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(radius)
            };
        }

        private static Button ActionButton(
            string content,
            double width,
            bool primary)
        {
            Button button = new Button
            {
                Content = content,
                Width = width,
                Height = 40,
                Margin = new Thickness(4),
                Padding = new Thickness(12, 0, 12, 0),
                Foreground = Brush(Text),
                Background = primary
                    ? Brush(WithAlpha(Purple, 30))
                    : Brush(Surface2),
                BorderBrush = primary
                    ? Brush(Purple)
                    : Brush(Border),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };

            return button;
        }

        private static Button IconButton(string content, double size)
        {
            return new Button
            {
                Content = content,
                Width = size,
                Height = size,
                Margin = new Thickness(4),
                Foreground = Brush(Text2),
                Background = Brush(Surface2),
                BorderBrush = Brush(Border),
                BorderThickness = new Thickness(1),
                Cursor = Cursors.Hand
            };
        }

        private static TextBlock MakeText(
            string value,
            double size,
            Brush color,
            FontWeight weight)
        {
            TextBlock block = new TextBlock
            {
                Text = value ?? string.Empty,
                FontSize = size,
                Foreground = color,
                FontWeight = weight,
                FontFamily = new FontFamily("Segoe UI")
            };
            TextOptions.SetTextFormattingMode(block, TextFormattingMode.Display);
            return block;
        }

        private static SolidColorBrush Brush(Color color)
        {
            return new SolidColorBrush(color);
        }

        private static Color WithAlpha(Color color, byte alpha)
        {
            return Color.FromArgb(alpha, color.R, color.G, color.B);
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min)
                return min;

            if (value > max)
                return max;

            return value;
        }

        private static string FormatPercent(double value)
        {
            return value.ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        private static string ResourceCaption(double value, bool inverted)
        {
            if (inverted)
                return value <= 50 ? "جيد" : "مرتفع";

            if (value < 40)
                return "منخفض";

            if (value < 80)
                return "طبيعي";

            return "مرتفع";
        }

        private static string TemperatureCaption(double temperature)
        {
            if (temperature < 60)
                return "طبيعية";

            if (temperature < 80)
                return "مرتفعة";

            return "تحذير";
        }

        private static double TemperatureToScore(double temperature)
        {
            // Converts a temperature reading into a "health-like" score
            // for visualization only. The actual temperature is still shown
            // in Celsius in the metric card.
            if (temperature <= 35)
                return 100;

            if (temperature >= 95)
                return 0;

            return 100 - ((temperature - 35) / 60.0 * 100.0);
        }

        private static double FpsToScore(double fps)
        {
            if (fps <= 0)
                return 0;

            if (fps >= 144)
                return 100;

            return Clamp(fps / 144.0 * 100.0, 0, 100);
        }

        #endregion

        #region Nested Types

        private sealed class MetricCard
        {
            public Border Container;
            public TextBlock Value;
            public TextBlock Trend;
        }

        /// <summary>
        /// Native WPF radar chart. No third-party chart library required.
        /// </summary>
        private sealed class RadarChart : FrameworkElement
        {
            private readonly List<string> _labels = new List<string>
            {
                "CPU",
                "RAM",
                "GPU",
                "حرارة",
                "FPS",
                "الصحة"
            };

            private readonly double[] _values = new double[6];

            public void SetValues(
                double cpu,
                double ram,
                double gpu,
                double temperatureScore,
                double fpsScore,
                double health)
            {
                _values[0] = Clamp(cpu, 0, 100);
                _values[1] = Clamp(ram, 0, 100);
                _values[2] = Clamp(gpu, 0, 100);
                _values[3] = Clamp(temperatureScore, 0, 100);
                _values[4] = Clamp(fpsScore, 0, 100);
                _values[5] = Clamp(health, 0, 100);

                InvalidateVisual();
            }

            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);

                double width = ActualWidth;
                double height = ActualHeight;

                if (width <= 1 || height <= 1)
                    return;

                Point center = new Point(width / 2.0, height / 2.0);
                double radius = Math.Min(width, height) * 0.31;

                Pen gridPen = new Pen(Brush(WithAlpha(Purple, 100)), 1);
                Pen axisPen = new Pen(Brush(WithAlpha(Cyan, 80)), 1);
                Pen dataPen = new Pen(Brush(PurpleBright), 1.7);

                // Background.
                dc.DrawRoundedRectangle(
                    Brush(WithAlpha(Surface2, 160)),
                    new Pen(Brush(BorderSoft), 1),
                    new Rect(0, 0, width, height),
                    12,
                    12);

                // Five rings.
                for (int ring = 1; ring <= 5; ring++)
                {
                    double r = radius * ring / 5.0;
                    StreamGeometry polygon = PolygonGeometry(center, r);

                    dc.DrawGeometry(
                        null,
                        gridPen,
                        polygon);
                }

                // Axes.
                for (int i = 0; i < 6; i++)
                {
                    Point p = PointAt(center, radius, i, 6);

                    dc.DrawLine(axisPen, center, p);

                    DrawLabel(
                        dc,
                        _labels[i],
                        PointAt(center, radius + 18, i, 6));
                }

                // Data polygon.
                StreamGeometry data = new StreamGeometry();

                using (StreamGeometryContext ctx = data.Open())
                {
                    for (int i = 0; i < 6; i++)
                    {
                        double r = radius * _values[i] / 100.0;
                        Point p = PointAt(center, r, i, 6);

                        if (i == 0)
                            ctx.BeginFigure(p, true, true);
                        else
                            ctx.LineTo(p, true, false);
                    }
                }

                dc.DrawGeometry(
                    Brush(WithAlpha(Purple, 35)),
                    dataPen,
                    data);

                // Data points.
                for (int i = 0; i < 6; i++)
                {
                    double r = radius * _values[i] / 100.0;
                    Point p = PointAt(center, r, i, 6);

                    dc.DrawEllipse(
                        Brush(Cyan),
                        null,
                        p,
                        3,
                        3);
                }
            }

            private static StreamGeometry PolygonGeometry(Point center, double radius)
            {
                StreamGeometry geometry = new StreamGeometry();

                using (StreamGeometryContext ctx = geometry.Open())
                {
                    for (int i = 0; i < 6; i++)
                    {
                        Point p = PointAt(center, radius, i, 6);

                        if (i == 0)
                            ctx.BeginFigure(p, false, true);
                        else
                            ctx.LineTo(p, true, false);
                    }
                }

                return geometry;
            }

            private void DrawLabel(
                DrawingContext dc,
                string label,
                Point point)
            {
                FormattedText text = new FormattedText(
                    label,
                    CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    8,
                    Brush(Text2),
                    1.0);

                dc.DrawText(
                    text,
                    new Point(
                        point.X - text.Width / 2.0,
                        point.Y - text.Height / 2.0));
            }

            private static Point PointAt(
                Point center,
                double radius,
                int index,
                int count)
            {
                // Start from top and move clockwise.
                double angle =
                    (-Math.PI / 2.0) +
                    (Math.PI * 2.0 * index / count);

                return new Point(
                    center.X + Math.Cos(angle) * radius,
                    center.Y + Math.Sin(angle) * radius);
            }

            private static SolidColorBrush Brush(Color color)
            {
                return new SolidColorBrush(color);
            }

            private static Color WithAlpha(Color color, byte alpha)
            {
                return Color.FromArgb(alpha, color.R, color.G, color.B);
            }

            private static double Clamp(double value, double min, double max)
            {
                if (value < min)
                    return min;

                if (value > max)
                    return max;

                return value;
            }
        }

        #endregion
    }
}
