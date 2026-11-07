using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClearWin
{
    public partial class MainWindow
    {
        // === Engine page (Decision & Learning Engine) UI state ===
        private TextBlock[] _engineGaugeVals;
        private TextBlock[] _engineGaugeSubs;
        private Border[][] _engineGaugeSegs;   // [gauge][segment] LED strip
        private Brush[] _engineGaugeBrushes;
        private TextBlock _engineConfTxt, _engineConfSub;
        private TextBlock _engineSevTxt, _engineSevSub;
        private TextBlock _engineRelTxt, _engineRelSub;
        private TextBlock _engineDecisionTxt, _engineDecisionSub, _engineDecisionMeta, _engineModeTxt;

        private UIElement BuildEnginePage()
        {
            _engineGaugeVals = new TextBlock[8];
            _engineGaugeSubs = new TextBlock[8];
            _engineGaugeSegs = new Border[8][];
            _engineGaugeBrushes = new Brush[8];

            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel = new StackPanel { Margin = new Thickness(32, 16, 32, 16) };

            // === Title ===
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            titleRow.Children.Add(new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Accent), Margin = new Thickness(0, 4, 12, 4) });
            titleRow.Children.Add(MakeText("المحرك الذكي", 28, Colors.White, true));
            titleRow.Children.Add(MakeText("  Decision & Learning Engine", 14, Theme.TextMuted, false));
            panel.Children.Add(titleRow);
            panel.Children.Add(MakeText("مؤشرات المكونات الثمانية وقرار الحركة الآني — ثقة / خطورة / موثوقية", 13, Color.FromArgb(136, 255, 255, 255), false));
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 16, 0, 8) });

            // === Section 1: Eight component gauges ===
            panel.Children.Add(MakeSectionHeader("\uE9D9", "مكونات النظام", Theme.Cyan));
            var gaugeGrid = new Grid { Margin = new Thickness(10, 0, 10, 8) };
            for (int i = 0; i < 4; i++) gaugeGrid.ColumnDefinitions.Add(new ColumnDefinition());
            gaugeGrid.RowDefinitions.Add(new RowDefinition());
            gaugeGrid.RowDefinitions.Add(new RowDefinition());

            string[] gAr = new[] { "المعالج", "الذاكرة", "الرسوميات", "القرص", "الشبكة", "الحرارة", "البطارية", "الأمان" };
            string[] gEn = new[] { "CPU", "RAM", "GPU", "Disk I/O", "Network", "Thermal", "Battery", "Security" };
            Color[] gClr = new[] { Theme.Blue, Theme.Purple, Theme.Cyan, Theme.Green, Theme.Blue, Theme.Warning, Theme.Green, Theme.Accent };
            for (int i = 0; i < 8; i++)
            {
                var card = MakeEngineGaugeCard(i, gAr[i], gEn[i], gClr[i]);
                Grid.SetRow(card, i / 4);
                Grid.SetColumn(card, i % 4);
                gaugeGrid.Children.Add(card);
            }
            panel.Children.Add(gaugeGrid);

            // === Section 2: Live decision readouts (Confidence / Severity / Reliability) ===
            var decHeader = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 12, 10, 4) };
            decHeader.Children.Add(new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Accent), Margin = new Thickness(0, 3, 12, 3) });
            decHeader.Children.Add(MakeText("قرار الحركة الآني", 16, Theme.TextPrimary, true));
            decHeader.Children.Add(MakeText("  Confidence / Severity / Reliability", 11, Theme.TextMuted, false));
            panel.Children.Add(decHeader);

            var readoutGrid = new Grid();
            for (int i = 0; i < 3; i++) readoutGrid.ColumnDefinitions.Add(new ColumnDefinition());
            var confCard = MakeEngineReadoutCard("الثقة", "Confidence", Theme.Green, out _engineConfTxt, out _engineConfSub);
            var sevCard = MakeEngineReadoutCard("الخطورة", "Severity", Theme.Green, out _engineSevTxt, out _engineSevSub);
            var relCard = MakeEngineReadoutCard("الموثوقية", "Reliability", Theme.Cyan, out _engineRelTxt, out _engineRelSub);
            Grid.SetColumn(confCard, 0);
            Grid.SetColumn(sevCard, 1);
            Grid.SetColumn(relCard, 2);
            readoutGrid.Children.Add(confCard);
            readoutGrid.Children.Add(sevCard);
            readoutGrid.Children.Add(relCard);
            panel.Children.Add(readoutGrid);

            // === Section 3: Current decision card ===
            var decCard = new Border
            {
                Background = UI.GlassFill(),
                CornerRadius = new CornerRadius(18),
                BorderBrush = UI.GlassBorder(),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(20, 14, 20, 14),
                Margin = new Thickness(10, 10, 10, 10),
                Effect = UI.GlassShadow()
            };
            var decStack = new StackPanel();
            var decRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            decRow.Children.Add(new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), Background = new SolidColorBrush(Theme.Accent), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            decRow.Children.Add(MakeText("القرار الحالي", 14.5, Theme.TextPrimary, true));
            _engineModeTxt = MakeText("وضع: --", 10.5, Theme.TextMuted, false);
            _engineModeTxt.Margin = new Thickness(14, 0, 0, 0);
            _engineModeTxt.VerticalAlignment = VerticalAlignment.Center;
            decRow.Children.Add(_engineModeTxt);
            decStack.Children.Add(decRow);

            _engineDecisionTxt = new TextBlock { Text = "بانتظار أول قرار...", FontSize = 16, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.Green) };
            decStack.Children.Add(_engineDecisionTxt);
            _engineDecisionSub = MakeText("لا توجد إجراءات مطلوبة حاليا — المحرك يراقب ويتعلم فقط", 11.5, Theme.TextSecondary, false);
            _engineDecisionSub.Margin = new Thickness(0, 6, 0, 6);
            _engineDecisionSub.TextWrapping = TextWrapping.Wrap;
            decStack.Children.Add(_engineDecisionSub);
            _engineDecisionMeta = MakeText("", 10, Theme.TextMuted, false);
            decStack.Children.Add(_engineDecisionMeta);
            decStack.Children.Add(MakeText("أولوية القرار: 0.35×Confidence + 0.35×Severity + 0.30×Reliability — لا ينفذ أي قرار تلقائيا قبل تخطي العتبات", 9, Theme.TextMuted, false));
            decCard.Child = decStack;
            panel.Children.Add(decCard);

            root.Content = panel;
            return root;
        }

        private Border MakeEngineReadoutCard(string arLabel, string enLabel, Color color, out TextBlock valOut, out TextBlock subOut)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            var card = new Border
            {
                Background = UI.GlassFill(),
                CornerRadius = new CornerRadius(18),
                BorderBrush = UI.GlassBorder(),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(4),
                Effect = UI.GlassShadow()
            };
            var sp = new StackPanel();
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), Background = brush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(MakeText(arLabel, 13.5, Theme.TextPrimary, true));
            row.Children.Add(MakeText("  " + enLabel, 10, Theme.TextMuted, false));
            sp.Children.Add(row);
            valOut = new TextBlock { Text = "--", FontSize = 26, FontWeight = FontWeights.Light, Foreground = brush };
            sp.Children.Add(valOut);
            subOut = MakeText("--", 10, Theme.TextMuted, false);
            subOut.Margin = new Thickness(0, 4, 0, 0);
            sp.Children.Add(subOut);
            card.Child = sp;
            return card;
        }

        private Border MakeEngineGaugeCard(int idx, string arLabel, string enLabel, Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            _engineGaugeBrushes[idx] = brush;

            var card = new Border
            {
                Background = UI.GlassFill(),
                CornerRadius = new CornerRadius(18),
                BorderBrush = UI.GlassBorder(),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(16, 12, 16, 14),
                Margin = new Thickness(4),
                Effect = UI.GlassShadow()
            };
            var sp = new StackPanel();
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            row.Children.Add(new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), Background = brush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(MakeText(arLabel, 13.5, Theme.TextPrimary, true));
            row.Children.Add(MakeText("  " + enLabel, 10, Theme.TextMuted, false));
            sp.Children.Add(row);

            _engineGaugeVals[idx] = new TextBlock { Text = "--", FontSize = 24, FontWeight = FontWeights.Light, Foreground = brush };
            sp.Children.Add(_engineGaugeVals[idx]);
            _engineGaugeSubs[idx] = MakeText("--", 9.5, Theme.TextMuted, false);
            _engineGaugeSubs[idx].Margin = new Thickness(0, 2, 0, 8);
            _engineGaugeSubs[idx].TextTrimming = TextTrimming.CharacterEllipsis;
            _engineGaugeSubs[idx].MaxWidth = 180;
            sp.Children.Add(_engineGaugeSubs[idx]);

            var strip = new StackPanel { Orientation = Orientation.Horizontal };
            _engineGaugeSegs[idx] = new Border[8];
            for (int s = 0; s < 8; s++)
            {
                _engineGaugeSegs[idx][s] = new Border
                {
                    Width = 12,
                    Height = 12,
                    CornerRadius = new CornerRadius(3),
                    Background = Theme.SubCardBrush,
                    Margin = new Thickness(0, 0, s < 7 ? 4 : 0, 0)
                };
                strip.Children.Add(_engineGaugeSegs[idx][s]);
            }
            sp.Children.Add(strip);
            card.Child = sp;
            return card;
        }

        private void SetEngineGauge(int i, double pct, string val, string sub)
        {
            if (_engineGaugeVals == null || i >= _engineGaugeVals.Length || _engineGaugeVals[i] == null) return;
            _engineGaugeVals[i].Text = val;
            _engineGaugeSubs[i].Text = sub;
            double clamped = Math.Max(0.0, Math.Min(100.0, pct));
            int lit = (int)Math.Round(clamped / 100.0 * 8.0);
            for (int s = 0; s < 8; s++)
                _engineGaugeSegs[i][s].Background = (s < lit) ? _engineGaugeBrushes[i] : Theme.SubCardBrush;
        }

        private void UpdateEnginePage()
        {
            if (!_enginePageActive) return;
            if (_engineGaugeVals == null) return;

            // === Gauges: 8 components ===
            SetEngineGauge(0, _realCpu, string.Format("{0:F0}%", _realCpu), "حمل المعالج");
            SetEngineGauge(1, _realRam, string.Format("{0:F0}%", _realRam), _cachedTotalRamGB > 0 ? string.Format("إجمالي {0:F0} GB", _cachedTotalRamGB) : "الذاكرة المستخدمة");
            SetEngineGauge(2, _realGpu, string.Format("{0:F0}%", _realGpu), string.IsNullOrEmpty(_gpuName) ? "معالجة GPU" : _gpuName);
            SetEngineGauge(3, _realDisk, string.Format("{0:F0}%", _realDisk), "قراءة / كتابة");
            double rxMbps = _netRecvSpeed * 8.0 / 1024.0;
            double txMbps = _netSentSpeed * 8.0 / 1024.0;
            SetEngineGauge(4, Math.Min(100.0, rxMbps / 5.0), string.Format("{0:F1} Mbps", rxMbps), string.Format("↑ {0:F1} Mbps", txMbps));
            SetEngineGauge(5, _realTemp * 100.0 / 95.0, string.Format("{0:F0}°C", _realTemp), "حرارة المعالج");
            int batPct = (_agent != null) ? _agent.BatteryPercent : 0;
            if (batPct > 0)
                SetEngineGauge(6, batPct, string.Format("{0}%", batPct), (_agent != null && _agent.IsOnBattery) ? "وضع البطارية" : "الشاحن متصل");
            else
                SetEngineGauge(6, 100.0, "الشاحن", "لا توجد بطارية / سطح مكتب");
            HealthScores hs = (_agent != null) ? _agent.GetHealthScores() : null;
            double secScore = (hs != null) ? hs.Security : 0;
            SetEngineGauge(7, secScore, string.Format("{0:F0}/100", secScore), "حماية النظام");

            // === Agent off: show muted state, no readouts ===
            if (_agent != null && _agent.Mode == AgentMode.Off)
            {
                _engineModeTxt.Text = "وضع: معطل";
                _engineConfTxt.Text = "--";
                _engineConfSub.Text = "الوكيل معطل";
                _engineSevTxt.Text = "--";
                _engineSevSub.Text = "بلا مراقبة";
                _engineRelTxt.Text = "--";
                _engineRelSub.Text = "بلا قرارات";
                _engineDecisionTxt.Text = "الوكيل الذكي معطل";
                _engineDecisionTxt.Foreground = new SolidColorBrush(Theme.TextMuted);
                _engineDecisionSub.Text = "لا توجد مراقبة ولا قرارات — فعّل الوكيل من صفحة Smart Agent";
                _engineDecisionMeta.Text = "";
                return;
            }
            if (_agent != null) _engineModeTxt.Text = "وضع: " + GetModeDisplayName(_agent.Mode);

            // === Readouts: Confidence / Severity / Reliability ===
            EliteDecisionExplanation exp = (_agent != null) ? _agent.GetLastEliteExplanation() : null;
            if (exp != null && exp.ChosenAction != null)
            {
                // Confidence: final blended confidence + its three factors
                double conf = (exp.ConfidenceBreakdown != null) ? exp.ConfidenceBreakdown.FinalConfidence : exp.ChosenAction.Confidence;
                _engineConfTxt.Text = string.Format("{0:F0}%", conf * 100);
                _engineConfSub.Text = (exp.ConfidenceBreakdown != null)
                    ? string.Format("حرارة {0:F0}% · موارد {1:F0}% · تاريخ {2:F0}%",
                        exp.ConfidenceBreakdown.ThermalFactor * 100,
                        exp.ConfidenceBreakdown.ResourceFactor * 100,
                        exp.ConfidenceBreakdown.HistoricalFactor * 100)
                    : "العوامل: غير متاحة";

                // Severity: risk level of the current decision (same scale as the explain page)
                double riskPct = exp.ChosenAction.Risk * 100;
                string sevLevel = riskPct > 60 ? "مرتفع" : (riskPct > 30 ? "متوسط" : "منخفض");
                _engineSevTxt.Text = string.Format("{0:F0}%", riskPct);
                _engineSevTxt.Foreground = new SolidColorBrush(riskPct > 60 ? Theme.Warning : (riskPct > 30 ? Theme.Purple : Theme.Green));
                _engineSevSub.Text = "الخطورة: " + sevLevel;

                // Reliability: real execution success rate, falling back to simulation success
                double relPct;
                string relSrc;
                if (_agent.Stats.TotalDecisions > 0)
                {
                    relPct = (double)_agent.Stats.SuccessfulDecisions / _agent.Stats.TotalDecisions * 100.0;
                    relSrc = string.Format("من {0} قرار منفذ", _agent.Stats.TotalDecisions);
                }
                else
                {
                    relPct = _agent.GetAvgSimSuccessRate();
                    relSrc = "من المحاكات";
                }
                _engineRelTxt.Text = string.Format("{0:F0}%", relPct);
                _engineRelSub.Text = relSrc;

                // Current decision + reason
                string actionName = string.IsNullOrEmpty(exp.ChosenAction.Description)
                    ? GetArabicCategory(exp.ChosenAction.Category)
                    : exp.ChosenAction.Description;
                _engineDecisionTxt.Text = (exp.ChosenAction.Executed ? "تم التنفيذ: " : "مقترح (بلا تنفيذ تلقائي): ") + actionName;
                _engineDecisionTxt.Foreground = new SolidColorBrush(Theme.TextPrimary);
                string reason = exp.ChosenAction.Reason ?? "";
                if (reason.Length > 160) reason = reason.Substring(0, 160) + "...";
                _engineDecisionSub.Text = reason.Length > 0 ? "السبب: " + reason : "القرار مبني على حالة النظام الحالية";
                _engineDecisionMeta.Text = string.Format("{0} · المصدر: {1} · البدائل المرفوضة: {2}",
                    exp.Timestamp.ToLocalTime().ToString("HH:mm:ss"),
                    string.IsNullOrEmpty(exp.ChosenAction.Source) ? "--" : exp.ChosenAction.Source,
                    exp.Alternatives != null ? exp.Alternatives.Count : 0);
            }
            else
            {
                _engineConfTxt.Text = "--";
                _engineConfSub.Text = "بانتظار أول قرار";
                _engineSevTxt.Text = "--";
                _engineSevTxt.Foreground = new SolidColorBrush(Theme.Green);
                _engineSevSub.Text = "لا يوجد";
                _engineRelTxt.Text = (_agent != null) ? string.Format("{0:F0}%", _agent.GetAvgSimSuccessRate()) : "--";
                _engineRelSub.Text = "من المحاكات";
                _engineDecisionTxt.Text = "بانتظار أول قرار...";
                _engineDecisionTxt.Foreground = new SolidColorBrush(Theme.Green);
                _engineDecisionSub.Text = "لا توجد إجراءات مطلوبة حاليا — المحرك يراقب ويتعلم فقط";
                _engineDecisionMeta.Text = "";
            }
        }
    }
}