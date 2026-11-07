using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Input;

namespace ClearWin
{
    public partial class MainWindow
    {
        private Border[] _dashMetricBars = new Border[6];
        private Border[] _dashMetricCards = new Border[6];
        private bool _valuesTestAutoRun;
        private TextBlock _testVerdict;
        private TextBlock[] _testDisp = new TextBlock[6];
        private TextBlock[] _testOrig = new TextBlock[6];
        private TextBlock[] _testRows = new TextBlock[6];
        private TextBlock[] _fpsStabVals = new TextBlock[6];
        private TextBlock _fpsStabStatus;
        private TextBlock[] _mlLearnVals = new TextBlock[4];

        // === SmartAgent manual control card (dashboard Row 3.5) fields ===
        private Border _agentCardDot, _agentPauseBtn, _agentApproveBtn, _agentAutoPilotBtn, _agentEmergencyBtn, _agentEmergencyClearBtn;
        private TextBlock _agentCardStatusTxt, _agentLastActionTxt, _agentConfidenceTxt, _agentHealthTxt, _agentPendingTxt, _agentLastResultTxt, _agentPauseBtnLbl, _agentApproveBtnLbl;

        private Border MakeAgentBtn(Color c, string text)
        {
            var b = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(24, c.R, c.G, c.B)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(110, c.R, c.G, c.B)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12, 6, 12, 6),
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand
            };
            b.Child = MakeText(text, 10.5, Colors.White, true);
            return b;
        }
        private UIElement BuildModernDashboard()
        {
            try
            {
                var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(8, 8, 8, 8) };
                var panel = new StackPanel();

                // === Row 1: Live Metrics ===
                var metricsGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                for (int i = 0; i < 6; i++) metricsGrid.ColumnDefinitions.Add(new ColumnDefinition());

                string[] mNames = { "CPU", "RAM", "GPU", "Temp", "FPS", "Health" };
                string[] mNamesAr = { "المعالج", "الذاكرة", "الرسوميات", "الحرارة", "FPS", "الصحة" };
                Color[] mColors = { Theme.Blue, Theme.Accent, Theme.Green, Theme.Error, Theme.Cyan, Theme.Purple };

                for (int i = 0; i < 6; i++)
                {
                    int idx = i; Color c = mColors[idx];
                    var card = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(24, c.R, c.G, c.B)),
                        CornerRadius = new CornerRadius(18),
                        Padding = new Thickness(4, 2, 4, 6), Margin = new Thickness(4),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                        BorderThickness = new Thickness(1),
                        ClipToBounds = true
                    };
                    _dashMetricCards[idx] = card;

                    var outer = new Grid();
                    outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });
                    outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    var accentStrip = new Border { Background = new SolidColorBrush(Color.FromArgb(130, c.R, c.G, c.B)), CornerRadius = new CornerRadius(14, 14, 0, 0) };
                    Grid.SetRow(accentStrip, 0);
                    outer.Children.Add(accentStrip);

                    var sp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    var valTxt = new TextBlock
                    {
                        FontSize = 26, FontWeight = FontWeights.Light,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Foreground = new SolidColorBrush(c),
                        Text = "--"
                    };
                    var lblTxt = new TextBlock
                    {
                        Text = mNamesAr[idx], FontSize = 10.5,
                        Foreground = new SolidColorBrush(Theme.TextSecondary),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 5)
                    };
                    sp.Children.Add(valTxt);
                    sp.Children.Add(lblTxt);

                    var barTrack = new Border { Height = 4, CornerRadius = new CornerRadius(2), Background = Theme.SubCardBrush, Margin = new Thickness(6, 2, 6, 0) };
                    _dashMetricBars[idx] = new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Left, CornerRadius = new CornerRadius(2), Background = new SolidColorBrush(c) };
                    barTrack.Child = _dashMetricBars[idx];
                    sp.Children.Add(barTrack);

                    Grid.SetRow(sp, 1);
                    outer.Children.Add(sp);
                    card.Child = outer;
                    card.MouseEnter += (se, ee) => { card.RenderTransform = new TranslateTransform(0, -2); card.Effect = new DropShadowEffect { Color = c, BlurRadius = 22, ShadowDepth = 0, Opacity = 0.22 }; };
                    card.MouseLeave += (se, ee) => { card.RenderTransform = new TranslateTransform(0, 0); card.Effect = new DropShadowEffect { Color = c, BlurRadius = 14, ShadowDepth = 0, Opacity = 0.07 }; };
                    metricsGrid.Children.Add(card);
                    Grid.SetColumn(card, idx);
                    _dashMetricTxts[idx] = valTxt;
                }
                panel.Children.Add(metricsGrid);

                // === Row 2: WinOptAI Console ===
                var woaCard = new Border
                {
                    Background = UI.GlassFill(),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(18, 14, 18, 14), Margin = new Thickness(4, 0, 4, 10),
                    BorderBrush = UI.GlassBorder(),
                    BorderThickness = new Thickness(1),
                    ClipToBounds = true,
                    Effect = UI.GlassShadow()
                };
                var woaPanel = new StackPanel();

                var woaHdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                _woaStatusDot = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = Theme.GreenBrush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                woaHdr.Children.Add(_woaStatusDot);
                _woaStatusTxt = new TextBlock { Text = "محرك الذكاء الأمثل", FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextPrimary), VerticalAlignment = VerticalAlignment.Center };
                woaHdr.Children.Add(_woaStatusTxt);
                var woaSub = new TextBlock { Text = "الشبكة العصبية المحلية", FontSize = 11, Foreground = new SolidColorBrush(Theme.TextMuted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
                woaHdr.Children.Add(woaSub);
                _woaActivateBtn = new Border
                {
                    Background = new SolidColorBrush(Color.FromArgb(28, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(150, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(14, 7, 14, 7),
                    Margin = new Thickness(14, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                    Cursor = Cursors.Hand,
                    Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.2 }
                };
                _woaActivateBtn.Child = MakeText("تفعيل الذكاء الاصطناعي", 11, Colors.White, true);
                _woaActivateBtn.MouseDown += (s, e) => ActivateLocalModel();
                _woaActivateBtn.MouseEnter += (s, e) => { _woaActivateBtn.Background = new SolidColorBrush(Color.FromArgb(55, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)); _woaActivateBtn.Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.35 }; };
                _woaActivateBtn.MouseLeave += (s, e) => { _woaActivateBtn.Background = new SolidColorBrush(Color.FromArgb(28, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)); _woaActivateBtn.Effect = new DropShadowEffect { Color = Theme.Accent, BlurRadius = 12, ShadowDepth = 0, Opacity = 0.2 }; };
                woaHdr.Children.Add(_woaActivateBtn);
                woaPanel.Children.Add(woaHdr);

                // Two-column body
                var bodyGrid = new Grid { Margin = new Thickness(0, 0, 0, 0) };
                bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                // Left column: Decision + Confidence
                var leftBox = new Border { Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 6, 0) };
                var leftStack = new StackPanel();
                var decRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                decRow.Children.Add(new TextBlock { Text = "القرار:", FontSize = 11, Foreground = new SolidColorBrush(Theme.TextMuted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _woaDecisionTxt = new TextBlock { Text = "بانتظار القرار...", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.Green), VerticalAlignment = VerticalAlignment.Center };
                decRow.Children.Add(_woaDecisionTxt);
                leftStack.Children.Add(decRow);
                var confRow = new StackPanel { Orientation = Orientation.Horizontal };
                confRow.Children.Add(new TextBlock { Text = "الثقة:", FontSize = 11, Foreground = new SolidColorBrush(Theme.TextMuted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _woaConfidenceTxt = new TextBlock { Text = "--", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextPrimary), VerticalAlignment = VerticalAlignment.Center };
                confRow.Children.Add(_woaConfidenceTxt);
                leftStack.Children.Add(confRow);
                leftBox.Child = leftStack;
                Grid.SetColumn(leftBox, 0);
                bodyGrid.Children.Add(leftBox);

                // Right column: Prompt + Response in code boxes
                var rightCol = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
                var pBox = new Border { Background = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 0, 4) };
                var pInner = new StackPanel();
                pInner.Children.Add(new TextBlock { Text = "الطلب المرسل", FontSize = 9, Foreground = new SolidColorBrush(Theme.TextMuted), Margin = new Thickness(0, 0, 0, 2) });
                _woaPromptTxt = new TextBlock { Text = "--", FontSize = 10, Foreground = new SolidColorBrush(Theme.TextSecondary), TextWrapping = TextWrapping.Wrap, MaxHeight = 40, FontFamily = new FontFamily("Consolas") };
                pInner.Children.Add(_woaPromptTxt);
                pBox.Child = pInner;
                rightCol.Children.Add(pBox);

                var rBox = new Border { Background = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255)), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 6) };
                var rInner = new StackPanel();
                rInner.Children.Add(new TextBlock { Text = "الاستجابة الخام", FontSize = 9, Foreground = new SolidColorBrush(Theme.TextMuted), Margin = new Thickness(0, 0, 0, 2) });
                _woaResponseTxt = new TextBlock { Text = "--", FontSize = 10, Foreground = new SolidColorBrush(Theme.Green), TextWrapping = TextWrapping.Wrap, MaxHeight = 40, FontFamily = new FontFamily("Consolas") };
                rInner.Children.Add(_woaResponseTxt);
                rBox.Child = rInner;
                rightCol.Children.Add(rBox);
                Grid.SetColumn(rightCol, 1);
                bodyGrid.Children.Add(rightCol);

                woaPanel.Children.Add(bodyGrid);

                // Neural network learning metrics strip (live, tick-safe)
                var learnRow = new Grid { Margin = new Thickness(0, 8, 0, 0) };
                for (int li = 0; li < 4; li++) learnRow.ColumnDefinitions.Add(new ColumnDefinition());
                string[] learnNames = { "عينات التدريب", "أنسبلات الشبكة", "آخر خسارة", "الدقة (آخر)" };
                for (int li = 0; li < 4; li++)
                {
                    int l = li;
                    var cell = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
                    cell.Children.Add(new TextBlock { Text = learnNames[l], FontSize = 8.5, Foreground = new SolidColorBrush(Theme.TextMuted) });
                    _mlLearnVals[l] = new TextBlock { Text = "--", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextPrimary) };
                    cell.Children.Add(_mlLearnVals[l]);
                    Grid.SetColumn(cell, l);
                    learnRow.Children.Add(cell);
                }
                woaPanel.Children.Add(learnRow);

                woaCard.Child = woaPanel;
                panel.Children.Add(woaCard);

                // === Row 3: Activity Feed ===
                var feedCard = new Border
                {
                    Background = UI.GlassFill(),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(18, 14, 18, 14), Margin = new Thickness(4),
                    BorderBrush = UI.GlassBorder(), BorderThickness = new Thickness(1),
                    ClipToBounds = true,
                    Effect = UI.GlassShadow()
                };
                var feedOuter = new StackPanel();
                var feedHdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
                feedHdr.Children.Add(new TextBlock { Text = "سجل النشاط", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextPrimary) });
                feedHdr.Children.Add(new TextBlock { Text = "قرارات الذكاء الاصطناعي", FontSize = 9, Foreground = new SolidColorBrush(Theme.TextMuted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
                feedOuter.Children.Add(feedHdr);
                _woaFeedStack = new StackPanel { Margin = new Thickness(0, 0, 0, 0) };
                _woaFeedStack.Children.Add(new TextBlock { Text = "لا يوجد نشاط بعد — فعّل الوكيل الذكي للبدء.", FontSize = 10, Foreground = new SolidColorBrush(Theme.TextMuted) });
                feedOuter.Children.Add(_woaFeedStack);
                feedCard.Child = feedOuter;
                panel.Children.Add(feedCard);

                // === Row 3.5: Agent — Last Action + Manual Controls ===
                var agentCard = new Border
                {
                    Background = UI.GlassFill(),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(18, 14, 18, 14), Margin = new Thickness(4, 0, 4, 10),
                    BorderBrush = UI.GlassBorder(),
                    BorderThickness = new Thickness(1),
                    ClipToBounds = true,
                    Effect = UI.GlassShadow()
                };
                var agentOuter = new StackPanel();
                var agentHdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
                _agentCardDot = new Border { Width = 10, Height = 10, CornerRadius = new CornerRadius(5), Background = new SolidColorBrush(Theme.Disabled), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                agentHdr.Children.Add(_agentCardDot);
                agentHdr.Children.Add(new TextBlock { Text = "الوكيل — آخر إجراء", FontSize = 14, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextPrimary), VerticalAlignment = VerticalAlignment.Center });
                agentHdr.Children.Add(new TextBlock { Text = "قرارات الوكيل الحيّة والتحكم اليدوي — لا ينفذ أي تغيير دون موافقة", FontSize = 9, Foreground = new SolidColorBrush(Theme.TextMuted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
                _agentCardStatusTxt = new TextBlock { Text = "بانتظار التهيئة", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextSecondary), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14, 0, 0, 0) };
                agentHdr.Children.Add(_agentCardStatusTxt);
                agentOuter.Children.Add(agentHdr);

                var agentBodyBox = new Border { Background = new SolidColorBrush(Color.FromArgb(16, 255, 255, 255)), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 0, 8) };
                var agentBodyStack = new StackPanel();
                var agentActRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
                agentActRow.Children.Add(new TextBlock { Text = "آخر قرار:", FontSize = 11, Foreground = new SolidColorBrush(Theme.TextMuted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
                _agentLastActionTxt = new TextBlock { Text = "لا إجراءات بعد", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.Green), VerticalAlignment = VerticalAlignment.Center };
                agentActRow.Children.Add(_agentLastActionTxt);
                agentActRow.Children.Add(new TextBlock { Text = "   الثقة:", FontSize = 11, Foreground = new SolidColorBrush(Theme.TextMuted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
                _agentConfidenceTxt = new TextBlock { Text = "--", FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextPrimary), VerticalAlignment = VerticalAlignment.Center };
                agentActRow.Children.Add(_agentConfidenceTxt);
                agentBodyStack.Children.Add(agentActRow);
                _agentLastResultTxt = new TextBlock { Text = "آخر نتيجة تحسين: لا إجراءات بعد", FontSize = 10.5, Foreground = new SolidColorBrush(Theme.TextSecondary), TextWrapping = TextWrapping.Wrap };
                agentBodyStack.Children.Add(_agentLastResultTxt);
                agentBodyBox.Child = agentBodyStack;
                agentOuter.Children.Add(agentBodyBox);

                var agentPillsRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                for (int ai = 0; ai < 2; ai++) agentPillsRow.ColumnDefinitions.Add(new ColumnDefinition());
                string[] agentPillNames = { "الصحة", "بانتظار الموافقة" };
                for (int ai = 0; ai < 2; ai++)
                {
                    int aidx = ai;
                    var agentPill = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
                    agentPill.Children.Add(new TextBlock { Text = agentPillNames[aidx], FontSize = 8.5, Foreground = new SolidColorBrush(Theme.TextMuted) });
                    TextBlock agentPillVal;
                    if (aidx == 0) agentPillVal = _agentHealthTxt = new TextBlock { Text = "--", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextPrimary) };
                    else agentPillVal = _agentPendingTxt = new TextBlock { Text = "0", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextMuted) };
                    agentPill.Children.Add(agentPillVal);
                    Grid.SetColumn(agentPill, aidx);
                    agentPillsRow.Children.Add(agentPill);
                }
                agentOuter.Children.Add(agentPillsRow);
                var agentBtnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                _agentPauseBtn = MakeAgentBtn(Theme.Cyan, "إيقاف الوكيل مؤقتا");
                _agentPauseBtnLbl = (TextBlock)_agentPauseBtn.Child;
                _agentPauseBtn.MouseDown += (s, e) => { try { if (_agent != null) { if (_agent.IsPaused) _agent.ResumeAgent(); else _agent.PauseAgent(3); } UpdateAgentDashboardCard(); } catch (Exception exB1) { CWLog.Error("MainWindow.AgentCard.Pause", exB1); } };
                agentBtnRow.Children.Add(_agentPauseBtn);
                _agentApproveBtn = MakeAgentBtn(Theme.Green, "موافقة على معلق (0)");
                _agentApproveBtnLbl = (TextBlock)_agentApproveBtn.Child;
                _agentApproveBtn.Opacity = 0.45;
                _agentApproveBtn.MouseDown += (s, e) => { try { if (_agent != null) { var q = _agent.GetApprovalQueue(); if (q.Count > 0) _agent.ApprovePendingDecision(q[0]); } UpdateAgentDashboardCard(); } catch (Exception exB2) { CWLog.Error("MainWindow.AgentCard.Approve", exB2); } };
                agentBtnRow.Children.Add(_agentApproveBtn);
                _agentAutoPilotBtn = MakeAgentBtn(Theme.Accent, "تبديل الطيار الآلي");
                _agentAutoPilotBtn.MouseDown += (s, e) => { try { if (_agent != null) _agent.ToggleAgent(); UpdateAgentDashboardCard(); } catch (Exception exB3) { CWLog.Error("MainWindow.AgentCard.AutoPilot", exB3); } };
                agentBtnRow.Children.Add(_agentAutoPilotBtn);
                _agentEmergencyBtn = MakeAgentBtn(Theme.Error, "إيقاف طوارئ");
                _agentEmergencyBtn.MouseDown += (s, e) => { try { if (_agent != null) _agent.EmergencyStopAllOptimizations(); UpdateAgentDashboardCard(); } catch (Exception exB4) { CWLog.Error("MainWindow.AgentCard.Emergency", exB4); } };
                agentBtnRow.Children.Add(_agentEmergencyBtn);
                _agentEmergencyClearBtn = MakeAgentBtn(Theme.Disabled, "مسح الإيقاف الطارئ");
                _agentEmergencyClearBtn.MouseDown += (s, e) => { try { if (_agent != null) _agent.ResetKillSwitch(); UpdateAgentDashboardCard(); } catch (Exception exB5) { CWLog.Error("MainWindow.AgentCard.EmergencyClear", exB5); } };
                agentBtnRow.Children.Add(_agentEmergencyClearBtn);
                agentOuter.Children.Add(agentBtnRow);

                agentCard.Child = agentOuter;
                panel.Children.Add(agentCard);

// === Row 4: Values Integrity Test ===
                var testCard = new Border
                {
                    Background = UI.GlassFill(),
                    BorderBrush = UI.GlassBorder(),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(18, 14, 18, 14),
                    Margin = new Thickness(4, 0, 4, 4),
                    ClipToBounds = true,
                    Effect = UI.GlassShadow()
                };
                var testOuter = new StackPanel();

                var testHdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                testHdr.Children.Add(new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), Background = Theme.GreenBrush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
                testHdr.Children.Add(new TextBlock { Text = "اختبار سلامة القيم", FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextPrimary), VerticalAlignment = VerticalAlignment.Center });
                testHdr.Children.Add(new TextBlock { Text = "القيم الظاهرة على الشاشة مقابل مصدر القياس الأصلي", FontSize = 9.5, Foreground = new SolidColorBrush(Theme.TextMuted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
                var testBtn = UI.PillButton("تشغيل الاختبار", Theme.Cyan, 10.5);
                testBtn.Margin = new Thickness(14, 0, 0, 0);
                testBtn.VerticalAlignment = VerticalAlignment.Center;
                testBtn.MouseDown += (s, e) => RunValuesTest();
                testHdr.Children.Add(testBtn);
                testOuter.Children.Add(testHdr);

                var tableBox = new Border { Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)), CornerRadius = new CornerRadius(10), Padding = new Thickness(12, 8, 12, 8) };
                var tableStack = new StackPanel();

                var colGrid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                for (int ci = 0; ci < 4; ci++) colGrid.ColumnDefinitions.Add(new ColumnDefinition());
                colGrid.ColumnDefinitions[0].Width = new GridLength(1.1, GridUnitType.Star);
                colGrid.ColumnDefinitions[1].Width = new GridLength(0.8, GridUnitType.Star);
                colGrid.ColumnDefinitions[2].Width = new GridLength(0.9, GridUnitType.Star);
                colGrid.ColumnDefinitions[3].Width = new GridLength(0.6, GridUnitType.Star);
                var h1 = MakeText("القيمة", 9.5, Theme.TextMuted, false);
                var h2 = MakeText("المعروضة", 9.5, Theme.TextMuted, false);
                var h3 = MakeText("الأصلية", 9.5, Theme.TextMuted, false);
                var h4 = MakeText("الحالة", 9.5, Theme.TextMuted, false);
                colGrid.Children.Add(h1); colGrid.Children.Add(h2); colGrid.Children.Add(h3); colGrid.Children.Add(h4);
                Grid.SetColumn(h1, 0); Grid.SetColumn(h2, 1); Grid.SetColumn(h3, 2); Grid.SetColumn(h4, 3);
                tableStack.Children.Add(colGrid);

                string[] testNames = { "المعالج", "الذاكرة", "الرسوميات", "الحرارة", "FPS", "الصحة" };
                for (int i = 0; i < 6; i++)
                {
                    int ti = i;
                    var rowGrid = new Grid { Margin = new Thickness(0, 0, 0, 3) };
                    for (int ci = 0; ci < 4; ci++) rowGrid.ColumnDefinitions.Add(new ColumnDefinition());
                    rowGrid.ColumnDefinitions[0].Width = new GridLength(1.1, GridUnitType.Star);
                    rowGrid.ColumnDefinitions[1].Width = new GridLength(0.8, GridUnitType.Star);
                    rowGrid.ColumnDefinitions[2].Width = new GridLength(0.9, GridUnitType.Star);
                    rowGrid.ColumnDefinitions[3].Width = new GridLength(0.6, GridUnitType.Star);
                    var nTxt = MakeText(testNames[ti], 10.5, Theme.TextSecondary, false);
                    _testDisp[ti] = MakeText("--", 11, Theme.TextPrimary, true);
                    _testOrig[ti] = MakeText("--", 11, Theme.TextSecondary, false);
                    _testRows[ti] = MakeText("—", 11, Theme.TextMuted, true);
                    rowGrid.Children.Add(nTxt); rowGrid.Children.Add(_testDisp[ti]); rowGrid.Children.Add(_testOrig[ti]); rowGrid.Children.Add(_testRows[ti]);
                    Grid.SetColumn(nTxt, 0); Grid.SetColumn(_testDisp[ti], 1); Grid.SetColumn(_testOrig[ti], 2); Grid.SetColumn(_testRows[ti], 3);
                    tableStack.Children.Add(rowGrid);
                }
                tableBox.Child = tableStack;
                testOuter.Children.Add(tableBox);

                var verdictRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
                var vDot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = Theme.GreenBrush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                verdictRow.Children.Add(vDot);
                _testVerdict = new TextBlock { Text = "انقر «تشغيل الاختبار» للبدء — أو يبدأ تلقائياً خلال ثوانٍ.", FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextSecondary), VerticalAlignment = VerticalAlignment.Center };
                verdictRow.Children.Add(_testVerdict);
                testOuter.Children.Add(verdictRow);

                testCard.Child = testOuter;
                panel.Children.Add(testCard);

                // === Row 5: AI FPS Stabilizer ===
                var fpsCard = new Border
                {
                    Background = UI.GlassFill(),
                    BorderBrush = UI.GlassBorder(),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(18),
                    Padding = new Thickness(18, 14, 18, 14),
                    Margin = new Thickness(4, 0, 4, 4),
                    ClipToBounds = true,
                    Effect = UI.GlassShadow()
                };
                var fpsOuter = new StackPanel();
                var fpsHdr = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
                fpsHdr.Children.Add(new Border { Width = 9, Height = 9, CornerRadius = new CornerRadius(4.5), Background = new SolidColorBrush(Theme.Cyan), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center });
                fpsHdr.Children.Add(new TextBlock { Text = "AI FPS Stabilizer", FontSize = 13.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextPrimary), VerticalAlignment = VerticalAlignment.Center });
                fpsHdr.Children.Add(new TextBlock { Text = "تثبيت FPS وتوقع الهبوط المفاجئ", FontSize = 9.5, Foreground = new SolidColorBrush(Theme.TextMuted), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) });
                fpsOuter.Children.Add(fpsHdr);

                string[] fpsNames = { "الآن", "متوسط 10 ث", "1% Low", "Frame Time", "الاستقرار", "منع هبوط" };
                string[] fpsUnits = { "FPS", "FPS", "FPS", "ms", "/100", "عدد" };
                Color[] fpsColors = { Theme.Cyan, Theme.Blue, Theme.Warning, Theme.Purple, Theme.Green, Theme.Accent };
                var fpsGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                for (int i = 0; i < 6; i++) fpsGrid.ColumnDefinitions.Add(new ColumnDefinition());
                for (int i = 0; i < 6; i++)
                {
                    int fi = i; Color c = fpsColors[fi];
                    var tile = new Border
                    {
                        Background = new SolidColorBrush(Color.FromArgb(18, c.R, c.G, c.B)),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(6, 6, 6, 6),
                        Margin = new Thickness(3),
                        BorderBrush = new SolidColorBrush(Color.FromArgb(45, c.R, c.G, c.B)),
                        BorderThickness = new Thickness(1)
                    };
                    var tsp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                    _fpsStabVals[fi] = new TextBlock
                    {
                        Text = "--", FontSize = 19, FontWeight = FontWeights.Light,
                        Foreground = new SolidColorBrush(c), HorizontalAlignment = HorizontalAlignment.Center
                    };
                    tsp.Children.Add(_fpsStabVals[fi]);
                    tsp.Children.Add(new TextBlock { Text = fpsNames[fi], FontSize = 9.5, Foreground = new SolidColorBrush(Theme.TextSecondary), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 0) });
                    tsp.Children.Add(new TextBlock { Text = fpsUnits[fi], FontSize = 8, Foreground = new SolidColorBrush(Theme.TextMuted), HorizontalAlignment = HorizontalAlignment.Center });
                    tile.Child = tsp;
                    fpsGrid.Children.Add(tile);
                    Grid.SetColumn(tile, fi);
                }
                fpsOuter.Children.Add(fpsGrid);

                var fpsStatusRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
                var fsDot = new Border { Width = 8, Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Theme.Cyan), Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
                fpsStatusRow.Children.Add(fsDot);
                _fpsStabStatus = new TextBlock { Text = "بانتظار البيانات...", FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Theme.TextSecondary), VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
                fpsStatusRow.Children.Add(_fpsStabStatus);
                fpsOuter.Children.Add(fpsStatusRow);

                fpsCard.Child = fpsOuter;
                panel.Children.Add(fpsCard);

                scroll.Content = panel;
                return scroll;
            }
            catch (Exception ex)
            {
                return new TextBlock { Text = "Dashboard Error: " + ex.Message, Foreground = new SolidColorBrush(Colors.Red), Margin = new Thickness(20), TextWrapping = TextWrapping.Wrap };
            }
        }

        /// <summary>Activates/stops the Windows Optimization AI from the dashboard button.</summary>
        private void ActivateLocalModel()
        {
            try
            {
                if (_agent == null) return;
                if (_agent.WinOptAI == null)
                    _agent.InitializeWindowsOptimizationAI();
                var ai = _agent.WinOptAI;
                if (ai == null)
                {
                    ShowNotification("WinOptAI", "تعذر إنشاء نموذج تحسين الويندوز");
                    return;
                }
                if (ai.IsRunning)
                {
                    // Manual stop: keep it off until the user reactivates it.
                    ai.IntentionallyStopped = true;
                    _agent.StopWindowsOptimizationAI();
                    ShowNotification("WinOptAI", "تم إيقاف AI يدويًا");
                }
                else
                {
                    // Manual activate: it stays on and is only stopped again here.
                    ai.ManualModelControl = true;
                    ai.IntentionallyStopped = false;
                    _lastModelStartAttempt = DateTime.Now;
                    _agent.StartWindowsOptimizationAI();
                    ShowNotification("WinOptAI", "جارٍ تفعيل AI...");
                }
                UpdateWoaActivateButton();
            }
            catch (Exception ex) { CWLog.Error("MainWindow.ActivateLocalModel", ex); }
        }

        private void UpdateWoaActivateButton()
        {
            try
            {
                if (_woaActivateBtn == null) return;
                var ai = _agent != null ? _agent.WinOptAI : null;
                bool running = ai != null && ai.IsRunning;
                if (running == _lastWoaBtnRunning && _woaActivateBtn.Child != null) return;
                _lastWoaBtnRunning = running;
                if (running)
                {
                    _woaActivateBtn.Child = MakeText("إيقاف الذكاء الاصطناعي", 11, Colors.White, true);
                    _woaActivateBtn.Background = new SolidColorBrush(Color.FromArgb(28, Theme.Error.R, Theme.Error.G, Theme.Error.B));
                    _woaActivateBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(150, Theme.Error.R, Theme.Error.G, Theme.Error.B));
                }
                else
                {
                _woaActivateBtn.Child = MakeText("تفعيل الذكاء الاصطناعي", 12, Colors.White, true);
                    _woaActivateBtn.Background = new SolidColorBrush(Color.FromArgb(28, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B));
                    _woaActivateBtn.BorderBrush = new SolidColorBrush(Color.FromArgb(150, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B));
                }
            }
            catch (Exception ex2) { CWLog.Error("MainWindow.UpdateWoaActivateButton", ex2); }
        }

        private string FormatOrigValue(int i, double v)
        {
            switch (i)
            {
                case 3: return v > 0.5 ? string.Format("{0:F0}\xB0C", v) : "N/A";
                case 4: return v > 0.5 ? string.Format("{0:F0}", v) : "--";
                default: return v >= 0.5 ? string.Format("{0:F0}%", v) : "--";
            }
        }

        private double ParseDisplayedValue(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return double.NaN;
            string s = t.Replace("%", "").Replace("\xB0C", "").Replace("°C", "").Trim();
            if (s == "--" || s == "N/A" || s == "n/a") return double.NaN;
            double v;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return double.NaN;
        }

        private void ParseLastDecision(ref string action, ref double confidence)
        {
            try
            {
                if (_agent == null) return;
                var log = _agent.DecisionHistoryLog;
                if (log == null || log.Count == 0) return;
                string last = log[log.Count - 1];
                if (string.IsNullOrEmpty(last)) return;
                int bIdx = last.IndexOf(']');
                if (bIdx >= 0 && bIdx + 2 < last.Length)
                {
                    string rest = last.Substring(bIdx + 2);
                    int pipe = rest.IndexOf(" | ");
                    if (pipe > 0)
                    {
                        string[] parts = rest.Split(new[] { " | " }, StringSplitOptions.None);
                        if (parts.Length >= 2) action = parts[1].Trim();
                        else action = rest.Substring(0, pipe);
                    }
                    else action = rest;
                }
                int confIdx = last.IndexOf("Conf:", StringComparison.Ordinal);
                if (confIdx >= 0)
                {
                    int pctIdx = last.IndexOf('%', confIdx);
                    if (pctIdx > confIdx)
                    {
                        string raw = last.Substring(confIdx + 5, pctIdx - confIdx - 5).Trim();
                        double cval;
                        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out cval)) confidence = cval / 100.0;
                    }
                }
            }
            catch (Exception ex3) { CWLog.Error("MainWindow.ParseLastDecision", ex3); }
        }
        // === Live update for "Agent — Last Action" card (called from tick loop) ===
        private void UpdateAgentDashboardCard()
        {
            try
            {
                if (_agentLastActionTxt == null || _agent == null) return;
                string action = _agent.WinOptAILastAction;
                _agentLastActionTxt.Text = string.IsNullOrEmpty(action) ? "لا إجراءات بعد" : action;
                _agentLastActionTxt.Foreground = new SolidColorBrush(string.IsNullOrEmpty(action) ? Theme.TextMuted : Theme.Green);
                _agentConfidenceTxt.Text = string.IsNullOrEmpty(action) ? "--" : string.Format("{0:F0}%", _agent.WinOptAILastConfidence * 100);
                string result = _agent.LastOptimizationResult;
                _agentLastResultTxt.Text = string.IsNullOrEmpty(result) ? "آخر نتيجة تحسين: لا إجراءات بعد" : result;
                int pending = 0;
                try { pending = _agent.GetApprovalQueue().Count; } catch { pending = 0; }
                _agentPendingTxt.Text = pending.ToString();
                _agentPendingTxt.Foreground = new SolidColorBrush(pending > 0 ? Theme.Warning : Theme.TextMuted);
                double hScore;
                try { hScore = _agent.GetOverallHealthScore(); } catch { hScore = _agent.SystemHealthScore; }
                _agentHealthTxt.Text = string.Format("{0:F0}%", Math.Max(0, Math.Min(100, hScore)));
                _agentHealthTxt.Foreground = new SolidColorBrush(hScore >= 70 ? Theme.Green : (hScore >= 40 ? Theme.Warning : Theme.Error));
                string modeName;
                switch (_agent.Mode)
                {
                    case AgentMode.AutoPilot: modeName = "طيار آلي"; break;
                    case AgentMode.Assisted: modeName = "مساعد"; break;
                    case AgentMode.GameMode: modeName = "وضع الألعاب"; break;
                    case AgentMode.BalanceMode: modeName = "متوازن"; break;
                    case AgentMode.CoolMode: modeName = "تبريد"; break;
                    case AgentMode.Custom: modeName = "مخصص"; break;
                    default: modeName = "معطل"; break;
                }
                bool emg = _agent.IsKillSwitchActive;
                bool paused = _agent.IsPaused;
                string status;
                if (emg) status = "إيقاف طوارئ نشط";
                else if (_agent.SafeModeEnabled) status = "وضع آمن — مراقبة فقط";
                else if (paused) status = "الوكيل موقوف مؤقتاً";
                else status = modeName;
                _agentCardStatusTxt.Text = status;
                _agentCardStatusTxt.Foreground = new SolidColorBrush(emg ? Theme.Error : (paused || _agent.SafeModeEnabled ? Theme.Warning : Theme.Green));
                _agentCardDot.Background = new SolidColorBrush(emg ? Theme.Error : (paused ? Theme.Warning : (string.IsNullOrEmpty(action) ? Theme.Disabled : Theme.Green)));
                if (_agentPauseBtnLbl != null)
                    _agentPauseBtnLbl.Text = paused ? "استئناف الوكيل" : "إيقاف الوكيل مؤقتاً";
                if (_agentApproveBtnLbl != null)
                {
                    _agentApproveBtnLbl.Text = string.Format("موافقة على معلق ({0})", pending);
                    _agentApproveBtn.Opacity = pending > 0 ? 1.0 : 0.45;
                }
            }
            catch (Exception exA) { CWLog.Error("MainWindow.UpdateAgentDashboardCard", exA); }
        }

        private void RunValuesTest()
        {
            try
            {
                if (_dashMetricTxts[0] == null || _testRows == null || _testRows[0] == null) return;
                double[] orig = new double[6];
                orig[0] = _realCpu; orig[1] = _realRam; orig[2] = _realGpu;
                orig[3] = _realTemp; orig[4] = _frameTracker != null ? _frameTracker.CurrentFps : 0;
                orig[5] = _healthScore;
                int pass = 0, fail = 0;
                for (int i = 0; i < 6; i++)
                {
                    double dv = ParseDisplayedValue(_dashMetricTxts[i].Text);
                    double ov = orig[i];
                    bool ok;
                    if (ov < 0.5) ok = double.IsNaN(dv);
                    else if (double.IsNaN(dv)) ok = false;
                    else { double tol = (i == 4) ? 20.0 : 1.0; ok = Math.Abs(dv - ov) <= tol; }
                    if (ok) pass++; else fail++;
                    if (_testDisp[i] != null) _testDisp[i].Text = _dashMetricTxts[i].Text;
                    if (_testOrig[i] != null) _testOrig[i].Text = FormatOrigValue(i, ov);
                    if (_testRows[i] != null)
                    {
                        _testRows[i].Text = ok ? "مطابقة" : "مختلفة";
                        _testRows[i].Foreground = new SolidColorBrush(ok ? Theme.Green : Theme.Error);
                    }
                    CWLog.Info("ValuesTest", string.Format("{0}: displayed={1} original={2} => {3}", LogName(i), _dashMetricTxts[i].Text, FormatOrigValue(i, ov), ok ? "PASS" : "FAIL"));
                }
                if (_testVerdict != null)
                {
                    bool allOk = fail == 0;
                    _testVerdict.Text = allOk
                        ? "النتيجة: 6/6 مطابقة — جميع القيم الظاهرة صحيحة وسليمة"
                        : string.Format("النتيجة: {0}/6 مطابقة — يوجد {1} اختلاف، راجع السجل", pass, fail);
                    _testVerdict.Foreground = new SolidColorBrush(allOk ? Theme.Green : Theme.Error);
                }
            }
            catch (Exception ex4) { CWLog.Error("MainWindow.ValuesTest", ex4); }
        }

        private static string LogName(int i)
        {
            switch (i)
            {
                case 0: return "CPU";
                case 1: return "RAM";
                case 2: return "GPU";
                case 3: return "TEMP";
                case 4: return "FPS";
                default: return "HEALTH";
            }
        }
    }
}
