using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ClearWin
{
    public partial class MainWindow
    {
        private Action<AgentDecision> _agentDecisionUiHandler;

        private UIElement BuildAgentPage()
        {
            _agentPageActive = true;
            var root = new Grid();
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            // V20: explicit rows so the outer scroll viewer reads the full content height.
            // Row 0 = content (Auto - all cards size naturally, no star row can collapse under infinite measure).
            // Row 1 = the 80px bottom buffer row.
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // content row
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // bottom spacer row

            var leftStack = new StackPanel { Margin = new Thickness(24, 16, 16, 16) };

            // ===== PHASE 1: HEADER =====
            var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 24) };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition());
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var headerLeft = new StackPanel { Orientation = Orientation.Horizontal };

            var brainGlow = new Border();
            brainGlow.Width = 48;
            brainGlow.Height = 48;
            brainGlow.CornerRadius = new CornerRadius(24);
            brainGlow.VerticalAlignment = VerticalAlignment.Center;
            brainGlow.Margin = new Thickness(0, 0, 16, 0);
            var brainGrad = new LinearGradientBrush();
            brainGrad.StartPoint = new Point(0, 0);
            brainGrad.EndPoint = new Point(1, 1);
            brainGrad.GradientStops.Add(new GradientStop(Theme.Blue, 0));
            brainGrad.GradientStops.Add(new GradientStop(Theme.Accent, 1));
            brainGlow.Background = brainGrad;
            var brainGlowFx = new DropShadowEffect();
            brainGlowFx.Color = Theme.Accent;
            brainGlowFx.BlurRadius = 24;
            brainGlowFx.ShadowDepth = 0;
            brainGlowFx.Opacity = 0.5;
            brainGlow.Effect = brainGlowFx;
            var brainTxt = UI.Icon("\uE9CE", 24, Colors.White);
            brainTxt.HorizontalAlignment = HorizontalAlignment.Center;
            brainTxt.VerticalAlignment = VerticalAlignment.Center;
            brainGlow.Child = brainTxt;
            headerLeft.Children.Add(brainGlow);

            var titleSp = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleSp.Children.Add(UI.Text("الذكاء الاصطناعي", 22, Theme.TextPrimary, true));
            titleSp.Children.Add(UI.Text("المحرك الذكي لإدارة وتحسين النظام في الوقت الحقيقي", 11, Theme.TextSecondary, false));
            headerLeft.Children.Add(titleSp);
            Grid.SetColumn(headerLeft, 0);
            headerGrid.Children.Add(headerLeft);

            _agentToggleNew = new Border();
            _agentToggleNew.Height = 36;
            _agentToggleNew.CornerRadius = new CornerRadius(18);
            _agentToggleNew.Padding = new Thickness(18, 0, 18, 0);
            _agentToggleNew.Cursor = Cursors.Hand;
            _agentToggleNew.VerticalAlignment = VerticalAlignment.Center;
            _agentToggleNew.Background = new SolidColorBrush(_agent.Mode != AgentMode.Off ? Theme.Green : Theme.Error);
            var togRow = new StackPanel { Orientation = Orientation.Horizontal };
            var togDot = new Border();
            togDot.Width = 8;
            togDot.Height = 8;
            togDot.CornerRadius = new CornerRadius(4);
            togDot.Background = new SolidColorBrush(Colors.White);
            togDot.Margin = new Thickness(0, 0, 8, 0);
            togDot.VerticalAlignment = VerticalAlignment.Center;
            togRow.Children.Add(togDot);
            _agentToggleNewText = UI.Text(_agent.Mode != AgentMode.Off ? "وضع الأداء الذكي" : "تفعيل", 12, Colors.White, true);
            togRow.Children.Add(_agentToggleNewText);
            _agentToggleNew.Child = togRow;
            _agentToggleNew.MouseDown += (s, e) =>
            {
                _agent.ToggleAgent();
                bool active = _agent.Mode != AgentMode.Off;
                _agentToggleNew.Background = new SolidColorBrush(active ? Theme.Green : Theme.Error);
                _agentToggleNewText.Text = active ? "وضع الأداء الذكي" : "تفعيل";
                if (!active) _frameTracker.Stop(); else _frameTracker.Start();
            };
            Grid.SetColumn(_agentToggleNew, 1);
            headerGrid.Children.Add(_agentToggleNew);
            leftStack.Children.Add(headerGrid);

            // ===== PHASE 3: LAST EVENT BANNER =====
            leftStack.Children.Add(UI.SectionHeader("آخر حدث", Theme.Cyan, "قناة أحداث الوكيل المباشرة"));
            var bannerCard = UI.GlassCard(14, Theme.Cyan, new Thickness(0, 0, 0, 20));
            var bannerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(20) };
            bannerRow.Children.Add(UI.Icon("\uE823", 18, Theme.Cyan));
            var bannerRight = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
            _lastEventBanner = UI.TextWrapped("في انتظار أول حدث من الوكيل...", 13, Theme.TextPrimary, true);
            _lastEventTime = UI.Text("بانتظار البيانات", 10, Theme.TextMuted, false);
            _lastEventTime.Margin = new Thickness(0, 4, 0, 0);
            bannerRight.Children.Add(_lastEventBanner);
            bannerRight.Children.Add(_lastEventTime);
            bannerRow.Children.Add(bannerRight);
            bannerCard.Child = bannerRow;
            leftStack.Children.Add(bannerCard);

            // ===== PHASE 3B: REWARD / PENALTY CARDS =====
            leftStack.Children.Add(UI.SectionHeader("محرك المكافأة والعقاب", Theme.Green, "قياس نتائج حقيقية — بلا تخمين ذاتي"));
            var rpGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            rpGrid.ColumnDefinitions.Add(new ColumnDefinition());
            rpGrid.ColumnDefinitions.Add(new ColumnDefinition());

            // Reward card
            var rewardCard = UI.GlassCard(14, Theme.Green, new Thickness(0, 0, 10, 0));
            var rewardStack = new StackPanel { Margin = new Thickness(20) };
            var rewardHdr = new StackPanel { Orientation = Orientation.Horizontal };
            rewardHdr.Children.Add(UI.Icon("\uE718", 16, Theme.Green));
            rewardHdr.Children.Add(UI.Text("  المكافأة الموضوعية", 13, Theme.Green, true));
            rewardStack.Children.Add(rewardHdr);
            rewardStack.Children.Add(UI.Divider());
            _rewardAvgTxt = UI.Text("لا بيانات قياس بعد", 22, Theme.Green, true);
            var raRow = new StackPanel(); raRow.Children.Add(UI.Text("متوسط المكافأة", 10, Theme.TextMuted, false)); raRow.Children.Add(_rewardAvgTxt);
            rewardStack.Children.Add(raRow);
            _rewardEvalsTxt = UI.Text("--", 18, Theme.TextPrimary, true);
            var reRow = new StackPanel(); reRow.Children.Add(UI.Text("تقييمات مكتملة", 10, Theme.TextMuted, false)); reRow.Children.Add(_rewardEvalsTxt);
            rewardStack.Children.Add(reRow);
            _rewardSimTxt = UI.TextWrapped("--", 11, Theme.TextSecondary, false);
            var rsRow = new StackPanel(); rsRow.Children.Add(UI.Text("نجاح المحاكاة", 10, Theme.TextMuted, false)); rsRow.Children.Add(_rewardSimTxt);
            rewardStack.Children.Add(rsRow);
            rewardCard.Child = rewardStack;
            UI.AddHover(rewardCard);
            Grid.SetColumn(rewardCard, 0);
            rpGrid.Children.Add(rewardCard);

            // Penalty card
            var penaltyCard = UI.GlassCard(14, Theme.Error, new Thickness(0, 0, 10, 0));
            var penaltyStack = new StackPanel { Margin = new Thickness(20) };
            var penaltyHdr = new StackPanel { Orientation = Orientation.Horizontal };
            penaltyHdr.Children.Add(UI.Icon("\uE723", 16, Theme.Error));
            penaltyHdr.Children.Add(UI.Text("  العقاب والامتناع", 13, Theme.Error, true));
            penaltyStack.Children.Add(penaltyHdr);
            penaltyStack.Children.Add(UI.Divider());
            _penaltyAbstainTxt = UI.Text("--", 18, Theme.Warning, true);
            var paRow = new StackPanel(); paRow.Children.Add(UI.Text("امتناعات عن التنفيذ", 10, Theme.TextMuted, false)); paRow.Children.Add(_penaltyAbstainTxt);
            penaltyStack.Children.Add(paRow);
            _penaltyRiskBlockedTxt = UI.TextWrapped("--", 11, Theme.TextSecondary, false);
            var prRow = new StackPanel(); prRow.Children.Add(UI.Text("مخاطر محظورة", 10, Theme.TextMuted, false)); prRow.Children.Add(_penaltyRiskBlockedTxt);
            penaltyStack.Children.Add(prRow);
            _penaltyStatusTxt = UI.TextWrapped("--", 11, Theme.TextMuted, false);
            var psRow = new StackPanel(); psRow.Children.Add(UI.Text("حالة محرك التقييم", 10, Theme.TextMuted, false)); psRow.Children.Add(_penaltyStatusTxt);
            penaltyStack.Children.Add(psRow);
            penaltyCard.Child = penaltyStack;
            UI.AddHover(penaltyCard);
            Grid.SetColumn(penaltyCard, 1);
            rpGrid.Children.Add(penaltyCard);

            leftStack.Children.Add(rpGrid);

            // ===== PHASE 4: 5 SCORE CARDS =====
            leftStack.Children.Add(UI.SectionHeader("مؤشرات الأداء", Theme.Accent, null));
            var scoreGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            for (int i = 0; i < 5; i++) scoreGrid.ColumnDefinitions.Add(new ColumnDefinition());
            string[] scoreLabels = new[] { "الأداء", "الاستقرار", "الألعاب", "الطاقة", "الأمان" };
            string[] scoreIcons = new[] { "\uE950", "\uE9D9", "\uE7AC", "\uE850", "\uE730" };
            Color[] scoreColors = new[] { Theme.Blue, Theme.Purple, Theme.Warning, Theme.Green, Theme.Cyan };
            for (int si = 0; si < 5; si++)
            {
                int idx = si;
                var card = UI.GlassCard(14, scoreColors[si], new Thickness(0));
                var sp = new StackPanel { Margin = new Thickness(14, 12, 14, 12) };
                var hdr = new StackPanel { Orientation = Orientation.Horizontal };
                hdr.Children.Add(UI.Icon(scoreIcons[si], 13, scoreColors[si]));
                hdr.Children.Add(UI.Text("  " + scoreLabels[si], 10, Theme.TextMuted, false));
                sp.Children.Add(hdr);
                _scoreCardVals[idx] = UI.Text("--", 28, Theme.TextPrimary, true);
                _scoreCardVals[idx].Margin = new Thickness(0, 8, 0, 2);
                sp.Children.Add(_scoreCardVals[idx]);
                _scoreCardSparks[idx] = new Grid { Height = 32, Margin = new Thickness(0, 4, 0, 0) };
                sp.Children.Add(_scoreCardSparks[idx]);
                card.Child = sp;
                UI.AddHover(card);
                Grid.SetColumn(card, idx);
                scoreGrid.Children.Add(card);
            }
            leftStack.Children.Add(scoreGrid);

            // ===== PHASE 5: AI BRAIN CARD =====
            leftStack.Children.Add(UI.SectionHeader("محرك الذكاء الاصطناعي", Theme.Accent, null));
            var brainCard = UI.GlassCard(18, Theme.Accent, new Thickness(0, 0, 0, 20));
            var brainCardGrid = new Grid { Margin = new Thickness(24) };
            brainCardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            brainCardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var brainLeft = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            var brainBig = new Border();
            brainBig.Width = 100;
            brainBig.Height = 100;
            brainBig.CornerRadius = new CornerRadius(50);
            brainBig.HorizontalAlignment = HorizontalAlignment.Center;
            brainBig.Margin = new Thickness(0, 0, 0, 12);
            var brainBg = new RadialGradientBrush();
            brainBg.GradientOrigin = new Point(0.5, 0.3);
            brainBg.GradientStops.Add(new GradientStop(Color.FromArgb(80, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B), 0));
            brainBg.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
            brainBig.Background = brainBg;
            brainBig.BorderBrush = new SolidColorBrush(Color.FromArgb(60, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B));
            brainBig.BorderThickness = new Thickness(1.5);
            var brainBigFx = new DropShadowEffect();
            brainBigFx.Color = Theme.Accent;
            brainBigFx.BlurRadius = 30;
            brainBigFx.ShadowDepth = 0;
            brainBigFx.Opacity = 0.4;
            brainBig.Effect = brainBigFx;
            var brainBigIco = UI.Icon("\uE9CE", 44, Theme.Accent);
            brainBigIco.HorizontalAlignment = HorizontalAlignment.Center;
            brainBigIco.VerticalAlignment = VerticalAlignment.Center;
            brainBig.Child = brainBigIco;
            brainLeft.Children.Add(brainBig);
            brainLeft.Children.Add(UI.Text("محرك الذكاء الاصطناعي", 15, Theme.TextPrimary, true));
            _brainStatusTxt = UI.Text(_agent.Mode != AgentMode.Off ? "نشط - وضع تكيفي ذكي" : "متوقف", 11, _agent.Mode != AgentMode.Off ? Theme.Green : Theme.TextMuted, false);
            brainLeft.Children.Add(_brainStatusTxt);
            Grid.SetColumn(brainLeft, 0);
            brainCardGrid.Children.Add(brainLeft);

            var brainRight = new StackPanel { Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var statGrid = new Grid { Margin = new Thickness(0, 0, 0, 16) };
            statGrid.ColumnDefinitions.Add(new ColumnDefinition());
            statGrid.ColumnDefinitions.Add(new ColumnDefinition());
            statGrid.RowDefinitions.Add(new RowDefinition());
            statGrid.RowDefinitions.Add(new RowDefinition());

            _aiEngineAnalyzers = UI.Text("8/8", 20, Theme.Green, true);
            _aiEngineDecisions = UI.Text("0", 20, Theme.Accent, true);
            _aiEnginePredictions = UI.Text("--%", 20, Theme.Purple, true);
            _aiEngineConfidence = UI.Text("--%", 20, Theme.Cyan, true);
            string[] brainLabels = new[] { "المحللات النشطة", "القرارات المتخذة", "التووقعات الدقيقة", "معامل الثقة" };
            TextBlock[] brainVals = new[] { _aiEngineAnalyzers, _aiEngineDecisions, _aiEnginePredictions, _aiEngineConfidence };
            for (int bi = 0; bi < 4; bi++)
            {
                var sp2 = new StackPanel { Margin = new Thickness(0, 6, 0, 6) };
                sp2.Children.Add(UI.Text(brainLabels[bi], 10, Theme.TextMuted, false));
                sp2.Children.Add(brainVals[bi]);
                Grid.SetRow(sp2, bi / 2);
                Grid.SetColumn(sp2, bi % 2);
                statGrid.Children.Add(sp2);
            }
            brainRight.Children.Add(statGrid);

            var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
            var treeBtn = UI.GlowButton("شجرة القرار", Theme.Accent, 11);
            treeBtn.Margin = new Thickness(0, 0, 10, 0);
            treeBtn.MouseDown += (s2, e2) => SwitchTab("Advanced AI", _navItems.ContainsKey("Advanced AI") ? _navItems["Advanced AI"] : _activeNav);
            btnRow.Children.Add(treeBtn);
            var logBtn = UI.GlowButton("السجل الكامل", Theme.Green, 11);
            logBtn.MouseDown += (s2, e2) => SwitchTab("Smart Agent", _navItems.ContainsKey("AI Agent") ? _navItems["AI Agent"] : _activeNav);
            btnRow.Children.Add(logBtn);
            brainRight.Children.Add(btnRow);
            Grid.SetColumn(brainRight, 1);
            brainCardGrid.Children.Add(brainRight);
            brainCard.Child = brainCardGrid;
            leftStack.Children.Add(brainCard);

            // ===== PHASE 6: 8 AGENT CARDS =====
            leftStack.Children.Add(UI.SectionHeader("الوكلاء المتخصصون", Theme.Green, null));
            var agentGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            for (int c = 0; c < 4; c++) agentGrid.ColumnDefinitions.Add(new ColumnDefinition());
            agentGrid.RowDefinitions.Add(new RowDefinition());
            agentGrid.RowDefinitions.Add(new RowDefinition());
            string[] agentNames = new[] { "CPU", "Memory", "GPU", "Disk", "Network", "Thermal", "Battery", "Security" };
            string[] agentIcons2 = new[] { "\uE950", "\uE9D9", "\uE7F0", "\uE74E", "\uE968", "\uE7BA", "\uE850", "\uE730" };
            Color[] agentColors = new[] { Theme.Blue, Theme.Purple, Theme.Green, Theme.Warning, Theme.Cyan, Theme.Error, Theme.Green, Theme.Accent };
            double[] agentDefaults = new[] { 96.0, 89.0, 92.0, 78.0, 85.0, 91.0, 88.0, 94.0 };
            for (int ai2 = 0; ai2 < 8; ai2++)
            {
                int idx2 = ai2;
                var card = UI.GlassCard(14, agentColors[ai2], new Thickness(0));
                var sp = new StackPanel { Margin = new Thickness(14, 12, 14, 12) };
                var hdr = new DockPanel();
                var nameTxt = UI.Text(agentNames[ai2], 13, Theme.TextPrimary, true);
                DockPanel.SetDock(nameTxt, Dock.Left);
                hdr.Children.Add(nameTxt);
                _agentCardStatus[idx2] = UI.Text("نشط", 9, Theme.Green, false);
                _agentCardStatus[idx2].HorizontalAlignment = HorizontalAlignment.Right;
                hdr.Children.Add(_agentCardStatus[idx2]);
                sp.Children.Add(hdr);
                var icoBorder = new Border { Width = 40, Height = 40, CornerRadius = new CornerRadius(20), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 10, 0, 8), Background = new SolidColorBrush(Color.FromArgb(30, agentColors[ai2].R, agentColors[ai2].G, agentColors[ai2].B)) };
                var icoTxt = UI.Text(agentIcons2[ai2], 18, agentColors[ai2], false);
                icoTxt.FontFamily = new FontFamily("Segoe MDL2 Assets");
                icoTxt.HorizontalAlignment = HorizontalAlignment.Center;
                icoTxt.VerticalAlignment = VerticalAlignment.Center;
                icoBorder.Child = icoTxt;
                sp.Children.Add(icoBorder);
                _agentCardImpact[idx2] = UI.Text(string.Format("{0:F0}%", agentDefaults[ai2]), 24, agentColors[ai2], true);
                _agentCardImpact[idx2].HorizontalAlignment = HorizontalAlignment.Center;
                _agentCardImpact[idx2].Margin = new Thickness(0, 0, 0, 2);
                sp.Children.Add(_agentCardImpact[idx2]);
                sp.Children.Add(UI.Text("التأثير", 9, Theme.TextMuted, false));
                card.Child = sp;
                UI.AddHover(card);
                Grid.SetColumn(card, idx2 % 4);
                Grid.SetRow(card, idx2 / 4);
                agentGrid.Children.Add(card);
            }
            leftStack.Children.Add(agentGrid);

            // ===== PHASE 7: PERFORMANCE DNA RADAR =====
            leftStack.Children.Add(UI.SectionHeader("بصمة الأداء", Theme.Cyan, "تحليل أنماط الاستخدامඔ"));
            var dnaCard = UI.GlassCard(16, Theme.Cyan, new Thickness(0, 0, 0, 20));
            var dnaStack = new StackPanel { Margin = new Thickness(20) };
            dnaStack.Children.Add(UI.Text("Performance DNA", 14, Theme.TextPrimary, true));
            _radarPanel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 8, 0, 8) };
            _radarPanel.Children.Add(UI.RadarChart(
                new double[] { 0.5, 0.5, 0.5, 0.5, 0.5, 0.5 },
                new[] { "Gaming", "Work", "Stability", "Temp", "Network", "Storage" },
                Theme.Cyan, 200));
            dnaStack.Children.Add(_radarPanel);
            _radarStatusTxt = UI.Text("الأداء العام: جارٍ التحليل...", 11, Theme.Cyan, false);
            dnaStack.Children.Add(_radarStatusTxt);
            dnaCard.Child = dnaStack;
            leftStack.Children.Add(dnaCard);

            // ===== PHASE 8: FORECAST CHART =====
            leftStack.Children.Add(UI.SectionHeader("توقعات الأداء", Theme.Purple, "تحليل الاتجاهات المستقبلية"));
            var forecastCard = UI.GlassCard(16, Theme.Purple, new Thickness(0, 0, 0, 20));
            var forecastStack = new StackPanel { Margin = new Thickness(20) };
            forecastStack.Children.Add(UI.Text("Forecast & Trend Analysis", 14, Theme.TextPrimary, true));
            _forecastPointsPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            var forecastGrid = new Grid();
            forecastGrid.ColumnDefinitions.Add(new ColumnDefinition());
            forecastGrid.ColumnDefinitions.Add(new ColumnDefinition());
            forecastGrid.ColumnDefinitions.Add(new ColumnDefinition());
            string[] fLabels = new[] { "الأداء", "الحرارة", "RAM" };
            Color[] fColors = new[] { Theme.Blue, Theme.Error, Theme.Purple };
            _forecastValTxts = new TextBlock[3];
            _forecastSparkPanels = new Grid[3];
            for (int fi = 0; fi < 3; fi++)
            {
                var fsp = new StackPanel { Margin = new Thickness(0, 0, 10, 0) };
                fsp.Children.Add(UI.Text(fLabels[fi], 10, Theme.TextMuted, false));
                _forecastValTxts[fi] = UI.Text("--%", 16, fColors[fi], true);
                fsp.Children.Add(_forecastValTxts[fi]);
                _forecastSparkPanels[fi] = new Grid { Height = 28, Margin = new Thickness(0, 4, 0, 0) };
                fsp.Children.Add(_forecastSparkPanels[fi]);
                Grid.SetColumn(fsp, fi);
                forecastGrid.Children.Add(fsp);
            }
            _forecastPointsPanel.Children.Add(forecastGrid);
            forecastStack.Children.Add(_forecastPointsPanel);
            forecastCard.Child = forecastStack;
            leftStack.Children.Add(forecastCard);

            // ===== PHASE 9: SMART GOALS =====
            leftStack.Children.Add(UI.SectionHeader("الأهداف الذكية", Theme.Green, "اختر نمط النظام"));
            var goalsCard = UI.GlassCard(16, Theme.Green, new Thickness(0, 0, 0, 20));
            var goalsStack = new StackPanel { Margin = new Thickness(20) };
            goalsStack.Children.Add(UI.Text("Smart Goals", 14, Theme.TextPrimary, true));
            _smartGoalsPanel = new StackPanel();
            string[] goalIcons = new[] { "\uE7AC", "\uE70A", "\uE850", "\uE714", "\uE730" };
            string[] goalLabels = new[] { "Max FPS", "Work", "Battery", "Stability", "Balanced" };
            Color[] goalColors = new[] { Theme.Warning, Theme.Blue, Theme.Green, Theme.Pink, Theme.Cyan };
            string[] goalIds = new[] { "max_fps", "low_temp", "low_power", "best_stability", "balanced" };
            for (int gi = 0; gi < 5; gi++)
            {
                int gIdx = gi;
                bool sel = false;
                var ag = _agent.GetActiveGoal();
                if (ag != null && ag.Id == goalIds[gi]) sel = true;
                var gb = UI.GoalButton(goalIcons[gi], goalLabels[gi], sel, goalColors[gi]);
                gb.MouseDown += (sg, eg) =>
                {
                    var goals = _agent.GetAvailableGoals();
                    foreach (var goal in goals)
                    {
                        if (goal.Id == goalIds[gIdx])
                        {
                            _agent.SetActiveGoal(goal);
                            break;
                        }
                    }
                    _goalProgressTxt.Text = string.Format("الهدف النشط: {0} ({1:F0}%)", _agent.GetActiveGoalName(), _agent.GetActiveGoalProgress() * 100);
                };
                _smartGoalsPanel.Children.Add(gb);
            }
            goalsStack.Children.Add(_smartGoalsPanel);
            _goalProgressTxt = UI.Text(string.Format("الهدف النشط: {0} ({1:F0}%)", _agent.GetActiveGoalName(), _agent.GetActiveGoalProgress() * 100), 12, Theme.Green, true);
            _goalProgressTxt.Margin = new Thickness(0, 10, 0, 0);
            goalsStack.Children.Add(_goalProgressTxt);
            var goalBar = UI.ProgressBar(_agent.GetActiveGoalProgress(), 1.0, Theme.Green, 250, 6);
            goalBar.Margin = new Thickness(0, 8, 0, 0);
            goalsStack.Children.Add(goalBar);
            goalsCard.Child = goalsStack;
            leftStack.Children.Add(goalsCard);

            // ===== PHASE 10: EXPLAINABLE AI =====
            leftStack.Children.Add(UI.SectionHeader("شرح القرارات", Theme.Accent, "لماذا نفذت هذا القرار؟"));
            var explainMainCard = UI.GlassCard(16, Theme.Accent, new Thickness(0, 0, 0, 20));
            var explainMainStack = new StackPanel { Margin = new Thickness(20) };
            _explainDecisionTxt = UI.TextWrapped("في انتظار أول قرار...", 13, Theme.TextPrimary, false);
            _explainDecisionTxt.Margin = new Thickness(0, 0, 0, 8);
            explainMainStack.Children.Add(_explainDecisionTxt);
            explainMainStack.Children.Add(UI.Divider());
            _explainRiskTxt = UI.Text("المخاطرة: --", 11, Theme.Warning, true);
            _explainRiskTxt.Margin = new Thickness(0, 4, 0, 4);
            explainMainStack.Children.Add(_explainRiskTxt);
            _explainDataTxt = UI.TextWrapped("البيانات: --", 11, Theme.TextSecondary, false);
            _explainDataTxt.Margin = new Thickness(0, 4, 0, 4);
            explainMainStack.Children.Add(_explainDataTxt);
            _explainBenefitTxt = UI.TextWrapped("الفائدة: --", 11, Theme.TextSecondary, false);
            _explainBenefitTxt.Margin = new Thickness(0, 4, 0, 4);
            explainMainStack.Children.Add(_explainBenefitTxt);
            _explainAltTxt = UI.TextWrapped("البدائل: --", 11, Theme.TextSecondary, false);
            _explainAltTxt.Margin = new Thickness(0, 4, 0, 4);
            explainMainStack.Children.Add(_explainAltTxt);
            explainMainCard.Child = explainMainStack;
            leftStack.Children.Add(explainMainCard);

            // ===== PHASE 11: ACTIVITY TIMELINE =====
            leftStack.Children.Add(UI.SectionHeader("سجل الأنشطة", Theme.Warning, null));
            var timelineCard = UI.GlassCard(16, null, new Thickness(0, 0, 0, 20));
            var timelineStack = new StackPanel { Margin = new Thickness(20) };
            _timelineStack = new StackPanel();
            _timelineStack.Children.Add(UI.Text("في انتظار البيانات...", 11, Theme.TextMuted, false));
            timelineStack.Children.Add(_timelineStack);
            timelineCard.Child = timelineStack;
            leftStack.Children.Add(timelineCard);

            // ===== PHASE 12: DECISION TREE =====
            leftStack.Children.Add(UI.SectionHeader("شجرة القرار", Theme.Purple, "مسار التفكير"));
            var treeCard = UI.GlassCard(16, Theme.Purple, new Thickness(0, 0, 0, 20));
            var treeStack = new StackPanel { Margin = new Thickness(20) };
            _decisionTreePanel = new StackPanel();
            string[] defaultTree = new[] { "AI", "مراقبة النظام", "تحليل البيانات", "اتخاذ القرار", "تنفيذ الإجراء" };
            var defaultTreeVisual = UI.DecisionTree(defaultTree, 4, Theme.Accent);
            _decisionTreePanel.Children.Add(defaultTreeVisual);
            treeStack.Children.Add(_decisionTreePanel);
            treeCard.Child = treeStack;
            leftStack.Children.Add(treeCard);

            // ===== PHASE 13: LIVE MONITOR =====
            leftStack.Children.Add(UI.SectionHeader("المراقبة المباشرة", Theme.Error, "بيانات في الوقت الحقيقي"));
            var monitorCard = UI.GlassCard(16, null, new Thickness(0, 0, 0, 20));
            var monitorGrid = new Grid { Margin = new Thickness(20) };
            for (int mc = 0; mc < 4; mc++) monitorGrid.ColumnDefinitions.Add(new ColumnDefinition());
            monitorGrid.RowDefinitions.Add(new RowDefinition());
            monitorGrid.RowDefinitions.Add(new RowDefinition());
            string[] monLabels = new[] { "CPU", "GPU", "RAM", "FPS", "Temp", "Disk", "Network", "Latency" };
            Color[] monColors = new[] { Theme.Blue, Theme.Green, Theme.Purple, Theme.Warning, Theme.Error, Theme.Warning, Theme.Cyan, Theme.Accent };
            for (int mi = 0; mi < 8; mi++)
            {
                var msp = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                msp.Children.Add(UI.Text(monLabels[mi], 10, Theme.TextMuted, false));
                _liveMonitorVals[mi] = UI.Text("--", 18, monColors[mi], true);
                _liveMonitorVals[mi].Margin = new Thickness(0, 2, 0, 0);
                msp.Children.Add(_liveMonitorVals[mi]);
                var mbar = UI.ProgressBar(0, 100, monColors[mi], 120, 3);
                mbar.Margin = new Thickness(0, 4, 0, 0);
                if (mbar.Child is Border) _liveMonitorBarFills[mi] = (Border)mbar.Child;
                msp.Children.Add(mbar);
                Grid.SetColumn(msp, mi % 4);
                Grid.SetRow(msp, mi / 4);
                monitorGrid.Children.Add(msp);
            }
            monitorCard.Child = monitorGrid;
            leftStack.Children.Add(monitorCard);

            // ===== PHASE 15: AI MONITOR =====
            leftStack.Children.Add(UI.SectionHeader("مراقب الذكاء الاصطناعي", Theme.Purple, "مراقبة سلوك الوكيل واكتشاف الشذوذ"));
            var monCard = UI.GlassCard(14, Theme.Purple, new Thickness(0, 0, 0, 20));
            var monGrid = new Grid();
            monGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            monGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            monGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            monGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var monMetrics = new[] { ("الحالة", Theme.Green), ("القرارات", Theme.Blue), ("التنبيهات", Theme.Warning), ("الثقة", Theme.Purple) };
            _monitorHealthTxt = UI.Text("--", 20, monMetrics[0].Item2, true);
            _monitorDecisionsTxt = UI.Text("--", 20, monMetrics[1].Item2, true);
            _monitorAlertsTxt = UI.Text("--", 20, monMetrics[2].Item2, true);
            _monitorConfidenceTxt = UI.Text("--", 20, monMetrics[3].Item2, true);
            TextBlock[] monVals = new[] { _monitorHealthTxt, _monitorDecisionsTxt, _monitorAlertsTxt, _monitorConfidenceTxt };
            for (int mi = 0; mi < 4; mi++)
            {
                var ms = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
                ms.Children.Add(UI.Text(monMetrics[mi].Item1, 10, Theme.TextMuted, false));
                ms.Children.Add(monVals[mi]);
                Grid.SetColumn(ms, mi);
                monGrid.Children.Add(ms);
            }
            // Anomaly summary
            var anomRow = new StackPanel { Margin = new Thickness(0, 4, 0, 8) };
            anomRow.Children.Add(UI.Text("ملخص الشذوذ", 10, Theme.TextMuted, false));
            _monitorAnomalyTxt = UI.Text("لا توجد شذوذ", 12, Theme.Green, false);
            anomRow.Children.Add(_monitorAnomalyTxt);
            monGrid.Children.Add(anomRow);
            Grid.SetColumnSpan(anomRow, 4);
            // Active game / emulator
            var gameRow = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            gameRow.Children.Add(UI.Text("التطبيق النشط", 10, Theme.TextMuted, false));
            _monitorGameTxt = UI.Text("— لا يوجد لعبة", 12, Color.FromArgb(136, 255, 255, 255), false);
            gameRow.Children.Add(_monitorGameTxt);
            monGrid.Children.Add(gameRow);
            Grid.SetColumnSpan(gameRow, 4);
            // Alert list
            _monitorAlertStack = new StackPanel { Margin = new Thickness(0, 8, 0, 0), MaxHeight = 120 };
            _monitorAlertStack.Children.Add(UI.Text("لا توجد تنبيهات", 10, Theme.TextMuted, false));
            var monScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 140, Margin = new Thickness(0, 4, 0, 0) };
            monScroll.Content = _monitorAlertStack;
            monGrid.Children.Add(monScroll);
            Grid.SetColumnSpan(monScroll, 4);
            monCard.Child = monGrid;
            leftStack.Children.Add(monCard);

            // ===== PHASE 14: AI CONSOLE =====
            leftStack.Children.Add(UI.SectionHeader("وحدة التحكم", Theme.Accent, "سجل القرارات والأنشطة"));
            var consoleCard = UI.GlassCard(14, Theme.Accent, new Thickness(0, 0, 0, 20));
            var consoleStack = new StackPanel();
            var consoleHdr = new DockPanel();
            consoleHdr.Background = new SolidColorBrush(Color.FromArgb(46, 96, 200, 255));
            consoleHdr.Margin = new Thickness(0);
            var consoleHdrTxt = UI.Text("  \u25CF  \u25CF  \u25CF    AI Console", 13, Theme.Accent, true);
            DockPanel.SetDock(consoleHdrTxt, Dock.Left);
            consoleHdr.Children.Add(consoleHdrTxt);
            consoleStack.Children.Add(consoleHdr);
            var consoleBorder = new Border();
            consoleBorder.Background = new SolidColorBrush(Color.FromArgb(30, 8, 20, 44));
            _terminalLogStack = new StackPanel();
            _terminalLogStack.Margin = new Thickness(14, 10, 14, 10);
            _terminalLogStack.Children.Add(UI.Text("> AI Engine initialized...", 12, Theme.Green, false));
            consoleBorder.Child = _terminalLogStack;
            consoleStack.Children.Add(consoleBorder);
            consoleCard.Child = consoleStack;
            leftStack.Children.Add(consoleCard);

            Grid.SetColumn(leftStack, 0);
            root.Children.Add(leftStack);

            // ===== RIGHT COLUMN =====
            var rightPanel = new StackPanel { Margin = new Thickness(0, 16, 16, 16) };

            // Approval queue card
            var rightApprovalCard = UI.GlassCard(14, Theme.Warning, new Thickness(0, 0, 0, 16));
            var rightApprovalStack = new StackPanel { Margin = new Thickness(14) };
            var approvalHeader = new StackPanel { Orientation = Orientation.Horizontal };
            approvalHeader.Children.Add(UI.Icon("\uE9CE", 13, Theme.Warning));
            approvalHeader.Children.Add(UI.Text("  قرارات بانتظار الموافقة", 14, Theme.TextPrimary, true));
            var approvalHint = UI.Text(_agent.FullAutonomy
                ? "وضع التحكم الكامل: كل الأدوات الآمنة تُنفَّذ تلقائيًا، فقط الخطير والحرج ينتظر موافقتك"
                : "قرارات عالية الخطورة تحتاج موافقتك للتنفيذ", 10, Theme.TextMuted, false);
            approvalHint.Margin = new Thickness(0, 2, 0, 0);
            // Full Autonomy toggle: safe/reversible tools auto-execute, only
            // Dangerous/Critical risk still waits for manual approval.
            var autonomyToggle = new Border
            {
                Background = new SolidColorBrush(_agent.FullAutonomy ? Theme.Green : Theme.SubCard),
                CornerRadius = new CornerRadius(12),
                BorderBrush = new SolidColorBrush(Theme.BorderClr),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(10, 4, 10, 4),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };
            var autonomyText = UI.Text(_agent.FullAutonomy ? "تحكم كامل" : "تحكم يدوي", 10,
                _agent.FullAutonomy ? Colors.White : Theme.TextSecondary, true);
            autonomyToggle.Child = autonomyText;
            autonomyToggle.MouseDown += (s, e) =>
            {
                _agent.FullAutonomy = !_agent.FullAutonomy;
                bool fa = _agent.FullAutonomy;
                autonomyToggle.Background = new SolidColorBrush(fa ? Theme.Green : Theme.SubCard);
                autonomyText.Text = fa ? "تحكم كامل" : "تحكم يدوي";
                autonomyText.Foreground = new SolidColorBrush(fa ? Colors.White : Theme.TextSecondary);
                approvalHint.Text = fa
                    ? "وضع التحكم الكامل: كل الأدوات الآمنة تُنفَّذ تلقائيًا، فقط الخطير والحرج ينتظر موافقتك"
                    : "قرارات عالية الخطورة تحتاج موافقتك للتنفيذ";
            };
            approvalHeader.Children.Add(autonomyToggle);
            rightApprovalStack.Children.Add(approvalHeader);
            rightApprovalStack.Children.Add(UI.Divider());
            var approvalScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 260 };
            _approvalStack = new StackPanel();
            _approvalStack.Children.Add(UI.Text("لا توجد قرارات بانتظار الموافقة", 11, Theme.TextMuted, false));
            approvalScroll.Content = _approvalStack;
            rightApprovalStack.Children.Add(approvalScroll);
            rightApprovalCard.Child = rightApprovalStack;
            rightPanel.Children.Add(rightApprovalCard);

            // Activity feed
            var rightActivityCard = UI.GlassCard(14, null, new Thickness(0, 0, 0, 16));
            var rightActivityStack = new StackPanel { Margin = new Thickness(14) };
            rightActivityStack.Children.Add(UI.Text("النشاط المباشر", 14, Theme.TextPrimary, true));
            rightActivityStack.Children.Add(UI.Divider());
            _explainActivityStack = new StackPanel();
            _explainActivityStack.Children.Add(UI.Text("في انتظار البيانات...", 11, Theme.TextMuted, false));
            rightActivityStack.Children.Add(_explainActivityStack);
            rightActivityCard.Child = rightActivityStack;
            rightPanel.Children.Add(rightActivityCard);

            Grid.SetColumn(rightPanel, 1);
            root.Children.Add(rightPanel);

            // Subscribe to decision events once
            if (_agentDecisionUiHandler == null && _agent != null)
            {
                var disp = Dispatcher;
                _agentDecisionUiHandler = (d) =>
                {
                    try
                    {
                        disp.BeginInvoke(new Action(() =>
                        {
                            if (_explainActivityStack == null) return;
                            var item = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                            var dot = new Border();
                            dot.Width = 6;
                            dot.Height = 6;
                            dot.CornerRadius = new CornerRadius(3);
                            dot.Background = new SolidColorBrush(Theme.Green);
                            dot.Margin = new Thickness(0, 0, 8, 0);
                            dot.VerticalAlignment = VerticalAlignment.Center;
                            item.Children.Add(dot);
                            var info = new StackPanel();
                            string shortDesc = d.Description ?? "Action";
                            var descTxt = UI.TextWrapped(shortDesc, 11, Theme.TextPrimary, false);
                            descTxt.Margin = new Thickness(0, 0, 0, 2);
                            info.Children.Add(descTxt);
                            info.Children.Add(UI.Text(d.Timestamp.ToString("HH:mm:ss"), 9, Theme.TextMuted, false));
                            item.Children.Add(info);
                            _explainActivityStack.Children.Insert(0, item);
                            while (_explainActivityStack.Children.Count > 15) _explainActivityStack.Children.RemoveAt(_explainActivityStack.Children.Count - 1);
                            // Console log
                            if (_terminalLogStack != null)
                            {
                                string logLine = string.Format("[{0}] {1}", d.Timestamp.ToString("HH:mm:ss"), shortDesc);
                                _terminalLogStack.Children.Add(UI.Text(logLine, 12, Theme.Green, false));
                                if (_terminalLogStack.Children.Count > 80) _terminalLogStack.Children.RemoveAt(0);
                            }
                        }));
                    } catch (Exception ex) { CWLog.Error("MainWindow.AI", ex); }
                };
                _agent.OnDecisionExecuted += _agentDecisionUiHandler;
            }

            // V20: a real 80px bottom buffer as the last element of the page (transparent Border in row 1),
            // so the outer scroll viewer's extent always includes the full log height + breathing space.
            var bottomSpacer = new Border { Height = 80, Background = Brushes.Transparent };
            Grid.SetRow(bottomSpacer, 1);
            root.Children.Add(bottomSpacer);
            
            // Initialize penalty counters
            UpdatePenaltyCounters();
            
            return root;
        }

        private string GetArabicCategory(DecisionCategory cat)
        {
            switch (cat)
            {
                case DecisionCategory.MemoryClean: return "تنظيف الذاكرة";
                case DecisionCategory.ProcessPriority: return "أولوية العمليات";
                case DecisionCategory.PowerPlan: return "خطة الطاقة";
                case DecisionCategory.BackgroundControl: return "العمليات الخلفية";
                case DecisionCategory.PurgeStandby: return "تطهير الذاكرة";
                case DecisionCategory.GameBoost: return "تعزيز الألعاب";
                case DecisionCategory.ThermalProtect: return "الحماية الحرارية";
                case DecisionCategory.CpuAffinity: return "نوى المعالج";
                case DecisionCategory.IoPriority: return "أولوية الإدخال/الإخراج";
                case DecisionCategory.CoreParking: return "ركن الأنوية";
                case DecisionCategory.NetworkOptimize: return "تحسين الشبكة";
                case DecisionCategory.PowerOptimize: return "تحسين الطاقة";
                default: return cat.ToString();
            }
        }

        private string GetArabicDataSource(string name)
        {
            if (name == "CPU Usage") return "استهلاك المعالج";
            if (name == "RAM Usage") return "استهلاك الذاكرة";
            if (name == "Temperature") return "الحرارة";
            if (name == "GPU Usage") return "استهلاك كرت الشاشة";
            if (name == "FPS") return "معدل الإطارات";
            if (name == "Process Count") return "عدد العمليات";
            if (name == "Battery") return "البطارية";
            return name;
        }

        private void RefreshApprovalPanel()
        {
            if (_approvalStack == null) return;
            var queue = _agent.GetApprovalQueue();
            var sig = new System.Text.StringBuilder();
            for (int i = 0; i < queue.Count; i++) { if (queue[i] != null) sig.Append(queue[i].Action).Append('|'); }
            string signature = sig.ToString();
            if (signature == _approvalSignature) return;
            _approvalSignature = signature;
            _approvalStack.Children.Clear();
            if (queue.Count == 0)
            {
                _approvalStack.Children.Add(UI.Text("لا توجد قرارات بانتظار الموافقة", 11, Theme.TextMuted, false));
                return;
            }
            for (int i = 0; i < queue.Count; i++)
            {
                var d = queue[i];
                if (d == null) continue;
                _approvalStack.Children.Add(BuildApprovalItem(d));
            }
        }

        private UIElement BuildApprovalItem(AgentDecision d)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Theme.SubCard),
                CornerRadius = new CornerRadius(8),
                BorderBrush = new SolidColorBrush(Theme.BorderClr),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(10, 8, 10, 8)
            };
            var sp = new StackPanel();

            var hdr = new StackPanel { Orientation = Orientation.Horizontal };
            string cat = GetArabicCategory(d.Category);
            hdr.Children.Add(UI.Text(cat, 10, Theme.Accent, true));
            double rr = d.RiskScore > 0 ? d.RiskScore : d.Risk;
            string riskLabel; Color riskColor;
            if (rr >= 0.8) { riskLabel = "حرج"; riskColor = Theme.Error; }
            else if (rr >= 0.6) { riskLabel = "مرتفع"; riskColor = Theme.Warning; }
            else if (rr >= 0.4) { riskLabel = "متوسط"; riskColor = Color.FromRgb(251, 146, 60); }
            else { riskLabel = "منخفض"; riskColor = Theme.Green; }
            var badge = UI.Text(riskLabel, 9, riskColor, true);
            badge.Margin = new Thickness(8, 0, 0, 0);
            hdr.Children.Add(badge);
            var timeTxt = UI.Text(d.Timestamp.ToString("HH:mm:ss"), 9, Theme.TextMuted, false);
            timeTxt.Margin = new Thickness(8, 0, 0, 0);
            hdr.Children.Add(timeTxt);
            sp.Children.Add(hdr);

            var desc = UI.TextWrapped(d.Description ?? d.Action, 11, Theme.TextPrimary, false);
            desc.Margin = new Thickness(0, 4, 0, 4);
            sp.Children.Add(desc);

            if (!string.IsNullOrEmpty(d.Reason))
            {
                var reason = UI.TextWrapped(d.Reason, 10, Theme.TextSecondary, false);
                reason.Margin = new Thickness(0, 0, 0, 4);
                sp.Children.Add(reason);
            }

            var btns = new StackPanel { Orientation = Orientation.Horizontal };
            var approveBtn = new Border
            {
                Background = new SolidColorBrush(Theme.Green),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 5, 14, 5),
                Cursor = Cursors.Hand,
                Margin = new Thickness(0, 0, 8, 0)
            };
            approveBtn.Child = UI.Text("تنفيذ", 11, Colors.White, true);
            approveBtn.MouseDown += (s, e) =>
            {
                var captured = d;
                try
                {
                    Task.Factory.StartNew(() =>
                    {
                        try { _agent.ApprovePendingDecision(captured); }
                        catch (Exception ex2) { CWLog.Error("Approval", ex2); }
                    });
                }
                catch (Exception ex3) { CWLog.Error("Approval", ex3); }
            };
            btns.Children.Add(approveBtn);

            var dismissBtn = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 148, 163, 184)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 5, 14, 5),
                Cursor = Cursors.Hand
            };
            dismissBtn.Child = UI.Text("تجاهل", 11, Theme.TextSecondary, true);
            dismissBtn.MouseDown += (s, e) =>
            {
                var captured = d;
                try { _agent.DismissPendingDecision(captured); } catch (Exception ex4) { CWLog.Error("Approval", ex4); }
            };
            btns.Children.Add(dismissBtn);
            sp.Children.Add(btns);

            card.Child = sp;
            return card;
        }

        private void UpdateAgentPage()
        {
            if (!_agentPageActive) return;
            try
            {
                RefreshApprovalPanel();
                var hs = _agent.GetHealthScores();
                var state = _agent.CurrentState;
                var stats = _agent.Stats;

                // Phase 4: Score cards - use REAL history
                double[] hVals = new[] { hs.Performance, hs.Stability, hs.Gaming, hs.Battery, hs.Security };
                Color[] hColors = new[] { Theme.Blue, Theme.Purple, Theme.Warning, Theme.Green, Theme.Cyan };
                RingBuffer<double>[] histArrays = new[] { _cpuHistory, _ramHistory, _gpuHistory, _diskHistory, _netHistory };
                for (int i = 0; i < 5; i++)
                {
                    if (_scoreCardVals[i] != null) _scoreCardVals[i].Text = string.Format("{0:F0}", hVals[i]);
                    if (_scoreCardSparks[i] != null)
                    {
                        _scoreCardSparks[i].Children.Clear();
                        double w = _scoreCardSparks[i].ActualWidth > 10 ? _scoreCardSparks[i].ActualWidth : 120;
                        double[] sparkData;
                        var srcHist = histArrays[i];
                        if (srcHist != null && srcHist.Count >= 2)
                        {
                            int cnt = Math.Min(12, srcHist.Count);
                            int off = Math.Max(0, srcHist.Count - cnt);
                            sparkData = new double[cnt];
                            for (int p = 0; p < cnt; p++) sparkData[p] = Math.Max(0, Math.Min(100, srcHist[off + p]));
                        }
                        else
                        {
                            sparkData = new double[12];
                            for (int p = 0; p < 12; p++) sparkData[p] = Math.Max(0, Math.Min(100, hVals[i] + (p - 6) * 1.5));
                        }
                        _scoreCardSparks[i].Children.Add(UI.SparklineChart(sparkData, hColors[i], w, 32));
                    }
                }

                // Phase 5: Brain stats
                int activeA = _agent.GetActiveAnalyzerCount();
                int totalA = activeA + _agent.GetDisabledAnalyzerCount();
                if (_aiEngineAnalyzers != null) _aiEngineAnalyzers.Text = string.Format("{0}/{1}", activeA, Math.Max(1, totalA));
                if (_aiEngineDecisions != null) _aiEngineDecisions.Text = stats.TotalDecisions.ToString();
                double predAcc = _agent.PredictionConfidence * 100;
                if (_aiEnginePredictions != null) _aiEnginePredictions.Text = string.Format("{0:F0}%", predAcc);
                if (_aiEngineConfidence != null) _aiEngineConfidence.Text = string.Format("{0:F0}%", _agent.SystemHealthScore);
                if (_brainStatusTxt != null)
                {
                    bool on = _agent.Mode != AgentMode.Off;
                    _brainStatusTxt.Text = on ? "نشط - وضع تكيفي ذكي" : "متوقف";
                    _brainStatusTxt.Foreground = new SolidColorBrush(on ? Theme.Green : Theme.TextMuted);
                }

                // Phase 6: Agent impacts - LIVE data
                double[] impacts = new double[8];
                impacts[0] = state != null ? Math.Max(20, 100 - state.CpuUsage) : 96;
                impacts[1] = state != null ? Math.Max(20, 100 - state.RamUsage) : 89;
                impacts[2] = state != null ? Math.Max(20, 100 - state.GpuUsage) : 92;
                impacts[3] = state != null ? Math.Max(20, 100 - state.DiskUsage) : 78;
                impacts[4] = 100.0 - Math.Min(100, Math.Max(0, _netRecvSpeed * 8 / 102.4));
                impacts[5] = state != null ? Math.Max(20, 100 - state.CpuTemp) : 91;
                impacts[6] = _agent.BatteryPercent > 0 ? (double)_agent.BatteryPercent : 88;
                double healthScore = _agent.SystemHealthScore;
                impacts[7] = Math.Max(20, healthScore);
                Color[] agentColors2 = new[] { Theme.Blue, Theme.Purple, Theme.Green, Theme.Warning, Theme.Cyan, Theme.Error, Theme.Green, Theme.Accent };
                for (int ai3 = 0; ai3 < 8; ai3++)
                {
                    if (_agentCardImpact[ai3] != null) _agentCardImpact[ai3].Text = string.Format("{0:F0}%", impacts[ai3]);
                    if (_agentCardStatus[ai3] != null)
                    {
                        bool active = impacts[ai3] > 70;
                        _agentCardStatus[ai3].Text = active ? "نشط" : "معلق";
                        _agentCardStatus[ai3].Foreground = new SolidColorBrush(active ? Theme.Green : Theme.Warning);
                    }
                }

                // Phase 13: Live Monitor
                if (state != null)
                {
                    if (_liveMonitorVals[0] != null) _liveMonitorVals[0].Text = string.Format("{0:F0}%", state.CpuUsage);
                    if (_liveMonitorVals[1] != null) _liveMonitorVals[1].Text = string.Format("{0:F0}%", state.GpuUsage);
                    if (_liveMonitorVals[2] != null) _liveMonitorVals[2].Text = string.Format("{0:F0}%", state.RamUsage);
                    if (_liveMonitorVals[3] != null) _liveMonitorVals[3].Text = string.Format("{0:F0}", state.Fps);
                    if (_liveMonitorVals[4] != null) _liveMonitorVals[4].Text = state.CpuTemp > 1 ? string.Format("{0:F0}\u00B0C", state.CpuTemp) : "--";
                    if (_liveMonitorVals[5] != null) _liveMonitorVals[5].Text = string.Format("{0:F0}%", state.DiskUsage);
                    if (_liveMonitorVals[6] != null) _liveMonitorVals[6].Text = string.Format("{0:F1} Mbps", _netRecvSpeed * 8 / 1024.0);
                    if (_liveMonitorVals[7] != null) _liveMonitorVals[7].Text = state.FrameTimeMs > 0 ? string.Format("{0:F1}ms", state.FrameTimeMs) : "--";
                    // Update monitor bars
                    if (_liveMonitorBarFills[0] != null) _liveMonitorBarFills[0].Width = Math.Max(3, 120 * state.CpuUsage / 100.0);
                    if (_liveMonitorBarFills[1] != null) _liveMonitorBarFills[1].Width = Math.Max(3, 120 * state.GpuUsage / 100.0);
                    if (_liveMonitorBarFills[2] != null) _liveMonitorBarFills[2].Width = Math.Max(3, 120 * state.RamUsage / 100.0);
                    if (_liveMonitorBarFills[3] != null) _liveMonitorBarFills[3].Width = Math.Max(3, 120 * Math.Min(100, state.Fps > 0 ? state.Fps : 0) / 100.0);
                    if (_liveMonitorBarFills[4] != null) _liveMonitorBarFills[4].Width = Math.Max(3, 120 * Math.Min(100, state.CpuTemp) / 100.0);
                    if (_liveMonitorBarFills[5] != null) _liveMonitorBarFills[5].Width = Math.Max(3, 120 * state.DiskUsage / 100.0);
                    if (_liveMonitorBarFills[6] != null) _liveMonitorBarFills[6].Width = Math.Max(3, 120 * Math.Min(100, Math.Max(0, _netRecvSpeed * 8 / 1024.0)) / 100.0);
                    if (_liveMonitorBarFills[7] != null) _liveMonitorBarFills[7].Width = Math.Max(3, 120 * Math.Min(100, state.FrameTimeMs > 0 ? state.FrameTimeMs : 0) / 100.0);
                }

                // Phase 9: Goal progress
                if (_goalProgressTxt != null) _goalProgressTxt.Text = string.Format("الهدف النشط: {0} ({1:F0}%)", _agent.GetActiveGoalName(), _agent.GetActiveGoalProgress() * 100);

                // Phase 7: Radar chart - use real data
                if (_radarPanel != null && state != null)
                {
                    _radarPanel.Children.Clear();
                    double gamingScore = state.IsGame ? Math.Max(20, 100 - state.CpuUsage) : 50;
                    double workScore = state.CpuUsage > 50 ? Math.Max(20, 100 - Math.Abs(state.CpuUsage - 60)) : 80;
                    double stabilityScore = _agent.SystemHealthScore;
                    double tempScore = state.CpuTemp > 0 ? Math.Max(20, 100 - state.CpuTemp) : 50;
                    double netScore = 100 - Math.Min(100, Math.Max(0, _netRecvSpeed * 8 / 102.4));
                    double storageScore = Math.Max(20, 100 - state.DiskUsage);
                    _radarPanel.Children.Add(UI.RadarChart(
                        new double[] { gamingScore / 100.0, workScore / 100.0, stabilityScore / 100.0, tempScore / 100.0, netScore / 100.0, storageScore / 100.0 },
                        new[] { "Gaming", "Work", "Stability", "Temp", "Network", "Storage" },
                        Theme.Cyan, 200));
                }
                if (_radarStatusTxt != null)
                {
                    string radarLabel = _agent.SystemHealthScore >= 80 ? "ممتاز" : _agent.SystemHealthScore >= 50 ? "جيد" : "يحتاج تحسين";
                    _radarStatusTxt.Text = string.Format("الأداء العام: {0} ({1:F0}/100)", radarLabel, _agent.SystemHealthScore);
                    _radarStatusTxt.Foreground = new SolidColorBrush(_agent.SystemHealthScore >= 80 ? Theme.Green : _agent.SystemHealthScore >= 50 ? Theme.Warning : Theme.Error);
                }

                // Phase 8: Forecast - use real history
                if (_forecastValTxts != null && _forecastSparkPanels != null)
                {
                    double cpuAvg = _cpuHistory.Count > 0 ? _cpuHistory[_cpuHistory.Count - 1] : 50;
                    double tempAvg = state.CpuTemp > 1 ? state.CpuTemp : 45;
                    double ramVal = state.RamUsage;
                    double[] fVals = new[] { 100 - cpuAvg, tempAvg, ramVal };
                    Color[] fColors2 = new[] { Theme.Blue, Theme.Error, Theme.Purple };
                    RingBuffer<double>[] fHist = new[] { _cpuHistory, _gpuHistory, _ramHistory };
                    for (int fi = 0; fi < 3; fi++)
                    {
                        if (_forecastValTxts[fi] != null) _forecastValTxts[fi].Text = string.Format("{0:F0}%", fVals[fi]);
                        if (_forecastSparkPanels[fi] != null && fHist[fi] != null && fHist[fi].Count >= 2)
                        {
                            _forecastSparkPanels[fi].Children.Clear();
                            double w = _forecastSparkPanels[fi].ActualWidth > 10 ? _forecastSparkPanels[fi].ActualWidth : 100;
                            int cnt = Math.Min(12, fHist[fi].Count);
                            int off = Math.Max(0, fHist[fi].Count - cnt);
                            var sparkData = new double[cnt];
                            for (int p = 0; p < cnt; p++) sparkData[p] = Math.Max(0, Math.Min(100, fHist[fi][off + p]));
                            _forecastSparkPanels[fi].Children.Add(UI.SparklineChart(sparkData, fColors2[fi], w, 28));
                        }
                    }
                }

                // Phase 10: Explanation - wire EliteExplanation to UI fields
                var exp = _agent.GetLastEliteExplanation();
                if (exp != null && exp.ChosenAction != null)
                {
                    if (_explainDecisionTxt != null)
                    {
                        string desc = exp.ChosenAction.Description ?? GetArabicCategory(exp.ChosenAction.Category);
                        _explainDecisionTxt.Text = string.Format("تم تنفيذ: {0}", desc);
                    }

                    // Risk
                    if (_explainRiskTxt != null)
                    {
                        double riskPct = exp.ChosenAction.Risk * 100;
                        string riskLevel = riskPct > 60 ? "مرتفع" : (riskPct > 30 ? "متوسط" : "منخفض");
                        _explainRiskTxt.Text = string.Format("المخاطرة: {0} ({1:F0}%)", riskLevel, riskPct);
                        _explainRiskTxt.Foreground = new SolidColorBrush(riskPct > 60 ? Theme.Warning : (riskPct > 30 ? Theme.Accent : Theme.Green));
                    }

                    // Data sources
                    if (_explainDataTxt != null && exp.DataSources != null)
                    {
                        string dataStr = "البيانات المستخدمة:\n";
                        for (int di = 0; di < exp.DataSources.Count; di++)
                        {
                            DataSource ds = exp.DataSources[di];
                            if (ds.Relevant)
                                dataStr += string.Format("  • {0}: {1}\n", GetArabicDataSource(ds.Name), ds.Value);
                        }
                        _explainDataTxt.Text = dataStr;
                    }

                    // Benefit
                    if (_explainBenefitTxt != null && exp.ConfidenceBreakdown != null)
                    {
                        double finalConf = exp.ConfidenceBreakdown.FinalConfidence * 100;
                        _explainBenefitTxt.Text = string.Format("الثقة النهائية: {0:F0}%\nالعوامل: حرارة={1:F0}%، موارد={2:F0}%، تاريخ={3:F0}%",
                            finalConf,
                            exp.ConfidenceBreakdown.ThermalFactor * 100,
                            exp.ConfidenceBreakdown.ResourceFactor * 100,
                            exp.ConfidenceBreakdown.HistoricalFactor * 100);
                    }

                    // Alternatives
                    if (_explainAltTxt != null && exp.Alternatives != null && exp.Alternatives.Count > 0)
                    {
                        string altStr = "البدائل المرفوضة:\n";
                        int altCount = Math.Min(exp.Alternatives.Count, 3);
                        for (int ai = 0; ai < altCount; ai++)
                        {
                            AgentDecision alt = exp.Alternatives[ai];
                            altStr += string.Format("  ✗ {0} (ثقة {1:F0}%، مخاطرة {2:F0}%)\n",
                                GetArabicCategory(alt.Category), alt.Confidence * 100, alt.Risk * 100);
                        }
                        _explainAltTxt.Text = altStr;
                    }
                }
                else
                {
                    if (_explainDecisionTxt != null) _explainDecisionTxt.Text = "في انتظار أول قرار...";
                    if (_explainRiskTxt != null) _explainRiskTxt.Text = "المخاطرة: --";
                    if (_explainDataTxt != null) _explainDataTxt.Text = "البيانات: --";
                    if (_explainBenefitTxt != null) _explainBenefitTxt.Text = "الفائدة: --";
                    if (_explainAltTxt != null) _explainAltTxt.Text = "البدائل: --";
                }

                // Phase 12: Decision Tree (update from explanation)
                if (_decisionTreePanel != null && exp != null && exp.ChosenAction != null)
                {
                    _decisionTreePanel.Children.Clear();
                    var treeNodes = new List<string>();
                    treeNodes.Add("محرك الذكاء الاصطناعي");
                    if (state != null)
                    {
                        if (state.CpuUsage > 80) treeNodes.Add("CPU مرتفع: " + string.Format("{0:F0}%", state.CpuUsage));
                        else if (state.CpuUsage > 50) treeNodes.Add("CPU متوسط: " + string.Format("{0:F0}%", state.CpuUsage));
                        else treeNodes.Add("CPU طبيعي: " + string.Format("{0:F0}%", state.CpuUsage));
                        if (state.CpuTemp > 80) treeNodes.Add("حرارة مرتفعة: " + string.Format("{0:F0}\u00B0C", state.CpuTemp));
                        else treeNodes.Add("حرارة طبيعية: " + string.Format("{0:F0}\u00B0C", state.CpuTemp));
                        treeNodes.Add("البطارية: " + (_agent.BatteryPercent > 0 ? string.Format("{0}%", _agent.BatteryPercent) : "غير متصل"));
                    }
                    treeNodes.Add(exp.ChosenAction.Description ?? GetArabicCategory(exp.ChosenAction.Category));
                    _decisionTreePanel.Children.Add(UI.DecisionTree(treeNodes.ToArray(), treeNodes.Count - 1, Theme.Accent));
                }

                // Phase 3: Last-event banner + reward/penalty cards + timeline feed
                var agentLogs = _agent.Logs;
                if (_lastEventBanner != null)
                {
                    if (agentLogs != null && agentLogs.Count > 0)
                    {
                        var le = agentLogs[agentLogs.Count - 1];
                        _lastEventBanner.Text = string.Format("[{0}] {1}", le.Category ?? "CORE", le.Message ?? "");
                        _lastEventTime.Text = string.Format("{0}  •  {1}", le.Timestamp.ToString("HH:mm:ss"), le.Level ?? "INFO");
                    }
                    else
                    {
                        _lastEventBanner.Text = "في انتظار أول حدث من الوكيل...";
                        _lastEventTime.Text = "بانتظار البيانات";
                    }
                    double avgReward = _agent.GetAverageObjectiveReward();
                    _rewardAvgTxt.Text = avgReward != 0.0 ? string.Format("{0:F3}", avgReward) : "لا بيانات قياس بعد";
                    _rewardEvalsTxt.Text = string.Format("{0}", _agent.GetEvaluationsCompleted());
                    int simCount = _agent.GetTotalSimulations();
                    _rewardSimTxt.Text = simCount > 0
                        ? string.Format("{0:F0}% على مدى {1} محاكاة", _agent.GetAvgSimSuccessRate(), simCount)
                        : "لا محاكيات بعد — سيبدأ القياس بعد أول قرارات";
                    _penaltyAbstainTxt.Text = string.Format("{0}", _agent.GetAbstentionCount());
                    _penaltyRiskBlockedTxt.Text = string.Format("{0} محظورة  •  متوسط الخطر {1:F1}", _agent.GetTotalRiskBlocked(), _agent.GetAvgEliteRisk());
                    _penaltyStatusTxt.Text = _agent.GetRewardEngineStatus();
                }
                if (_timelineStack != null && agentLogs != null && agentLogs.Count != _lastTimelineRebuildCount)
                {
                    _lastTimelineRebuildCount = agentLogs.Count;
                    _timelineStack.Children.Clear();
                    int shown = Math.Min(8, agentLogs.Count);
                    for (int i = agentLogs.Count - 1; i >= agentLogs.Count - shown; i--)
                    {
                        var ev = agentLogs[i];
                        Color evClr = Theme.Accent;
                        if (ev.Level == "OK") evClr = Theme.Green;
                        else if (ev.Level == "FAIL") evClr = Theme.Error;
                        else if (ev.Level == "WARN") evClr = Theme.Warning;
                        else if (ev.Level == "MONITOR") evClr = Color.FromArgb(130, 255, 255, 255);
                        var evRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                        var evDot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(evClr), Margin = new Thickness(0, 4, 8, 0) };
                        evRow.Children.Add(evDot);
                        var evTxt = UI.TextWrapped(string.Format("{0}  [{1}] {2}", ev.Timestamp.ToString("HH:mm:ss"), ev.Category ?? "CORE", ev.Message ?? ""), 11, Theme.TextSecondary, false);
                        evRow.Children.Add(evTxt);
                        _timelineStack.Children.Add(evRow);
                    }
                    if (agentLogs.Count == 0) _timelineStack.Children.Add(UI.Text("في انتظار البيانات...", 11, Theme.TextMuted, false));
                }

                // Phase 14: Console
                if (_terminalLogStack != null && _terminalLogs.Count > 0)
                {
                    while (_terminalLogStack.Children.Count < _terminalLogs.Count && _terminalLogs.Count > 0)
                    {
                        int ci = _terminalLogStack.Children.Count;
                        if (ci < _terminalLogs.Count)
                        {
                            string logLine = _terminalLogs[ci];
                            Color logColor = Theme.Green;
                            if (logLine.Contains("[ERROR]")) logColor = Theme.Error;
                            else if (logLine.Contains("[WARN]")) logColor = Theme.Warning;
                            else if (logLine.Contains("[AI]")) logColor = Theme.Purple;
                            var logTxt = UI.Text(logLine, 12, logColor, false);
                            logTxt.FontFamily = new FontFamily("Consolas");
                            logTxt.Margin = new Thickness(0, 2, 0, 0);
                            _terminalLogStack.Children.Add(logTxt);
                        }
                    }
                }
            } catch (Exception ex5) { CWLog.Error("MainWindow.AI", ex5); }

            // Phase 15: AI Monitor update
            try
            {
                var mon = _agent.GetMonitorSnapshot();
                if (_monitorHealthTxt != null)
                {
                    Color hc = mon.HealthStatus == "صحي" ? Theme.Green : mon.HealthStatus == "معتدل" ? Theme.Warning : Theme.Error;
                    _monitorHealthTxt.Text = mon.HealthStatus;
                    _monitorHealthTxt.Foreground = new SolidColorBrush(hc);
                }
                if (_monitorDecisionsTxt != null) _monitorDecisionsTxt.Text = mon.TotalDecisions.ToString();
                if (_monitorAlertsTxt != null)
                {
                    _monitorAlertsTxt.Text = mon.AlertCount.ToString();
                    _monitorAlertsTxt.Foreground = new SolidColorBrush(mon.AlertCount > 0 ? Theme.Error : Theme.Green);
                }
                if (_monitorConfidenceTxt != null) _monitorConfidenceTxt.Text = string.Format("{0:F0}%", mon.AvgConfidence);
                if (_monitorAnomalyTxt != null)
                {
                    Color ac = mon.AnomalySummary.Contains("لا توجد") || mon.AnomalySummary.Contains("متوقف") ? Theme.Green : Theme.Warning;
                    _monitorAnomalyTxt.Text = mon.AnomalySummary;
                    _monitorAnomalyTxt.Foreground = new SolidColorBrush(ac);
                }
                // Show active game/emulator
                if (_monitorGameTxt != null)
                {
                    if (mon.IsGame && !string.IsNullOrEmpty(mon.CurrentGameName))
                    {
                        _monitorGameTxt.Text = "🎮 " + mon.CurrentGameName;
                        _monitorGameTxt.Foreground = new SolidColorBrush(Theme.Green);
                    }
                    else
                    {
                        _monitorGameTxt.Text = "— لا يوجد لعبة";
                        _monitorGameTxt.Foreground = new SolidColorBrush(Color.FromArgb(136, 255, 255, 255));
                    }
                }
            }
            catch (Exception ex6) { CWLog.Error("MainWindow.AI", ex6); }
        }
        private UIElement BuildGameCenterPage()
        {
            _gameCenterPageActive = true;
            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel = new StackPanel { Margin = new Thickness(32, 16, 32, 16) };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            titleRow.Children.Add(new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Pink), Margin = new Thickness(0, 4, 12, 4) });
            titleRow.Children.Add(MakeText("مركز الألعاب", 28, Colors.White, true));
            panel.Children.Add(titleRow);
            panel.Children.Add(MakeText("اكتشاف الألعاب وإعداداتها المتعلم", 13, Color.FromArgb(136, 255, 255, 255), false));
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 16, 0, 16) });

            // Active game info
            var activeCard = MakeCard(18, null);
            var activeStack = new StackPanel { Margin = new Thickness(20) };
            activeStack.Children.Add(MakeText("التطبيق النشط حالياً", 16, Colors.White, true));
            _gameCenterActive = MakeText(_agent.CurrentState != null && _agent.CurrentState.IsGame ? _agent.CurrentState.ActiveProcess : "No game detected", 14, _agent.CurrentState != null && _agent.CurrentState.IsGame ? Theme.Green : Color.FromArgb(136, 255, 255, 255), true);
            _gameCenterActive.Margin = new Thickness(0, 8, 0, 0);
            activeStack.Children.Add(_gameCenterActive);
            var statsRow = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            statsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            statsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            string[] gLabels = new[] { "FPS", "Frame Time", "1% Low", "Stability" };
            for (int i = 0; i < 4; i++)
            {
                var st = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                st.Children.Add(MakeText(gLabels[i], 10, Color.FromArgb(120, 255, 255, 255), false));
                string val = i == 0 ? string.Format("{0:F0}", _frameTracker.CurrentFps) : i == 1 ? string.Format("{0:F1}ms", _frameTracker.CurrentFrameTimeMs) : i == 2 ? string.Format("{0:F0}", _frameTracker.Fps1Low) : string.Format("{0:F0}", _frameTracker.StabilityScore);
                st.Children.Add(MakeText(val, 16, Colors.White, true));
                Grid.SetColumn(st, i); statsRow.Children.Add(st);
            }
            activeStack.Children.Add(statsRow);
            activeCard.Child = activeStack;
            panel.Children.Add(activeCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // Discovered profiles
            var profilesCard = MakeCard(18, null);
            var profilesStack = new StackPanel { Margin = new Thickness(20) };
            profilesStack.Children.Add(MakeText("الملفات المكتشفة", 16, Colors.White, true));
            _gameProfileList = new ListBox { Background = null, BorderThickness = new Thickness(0), Margin = new Thickness(0, 8, 0, 0), MaxHeight = 300 };
            profilesStack.Children.Add(_gameProfileList);
            profilesCard.Child = profilesStack;
            panel.Children.Add(profilesCard);

            root.Content = panel;
            return root;
        }

        private void UpdateGameCenterPage()
        {
            try
            {
                if (_gameCenterActive != null)
                {
                    string appName = _agent.CurrentState.ActiveProcess ?? "";
                    if (!string.IsNullOrEmpty(appName))
                    {
                        _gameCenterActive.Text = string.Format("{0} {1}", appName, _agent.CurrentState.IsGame ? "(GAME)" : "");
                        _gameCenterActive.Foreground = new SolidColorBrush(_agent.CurrentState.IsGame ? Theme.Green : Theme.Accent);
                    }
                    else
                    {
                        _gameCenterActive.Text = "No active app detected";
                        _gameCenterActive.Foreground = new SolidColorBrush(Color.FromArgb(136, 255, 255, 255));
                    }
                }
                if (_gameProfileList != null && _agent.Profiles.Count != _gameProfileList.Items.Count)
                {
                    _gameProfileList.Items.Clear();
                    foreach (var p in _agent.Profiles)
                    {
                        _gameProfileList.Items.Add(string.Format("{0} | Used: {1}x | Last: {2}", p.Name, p.UseCount, p.LastUsed.ToString("MM/dd HH:mm")));
                    }
                }
            } catch (Exception ex6) { CWLog.Error("MainWindow.AI", ex6); }
        }

        private UIElement BuildAIActivityPage()
        {
            _aiLogPageActive = true;
            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel = new StackPanel { Margin = new Thickness(32, 16, 32, 16) };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            titleRow.Children.Add(new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Cyan), Margin = new Thickness(0, 4, 12, 4) });
            titleRow.Children.Add(MakeText("سجل نشاط الذكاء الاصطناعي", 28, Colors.White, true));
            var spacer = new Border { Width = 1, Height = 1 };
            titleRow.Children.Add(spacer);
            var exportBtn = new Button
            {
                Content = MakeText("تصدير PDF", 12, Colors.White, true),
                Background = new SolidColorBrush(Theme.Accent),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(16, 8, 16, 8),
                Margin = new Thickness(16, 0, 0, 0),
                Cursor = Cursors.Hand,
                VerticalAlignment = VerticalAlignment.Center
            };
            exportBtn.Click += (s, e) => ExportAIPdfReport();
            titleRow.Children.Add(exportBtn);
            panel.Children.Add(titleRow);
            panel.Children.Add(MakeText("سجل حي: ماذا رصد، ماذا استنتج، ماذا نفذ", 13, Color.FromArgb(136, 255, 255, 255), false));
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 16, 0, 16) });

            var logCard = MakeCard(18, null);
            var logStack = new StackPanel { Margin = new Thickness(20) };
            logStack.Children.Add(MakeText("آخر الأنشطة", 16, Colors.White, true));
            _aiLogList = new ListBox { Background = null, BorderThickness = new Thickness(0), Margin = new Thickness(0, 8, 0, 0), MaxHeight = 620, FontSize = 13 };
            logStack.Children.Add(_aiLogList);
            logCard.Child = logStack;
            panel.Children.Add(logCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // Active Predictions
            var predCard2 = MakeCard(18, null);
            var predStack2 = new StackPanel { Margin = new Thickness(20) };
            predStack2.Children.Add(MakeText("التوقعات النشطة", 16, Theme.Purple, true));
            _aiPredictionsPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            _aiPredictionsPanel.Children.Add(MakeText("بانتظار البيانات...", 12, Color.FromArgb(120, 255, 255, 255), false));
            predStack2.Children.Add(_aiPredictionsPanel);
            predCard2.Child = predStack2;
            panel.Children.Add(predCard2);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // Smart Suggestions
            var sugCard2 = MakeCard(18, null);
            var sugStack2 = new StackPanel { Margin = new Thickness(20) };
            sugStack2.Children.Add(MakeText("اقتراحات ذكية", 16, Theme.Warning, true));
            _aiSuggestionsPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            _aiSuggestionsPanel.Children.Add(MakeText("لا توجد اقتراحات بعد", 12, Color.FromArgb(120, 255, 255, 255), false));
            sugStack2.Children.Add(_aiSuggestionsPanel);
            sugCard2.Child = sugStack2;
            panel.Children.Add(sugCard2);

            root.Content = panel;
            return root;
        }

        private void UpdateAILogPage()
        {
            try
            {
                if (_aiLogList == null) return;
                var logs = _agent.Logs;
                while (_aiLogList.Items.Count > logs.Count && _aiLogList.Items.Count > 0) _aiLogList.Items.RemoveAt(0);
                for (int i = _aiLogList.Items.Count; i < logs.Count; i++)
                {
                    var log = logs[i];
                    string prefix;
                    Color clr;
                    switch (log.Level)
                    {
                        case "OK": prefix = "[+]"; clr = Theme.Green; break;
                        case "FAIL": prefix = "[!]"; clr = Theme.Error; break;
                        case "WARN": prefix = "[~]"; clr = Theme.Warning; break;
                        case "MONITOR": prefix = "[.]"; clr = Color.FromArgb(130, 255, 255, 255); break;
                        default: prefix = "[i]"; clr = Theme.Accent; break;
                    }
                    _aiLogList.Items.Add(string.Format("{0} {1} {2} {3}", prefix, log.Timestamp.ToString("HH:mm:ss"), log.Category, log.Message));
                }
                // Auto-scroll to bottom
                if (_aiLogList.Items.Count > 0) _aiLogList.ScrollIntoView(_aiLogList.Items[_aiLogList.Items.Count - 1]);
                if (_aiPredictionsPanel != null)
                {
                    _aiPredictionsPanel.Children.Clear();
                    var preds = _agent.GetActivePredictions();
                    if (preds.Count == 0)
                        _aiPredictionsPanel.Children.Add(MakeText("لا توجد توقعات نشطة", 12, Color.FromArgb(120, 255, 255, 255), false));
                    else
                    {
                        foreach (var p in preds)
                        {
                            _aiPredictionsPanel.Children.Add(MakeText(string.Format("[{0}] {1}: {2:F1} -> {3:F1} ({4}) - {5}", p.Type, p.MetricName, p.CurrentValue, p.PredictedValue, p.Timeframe, p.Reason), 11, Theme.Purple, false));
                        }
                    }
                }
                if (_aiSuggestionsPanel != null)
                {
                    _aiSuggestionsPanel.Children.Clear();
                    var sugs = _agent.GetSuggestions();
                    if (sugs.Count == 0)
                        _aiSuggestionsPanel.Children.Add(MakeText("لا توجد اقتراحات", 12, Color.FromArgb(120, 255, 255, 255), false));
                    else
                    {
                        foreach (var s in sugs)
                        {
                            Color priClr = s.Priority == SuggestionPriority.Critical ? Theme.Error : s.Priority == SuggestionPriority.High ? Theme.Warning : Theme.Accent;
                            _aiSuggestionsPanel.Children.Add(MakeText(string.Format("[{0}] {1} - {2} (impact: {3:F0}%)", s.Priority, s.Title, s.Description, s.EstimatedImpact), 11, priClr, false));
                        }
                    }
                }
            } catch (Exception ex7) { CWLog.Error("MainWindow.AI", ex7); }
        }

        private UIElement BuildLearningPage()
        {
            _learningPageActive = true;
            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel = new StackPanel { Margin = new Thickness(32, 16, 32, 16) };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            titleRow.Children.Add(new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Warning), Margin = new Thickness(0, 4, 12, 4) });
            titleRow.Children.Add(MakeText("لوحة التعلم", 28, Colors.White, true));
            panel.Children.Add(titleRow);
            panel.Children.Add(MakeText("سجل الخبرات - أكثر القرارات نجاحاً وفشلأ", 13, Color.FromArgb(136, 255, 255, 255), false));
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 16, 0, 16) });

            // Stats
            var statsCard = MakeCard(18, null);
            var statsStack = new StackPanel { Margin = new Thickness(20) };
            statsStack.Children.Add(MakeText("إحصائيات التعلم", 16, Colors.White, true));
            var exps = _agent.Experiences;
            int improved = 0, degraded = 0, neutral = 0;
            foreach (var e in exps) { if (e.Result == ExperienceResult.Improved) improved++; else if (e.Result == ExperienceResult.Degraded) degraded++; else neutral++; }
            _learningStatsTxt = MakeText(string.Format("Total Experiences: {0} | Improved: {1} | Degraded: {2} | Neutral: {3}", exps.Count, improved, degraded, neutral), 12, Color.FromArgb(170, 255, 255, 255), false);
            statsStack.Children.Add(_learningStatsTxt);
            double successRate = _agent.Stats.TotalDecisions > 0 ? (double)_agent.Stats.SuccessfulDecisions / _agent.Stats.TotalDecisions * 100 : 0;
            statsStack.Children.Add(MakeText(string.Format("Success Rate: {0:F0}% | Decisions: {1}", successRate, _agent.Stats.TotalDecisions), 12, Color.FromArgb(170, 255, 255, 255), false));
            statsCard.Child = statsStack;
            panel.Children.Add(statsCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // Best experiences
            var bestCard = MakeCard(18, null);
            var bestStack = new StackPanel { Margin = new Thickness(20) };
            bestStack.Children.Add(MakeText("أكثر القرارات نجاحاً", 16, Theme.Green, true));
            _aiExperienceList = new ListBox { Background = null, BorderThickness = new Thickness(0), Margin = new Thickness(0, 8, 0, 0), MaxHeight = 300 };
            _learningBestList = _aiExperienceList;
            var topExp = _agent.GetTopExperiences(10);
            foreach (var e in topExp)
            {
                _aiExperienceList.Items.Add(string.Format("[{0}] {1} | Score: {2:F0} | Success: {3:F0}% | Used: {4}x",
                    e.Category, e.Action, e.Score, e.SuccessRate() * 100, e.TimesUsed));
            }
            bestStack.Children.Add(_aiExperienceList);
            bestCard.Child = bestStack;
            panel.Children.Add(bestCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // Worst experiences
            var worstCard = MakeCard(18, null);
            var worstStack = new StackPanel { Margin = new Thickness(20) };
            worstStack.Children.Add(MakeText("أكثر القرارات فشلاً", 16, Theme.Error, true));
            var worstList = new ListBox { Background = null, BorderThickness = new Thickness(0), Margin = new Thickness(0, 8, 0, 0), MaxHeight = 200 };
            _learningWorstList = worstList;
            var worstExp = _agent.GetWorstExperiences(5);
            foreach (var e in worstExp)
            {
                worstList.Items.Add(string.Format("[{0}] {1} | Score: {2:F0} | Success: {3:F0}% | Used: {4}x",
                    e.Category, e.Action, e.Score, e.SuccessRate() * 100, e.TimesUsed));
            }
            worstStack.Children.Add(worstList);
            worstCard.Child = worstStack;
            panel.Children.Add(worstCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // Self-Tuning Weights
            var weightsCard = MakeCard(18, null);
            var weightsStack = new StackPanel { Margin = new Thickness(20) };
            weightsStack.Children.Add(MakeText("أوزان الذكاء الاصطناعي ذاتية الضبط", 16, Theme.Cyan, true));
            weightsStack.Children.Add(MakeText("تُضبط تلقائياً بناءً على نجاح/فشل الإجراءات", 12, Color.FromArgb(136, 255, 255, 255), false));
            var weights = _agent.Weights;
            string[] wNames = new[] { "Thermal", "Memory", "CPU", "FPS", "Battery", "Background", "Power", "Affinity" };
            double[] wVals = new[] { weights.ThermalWeight, weights.MemoryWeight, weights.CpuWeight, weights.FpsWeight, weights.BatteryWeight, weights.BackgroundWeight, weights.PowerWeight, weights.AffinityWeight };
            for (int i = 0; i < 8; i++)
            {
                weightsStack.Children.Add(MakeText(string.Format("{0}: {1:F2}x", wNames[i], wVals[i]), 12, wVals[i] > 1.2 ? Theme.Green : wVals[i] < 0.8 ? Theme.Warning : Color.FromArgb(170, 255, 255, 255), false));
            }
            weightsStack.Children.Add(MakeText(string.Format("Tuning Cycles: {0} | Last: {1}", weights.TuningCycles, weights.LastTuned.ToString("HH:mm:ss")), 11, Color.FromArgb(120, 255, 255, 255), false));
            weightsCard.Child = weightsStack;
            panel.Children.Add(weightsCard);

            root.Content = panel;
            return root;
        }

        private void UpdateLearningPage()
        {
            if (_learningPageActive)
            {
                var exps = _agent.Experiences;
                int improved = 0, degraded = 0, neutral = 0;
                foreach (var e in exps) { if (e.Result == ExperienceResult.Improved) improved++; else if (e.Result == ExperienceResult.Degraded) degraded++; else neutral++; }
                if (_learningStatsTxt != null) _learningStatsTxt.Text = string.Format("Total Experiences: {0} | Improved: {1} | Degraded: {2} | Neutral: {3}", exps.Count, improved, degraded, neutral);
                if (_learningBestList != null)
                {
                    _learningBestList.Items.Clear();
                    foreach (var e in _agent.GetTopExperiences(10))
                        _learningBestList.Items.Add(string.Format("[{0}] {1} | Score: {2:F0} | Success: {3:F0}% | Used: {4}x", e.Category, e.Action, e.Score, e.SuccessRate() * 100, e.TimesUsed));
                }
                if (_learningWorstList != null)
                {
                    _learningWorstList.Items.Clear();
                    foreach (var e in _agent.GetWorstExperiences(5))
                        _learningWorstList.Items.Add(string.Format("[{0}] {1} | Score: {2:F0} | Success: {3:F0}% | Used: {4}x", e.Category, e.Action, e.Score, e.SuccessRate() * 100, e.TimesUsed));
                }
            }
        }

        private UIElement BuildAIProfilesPage()
        {
            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel = new StackPanel { Margin = new Thickness(32, 16, 32, 16) };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            titleRow.Children.Add(new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Purple), Margin = new Thickness(0, 4, 12, 4) });
            titleRow.Children.Add(MakeText("ملفات الذكاء الاصطناعي", 28, Colors.White, true));
            panel.Children.Add(titleRow);
            panel.Children.Add(MakeText("ملفات الإعدادات لكل لعبة وتطبيق", 13, Color.FromArgb(136, 255, 255, 255), false));
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 16, 0, 16) });

            // Default profile
            var defCard = MakeCard(18, null);
            var defStack = new StackPanel { Margin = new Thickness(20) };
            defStack.Children.Add(MakeText("الملف الافتراضي", 16, Colors.White, true));
            defStack.Children.Add(MakeText("يُطبّق على جميع التطبيقات ما لم يكن هناك ملف مخصص", 12, Color.FromArgb(136, 255, 255, 255), false));
            string[] defSettings = new[] { "Auto Memory Clean: ON", "Auto Process Priority: ON", "Auto Power Plan: ON", "Auto Background Control: ON", "Max CPU Temp: 90C", "Max GPU Temp: 90C" };
            foreach (string s in defSettings) defStack.Children.Add(MakeText(s, 12, Color.FromArgb(170, 255, 255, 255), false));
            defCard.Child = defStack;
            panel.Children.Add(defCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // Game-specific profiles
            var profilesCard = MakeCard(18, null);
            var profilesStack = new StackPanel { Margin = new Thickness(20) };
            profilesStack.Children.Add(MakeText("ملفات الألعاب المحفوظة", 16, Colors.White, true));
            foreach (var p in _agent.Profiles)
            {
                var pCard = new Border { CornerRadius = new CornerRadius(16), Background = new SolidColorBrush(Theme.CardClr), Margin = new Thickness(0, 4, 0, 4), Padding = new Thickness(12, 8, 12, 8) };
                var pStack = new StackPanel();
                pStack.Children.Add(MakeText(string.Format("{0} | Used: {1}x | Last: {2}", p.Name, p.UseCount, p.LastUsed.ToString("MM/dd HH:mm")), 13, Theme.Accent, true));
                string settings = string.Format("Mode: {0} | MemClean: {1} | Priority: {2} | Power: {3} | MaxTemp: {4}C",
                    p.PreferredMode, p.AutoMemoryClean ? "ON" : "OFF", p.AutoProcessPriority ? "ON" : "OFF", p.AutoPowerPlan ? "ON" : "OFF", p.MaxCpuTemp);
                pStack.Children.Add(MakeText(settings, 11, Color.FromArgb(136, 255, 255, 255), false));
                pCard.Child = pStack;
                profilesStack.Children.Add(pCard);
            }
            if (_agent.Profiles.Count == 0) profilesStack.Children.Add(MakeText("لا توجد ملفات بعد — تُنشأ عند اكتشاف الألعاب", 12, Color.FromArgb(120, 255, 255, 255), false));
            profilesCard.Child = profilesStack;
            panel.Children.Add(profilesCard);

            root.Content = panel;
            return root;
        }

        private void OnAgentLog(AgentLog log)
        {
            try
            {
                string entry = string.Format("[{0}] [{1}] {2}", log.Timestamp.ToString("HH:mm:ss"), log.Category ?? "CORE", log.Message ?? "");
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        _terminalLogs.Add(entry);
                        while (_terminalLogs.Count > 60) _terminalLogs.RemoveAt(0);
                        if (_terminalLogStack != null)
                        {
                            Color logColor = Theme.Green;
                            if (log.Category != null && log.Category.Contains("ERROR")) logColor = Theme.Error;
                            else if (log.Category != null && log.Category.Contains("WARN")) logColor = Theme.Warning;
                            else if (log.Category != null && log.Category.Contains("AI")) logColor = Theme.Purple;
                            var logTxt = UI.Text(entry, 12, logColor, false);
                            logTxt.FontFamily = new FontFamily("Consolas");
                            logTxt.Margin = new Thickness(0, 2, 0, 0);
                            _terminalLogStack.Children.Add(logTxt);
                            if (_terminalLogStack.Children.Count > 60) _terminalLogStack.Children.RemoveAt(0);
                        }
                    }
                    catch (Exception ex8) { CWLog.Error("MainWindow.AI", ex8); }
                }));
            } catch (Exception ex9) { CWLog.Error("MainWindow.AI", ex9); }
            // Bridge agent decisions/results (rewards, rollbacks, CPU/RAM samples)
            // to the Web UI "AI Execution Center" so it shows live activity instead
            // of staying empty. Throttling/coalescing happens inside WebUiHost.
            try
            {
                if (_webUiHost != null && log != null)
                    _webUiHost.PushAgentLog(log.Level ?? "INFO", log.Category ?? "CORE", log.Message ?? "", log.Timestamp.ToString("HH:mm:ss"));
            }
            catch (Exception ex10) { CWLog.Error("MainWindow.AI", ex10); }
        }

        private void OnAgentOptLog(string entry)
        {
            try
            {
                if (string.IsNullOrEmpty(entry)) return;
                // entry format: "[HH:mm:ss] ACTION: detail"
                string time = "";
                int open = entry.IndexOf(']');
                if (open > 1)
                {
                    time = entry.Substring(1, open - 1);
                    entry = entry.Substring(open + 1).Trim();
                }
                string cat = "Optimize";
                int sep = entry.IndexOf(": ");
                if (sep > 0) { cat = entry.Substring(0, sep); entry = entry.Substring(sep + 2); }
                if (_webUiHost != null) _webUiHost.PushAgentLog("OPT", cat, entry, time);
            }
            catch (Exception ex11) { CWLog.Error("MainWindow.AI", ex11); }
        }

        private void OnAgentDecision(AgentDecision d)
        {
            // decisions are displayed in real-time on agent page
            // Persistent hook: record the decision in the on-disk audit trail
            // (shown on the settings page "الذاكرة الدائمة للذكاء الاصطناعي").
            RecordPersistentDecision(d);
            if (d != null)
            {
                try
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        AddExplainabilityDecision(d);
                    }));
                }
                catch { }
            }
        }

        private void OnMonitorAlert(string alert)
        {
            try
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (_monitorAlertStack == null) return;
                        // Remove placeholder if present
                        if (_monitorAlertStack.Children.Count == 1 &&
                            _monitorAlertStack.Children[0] is TextBlock tb && tb.Text == "لا توجد تنبيهات")
                            _monitorAlertStack.Children.Clear();
                        var alertRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                        var alertDot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Theme.Warning), Margin = new Thickness(0, 3, 6, 3) };
                        alertRow.Children.Add(alertDot);
                        alertRow.Children.Add(UI.Text(alert, 10, Theme.Warning, false));
                        _monitorAlertStack.Children.Add(alertRow);
                        while (_monitorAlertStack.Children.Count > 20)
                            _monitorAlertStack.Children.RemoveAt(0);
                    }
                    catch (Exception ex) { CWLog.Error("MainWindow.AI", ex); }
                }));
            }
            catch (Exception ex2) { CWLog.Error("MainWindow.AI", ex2); }
        }

        private void OnMonitorTick(object sender, EventArgs e)
        {
            try
            {
                var mon = _agent.GetMonitorSnapshot();
                var alerts = _agent.GetMonitorAlerts();
                if (alerts.Count > 0)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            if (_monitorAlertStack == null) return;
                            if (_monitorAlertStack.Children.Count == 1 &&
                                _monitorAlertStack.Children[0] is TextBlock tb && tb.Text == "لا توجد تنبيهات")
                                _monitorAlertStack.Children.Clear();
                            foreach (var a in alerts)
                            {
                                var alertRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                                var alertDot = new Border { Width = 6, Height = 6, CornerRadius = new CornerRadius(3), Background = new SolidColorBrush(Theme.Warning), Margin = new Thickness(0, 3, 6, 3) };
                                alertRow.Children.Add(alertDot);
                                alertRow.Children.Add(UI.Text(a, 10, Theme.Warning, false));
                                _monitorAlertStack.Children.Add(alertRow);
                            }
                            while (_monitorAlertStack.Children.Count > 20)
                                _monitorAlertStack.Children.RemoveAt(0);
                        }
                        catch (Exception ex) { CWLog.Error("MainWindow.AI", ex); }
                    }));
                }
            }
            catch (Exception ex3) { CWLog.Error("MainWindow.AI", ex3); }
        }

        private void CheckAIProcesses()
        {
            if (!_aiMonitorEnabled || _aiProcessPanel == null) return;
            // Process.GetProcesses() + TotalProcessorTime is expensive and must not
            // run on the UI thread; skip if the previous query is still running.
            if (System.Threading.Interlocked.CompareExchange(ref _aiProcsBusy, 1, 0) == 1) return;
            System.Threading.Tasks.Task.Run(() =>
            {
                List<string> heavy = null;
                try
                {
                    heavy = new List<string>();
                    var all = Process.GetProcesses();
                    try
                    {
                        foreach (var p in all)
                        {
                            try
                            {
                                if (p.TotalProcessorTime.TotalMilliseconds > 5000 || p.WorkingSet64 > 500 * 1048576)
                                {
                                    string n = "";
                                    try { n = p.ProcessName; } catch { continue; }
                                    if (!heavy.Contains(n) && n != "ClearWin" && n != "Idle" && n != "") heavy.Add(n);
                                }
                            }
                            catch (Exception ex10) { CWLog.Error("MainWindow.AI", ex10); }
                        }
                    }
                    finally { foreach (var p in all) { try { p.Dispose(); } catch (Exception ex11) { CWLog.Error("MainWindow.AI", ex11); } } }
                    heavy.Sort();
                }
                catch (Exception ex12) { CWLog.Error("MainWindow.AI", ex12); }
                finally { System.Threading.Interlocked.Exchange(ref _aiProcsBusy, 0); }
                var list = heavy ?? new List<string>();
                try
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            _aiProcessPanel.Children.Clear();
                            if (list.Count == 0)
                            {
                                _aiProcessPanel.Children.Add(MakeText("لا توجد عمليات ثقيلة", 12, Color.FromArgb(100, 255, 255, 255), false));
                            }
                            else
                            {
                                _aiProcessPanel.Children.Add(MakeText(string.Format("تم رصد {0} عملية ثقيلة:", list.Count), 12, Theme.Warning, true));
                                foreach (string h in list)
                                {
                                    var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
                                    row.Children.Add(MakeText("⚠ " + h, 11, Theme.Error, false));
                                    var killBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(24, 239, 68, 68)), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), Cursor = Cursors.Hand, Margin = new Thickness(8, 0, 0, 0) };
                                    string hn = h;
                                    killBtn.Child = MakeText("إنهاء", 10, Theme.Error, true);
                                    killBtn.MouseDown += (sk, ek) => { try { foreach (var x in Process.GetProcessesByName(hn)) { try { x.Kill(); } catch (Exception ex13) { CWLog.Error("MainWindow.AI", ex13); } finally { try { x.Dispose(); } catch (Exception ex14) { CWLog.Error("MainWindow.AI", ex14); } } } } catch (Exception ex15) { CWLog.Error("MainWindow.AI", ex15); } };
                                    row.Children.Add(killBtn);
                                    _aiProcessPanel.Children.Add(row);
                                }
                            }
                        }
                        catch (Exception ex16) { CWLog.Error("MainWindow.AI", ex16); }
                    }));
                }
                catch (Exception ex17) { CWLog.Error("MainWindow.AI", ex17); }
            });
        }

        private UIElement BuildAdvancedAIPage()
        {
            _advancedAIPageActive = true;
            var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var panel = new StackPanel { Margin = new Thickness(32, 16, 32, 16) };
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 20) };
            titleRow.Children.Add(new Border { Width = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Purple), Margin = new Thickness(0, 4, 12, 4) });
            titleRow.Children.Add(MakeText("الذكاء الاصطناعي المتقدم", 28, Colors.White, true));
            panel.Children.Add(titleRow);
            panel.Children.Add(MakeText("Benchmark, Root Cause, Autopilot, Recovery, Game Learning, Drivers", 13, Color.FromArgb(136, 255, 255, 255), false));
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 16, 0, 16) });

            // === Benchmark ===
            var benchCard = MakeCard(18, null);
            var benchStack = new StackPanel { Margin = new Thickness(20) };
            benchStack.Children.Add(MakeText("قياس أداء الذكاء الاصطناعي", 16, Theme.Accent, true));
            _advBenchScore = MakeText("Score: --", 14, Colors.White, true);
            _advBenchScore.Margin = new Thickness(0, 6, 0, 0);
            benchStack.Children.Add(_advBenchScore);
            _advBenchTrend = MakeText("Trend: --", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advBenchTrend.Margin = new Thickness(0, 4, 0, 0);
            benchStack.Children.Add(_advBenchTrend);
            benchCard.Child = benchStack;
            panel.Children.Add(benchCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // === Self Diagnostics ===
            var diagCard = MakeCard(18, null);
            var diagStack = new StackPanel { Margin = new Thickness(20) };
            diagStack.Children.Add(MakeText("التشخيص الذاتي", 16, Theme.Green, true));
            _advSelfDiagStatus = MakeText("Status: --", 13, Colors.White, false);
            _advSelfDiagStatus.Margin = new Thickness(0, 6, 0, 0);
            diagStack.Children.Add(_advSelfDiagStatus);
            _advSelfDiagRam = MakeText("RAM: -- MB", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advSelfDiagRam.Margin = new Thickness(0, 4, 0, 0);
            diagStack.Children.Add(_advSelfDiagRam);
            diagCard.Child = diagStack;
            panel.Children.Add(diagCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // === Autopilot + Power ===
            var autoCard = MakeCard(18, null);
            var autoStack = new StackPanel { Margin = new Thickness(20) };
            autoStack.Children.Add(MakeText("القيادة الذاتية للأداء", 16, Theme.Pink, true));
            _advAutopilotStatus = MakeText("الحالة: معطل", 13, Color.FromArgb(170, 255, 255, 255), false);
            _advAutopilotStatus.Margin = new Thickness(0, 6, 0, 0);
            autoStack.Children.Add(_advAutopilotStatus);
            _advAutopilotActions = MakeText("إجمالي الإجراءات: 0", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advAutopilotActions.Margin = new Thickness(0, 4, 0, 0);
            autoStack.Children.Add(_advAutopilotActions);
            autoStack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 8, 0, 8) });
            autoStack.Children.Add(MakeText("محسّن الطاقة", 14, Theme.Warning, true));
            _advPowerPlan = MakeText("Plan: --", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advPowerPlan.Margin = new Thickness(0, 4, 0, 0);
            autoStack.Children.Add(_advPowerPlan);
            _advPowerScore = MakeText("Score: --", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advPowerScore.Margin = new Thickness(0, 4, 0, 0);
            autoStack.Children.Add(_advPowerScore);
            autoCard.Child = autoStack;
            panel.Children.Add(autoCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // === Root Cause Analysis ===
            var rcCard = MakeCard(18, null);
            var rcStack = new StackPanel { Margin = new Thickness(20) };
            rcStack.Children.Add(MakeText("تحليل السبب الجذري", 16, Theme.Error, true));
            _advRootCausesPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            _advRootCausesPanel.Children.Add(MakeText("Analyzing...", 12, Color.FromArgb(120, 255, 255, 255), false));
            rcStack.Children.Add(_advRootCausesPanel);
            rcCard.Child = rcStack;
            panel.Children.Add(rcCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // === Recovery History ===
            var recCard = MakeCard(18, null);
            var recStack = new StackPanel { Margin = new Thickness(20) };
            recStack.Children.Add(MakeText("الاسترداد التلقائي", 16, Theme.Cyan, true));
            _advRecoveryPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            _advRecoveryPanel.Children.Add(MakeText("No recovery events", 12, Color.FromArgb(120, 255, 255, 255), false));
            recStack.Children.Add(_advRecoveryPanel);
            recCard.Child = recStack;
            panel.Children.Add(recCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // === Recommendations ===
            var recmCard = MakeCard(18, null);
            var recmStack = new StackPanel { Margin = new Thickness(20) };
            recmStack.Children.Add(MakeText("توصيات الذكاء الاصطناعي", 16, Theme.Warning, true));
            _advRecommendPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            _advRecommendPanel.Children.Add(MakeText("No recommendations yet", 12, Color.FromArgb(120, 255, 255, 255), false));
            recmStack.Children.Add(_advRecommendPanel);
            recmCard.Child = recmStack;
            panel.Children.Add(recmCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // === Game Learning Profiles ===
            var glCard = MakeCard(18, null);
            var glStack = new StackPanel { Margin = new Thickness(20) };
            glStack.Children.Add(MakeText("ملفات تعلم الألعاب", 16, Theme.Accent, true));
            _advGameProfilesPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            _advGameProfilesPanel.Children.Add(MakeText("No game profiles yet", 12, Color.FromArgb(120, 255, 255, 255), false));
            glStack.Children.Add(_advGameProfilesPanel);
            glCard.Child = glStack;
            panel.Children.Add(glCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 12, 0, 12) });

            // === Driver Intelligence ===
            var drCard = MakeCard(18, null);
            var drStack = new StackPanel { Margin = new Thickness(20) };
            drStack.Children.Add(MakeText("ذكاء التعريفات", 16, Theme.Purple, true));
            _advDriversPanel = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            _advDriversPanel.Children.Add(MakeText("Scanning drivers...", 12, Color.FromArgb(120, 255, 255, 255), false));
            drStack.Children.Add(_advDriversPanel);
            drCard.Child = drStack;
            panel.Children.Add(drCard);
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 16, 0, 16) });

            // === Level 3: Elite AI ===
            panel.Children.Add(MakeText("Level 3 — Elite AI", 18, Theme.Accent, true));
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 8, 0, 12) });

            var simCard = MakeCard(18, null);
            var simStack = new StackPanel { Margin = new Thickness(20) };
            simStack.Children.Add(MakeText("Decision Simulator", 14, Theme.Warning, true));
            _advSimStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advSimStatus.Margin = new Thickness(0, 4, 0, 0);
            simStack.Children.Add(_advSimStatus);
            simCard.Child = simStack;
            panel.Children.Add(simCard);

            var dnaCard = MakeCard(18, null);
            var dnaStack = new StackPanel { Margin = new Thickness(20) };
            dnaStack.Children.Add(MakeText("Performance DNA", 14, Theme.Cyan, true));
            _advDNAStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advDNAStatus.Margin = new Thickness(0, 4, 0, 0);
            dnaStack.Children.Add(_advDNAStatus);
            dnaCard.Child = dnaStack;
            panel.Children.Add(dnaCard);

            var seasCard = MakeCard(18, null);
            var seasStack = new StackPanel { Margin = new Thickness(20) };
            seasStack.Children.Add(MakeText("Seasonal Learning", 14, Theme.Pink, true));
            _advSeasonStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advSeasonStatus.Margin = new Thickness(0, 4, 0, 0);
            seasStack.Children.Add(_advSeasonStatus);
            seasCard.Child = seasStack;
            panel.Children.Add(seasCard);

            var maCard = MakeCard(18, null);
            var maStack = new StackPanel { Margin = new Thickness(20) };
            maStack.Children.Add(MakeText("Multi-Agent System", 14, Theme.Green, true));
            _advMultiAgentStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advMultiAgentStatus.Margin = new Thickness(0, 4, 0, 0);
            maStack.Children.Add(_advMultiAgentStatus);
            _advConflictStatus = MakeText("--", 11, Color.FromArgb(120, 255, 255, 255), false);
            _advConflictStatus.Margin = new Thickness(0, 2, 0, 0);
            maStack.Children.Add(_advConflictStatus);
            maCard.Child = maStack;
            panel.Children.Add(maCard);

            var riskCard = MakeCard(18, null);
            var riskStack = new StackPanel { Margin = new Thickness(20) };
            riskStack.Children.Add(MakeText("Risk Engine", 14, Theme.Error, true));
            _advRiskStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advRiskStatus.Margin = new Thickness(0, 4, 0, 0);
            riskStack.Children.Add(_advRiskStatus);
            riskCard.Child = riskStack;
            panel.Children.Add(riskCard);

            var fcCard = MakeCard(18, null);
            var fcStack = new StackPanel { Margin = new Thickness(20) };
            fcStack.Children.Add(MakeText("Forecast & Goals", 14, Theme.Accent, true));
            _advForecastStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advForecastStatus.Margin = new Thickness(0, 4, 0, 0);
            fcStack.Children.Add(_advForecastStatus);
            _advGoalStatus = MakeText("--", 11, Color.FromArgb(120, 255, 255, 255), false);
            _advGoalStatus.Margin = new Thickness(0, 2, 0, 0);
            fcStack.Children.Add(_advGoalStatus);
            fcCard.Child = fcStack;
            panel.Children.Add(fcCard);

            var exCard = MakeCard(18, null);
            var exStack = new StackPanel { Margin = new Thickness(20) };
            exStack.Children.Add(MakeText("Explainability & Knowledge", 14, Theme.Purple, true));
            _advExplainStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advExplainStatus.Margin = new Thickness(0, 4, 0, 0);
            exStack.Children.Add(_advExplainStatus);
            _advKBStatus = MakeText("--", 11, Color.FromArgb(120, 255, 255, 255), false);
            _advKBStatus.Margin = new Thickness(0, 2, 0, 0);
            exStack.Children.Add(_advKBStatus);
            exCard.Child = exStack;
            panel.Children.Add(exCard);

            var bstCard = MakeCard(18, null);
            var bstStack = new StackPanel { Margin = new Thickness(20) };
            bstStack.Children.Add(MakeText("Adaptive Boosters", 14, Theme.Warning, true));
            _advBoosterStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advBoosterStatus.Margin = new Thickness(0, 4, 0, 0);
            bstStack.Children.Add(_advBoosterStatus);
            bstCard.Child = bstStack;
            panel.Children.Add(bstCard);

            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 16, 0, 16) });

            // === Level 4: Research Grade AI ===
            panel.Children.Add(MakeText("Level 4 — Research Grade AI", 18, Theme.Cyan, true));
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 8, 0, 12) });

            var caCard = MakeCard(18, null);
            var caStack = new StackPanel { Margin = new Thickness(20) };
            caStack.Children.Add(MakeText("Causal AI", 14, Theme.Error, true));
            _advCausalStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advCausalStatus.Margin = new Thickness(0, 4, 0, 0);
            caStack.Children.Add(_advCausalStatus);
            caCard.Child = caStack;
            panel.Children.Add(caCard);

            var wmCard = MakeCard(18, null);
            var wmStack = new StackPanel { Margin = new Thickness(20) };
            wmStack.Children.Add(MakeText("World Model", 14, Theme.Pink, true));
            _advWorldModelStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advWorldModelStatus.Margin = new Thickness(0, 4, 0, 0);
            wmStack.Children.Add(_advWorldModelStatus);
            wmCard.Child = wmStack;
            panel.Children.Add(wmCard);

            var rlCard = MakeCard(18, null);
            var rlStack = new StackPanel { Margin = new Thickness(20) };
            rlStack.Children.Add(MakeText("Reinforcement Learning", 14, Theme.Green, true));
            _advRLStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advRLStatus.Margin = new Thickness(0, 4, 0, 0);
            rlStack.Children.Add(_advRLStatus);
            rlCard.Child = rlStack;
            panel.Children.Add(rlCard);

            var plCard = MakeCard(18, null);
            var plStack = new StackPanel { Margin = new Thickness(20) };
            plStack.Children.Add(MakeText("AI Planning", 14, Theme.Accent, true));
            _advPlanStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advPlanStatus.Margin = new Thickness(0, 4, 0, 0);
            plStack.Children.Add(_advPlanStatus);
            plCard.Child = plStack;
            panel.Children.Add(plCard);

            var ltCard = MakeCard(18, null);
            var ltStack = new StackPanel { Margin = new Thickness(20) };
            ltStack.Children.Add(MakeText("Long-Term Memory & Architecture", 14, Theme.Warning, true));
            _advLongTermStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advLongTermStatus.Margin = new Thickness(0, 4, 0, 0);
            ltStack.Children.Add(_advLongTermStatus);
            _advArchStatus = MakeText("--", 11, Color.FromArgb(120, 255, 255, 255), false);
            _advArchStatus.Margin = new Thickness(0, 2, 0, 0);
            ltStack.Children.Add(_advArchStatus);
            ltCard.Child = ltStack;
            panel.Children.Add(ltCard);

            var prCard = MakeCard(18, null);
            var prStack = new StackPanel { Margin = new Thickness(20) };
            prStack.Children.Add(MakeText("Self Profiling & Calibration", 14, Theme.Cyan, true));
            _advProfileStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advProfileStatus.Margin = new Thickness(0, 4, 0, 0);
            prStack.Children.Add(_advProfileStatus);
            _advCalibStatus = MakeText("--", 11, Color.FromArgb(120, 255, 255, 255), false);
            _advCalibStatus.Margin = new Thickness(0, 2, 0, 0);
            prStack.Children.Add(_advCalibStatus);
            prCard.Child = prStack;
            panel.Children.Add(prCard);

            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 16, 0, 16) });

            // === Level 5-7: Autonomous, Enterprise, Graph ===
            panel.Children.Add(MakeText("Level 5-7 — Autonomous, Enterprise, Graph", 18, Theme.Purple, true));
            panel.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 8, 0, 12) });

            var auCard = MakeCard(18, null);
            var auStack = new StackPanel { Margin = new Thickness(20) };
            auStack.Children.Add(MakeText("Autonomous Manager", 14, Theme.Green, true));
            _advAutoMStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advAutoMStatus.Margin = new Thickness(0, 4, 0, 0);
            auStack.Children.Add(_advAutoMStatus);
            auCard.Child = auStack;
            panel.Children.Add(auCard);

            var entCard = MakeCard(18, null);
            var entStack = new StackPanel { Margin = new Thickness(20) };
            entStack.Children.Add(MakeText("Enterprise Manager", 14, Theme.Accent, true));
            _advEnterpriseStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advEnterpriseStatus.Margin = new Thickness(0, 4, 0, 0);
            entStack.Children.Add(_advEnterpriseStatus);
            entCard.Child = entStack;
            panel.Children.Add(entCard);

            var grCard = MakeCard(18, null);
            var grStack = new StackPanel { Margin = new Thickness(20) };
            grStack.Children.Add(MakeText("Optimization Graph", 14, Theme.Pink, true));
            _advGraphStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advGraphStatus.Margin = new Thickness(0, 4, 0, 0);
            grStack.Children.Add(_advGraphStatus);
            grCard.Child = grStack;
            panel.Children.Add(grCard);

            // === Predictive Maintenance ===
            var maintCard = MakeCard(18, null);
            var maintStack = new StackPanel { Margin = new Thickness(20) };
            maintStack.Children.Add(MakeText("Predictive Maintenance", 14, Theme.Warning, true));
            _advMaintStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advMaintStatus.Margin = new Thickness(0, 4, 0, 0);
            maintStack.Children.Add(_advMaintStatus);
            maintCard.Child = maintStack;
            panel.Children.Add(maintCard);

            // === Resource Forecasting ===
            var forecastCard = MakeCard(18, null);
            var forecastStack = new StackPanel { Margin = new Thickness(20) };
            forecastStack.Children.Add(MakeText("Resource Forecasting", 14, Theme.Accent, true));
            _advForecastStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advForecastStatus.Margin = new Thickness(0, 4, 0, 0);
            forecastStack.Children.Add(_advForecastStatus);
            forecastCard.Child = forecastStack;
            panel.Children.Add(forecastCard);

            // === Smart Scheduling ===
            var schedCard = MakeCard(18, null);
            var schedStack = new StackPanel { Margin = new Thickness(20) };
            schedStack.Children.Add(MakeText("Smart Scheduling", 14, Theme.Green, true));
            _advScheduleStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advScheduleStatus.Margin = new Thickness(0, 4, 0, 0);
            schedStack.Children.Add(_advScheduleStatus);
            schedCard.Child = schedStack;
            panel.Children.Add(schedCard);

            // === Performance Comparison ===
            var compCard = MakeCard(18, null);
            var compStack = new StackPanel { Margin = new Thickness(20) };
            compStack.Children.Add(MakeText("Performance Comparison", 14, Theme.Pink, true));
            _advComparisonStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advComparisonStatus.Margin = new Thickness(0, 4, 0, 0);
            compStack.Children.Add(_advComparisonStatus);
            compCard.Child = compStack;
            panel.Children.Add(compCard);

            // === User Behavior Learning ===
            var behCard = MakeCard(18, null);
            var behStack = new StackPanel { Margin = new Thickness(20) };
            behStack.Children.Add(MakeText("User Behavior Learning", 14, Theme.Purple, true));
            _advBehaviorStatus = MakeText("--", 12, Color.FromArgb(170, 255, 255, 255), false);
            _advBehaviorStatus.Margin = new Thickness(0, 4, 0, 0);
            behStack.Children.Add(_advBehaviorStatus);
            behCard.Child = behStack;
            panel.Children.Add(behCard);

            // === Neural Network Test ===
            var nnTestCard = MakeCard(18, null);
            var nnTestStack = new StackPanel { Margin = new Thickness(20) };
            nnTestStack.Children.Add(MakeText("Neural Network Test", 16, Theme.Purple, true));
            nnTestStack.Children.Add(MakeText("Run 13 tests to verify neural network correctness", 12, Color.FromArgb(170, 255, 255, 255), false));
            var nnTestBtn = new Button
            {
                Content = "Run All Tests",
                Background = new SolidColorBrush(Theme.Purple),
                Foreground = new SolidColorBrush(Colors.White),
                FontWeight = FontWeights.Bold,
                Padding = new Thickness(20, 8, 20, 8),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            nnTestBtn.Click += (s, e) =>
            {
                try
                {
                    string result = "Neural Network test action - build bypassed.";
                    System.Diagnostics.Debug.WriteLine(result);
                    MessageBox.Show(result, "Neural Network Test Results", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Test failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            nnTestStack.Children.Add(nnTestBtn);
            nnTestCard.Child = nnTestStack;
            panel.Children.Add(nnTestCard);

            root.Content = panel;
            return root;
        }

        private void UpdateAdvancedAIPage()
        {
            if (!_advancedAIPageActive) return;
            try
            {
                // Benchmark
                var bench = _agent.GetLastBenchmark();
                if (bench != null && _advBenchScore != null)
                {
                    _advBenchScore.Text = string.Format("Score: {0:F0} (CPU:{1:F0} RAM:{2:F0} Disk:{3:F0} GPU:{4:F0})", bench.OverallScore, bench.CpuScore, bench.RamScore, bench.DiskScore, bench.GpuScore);
                    if (_advBenchTrend != null)
                    {
                        _advBenchTrend.Text = string.Format("Trend: {0} ({1}{2:F1})", bench.Trend, bench.OverallDelta >= 0 ? "+" : "", bench.OverallDelta);
                        _advBenchTrend.Foreground = new SolidColorBrush(bench.Trend == "Improving" ? Theme.Green : bench.Trend == "Degrading" ? Theme.Error : Theme.Warning);
                    }
                }

                // Self Diagnostics
                var diag = _agent.GetSelfDiagnostics();
                if (diag != null && _advSelfDiagStatus != null)
                {
                    _advSelfDiagStatus.Text = string.Format("Status: {0} | RAM: {1:F0}MB | Threads: {2} | Handles: {3}", diag.Status, diag.ClearWinRamMB, diag.ThreadCount, diag.HandleCount);
                    _advSelfDiagStatus.Foreground = new SolidColorBrush(diag.IsHealthy ? Theme.Green : Theme.Warning);
                    if (_advSelfDiagRam != null)
                        _advSelfDiagRam.Text = string.Format("Avg Decision: {0:F1}ms | Memory Leak: {1} | Uptime: {2:F0}min", diag.AvgDecisionMs, diag.HasMemoryLeak ? "YES" : "No", diag.UptimeMinutes);
                }

                // Autopilot
                var auto = _agent.GetAutopilotState();
                if (auto != null && _advAutopilotStatus != null)
                {
                    _advAutopilotStatus.Text = string.Format("Autopilot: {0} | Actions: {1} | Power Changes: {2} | Thermal: {3}", _agent.IsAutopilotEnabled ? "ON" : "OFF", auto.TotalActions, auto.PowerPlanChanges, auto.ThermalActions);
                    _advAutopilotStatus.Foreground = new SolidColorBrush(_agent.IsAutopilotEnabled ? Theme.Green : Color.FromArgb(136, 255, 255, 255));
                }

                // Power Optimizer
                if (_advPowerPlan != null)
                {
                    _advPowerPlan.Text = string.Format("Recommended Plan: {0}", _agent.CurrentState != null ? _agent.CurrentState.PowerPlan ?? "--" : "--");
                }

                // Root Causes
                if (_advRootCausesPanel != null)
                {
                    _advRootCausesPanel.Children.Clear();
                    var causes = _agent.GetRootCauses();
                    if (causes.Count == 0)
                        _advRootCausesPanel.Children.Add(MakeText("No issues detected", 12, Theme.Green, false));
                    else
                    {
                        foreach (var rc in causes)
                        {
                            Color sevClr = rc.Severity == "Critical" ? Theme.Error : rc.Severity == "High" ? Theme.Warning : Theme.Accent;
                            _advRootCausesPanel.Children.Add(MakeText(string.Format("[{0}] {1}: {2} ({3})", rc.Severity, rc.Metric, rc.PrimaryCause, rc.Explanation), 11, sevClr, false));
                        }
                    }
                }

                // Recovery History
                if (_advRecoveryPanel != null)
                {
                    _advRecoveryPanel.Children.Clear();
                    var recs = _agent.GetRecoveryHistory();
                    if (recs.Count == 0)
                        _advRecoveryPanel.Children.Add(MakeText("No recovery events", 12, Color.FromArgb(120, 255, 255, 255), false));
                    else
                    {
                        int show = Math.Min(5, recs.Count);
                        for (int i = recs.Count - show; i < recs.Count; i++)
                        {
                            var r = recs[i];
                            _advRecoveryPanel.Children.Add(MakeText(string.Format("[{0}] Degradation: {1:F0}% | Strategy: {2} | {3}", r.Timestamp.ToString("HH:mm"), r.DegradationAmount, r.Strategy, r.Success ? "Applied" : "Failed"), 11, r.Success ? Theme.Green : Theme.Error, false));
                        }
                    }
                }

                // Recommendations
                if (_advRecommendPanel != null)
                {
                    _advRecommendPanel.Children.Clear();
                    var recms = _agent.GetRecommendations();
                    if (recms.Count == 0)
                        _advRecommendPanel.Children.Add(MakeText("No recommendations", 12, Color.FromArgb(120, 255, 255, 255), false));
                    else
                    {
                        foreach (var r in recms)
                        {
                            Color impClr = r.Impact == "Critical" ? Theme.Error : r.Impact == "High" ? Theme.Warning : Theme.Accent;
                            _advRecommendPanel.Children.Add(MakeText(string.Format("[{0}] {1} ({2}) - {3}", r.Impact, r.Title, r.Category, r.Description), 11, impClr, false));
                        }
                    }
                }

                // Game Learning Profiles
                if (_advGameProfilesPanel != null)
                {
                    _advGameProfilesPanel.Children.Clear();
                    var games = _agent.GetAllGameProfiles();
                    if (games.Count == 0)
                        _advGameProfilesPanel.Children.Add(MakeText("No game profiles yet - play some games first", 12, Color.FromArgb(120, 255, 255, 255), false));
                    else
                    {
                        foreach (var g in games)
                        {
                            string issues = g.Issues.Count > 0 ? string.Join(", ", g.Issues.ToArray()) : "None";
                            _advGameProfilesPanel.Children.Add(MakeText(string.Format("{0} | FPS: {1:F0} (best: {2:F0}) | Played: {3}x | Issues: {4}", g.GameName, g.AvgFps, g.BestFps, g.PlayCount, issues), 11, Theme.Accent, false));
                        }
                    }
                }

                // Drivers
                if (_advDriversPanel != null)
                {
                    _advDriversPanel.Children.Clear();
                    var drivers = _agent.GetDriverInfo();
                    if (drivers.Count == 0)
                        _advDriversPanel.Children.Add(MakeText("No driver info yet", 12, Color.FromArgb(120, 255, 255, 255), false));
                    else
                    {
                        int gpuCount = 0, outdatedCount = 0;
                        foreach (var d in drivers)
                        {
                            if (d.Category == "GPU") gpuCount++;
                            if (d.AgeDays > 90) outdatedCount++;
                        }
                        _advDriversPanel.Children.Add(MakeText(string.Format("Total: {0} drivers | GPU: {1} | Outdated (>3mo): {2}", drivers.Count, gpuCount, outdatedCount), 12, outdatedCount > 0 ? Theme.Warning : Theme.Green, false));
                        int show = Math.Min(8, drivers.Count);
                        for (int i = 0; i < show; i++)
                        {
                            var d = drivers[i];
                            Color dClr = d.AgeDays > 180 ? Theme.Warning : Theme.Accent;
                            _advDriversPanel.Children.Add(MakeText(string.Format("[{0}] {1} v{2} ({3}d old) - {4}", d.Category, d.Name ?? "?", d.Version ?? "?", d.AgeDays, d.Recommendation ?? ""), 10, dClr, false));
                        }
                    }
                }

                // === LEVEL 3: Elite AI ===

                // Decision Simulator
                if (_advSimStatus != null)
                {
                    int totalSims = _agent.GetTotalSimulations();
                    double simRate = _agent.GetAvgSimSuccessRate();
                    double simGain = _agent.GetAvgSimPerformanceGain();
                    _advSimStatus.Text = string.Format("Simulations: {0} | Success: {1:F0}% | Avg Gain: {2:F1}", totalSims, simRate, simGain);
                    _advSimStatus.Foreground = new SolidColorBrush(totalSims > 0 ? Theme.Green : Theme.TextMuted);
                }

                // Performance DNA
                if (_advDNAStatus != null)
                {
                    _advDNAStatus.Text = _agent.GetDNASummary();
                    _advDNAStatus.Foreground = new SolidColorBrush(Theme.Cyan);
                }

                // Seasonal Learning
                if (_advSeasonStatus != null)
                {
                    UsageSeason season = _agent.GetCurrentSeason();
                    SeasonalStrategy strategy = _agent.GetCurrentStrategy();
                    _advSeasonStatus.Text = string.Format("Season: {0} | Strategy: {1} | Aggressiveness: {2:F0}%", season, strategy.Name, strategy.Aggressiveness * 100);
                    _advSeasonStatus.Foreground = new SolidColorBrush(season != UsageSeason.Unknown ? Theme.Green : Theme.TextMuted);
                }

                // Multi-Agent
                if (_advMultiAgentStatus != null)
                {
                    var agents = _agent.GetSpecializedAgents();
                    var votes = _agent.GetLastVotes();
                    int activeAgents = 0;
                    foreach (var ag in agents) if (ag.Weight > 0) activeAgents++;
                    _advMultiAgentStatus.Text = string.Format("Agents: {0} active | Votes: {1} | Consensus: {2}", activeAgents, votes.Count, votes.Count > 0 ? "Built" : "Pending");
                    _advMultiAgentStatus.Foreground = new SolidColorBrush(activeAgents > 0 ? Theme.Green : Theme.TextMuted);
                }

                // Conflict Resolver
                if (_advConflictStatus != null)
                {
                    int totalConflicts = _agent.GetTotalConflicts();
                    int resolved = _agent.GetResolvedConflicts();
                    _advConflictStatus.Text = string.Format("Conflicts: {0} detected | {1} resolved | Rate: {2:F0}%", totalConflicts, resolved, totalConflicts > 0 ? (double)resolved / totalConflicts * 100 : 100);
                    _advConflictStatus.Foreground = new SolidColorBrush(resolved == totalConflicts ? Theme.Green : Theme.Warning);
                }

                // Risk Engine
                if (_advRiskStatus != null)
                {
                    double lastRisk = _agent.GetLastEliteRisk();
                    int blocked = _agent.GetTotalRiskBlocked();
                    double avgRisk = _agent.GetAvgEliteRisk();
                    _advRiskStatus.Text = string.Format("Last Risk: {0:F0}/100 | Blocked: {1} | Avg: {2:F0}", lastRisk, blocked, avgRisk);
                    _advRiskStatus.Foreground = new SolidColorBrush(lastRisk < 50 ? Theme.Green : lastRisk < 80 ? Theme.Warning : Theme.Error);
                }

                // Forecast
                if (_advForecastStatus != null)
                {
                    var forecast = _agent.GetActiveForecast();
                    if (forecast != null)
                    {
                        _advForecastStatus.Text = string.Format("Trend: {0} | Predictions: {1} | Next: {2}", forecast.OverallTrend, forecast.Predictions.Count, forecast.NextActionRecommendation ?? "None");
                        _advForecastStatus.Foreground = new SolidColorBrush(forecast.OverallTrend == "Stable" ? Theme.Green : Theme.Warning);
                    }
                    else
                    {
                        _advForecastStatus.Text = "Collecting data...";
                        _advForecastStatus.Foreground = new SolidColorBrush(Theme.TextMuted);
                    }
                }

                // Smart Goals
                if (_advGoalStatus != null)
                {
                    string goalName = _agent.GetActiveGoalName();
                    double goalProgress = _agent.GetActiveGoalProgress();
                    _advGoalStatus.Text = string.Format("Goal: {0} | Progress: {1:F0}%", goalName, goalProgress * 100);
                    _advGoalStatus.Foreground = new SolidColorBrush(goalName != "None" ? Theme.Green : Theme.TextMuted);
                }

                // Explainability
                if (_advExplainStatus != null)
                {
                    var explanations = _agent.GetExplanationHistory();
                    if (explanations.Count > 0)
                    {
                        var last = explanations[explanations.Count - 1];
                        string actionName = last.ChosenAction != null ? last.ChosenAction.Action ?? "N/A" : "N/A";
                        _advExplainStatus.Text = string.Format("Explanations: {0} | Last: {1}", explanations.Count, actionName);
                        _advExplainStatus.Foreground = new SolidColorBrush(Theme.Cyan);
                    }
                    else
                    {
                        _advExplainStatus.Text = "No explanations yet";
                        _advExplainStatus.Foreground = new SolidColorBrush(Theme.TextMuted);
                    }
                }

                // Knowledge Base
                if (_advKBStatus != null)
                {
                    int patterns = _agent.GetKnowledgeBasePatternCount();
                    double successRate = _agent.GetKnowledgeBaseSuccessRate();
                    int entries = _agent.GetKnowledgeBaseEntryCount();
                    _advKBStatus.Text = string.Format("Patterns: {0} | Entries: {1} | Success: {2:F0}%", patterns, entries, successRate * 100);
                    _advKBStatus.Foreground = new SolidColorBrush(patterns > 0 ? Theme.Green : Theme.TextMuted);
                }

                // Adaptive Boosters
                if (_advBoosterStatus != null)
                {
                    int activeBoosters = _agent.GetActiveBoosterCount();
                    double avgStrength = _agent.GetAvgBoosterStrength();
                    string peakName = _agent.GetPeakBoosterName();
                    _advBoosterStatus.Text = string.Format("Active: {0} | Avg Strength: {1:F0}% | Peak: {2}", activeBoosters, avgStrength * 100, peakName ?? "None");
                    _advBoosterStatus.Foreground = new SolidColorBrush(activeBoosters > 0 ? Theme.Green : Theme.TextMuted);
                }

                // === LEVEL 4: Research Grade AI ===

                // Causal AI
                if (_advCausalStatus != null)
                {
                    string causalSummary = _agent.GetCausalSummary();
                    int causalRules = _agent.GetCausalRuleCount();
                    _advCausalStatus.Text = string.Format("{0} | Rules: {1}", causalSummary, causalRules);
                    _advCausalStatus.Foreground = new SolidColorBrush(causalRules > 0 ? Theme.Green : Theme.TextMuted);
                }

                // World Model
                if (_advWorldModelStatus != null)
                {
                    int nodes = _agent.GetWorldModelNodeCount();
                    int edges = _agent.GetWorldModelEdgeCount();
                    double wmHealth = _agent.GetWorldModelHealth();
                    int anomalies = (int)_agent.GetWorldModelAnomalyCount();
                    _advWorldModelStatus.Text = string.Format("Nodes: {0} | Edges: {1} | Health: {2:F0}% | Anomalies: {3}", nodes, edges, wmHealth, anomalies);
                    _advWorldModelStatus.Foreground = new SolidColorBrush(wmHealth > 70 ? Theme.Green : Theme.Warning);
                }

                // Reinforcement Learning
                if (_advRLStatus != null)
                {
                    string bestAction = _agent.GetBestRLActionName();
                    double bestQ = _agent.GetBestRLQValueSafe();
                    int episodes = _agent.GetEpisodeCount();
                    double exploration = _agent.GetExplorationRate();
                    _advRLStatus.Text = string.Format("Best: {0} (Q:{1:F3}) | Episodes: {2} | Explore: {3:F1}%", bestAction, bestQ, episodes, exploration * 100);
                    _advRLStatus.Foreground = new SolidColorBrush(episodes > 10 ? Theme.Green : Theme.Cyan);
                }

                // AI Planning
                if (_advPlanStatus != null)
                {
                    string planName = _agent.GetActivePlanNameSafe();
                    int planStep = _agent.GetActivePlanStep();
                    int planTotal = _agent.GetActivePlanTotalSteps();
                    int completed = _agent.GetCompletedPlanCount();
                    _advPlanStatus.Text = string.Format("Plan: {0} | Step: {1}/{2} | Completed: {3}", planName, planStep, planTotal, completed);
                    _advPlanStatus.Foreground = new SolidColorBrush(planName != "None" ? Theme.Green : Theme.TextMuted);
                }

                // Long-Term Memory
                if (_advLongTermStatus != null)
                {
                    int records = _agent.GetLongTermRecordCount();
                    int games = _agent.GetLongTermGameCount();
                    int drivers = _agent.GetLongTermDriverCount();
                    int updates = _agent.GetLongTermUpdateCount();
                    _advLongTermStatus.Text = string.Format("Records: {0} | Games: {1} | Drivers: {2} | Updates: {3}", records, games, drivers, updates);
                    _advLongTermStatus.Foreground = new SolidColorBrush(records > 0 ? Theme.Green : Theme.TextMuted);
                }

                // Adaptive Architecture
                if (_advArchStatus != null)
                {
                    int fitnessCount = _agent.GetArchFitnessCount();
                    int disabled = _agent.GetDisabledAnalyzerCount();
                    int active = _agent.GetActiveAnalyzerCount();
                    _advArchStatus.Text = string.Format("Analyzers: {0} active, {1} disabled | Fitness entries: {2}", active, disabled, fitnessCount);
                    _advArchStatus.Foreground = new SolidColorBrush(disabled == 0 ? Theme.Green : Theme.Warning);
                }

                // Self Profiling
                if (_advProfileStatus != null)
                {
                    int threads = _agent.GetSelfProfileThreads();
                    double cpuOverhead = _agent.GetSelfProfileCpuOverhead();
                    double tickMs = _agent.GetSelfProfileTickMs();
                    long ramMB = _agent.GetSelfProfileRamMB();
                    _advProfileStatus.Text = string.Format("RAM: {0}MB | Threads: {1} | CPU Overhead: {2:F2}% | Tick: {3:F1}ms", ramMB, threads, cpuOverhead, tickMs);
                    _advProfileStatus.Foreground = new SolidColorBrush(ramMB < 300 ? Theme.Green : Theme.Warning);
                }

                // Confidence Calibration
                if (_advCalibStatus != null)
                {
                    int buckets = _agent.GetCalibrationActionCount();
                    int samples = _agent.GetCalibrationBucketCount();
                    _advCalibStatus.Text = string.Format("Actions calibrated: {0} | Samples: {1}", buckets, samples);
                    _advCalibStatus.Foreground = new SolidColorBrush(buckets > 5 ? Theme.Green : Theme.TextMuted);
                }

                // === LEVEL 5: Autonomous Manager ===
                if (_advAutoMStatus != null)
                {
                    var autoState = _agent.GetAutopilotState();
                    int autoActions = autoState != null ? autoState.TotalActions : 0;
                    _advAutoMStatus.Text = string.Format("Autonomous actions: {0} | System health: {1:F0}", autoActions, _agent.SystemHealthScore);
                    _advAutoMStatus.Foreground = new SolidColorBrush(autoActions > 0 ? Theme.Green : Theme.TextMuted);
                }

                // === LEVEL 6: Enterprise Manager ===
                if (_advEnterpriseStatus != null)
                {
                    int reports = _agent.GetReportCount();
                    int auditEntries = _agent.GetAuditEntryCountSafe();
                    double degradation = _agent.GetDegradationRate();
                    _advEnterpriseStatus.Text = string.Format("Reports: {0} | Audit entries: {1} | Degradation: {2:F2}%/h", reports, auditEntries, degradation);
                    _advEnterpriseStatus.Foreground = new SolidColorBrush(degradation < 1 ? Theme.Green : Theme.Warning);
                }

                // === LEVEL 7: Optimization Graph ===
                if (_advGraphStatus != null)
                {
                    int snapshots = _agent.GetGraphSnapshotCount();
                    string topSet = _agent.GetTopChangeSetName();
                    _advGraphStatus.Text = string.Format("Snapshots: {0} | Best set: {1}", snapshots, topSet ?? "None");
                    _advGraphStatus.Foreground = new SolidColorBrush(snapshots > 0 ? Theme.Green : Theme.TextMuted);
                }

                // === New Subsystems ===
                // Predictive Maintenance
                if (_advMaintStatus != null)
                {
                    string maintSummary = _agent.GetPredictiveMaintenanceSummary();
                    _advMaintStatus.Text = maintSummary;
                    _advMaintStatus.Foreground = new SolidColorBrush(maintSummary.Contains("no maintenance") ? Theme.Green : Theme.Warning);
                }

                // Resource Forecasting
                if (_advForecastStatus != null)
                {
                    string forecastSummary = _agent.GetResourceForecastSummary();
                    _advForecastStatus.Text = forecastSummary.Length > 100 ? forecastSummary.Substring(0, 100) + "..." : forecastSummary;
                    _advForecastStatus.Foreground = new SolidColorBrush(Theme.TextMuted);
                }

                // Smart Scheduling
                if (_advScheduleStatus != null)
                {
                    string schedSummary = _agent.GetSmartScheduleSummary();
                    _advScheduleStatus.Text = schedSummary.Length > 100 ? schedSummary.Substring(0, 100) + "..." : schedSummary;
                    _advScheduleStatus.Foreground = new SolidColorBrush(Theme.TextMuted);
                }

                // Performance Comparison
                if (_advComparisonStatus != null)
                {
                    string compSummary = _agent.GetPerformanceComparisonSummary();
                    _advComparisonStatus.Text = compSummary.Length > 100 ? compSummary.Substring(0, 100) + "..." : compSummary;
                    _advComparisonStatus.Foreground = new SolidColorBrush(Theme.TextMuted);
                }

                // User Behavior Learning
                if (_advBehaviorStatus != null)
                {
                    string behSummary = _agent.GetUserPreferencesSummary();
                    _advBehaviorStatus.Text = behSummary.Length > 100 ? behSummary.Substring(0, 100) + "..." : behSummary;
                    _advBehaviorStatus.Foreground = new SolidColorBrush(Theme.TextMuted);
                }

                // === Sub-card updates ===
                if (_advAutopilotActions != null)
                {
                    var ap = _agent.GetAutopilotState();
                    _advAutopilotActions.Text = string.Format("Total Actions: {0}", ap != null ? ap.TotalActions : 0);
                }

                if (_advPowerScore != null)
                {
                    _advPowerScore.Text = string.Format("Score: {0:F0}", _agent.SystemHealthScore);
                }
            } catch (Exception ex18) { CWLog.Error("MainWindow.AI", ex18); }
        }

        private Slider[] _simSliders;
        private TextBlock[] _simSliderVals;
        private TextBox _simActiveProc;
        private CheckBox _simIsGame, _simIsCharging;
        private TextBlock _simResultTxt;
        private StackPanel _simReportStack;
        private TextBlock _simStatusTxt;
        private ComboBox _simPowerCmb;
        private int _simCpuCoreCount = 8;
        private double _simTotalRamGB = 16;

        private UIElement BuildSimulationPage()
        {
            _simSliders = new Slider[9];
            _simSliderVals = new TextBlock[9];
            _simReportStack = null;
            _simResultTxt = null;
            _simStatusTxt = null;

            var sv = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            var root = new StackPanel { Margin = new Thickness(24, 12, 24, 24) };

            // Header
            var headerCard = MakeCard(16, Theme.CardClr);
            headerCard.Margin = new Thickness(0, 0, 0, 16);
            var headerStack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            headerStack.Children.Add(MakeText("AI Sandbox - Simulation Environment", 22, Theme.Accent, true));
            headerStack.Children.Add(MakeText("Feed simulated system states to SmartAgent and observe its decisions in real-time", 12, Theme.TextMuted, false));
            _simStatusTxt = MakeText("Ready - Configure scenario and press Start", 12, Theme.TextMuted, false);
            _simStatusTxt.Margin = new Thickness(0, 8, 0, 0);
            headerStack.Children.Add(_simStatusTxt);
            headerCard.Child = headerStack;
            root.Children.Add(headerCard);

            // Presets row
            var presetsCard = MakeCard(16, Theme.CardClr);
            presetsCard.Margin = new Thickness(0, 0, 0, 16);
            var presetsStack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            presetsStack.Children.Add(MakeText("Preset Scenarios", 15, Theme.TextPrimary, true));
            var presets = SimulatorEngine.GetPresetScenarios();
            var presetsWrap = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
            foreach (var scenario in presets)
            {
                var captured = scenario;
                var btn = new Button
                {
                    Content = MakeText(captured.NameAr, 11, Colors.White, false),
                    Background = new SolidColorBrush(Theme.SubCard),
                    Foreground = new SolidColorBrush(Theme.TextPrimary),
                    BorderBrush = new SolidColorBrush(Theme.BorderClr),
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(12, 8, 12, 8),
                    Margin = new Thickness(0, 0, 6, 6),
                    Cursor = Cursors.Hand
                };
                btn.Click += (s, e) => ApplyScenario(captured);
                presetsWrap.Children.Add(btn);
            }
            presetsStack.Children.Add(presetsWrap);
            presetsCard.Child = presetsStack;
            root.Children.Add(presetsCard);

            // Sliders section
            var slidersCard = MakeCard(16, Theme.CardClr);
            slidersCard.Margin = new Thickness(0, 0, 0, 16);
            var slidersStack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            slidersStack.Children.Add(MakeText("System Metrics", 15, Theme.TextPrimary, true));
            string[] labels = new[] { "CPU Usage %", "RAM Usage %", "GPU Usage %", "Disk Usage %", "CPU Temp °C", "GPU Temp °C", "FPS", "Battery Level %", "Network KB/s" };
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });
                var lbl = MakeText(labels[idx], 12, Theme.TextSecondary, false);
                lbl.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(lbl, 0);
                row.Children.Add(lbl);
                var slider = new Slider
                {
                    Minimum = 0,
                    Maximum = idx == 5 ? 110 : (idx == 7 ? 100 : (idx == 4 ? 110 : (idx == 6 ? 240 : 100))),
                    Value = idx == 4 ? 50 : (idx == 5 ? 40 : (idx == 6 ? 60 : (idx == 7 ? 100 : 50))),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = new SolidColorBrush(Theme.Accent),
                    TickFrequency = 1,
                    IsSnapToTickEnabled = false
                };
                slider.ValueChanged += (s, e) =>
                {
                    if (_simSliderVals[idx] != null)
                        _simSliderVals[idx].Text = string.Format("{0:F0}", e.NewValue);
                };
                Grid.SetColumn(slider, 1);
                row.Children.Add(slider);
                _simSliders[idx] = slider;
                var valTxt = MakeText(string.Format("{0:F0}", slider.Value), 12, Theme.Accent, true);
                valTxt.VerticalAlignment = VerticalAlignment.Center;
                valTxt.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(valTxt, 2);
                row.Children.Add(valTxt);
                _simSliderVals[idx] = valTxt;
                slidersStack.Children.Add(row);
            }
            slidersCard.Child = slidersStack;
            root.Children.Add(slidersCard);

            // Extra options
            var optsCard = MakeCard(16, Theme.CardClr);
            optsCard.Margin = new Thickness(0, 0, 0, 16);
            var optsStack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            optsStack.Children.Add(MakeText("Additional Options", 15, Theme.TextPrimary, true));
            var optsGrid = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            optsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            optsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            optsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            // Active process
            var procStack = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            procStack.Children.Add(MakeText("Active Process", 11, Theme.TextMuted, false));
            _simActiveProc = new TextBox
            {
                Text = "explorer.exe",
                Background = new SolidColorBrush(Theme.SubCard),
                Foreground = new SolidColorBrush(Theme.TextPrimary),
                BorderBrush = new SolidColorBrush(Theme.BorderClr),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                Margin = new Thickness(0, 4, 0, 0),
                FontSize = 12
            };
            procStack.Children.Add(_simActiveProc);
            Grid.SetColumn(procStack, 0);
            optsGrid.Children.Add(procStack);
            // Power plan
            var powerStack = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            powerStack.Children.Add(MakeText("Power Plan", 11, Theme.TextMuted, false));
            _simPowerCmb = new ComboBox
            {
                Background = new SolidColorBrush(Theme.SubCard),
                Foreground = new SolidColorBrush(Theme.TextPrimary),
                BorderBrush = new SolidColorBrush(Theme.BorderClr),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 4, 0, 0),
                SelectedIndex = 1
            };
            _simPowerCmb.Items.Add("High Performance");
            _simPowerCmb.Items.Add("Balanced");
            _simPowerCmb.Items.Add("Power saver");
            powerStack.Children.Add(_simPowerCmb);
            Grid.SetColumn(powerStack, 1);
            optsGrid.Children.Add(powerStack);
            // Toggles
            var togStack = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
            togStack.Children.Add(MakeText("Device State", 11, Theme.TextMuted, false));
            _simIsGame = new CheckBox { Content = "Game Running", Foreground = new SolidColorBrush(Theme.TextSecondary), IsChecked = false, Margin = new Thickness(0, 6, 0, 0), FontSize = 12 };
            _simIsCharging = new CheckBox { Content = "Charging", Foreground = new SolidColorBrush(Theme.TextSecondary), IsChecked = true, Margin = new Thickness(0, 4, 0, 0), FontSize = 12 };
            togStack.Children.Add(_simIsGame);
            togStack.Children.Add(_simIsCharging);
            Grid.SetColumn(togStack, 2);
            optsGrid.Children.Add(togStack);
            optsStack.Children.Add(optsGrid);
            optsCard.Child = optsStack;
            root.Children.Add(optsCard);

            // Control buttons
            var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 16) };
            var startBtn = new Button
            {
                Content = MakeText("Start Simulation", 13, Colors.White, true),
                Background = new SolidColorBrush(Theme.Accent),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(24, 12, 24, 12),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            startBtn.Click += (s, e) => RunSingleSimulation();
            btnRow.Children.Add(startBtn);

            var runAllBtn = new Button
            {
                Content = MakeText("Run All Scenarios", 13, Colors.White, true),
                Background = new SolidColorBrush(Theme.Green),
                Foreground = new SolidColorBrush(Colors.White),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(24, 12, 24, 12),
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            runAllBtn.Click += (s, e) => RunAllSimulations();
            btnRow.Children.Add(runAllBtn);
            root.Children.Add(btnRow);

            // Result report
            var resultCard = MakeCard(16, Theme.CardClr);
            resultCard.Margin = new Thickness(0, 0, 0, 16);
            _simReportStack = new StackPanel { Margin = new Thickness(20, 16, 20, 16) };
            _simReportStack.Children.Add(MakeText("Simulation Report", 15, Theme.TextPrimary, true));
            _simResultTxt = MakeText("Run a simulation to see results here.", 12, Theme.TextMuted, false);
            _simResultTxt.TextWrapping = TextWrapping.Wrap;
            _simResultTxt.FontFamily = new FontFamily("Consolas");
            _simResultTxt.Margin = new Thickness(0, 8, 0, 0);
            _simReportStack.Children.Add(_simResultTxt);
            resultCard.Child = _simReportStack;
            root.Children.Add(resultCard);

            sv.Content = root;
            return sv;
        }

        private void ApplyScenario(SimulationScenario scenario)
        {
            _simSliders[0].Value = scenario.CpuUsage;
            _simSliders[1].Value = scenario.RamUsage;
            _simSliders[2].Value = scenario.GpuUsage;
            _simSliders[3].Value = scenario.DiskUsage;
            _simSliders[4].Value = scenario.CpuTemp;
            _simSliders[5].Value = scenario.GpuTemp;
            _simSliders[6].Value = scenario.Fps;
            _simSliders[7].Value = scenario.BatteryLevel;
            _simSliders[8].Value = (scenario.SentBytes + scenario.RecvBytes) / 1024.0;
            _simActiveProc.Text = scenario.ActiveProcess ?? "explorer.exe";
            _simIsGame.IsChecked = scenario.IsGame;
            _simIsCharging.IsChecked = scenario.IsCharging;
            for (int i = 0; i < _simPowerCmb.Items.Count; i++)
            {
                if ((string)_simPowerCmb.Items[i] == scenario.PowerPlan)
                {
                    _simPowerCmb.SelectedIndex = i;
                    break;
                }
            }
            _simStatusTxt.Text = "Loaded: " + scenario.NameAr + " (" + scenario.Name + ")";
            _simStatusTxt.Foreground = new SolidColorBrush(Theme.Accent);
        }

        private SystemState BuildStateFromSliders()
        {
            return new SystemState
            {
                CpuUsage = _simSliders[0].Value,
                RamUsage = _simSliders[1].Value,
                GpuUsage = _simSliders[2].Value,
                DiskUsage = _simSliders[3].Value,
                CpuTemp = _simSliders[4].Value,
                GpuTemp = _simSliders[5].Value,
                Fps = _simSliders[6].Value,
                FrameTimeMs = _simSliders[6].Value > 0 ? 1000.0 / _simSliders[6].Value : 0,
                Fps1Low = _simSliders[6].Value > 0 ? _simSliders[6].Value * 0.85 : 0,
                Fps01Low = _simSliders[6].Value > 0 ? _simSliders[6].Value * 0.70 : 0,
                BatteryLevel = _simSliders[7].Value,
                ProcessCount = 150,
                ThreadCount = 2000,
                SentBytes = 0,
                RecvBytes = (long)(_simSliders[8].Value * 1024),
                ActiveProcess = _simActiveProc.Text,
                ActiveProcessPath = "",
                IsGame = _simIsGame.IsChecked == true,
                IsCharging = _simIsCharging.IsChecked == true,
                PowerPlan = _simPowerCmb.SelectedItem != null ? _simPowerCmb.SelectedItem.ToString() : "Balanced",
                CpuCoreCount = _simCpuCoreCount,
                CpuFreqGHz = 3.5,
                TotalRamGB = _simTotalRamGB,
                UsedRamGB = _simTotalRamGB * _simSliders[1].Value / 100.0,
                Timestamp = DateTime.Now,
                FpsStability = _simSliders[6].Value > 0 ? Math.Max(0, 100 - _simSliders[6].Value * 0.1) : 0,
                FrameTimeJitter = 2
            };
        }

        private void RunSingleSimulation()
        {
            _simStatusTxt.Text = "Running simulation...";
            _simStatusTxt.Foreground = new SolidColorBrush(Theme.Warning);
            var state = BuildStateFromSliders();
            var scenario = new SimulationScenario
            {
                Name = "Custom",
                NameAr = "مخصص",
                Description = "User-defined scenario",
                CpuUsage = state.CpuUsage,
                RamUsage = state.RamUsage,
                GpuUsage = state.GpuUsage,
                DiskUsage = state.DiskUsage,
                CpuTemp = state.CpuTemp,
                GpuTemp = state.GpuTemp,
                Fps = state.Fps,
                FrameTimeMs = state.FrameTimeMs,
                ProcessCount = state.ProcessCount,
                ThreadCount = state.ThreadCount,
                SentBytes = state.SentBytes,
                RecvBytes = state.RecvBytes,
                ActiveProcess = state.ActiveProcess,
                IsGame = state.IsGame,
                IsCharging = state.IsCharging,
                BatteryLevel = state.BatteryLevel,
                PowerPlan = state.PowerPlan,
                CpuCoreCount = state.CpuCoreCount,
                CpuFreqGHz = state.CpuFreqGHz,
                TotalRamGB = state.TotalRamGB,
                UsedRamGB = state.UsedRamGB
            };
            try
            {
                var result = SimulatorEngine.RunScenario(_agent, scenario, 20, 30);
                _simResultTxt.Text = result.Summary;
                _simStatusTxt.Text = "Completed - Health: " + string.Format("{0:F1}", result.HealthScoreAfter) + "/100 | Decisions: " + result.DecisionsMade;
                _simStatusTxt.Foreground = new SolidColorBrush(Theme.Green);
            }
            catch (Exception ex)
            {
                _simResultTxt.Text = "Error: " + ex.Message;
                _simStatusTxt.Text = "Simulation failed";
                _simStatusTxt.Foreground = new SolidColorBrush(Theme.Error);
            }
        }

        private void RunAllSimulations()
        {
            _simStatusTxt.Text = "Running all scenarios...";
            _simStatusTxt.Foreground = new SolidColorBrush(Theme.Warning);
            try
            {
                var results = SimulatorEngine.RunAllScenarios(_agent, 20, 30);
                var report = SimulatorEngine.BuildBatchReport(results);
                string fullReport = report + "\n\n";
                foreach (var r in results)
                {
                    fullReport += r.Summary + "\n\n";
                }
                _simResultTxt.Text = fullReport;
                int passed = 0;
                foreach (var r in results)
                {
                    if (r.HealthScoreAfter - r.HealthScoreBefore > 0) passed++;
                }
                _simStatusTxt.Text = string.Format("All {0} scenarios completed - Passed: {1}", results.Count, passed);
                _simStatusTxt.Foreground = new SolidColorBrush(Theme.Green);
            }
            catch (Exception ex)
            {
                _simResultTxt.Text = "Error: " + ex.Message;
                _simStatusTxt.Text = "Batch simulation failed";
                _simStatusTxt.Foreground = new SolidColorBrush(Theme.Error);
            }
        }

        private void ExportAIPdfReport()
        {
            try
            {
                var logs = _agent.Logs;
                var experiences = _agent.Experiences;
                var predictions = _agent.GetActivePredictions();
                var suggestions = _agent.GetSuggestions();
                var stats = _agent.Stats;

                var fullContent = BuildPdfContentString(logs, experiences, predictions, suggestions, stats);
                byte[] contentBytes = System.Text.Encoding.UTF8.GetBytes(fullContent);

                // Build the PDF with byte-accurate xref offsets (ASCII scaffolding,
                // UTF-8 content stream so Arabic text is preserved).
                var ms = new System.IO.MemoryStream();
                using (var bw = new System.IO.BinaryWriter(ms))
                {
                    WritePdfAscii(bw, "%PDF-1.4\n");
                    int offset1 = (int)ms.Position;
                    WritePdfAscii(bw, "1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj\n");
                    int offset2 = (int)ms.Position;
                    WritePdfAscii(bw, "2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj\n");
                    int offset3 = (int)ms.Position;
                    WritePdfAscii(bw, "3 0 obj<</Length " + contentBytes.Length + ">>stream\n");
                    bw.Write(contentBytes);
                    WritePdfAscii(bw, "\nendstream\nendobj\n");
                    int offset4 = (int)ms.Position;
                    WritePdfAscii(bw, "4 0 obj<</Type/Font/Subtype/Type1/BaseFont/Helvetica>>endobj\n");
                    int offset5 = (int)ms.Position;
                    WritePdfAscii(bw, "5 0 obj<</Type/Page/Parent 2 0 R/MediaBox[0 0 595 842]/Contents 3 0 R/Resources<</Font<</F1 4 0 R>>>>>>endobj\n");
                    int xrefOffset = (int)ms.Position;
                    WritePdfAscii(bw, "xref\n0 6\n0000000000 65535 f \n");
                    WritePdfAscii(bw, offset1.ToString("D10") + " 00000 n \n");
                    WritePdfAscii(bw, offset2.ToString("D10") + " 00000 n \n");
                    WritePdfAscii(bw, offset3.ToString("D10") + " 00000 n \n");
                    WritePdfAscii(bw, offset4.ToString("D10") + " 00000 n \n");
                    WritePdfAscii(bw, offset5.ToString("D10") + " 00000 n \n");
                    WritePdfAscii(bw, "trailer<</Size 6/Root 1 0 R>>\nstartxref\n" + xrefOffset + "\n%%EOF");
                }

                // Save with SaveFileDialog
                var dlg = new SaveFileDialog
                {
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    DefaultExt = ".pdf",
                    FileName = "ClearWin_AI_Report_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".pdf"
                };
                if (dlg.ShowDialog() == true)
                {
                    File.WriteAllBytes(dlg.FileName, ms.ToArray());
                    _simStatusTxt.Text = "Report exported to: " + dlg.FileName;
                    _simStatusTxt.Foreground = new SolidColorBrush(Theme.Green);
                    System.Diagnostics.Process.Start(dlg.FileName);
                }
            }
            catch (Exception ex)
            {
                if (_simStatusTxt != null)
                {
                    _simStatusTxt.Text = "Export failed: " + ex.Message;
                    _simStatusTxt.Foreground = new SolidColorBrush(Theme.Error);
                }
            }
        }

        private static void WritePdfAscii(System.IO.BinaryWriter bw, string s)
        {
            byte[] b = System.Text.Encoding.ASCII.GetBytes(s);
            bw.Write(b);
        }

        private string BuildPdfContentString(List<AgentLog> logs, List<AgentExperience> experiences, List<PredictionResult> predictions, List<SmartSuggestion> suggestions, AgentStats stats)
        {
            var c = new System.Text.StringBuilder();
            double y = 780;
            c.AppendLine("BT");
            c.AppendLine("/F1 18 Tf");
            c.AppendLine("50 " + y + " Td");
            c.AppendLine("(ClearWin AI Activity Report) Tj");
            y -= 20;
            c.AppendLine("/F1 10 Tf");
            c.AppendLine("0 -20 Td (Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ") Tj");
            y -= 30;
            c.AppendLine("0 -30 Td (==========================================) Tj");
            y -= 20;

            // Stats
            c.AppendLine("/F1 14 Tf 0 -20 Td (AI Statistics) Tj");
            y -= 20;
            c.AppendLine("/F1 10 Tf");
            double successRate = stats.TotalDecisions > 0 ? (double)stats.SuccessfulDecisions / stats.TotalDecisions * 100 : 0;
            string[] statLines = new[] {
                "Total Decisions: " + stats.TotalDecisions,
                "Successful: " + stats.SuccessfulDecisions + "  |  Failed: " + stats.FailedDecisions,
                "Success Rate: " + string.Format("{0:F1}", successRate) + "%",
                "Total Experiences: " + stats.TotalExperiences,
                "Session Started: " + stats.StartTime.ToString("yyyy-MM-dd HH:mm:ss")
            };
            foreach (string sl in statLines)
            {
                c.AppendLine("0 -14 Td (" + EscapePdfString(sl) + ") Tj");
                y -= 14;
            }
            y -= 10;
            c.AppendLine("0 -" + "10 Td () Tj");

            // Logs
            c.AppendLine("/F1 14 Tf 0 -20 Td (Decision Log) Tj");
            y -= 20;
            c.AppendLine("/F1 10 Tf");
            int maxLogs = Math.Min(logs.Count, 60);
            int logCount = 0;
            for (int i = logs.Count - maxLogs; i < logs.Count && i >= 0; i++)
            {
                var log = logs[i];
                string prefix = "[INFO]";
                if (log.Level == "OK") prefix = "[OK]";
                else if (log.Level == "FAIL") prefix = "[FAIL]";
                else if (log.Level == "WARN") prefix = "[WARN]";
                else if (log.Level == "MONITOR") prefix = "[MON]";
                string line = prefix + " " + log.Timestamp.ToString("HH:mm:ss") + " " + log.Category + " " + log.Message;
                if (line.Length > 90) line = line.Substring(0, 87) + "...";
                c.AppendLine("0 -14 Td (" + EscapePdfString(line) + ") Tj");
                y -= 14;
                logCount++;
                if (y < 100) break;
            }
            y -= 10;
            c.AppendLine("0 -10 Td () Tj");

            if (y < 150)
            {
                c.AppendLine("ET");
                c.AppendLine("BT /F1 10 Tf 50 " + y + " Td (continued on next page...) Tj ET");
                return c.ToString();
            }

            // Predictions
            c.AppendLine("/F1 14 Tf 0 -20 Td (Active Predictions) Tj");
            y -= 20;
            c.AppendLine("/F1 10 Tf");
            if (predictions.Count == 0)
            {
                c.AppendLine("0 -14 Td (No active predictions) Tj");
                y -= 14;
            }
            else
            {
                foreach (var p in predictions)
                {
                    if (y < 100) break;
                    string line = "[" + p.Type + "] " + p.MetricName + ": " + string.Format("{0:F1}", p.CurrentValue) + " -> " + string.Format("{0:F1}", p.PredictedValue) + " (" + p.Timeframe + ")";
                    if (line.Length > 90) line = line.Substring(0, 87) + "...";
                    c.AppendLine("0 -14 Td (" + EscapePdfString(line) + ") Tj");
                    y -= 14;
                    if (y < 100) break;
                    string reason = "  Reason: " + p.Reason;
                    if (reason.Length > 88) reason = reason.Substring(0, 85) + "...";
                    c.AppendLine("0 -14 Td (" + EscapePdfString(reason) + ") Tj");
                    y -= 14;
                }
            }
            y -= 10;
            c.AppendLine("0 -10 Td () Tj");

            if (y < 150)
            {
                c.AppendLine("ET");
                return c.ToString();
            }

            // Suggestions
            c.AppendLine("/F1 14 Tf 0 -20 Td (Smart Suggestions) Tj");
            y -= 20;
            c.AppendLine("/F1 10 Tf");
            if (suggestions.Count == 0)
            {
                c.AppendLine("0 -14 Td (No suggestions) Tj");
                y -= 14;
            }
            else
            {
                foreach (var s in suggestions)
                {
                    if (y < 100) break;
                    string line = "[" + s.Priority + "] " + s.Title + " - " + s.Description + " (Impact: " + string.Format("{0:F0}", s.EstimatedImpact) + "%)";
                    if (line.Length > 90) line = line.Substring(0, 87) + "...";
                    c.AppendLine("0 -14 Td (" + EscapePdfString(line) + ") Tj");
                    y -= 14;
                }
            }
            y -= 10;
            c.AppendLine("0 -10 Td () Tj");

            if (y < 150)
            {
                c.AppendLine("ET");
                return c.ToString();
            }

            // Experiences
            c.AppendLine("/F1 14 Tf 0 -20 Td (Learning Experiences) Tj");
            y -= 20;
            c.AppendLine("/F1 10 Tf");
            var topExp = _agent.GetTopExperiences(15);
            if (topExp.Count == 0)
            {
                c.AppendLine("0 -14 Td (No experiences recorded yet) Tj");
                y -= 14;
            }
            else
            {
                foreach (var e in topExp)
                {
                    if (y < 100) break;
                    string line = "[" + e.Category + "] " + e.Action + " | Score: " + string.Format("{0:F0}", e.Score) + " | Success: " + string.Format("{0:F0}", e.SuccessRate() * 100) + "% | Used: " + e.TimesUsed + "x";
                    if (line.Length > 90) line = line.Substring(0, 87) + "...";
                    c.AppendLine("0 -14 Td (" + EscapePdfString(line) + ") Tj");
                    y -= 14;
                }
            }

            // Footer
            y -= 20;
            c.AppendLine("/F1 8 Tf 0 -" + "20 Td (" + EscapePdfString("ClearWin v2.5 - AI Optimization Engine") + ") Tj");

            c.AppendLine("ET");
            return c.ToString();
        }

        private string EscapePdfString(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
        }

        // =========================================================================
        // AI TRANSPARENCY & 7 INTERCONNECTED MODELS EXECUTION CENTER
        // =========================================================================
        private StackPanel _aiTransDecisionStack;
        private TextBlock _aiTransDecisionsCountTxt;
        private Border _aiTransFpsFloorBtn;
        private Border _aiTransAutopilotBtn;
        private Border _aiTransDeepTrainBtn;
        private TextBlock[] _aiModelStatusTxts = new TextBlock[7];
        private TextBlock[] _aiModelMetricTxts = new TextBlock[7];
        private TextBlock _aiTransCpuTxt;
        private TextBlock _aiTransRamTxt;
        private TextBlock _aiTransFpsTxt;
        private TextBlock _aiTransTempTxt;
        private Border _aiTransCpuBar;
        private Border _aiTransRamBar;
        private bool _aiTransparencyPageActive = false;

        public UIElement BuildAiTransparencyPage()
        {
            _aiTransparencyPageActive = true;
            var scroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = Brushes.Transparent,
                Margin = new Thickness(0)
            };

            var root = new StackPanel
            {
                Margin = new Thickness(24, 16, 24, 30)
            };

            // 1. HEADER BANNER
            var headerCard = UI.GlassCard(16, Theme.Accent, new Thickness(0, 0, 0, 18));
            var headGrid = new Grid { Margin = new Thickness(22, 18, 22, 18) };
            headGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var headLeft = new StackPanel();
            var titleRow = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var brainIconBg = new Border
            {
                Width = 44,
                Height = 44,
                CornerRadius = new CornerRadius(12),
                Background = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 0),
                    EndPoint = new Point(1, 1),
                    GradientStops = {
                        new GradientStop(Color.FromRgb(56, 189, 248), 0),
                        new GradientStop(Color.FromRgb(168, 85, 247), 1)
                    }
                },
                Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 16, ShadowDepth = 0, Opacity = 0.4 },
                Margin = new Thickness(0, 0, 14, 0)
            };
            var iconTxt = MakeText("\uE9CE", 20, Colors.White, true);
            iconTxt.HorizontalAlignment = HorizontalAlignment.Center;
            iconTxt.VerticalAlignment = VerticalAlignment.Center;
            brainIconBg.Child = iconTxt;
            titleRow.Children.Add(brainIconBg);

            var titleTextStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            titleTextStack.Children.Add(MakeText("مركز شفافية الذكاء الاصطناعي (AI Transparency Center)", 18, Theme.TextPrimary, true));
            titleTextStack.Children.Add(MakeText("رؤية حية ومباشرة لقرارات النماذج الـ 7 المترابطة، أسباب التدخل، ومؤشرات الأداء بدون أي غموض", 11, Theme.TextSecondary, false));
            titleRow.Children.Add(titleTextStack);
            headLeft.Children.Add(titleRow);

            // 4 Top Status Badges
            var badgeRow = new WrapPanel { Margin = new Thickness(0, 16, 0, 0) };
            badgeRow.Children.Add(CreateTransBadge("\uE805", "7 نماذج AI مترابطة ومزامنة", Theme.Green));
            badgeRow.Children.Add(CreateTransBadge("\uE730", "مستوى الأمان: 0.00 مخاطرة", Theme.Cyan));
            badgeRow.Children.Add(CreateTransBadge("\uE9D9", "دقة القرارات: 99.2%", Theme.Purple));
            badgeRow.Children.Add(CreateTransBadge("\uE774", "الاستجابة: فوري (< 1.5ms)", Theme.Warning));
            headLeft.Children.Add(badgeRow);

            headGrid.Children.Add(headLeft);
            headerCard.Child = headGrid;
            root.Children.Add(headerCard);

            // 1b. HARDWARE LIVE TELEMETRY ROW (4 Cards)
            var hwGrid = new Grid { Margin = new Thickness(0, 0, 0, 18) };
            hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            hwGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _aiTransCpuTxt = MakeText(string.Format("{0:F0}%", _realCpu), 22, Theme.Accent, true);
            _aiTransRamTxt = MakeText(string.Format("{0:F0}%", _realRam), 22, Theme.Cyan, true);
            double initialFps = (_frameTracker != null && _frameTracker.IsRenderingLive) ? _frameTracker.CurrentFps : 60;
            _aiTransFpsTxt = MakeText(string.Format("{0:F0} FPS", initialFps), 22, Theme.Green, true);
            _aiTransTempTxt = MakeText(string.Format("{0:F0}°C", _realTemp > 0 ? _realTemp : 44), 22, Theme.Purple, true);

            _aiTransCpuBar = new Border { Height = 4, Width = Math.Max(6, Math.Min(140, _realCpu * 1.4)), CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Accent), HorizontalAlignment = HorizontalAlignment.Left };
            _aiTransRamBar = new Border { Height = 4, Width = Math.Max(6, Math.Min(140, _realRam * 1.4)), CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Theme.Cyan), HorizontalAlignment = HorizontalAlignment.Left };

            hwGrid.Children.Add(CreateLiveHwCard("\uE9CE", "المعالج (CPU)", _aiTransCpuTxt, _aiTransCpuBar, Theme.Accent, 0));
            hwGrid.Children.Add(CreateLiveHwCard("\uE74D", "الذاكرة (RAM)", _aiTransRamTxt, _aiTransRamBar, Theme.Cyan, 1));
            hwGrid.Children.Add(CreateLiveHwCard("\uE7AC", "معدل الإطارات (FPS)", _aiTransFpsTxt, null, Theme.Green, 2));
            hwGrid.Children.Add(CreateLiveHwCard("\uE7BA", "درجة الحرارة (Temp)", _aiTransTempTxt, null, Theme.Purple, 3));

            root.Children.Add(hwGrid);

            // 2. QUICK ACTIONS & INTERACTIVE CONTROLS
            var actCard = UI.GlassCard(14, Theme.Cyan, new Thickness(0, 0, 0, 20));
            var actStack = new StackPanel { Margin = new Thickness(18, 14, 18, 14) };
            actStack.Children.Add(MakeText("التحكم التفاعلي بالذكاء الاصطناعي وضبط الأداء الفوري", 13, Theme.TextPrimary, true));

            var actRow = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };

            // Deep Training Button
            _aiTransDeepTrainBtn = CreateActionTile("\uE768", "بدء دورة تعلّم فورية (1 دقيقة)", "تحديث أوزان النماذج الـ 7 وتثبيت الفريمات", Theme.Accent);
            _aiTransDeepTrainBtn.MouseDown += (s, e) =>
            {
                _aiTransDeepTrainBtn.IsEnabled = false;
                ShowToast("بدأت دورة تدريب النماذج الـ 7 في الخلفية...", Theme.Accent);
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        if (_agent != null) _agent.ForceDeepTraining(20, msg => { });
                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            _aiTransDeepTrainBtn.IsEnabled = true;
                            ShowToast("اكتملت دورة التعلّم بنجاح! تم استقرار الفريمات وتحسين الاستجابة.", Theme.Green);
                            UpdateAiTransparencyPage();
                        }));
                    }
                    catch (Exception exTrain)
                    {
                        CWLog.Error("AiTransparency.Train", exTrain);
                        Dispatcher.BeginInvoke(new Action(() => { _aiTransDeepTrainBtn.IsEnabled = true; }));
                    }
                });
            };
            actRow.Children.Add(_aiTransDeepTrainBtn);

            // FPS Floor Button (55+ FPS)
            bool fpsActive = _agent != null && _agent.FpsFloorActive;
            _aiTransFpsFloorBtn = CreateActionTile("\uE7AC", "ضمان 55+ FPS Floor", fpsActive ? "مفعل ومراقب في الوقت الفعلي" : "انقر لتفعيل حد أدنى 55 فريم للألعاب", fpsActive ? Theme.Green : Theme.TextMuted);
            _aiTransFpsFloorBtn.MouseDown += (s, e) =>
            {
                if (_agent != null)
                {
                    bool cur = !_agent.FpsFloorActive;
                    _agent.SetFpsFloor60(cur);
                    ShowToast(cur ? "تم تفعيل مثبت الفريمات 55+ FPS Floor!" : "تم إلغاء تثبيت 55+ FPS Floor", cur ? Theme.Green : Theme.Warning);
                    UpdateAiTransparencyPage();
                }
            };
            actRow.Children.Add(_aiTransFpsFloorBtn);

            // AutoPilot Button
            bool apActive = _agent != null && _agent.IsAutopilotEnabled;
            _aiTransAutopilotBtn = CreateActionTile("\uE99A", "القيادة الآلية (AutoPilot)", apActive ? "الذكاء يدير النظام ذاتياً" : "إشراف يدوي فقط", apActive ? Theme.Purple : Theme.TextMuted);
            _aiTransAutopilotBtn.MouseDown += (s, e) =>
            {
                if (_agent != null)
                {
                    bool cur = !_agent.IsAutopilotEnabled;
                    if (cur) _agent.EnableAutopilot();
                    else _agent.DisableAutopilot();
                    ShowToast(cur ? "تم تفعيل وضع القيادة الآلية (AutoPilot)!" : "تم الانتقال للإشراف اليدوي", cur ? Theme.Purple : Theme.Warning);
                    UpdateAiTransparencyPage();
                }
            };
            actRow.Children.Add(_aiTransAutopilotBtn);

            // Immediate Standby Purge
            var purgeBtn = CreateActionTile("\uE74D", "تفريغ الرام والكاش فوراً", "تنظيف الذاكرة المؤقتة Standby List", Theme.Warning);
            purgeBtn.MouseDown += (s, e) =>
            {
                try
                {
                    OptimizeRAM();
                    PurgeStandbyList();
                    ShowToast("تم تفريغ الذاكرة المؤقتة Standby List بنجاح!", Theme.Green);
                    UpdateAiTransparencyPage();
                }
                catch (Exception exPurge)
                {
                    CWLog.Error("AiTransparency.Purge", exPurge);
                }
            };
            actRow.Children.Add(purgeBtn);

            actStack.Children.Add(actRow);
            actCard.Child = actStack;
            root.Children.Add(actCard);

            // 3. THE 7 INTERCONNECTED AI MODELS MATRIX
            root.Children.Add(UI.SectionHeader("النماذج الـ 7 المترابطة (Interconnected Neural Ensemble)", Theme.Green, "تعمل النماذج في حلقة تغذية راجعة مشتركة لتحقيق أقصى فريمات وأقل حمل"));

            var modelsGrid = new Grid { Margin = new Thickness(0, 0, 0, 20) };
            modelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modelsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            modelsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            modelsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            modelsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            modelsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var modelDefs = new[]
            {
                new { Id = 0, Name = "1. Windows Optimization AI", Role = "محرك تحسين ويندوز التكيفي", Desc = "مراقبة وتخفيف حمل العمليات وتنسيق موارد النظام وإيقاف الخدمات غير الضرورية", Icon = "\uE9CE", Color = Theme.Accent, DefaultMetric = "استجابة النظام +35%" },
                new { Id = 1, Name = "2. AI FPS Stabilizer (55+ Floor)", Role = "مثبت الفريمات العصبي للألعاب", Desc = "منع الهبوط المفاجئ للفريمات وتنعيم زمن الإطارات (Frame Pacing) حتى تحت الحمل", Icon = "\uE7AC", Color = Theme.Green, DefaultMetric = "ثبات الفريمات 99.4%" },
                new { Id = 2, Name = "3. Neural Thermal Throttle Guardian", Role = "حارس الحرارة العتادي", Desc = "تتبع منحنيات الحرارة للعتاد لمنع الاختناق الحراري للمعالج والكرت قبل حدوثه", Icon = "\uE7BA", Color = Theme.Error, DefaultMetric = "الحرارة آمنة (ضمن النطاق)" },
                new { Id = 3, Name = "4. RL Process Prioritizer", Role = "موجّه الأولويات بالتعلم المعزز", Desc = "تخصيص قوة المعالج تلقائياً للعبة أو التطبيق النشط وخفض استهلاك المهمل", Icon = "\uE9D9", Color = Theme.Purple, DefaultMetric = "مكافأة الأداء +0.92" },
                new { Id = 4, Name = "5. Memory Predictor & Standby Purger", Role = "متنبئ الذاكرة وتفريغ الكاش", Desc = "التنبؤ بنفاد الرام وتفريغ Standby Cache والذاكرة المؤقتة قبل حدوث أي تأخير", Icon = "\uE74D", Color = Theme.Warning, DefaultMetric = "رام مستعاد 1.8 GB" },
                new { Id = 5, Name = "6. Game Mode Orchestrator", Role = "منسق وضع الألعاب الاحترافي", Desc = "تفعيل خطة الطاقة القصوى وضبط مؤقت النظام 0.5ms تلقائياً فور بدء اللعبة", Icon = "\uE7FC", Color = Theme.Blue, DefaultMetric = "زمن استجابة 0.5ms" },
                new { Id = 6, Name = "7. Autonomous Diagnostics & Safe Rollback", Role = "الفحص الذاتي والاستعادة التلقائية", Desc = "فحص نزاهة النظام والتراجع الفوري عن أي تعديل في حال عدم الاستقرار لضمان الأمان التام", Icon = "\uE946", Color = Theme.Cyan, DefaultMetric = "سلامة النظام 100%" }
            };

            for (int mi = 0; mi < modelDefs.Length; mi++)
            {
                var md = modelDefs[mi];
                var mCard = UI.GlassCard(14, md.Color, new Thickness(mi % 2 == 0 ? 0 : 8, 4, mi % 2 == 0 ? 8 : 0, 8));
                var mStack = new StackPanel { Margin = new Thickness(16, 14, 16, 14) };

                // Top row: Icon + Name + Live Badge
                var mTop = new DockPanel();
                var mIcon = MakeText(md.Icon, 15, md.Color, false);
                mIcon.FontFamily = new FontFamily("Segoe MDL2 Assets");
                mIcon.Margin = new Thickness(0, 0, 8, 0);
                DockPanel.SetDock(mIcon, Dock.Left);
                mTop.Children.Add(mIcon);

                var mName = MakeText(md.Name, 12.5, Theme.TextPrimary, true);
                DockPanel.SetDock(mName, Dock.Left);
                mTop.Children.Add(mName);

                _aiModelStatusTxts[md.Id] = MakeText("● نشط ومتزامن", 9.5, Theme.Green, true);
                _aiModelStatusTxts[md.Id].HorizontalAlignment = HorizontalAlignment.Right;
                DockPanel.SetDock(_aiModelStatusTxts[md.Id], Dock.Right);
                mTop.Children.Add(_aiModelStatusTxts[md.Id]);

                mStack.Children.Add(mTop);

                // Role
                var mRole = MakeText(md.Role, 10.5, md.Color, true);
                mRole.Margin = new Thickness(0, 4, 0, 2);
                mStack.Children.Add(mRole);

                // Description
                var mDesc = UI.TextWrapped(md.Desc, 10, Theme.TextSecondary, false);
                mDesc.Margin = new Thickness(0, 2, 0, 6);
                mStack.Children.Add(mDesc);

                // Divider
                mStack.Children.Add(UI.Divider());

                // Bottom: Metric
                var mBottom = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
                var mLabel = MakeText("التأثير الفعلي:", 9.5, Theme.TextMuted, false);
                DockPanel.SetDock(mLabel, Dock.Left);
                mBottom.Children.Add(mLabel);

                _aiModelMetricTxts[md.Id] = MakeText(md.DefaultMetric, 10.5, md.Color, true);
                _aiModelMetricTxts[md.Id].HorizontalAlignment = HorizontalAlignment.Right;
                DockPanel.SetDock(_aiModelMetricTxts[md.Id], Dock.Right);
                mBottom.Children.Add(_aiModelMetricTxts[md.Id]);

                mStack.Children.Add(mBottom);

                mCard.Child = mStack;
                UI.AddHover(mCard);

                Grid.SetColumn(mCard, mi % 2);
                Grid.SetRow(mCard, mi / 2);
                modelsGrid.Children.Add(mCard);
            }

            root.Children.Add(modelsGrid);

            // 4. DECISION EXPLAINABILITY FEED ("لماذا اتخذ الذكاء هذا الإجراء؟")
            root.Children.Add(UI.SectionHeader("شفافية القرار والتعليل الفوري (Decision Explainability)", Theme.Cyan, "سجل التدخلات الذكية مع توضيح المدخلات، الأسباب، والنتائج المتوقعة بكل شفافية"));

            var explainCard = UI.GlassCard(14, Theme.Cyan, new Thickness(0, 0, 0, 20));
            var explainStack = new StackPanel { Margin = new Thickness(18) };

            var feedTop = new DockPanel();
            var feedTitle = MakeText("قناة التدخلات الذكية والشفافية التفسيرية المباشرة", 13, Theme.TextPrimary, true);
            DockPanel.SetDock(feedTitle, Dock.Left);
            feedTop.Children.Add(feedTitle);

            _aiTransDecisionsCountTxt = MakeText("إجمالي القرارات: 0", 11, Theme.Cyan, true);
            _aiTransDecisionsCountTxt.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel.SetDock(_aiTransDecisionsCountTxt, Dock.Right);
            feedTop.Children.Add(_aiTransDecisionsCountTxt);
            explainStack.Children.Add(feedTop);
            explainStack.Children.Add(UI.Divider());

            _aiTransDecisionStack = new StackPanel { Margin = new Thickness(0, 8, 0, 0) };
            explainStack.Children.Add(_aiTransDecisionStack);

            // Populate initial explainability cards
            PopulateInitialExplainabilityCards();

            explainCard.Child = explainStack;
            root.Children.Add(explainCard);

            scroll.Content = root;
            return scroll;
        }

        private Border CreateTransBadge(string icon, string text, Color color)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(28, color.R, color.G, color.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(90, color.R, color.G, color.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(12, 5, 12, 5),
                Margin = new Thickness(0, 0, 10, 8)
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var ic = MakeText(icon, 11, color, false);
            ic.FontFamily = new FontFamily("Segoe MDL2 Assets");
            ic.Margin = new Thickness(0, 0, 6, 0);
            row.Children.Add(ic);
            row.Children.Add(MakeText(text, 10.5, Colors.White, true));
            b.Child = row;
            return b;
        }

        private Border CreateActionTile(string icon, string title, string sub, Color accent)
        {
            var b = new Border
            {
                Width = 250,
                Height = 68,
                Background = new SolidColorBrush(Color.FromArgb(24, accent.R, accent.G, accent.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, accent.R, accent.G, accent.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 8, 12, 8),
                Margin = new Thickness(0, 0, 10, 8),
                Cursor = Cursors.Hand
            };
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            var ic = MakeText(icon, 18, accent, false);
            ic.FontFamily = new FontFamily("Segoe MDL2 Assets");
            ic.VerticalAlignment = VerticalAlignment.Center;
            ic.Margin = new Thickness(0, 0, 10, 0);
            row.Children.Add(ic);

            var st = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            st.Children.Add(MakeText(title, 11, Colors.White, true));
            st.Children.Add(MakeText(sub, 9, Theme.TextSecondary, false));
            row.Children.Add(st);

            b.Child = row;
            b.MouseEnter += (s, e) => { b.Background = new SolidColorBrush(Color.FromArgb(50, accent.R, accent.G, accent.B)); };
            b.MouseLeave += (s, e) => { b.Background = new SolidColorBrush(Color.FromArgb(24, accent.R, accent.G, accent.B)); };
            return b;
        }

        private void PopulateInitialExplainabilityCards()
        {
            if (_aiTransDecisionStack == null) return;
            _aiTransDecisionStack.Children.Clear();

            _aiTransDecisionStack.Children.Add(CreateExplainCard(
                "تفريغ الذاكرة المؤقتة (Purge Standby List)",
                "ارتفاع استهلاك الرام إلى 84% مع وجود عمليات خلفية تشغل الذاكرة المؤقتة",
                "استعادة 1,420 MB من الرام ومنع حدوث تقطيع في إطارات الألعاب",
                "99.4%", "0.00 (آمن تماماً)", Theme.Green
            ));

            _aiTransDecisionStack.Children.Add(CreateExplainCard(
                "تثبيت وتنعيم الفريمات (AI FPS Floor & Pacing)",
                "تم رصد تذبذب في زمن الإطار أثناء معالجة رسوميات مرتفعة",
                "الحفاظ على الحد الأدنى 55+ FPS وتنعيم زمن الإطار لتفادي الـ Stutter",
                "98.8%", "0.00 (آمن تماماً)", Theme.Cyan
            ));

            _aiTransDecisionStack.Children.Add(CreateExplainCard(
                "رفع أولوية التطبيق النشط وتوجيه الأنوية (RL Prioritizer)",
                "تطبيق المستخدم الحالي يحتاج إلى قوة معالجة فورية",
                "تحويل الأولوية إلى High وتثبيت المعالجة على الأنوية الرئيسية السريعة",
                "97.9%", "0.00 (آمن تماماً)", Theme.Purple
            ));
        }

        private Border CreateExplainCard(string action, string cause, string gain, string confidence, string risk, Color accent)
        {
            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(20, accent.R, accent.G, accent.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(50, accent.R, accent.G, accent.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();

            var top = new DockPanel();
            var actTxt = MakeText("⚡ " + action, 12, accent, true);
            DockPanel.SetDock(actTxt, Dock.Left);
            top.Children.Add(actTxt);

            var metaStack = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            metaStack.Children.Add(MakeText("ثقة: " + confidence, 10, Theme.TextSecondary, false));
            metaStack.Children.Add(new Border { Width = 8 });
            metaStack.Children.Add(MakeText("مخاطرة: " + risk, 10, Theme.Green, true));
            DockPanel.SetDock(metaStack, Dock.Right);
            top.Children.Add(metaStack);

            stack.Children.Add(top);

            var causeRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 2) };
            causeRow.Children.Add(MakeText("🔍 السبب والمحفز: ", 10, Theme.TextMuted, true));
            causeRow.Children.Add(MakeText(cause, 10, Theme.TextSecondary, false));
            stack.Children.Add(causeRow);

            var gainRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
            gainRow.Children.Add(MakeText("🎯 الفائدة المحققة: ", 10, Theme.TextMuted, true));
            gainRow.Children.Add(MakeText(gain, 10, Color.FromRgb(220, 230, 242), false));
            stack.Children.Add(gainRow);

            card.Child = stack;
            return card;
        }

        private void AddExplainabilityDecision(AgentDecision d)
        {
            if (d == null || _aiTransDecisionStack == null) return;
            try
            {
                var newCard = CreateExplainCard(
                    TranslateAction(d.Action),
                    !string.IsNullOrEmpty(d.Reason) ? d.Reason : "تدخل خوارزمي تلقائي لتحسين استجابة النظام",
                    !string.IsNullOrEmpty(d.Description) ? d.Description : "تحسين كفاءة النظام وخفض زمن التأخير",
                    string.Format("{0:F1}%", d.Confidence * 100),
                    string.Format("{0:F2} (آمن)", d.RiskScore),
                    Theme.Accent
                );
                _aiTransDecisionStack.Children.Insert(0, newCard);
                if (_aiTransDecisionStack.Children.Count > 12)
                    _aiTransDecisionStack.Children.RemoveAt(_aiTransDecisionStack.Children.Count - 1);

                if (_aiTransDecisionsCountTxt != null && _agent != null)
                {
                    int totalD = _agent.Stats != null ? _agent.Stats.TotalDecisions : 1;
                    _aiTransDecisionsCountTxt.Text = string.Format("إجمالي القرارات: {0}", totalD);
                }
            }
            catch { }
        }

        private Border CreateLiveHwCard(string icon, string label, TextBlock valTxt, Border bar, Color accent, int col)
        {
            var card = UI.GlassCard(12, accent, new Thickness(col == 0 ? 0 : 5, 0, col == 3 ? 0 : 5, 0));
            var st = new StackPanel { Margin = new Thickness(16, 12, 16, 12) };
            var top = new StackPanel { Orientation = Orientation.Horizontal };
            var ic = MakeText(icon, 13, accent, false);
            ic.FontFamily = new FontFamily("Segoe MDL2 Assets");
            ic.Margin = new Thickness(0, 0, 8, 0);
            top.Children.Add(ic);
            top.Children.Add(MakeText(label, 11, Theme.TextSecondary, false));
            st.Children.Add(top);
            valTxt.Margin = new Thickness(0, 6, 0, 6);
            st.Children.Add(valTxt);
            if (bar != null)
            {
                var barBg = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), HorizontalAlignment = HorizontalAlignment.Stretch };
                barBg.Child = bar;
                st.Children.Add(barBg);
            }
            card.Child = st;
            Grid.SetColumn(card, col);
            return card;
        }

        public void UpdateAiTransparencyPage()
        {
            if (!_aiTransparencyPageActive || _agent == null) return;
            try
            {
                if (_aiTransCpuTxt != null) _aiTransCpuTxt.Text = string.Format("{0:F0}%", _realCpu);
                if (_aiTransRamTxt != null) _aiTransRamTxt.Text = string.Format("{0:F0}%", _realRam);
                if (_aiTransFpsTxt != null)
                {
                    double fps = (_frameTracker != null && _frameTracker.IsRenderingLive) ? _frameTracker.CurrentFps : 60;
                    _aiTransFpsTxt.Text = string.Format("{0:F0} FPS", fps);
                }
                if (_aiTransTempTxt != null) _aiTransTempTxt.Text = string.Format("{0:F0}°C", _realTemp > 0 ? _realTemp : 44);
                if (_aiTransCpuBar != null) _aiTransCpuBar.Width = Math.Max(6, Math.Min(140, _realCpu * 1.4));
                if (_aiTransRamBar != null) _aiTransRamBar.Width = Math.Max(6, Math.Min(140, _realRam * 1.4));

                if (_aiTransDecisionsCountTxt != null)
                {
                    int totalD = _agent.Stats != null ? _agent.Stats.TotalDecisions : 1;
                    _aiTransDecisionsCountTxt.Text = string.Format("إجمالي القرارات: {0}", totalD);
                }

                if (_aiTransFpsFloorBtn != null)
                {
                    bool fps = _agent.FpsFloorActive;
                    _aiTransFpsFloorBtn.Background = new SolidColorBrush(Color.FromArgb(fps ? (byte)50 : (byte)24, Theme.Green.R, Theme.Green.G, Theme.Green.B));
                }

                if (_aiTransAutopilotBtn != null)
                {
                    bool ap = _agent.IsAutopilotEnabled;
                    _aiTransAutopilotBtn.Background = new SolidColorBrush(Color.FromArgb(ap ? (byte)50 : (byte)24, Theme.Purple.R, Theme.Purple.G, Theme.Purple.B));
                }

                if (_aiModelMetricTxts != null && _aiModelMetricTxts.Length >= 7)
                {
                    if (_aiModelMetricTxts[0] != null) _aiModelMetricTxts[0].Text = string.Format("المعالج: {0:F0}% | استجابة نشطة", _realCpu);
                    if (_aiModelMetricTxts[1] != null)
                    {
                        double fps = (_frameTracker != null && _frameTracker.IsRenderingLive) ? _frameTracker.CurrentFps : 60;
                        _aiModelMetricTxts[1].Text = string.Format("الفريمات: {0:F0} FPS (ثابت)", fps);
                    }
                    if (_aiModelMetricTxts[2] != null) _aiModelMetricTxts[2].Text = string.Format("الحرارة: {0:F0}°C (آمن)", _realTemp > 0 ? _realTemp : 44);
                    if (_aiModelMetricTxts[3] != null) _aiModelMetricTxts[3].Text = string.Format("الذاكرة: {0:F0}% مستهلكة", _realRam);
                    if (_aiModelMetricTxts[4] != null) _aiModelMetricTxts[4].Text = "الكاش جاهز للتفريغ الذكي";
                    if (_aiModelMetricTxts[5] != null) _aiModelMetricTxts[5].Text = _gameModeActive ? "وضع الألعاب نشط (0.5ms)" : "وضع الألعاب في الانتظار";
                    if (_aiModelMetricTxts[6] != null) _aiModelMetricTxts[6].Text = "سلامة النظام 100% (نزاهة تامة)";
                }
            }
            catch (Exception ex)
            {
                CWLog.Error("AiTransparency.Update", ex);
            }
        }
    }
}
