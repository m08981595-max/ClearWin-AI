using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Input;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Effects;
using Microsoft.Win32;

namespace ClearWin
{
    public partial class MainWindow
    {
        private UIElement BuildSettings()
        {
            var stack = new StackPanel();
            stack.Children.Add(MakeText("إعدادات", 28, Colors.White, true));
            var _ses = MakeText("إعدادات كليروين", 13, Color.FromArgb(136, 255, 255, 255), false); _ses.Margin = new Thickness(0, 0, 0, 20); stack.Children.Add(_ses);
            stack.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)), Margin = new Thickness(0, 0, 0, 16) });

            // === Section: General ===
            stack.Children.Add(MakeSectionHeader("\uE730", "عام", Theme.Accent));
            var card = MakeCard(14, null); var inner = new StackPanel { Margin = new Thickness(20) };



            // Overlay toggle
            var overlayRow = new Grid { Margin = new Thickness(0, 4, 0, 12) };
            overlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            overlayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var overlayInfo = new StackPanel();
            overlayInfo.Children.Add(MakeText("تراكب الألعاب", 14, Colors.White, true));
            overlayInfo.Children.Add(MakeText("عرض تراكب الإطارات والمعالج والذاكرة في الألعاب", 11, Color.FromArgb(136, 255, 255, 255), false));
            overlayInfo.Children.Add(MakeText("Ctrl+Shift+F9", 10, Theme.Accent, false));
            Grid.SetColumn(overlayInfo, 0); overlayRow.Children.Add(overlayInfo);
            var overlayToggle = MakeToggle(_overlayEnabled, (on) => {
                _overlayEnabled = on;
                if (!on) HideOverlay();
                SaveOverlaySettings();
            });
            Grid.SetColumn(overlayToggle, 1); overlayRow.Children.Add(overlayToggle);
            inner.Children.Add(overlayRow);

            // Quick search shortcut (AI control shortcuts moved to "المراقبة" group)
            var shortcutsHeader = new TextBlock { Text = "اختصارات التطبيق", FontSize = 16, Foreground = new SolidColorBrush(Theme.Accent), Margin = new Thickness(0, 12, 0, 8) };
            inner.Children.Add(shortcutsHeader);

            var kbRow = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            kbRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            kbRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var kbInfo = new StackPanel();
            kbInfo.Children.Add(MakeText("بحث سريع", 13, Colors.White, true));
            kbInfo.Children.Add(MakeText("فتح لوحة البحث للتنقل السريع", 11, Color.FromArgb(136, 255, 255, 255), false));
            kbInfo.Children.Add(MakeText("Ctrl+K", 10, Theme.Accent, false));
            Grid.SetColumn(kbInfo, 0); kbRow.Children.Add(kbInfo);
            inner.Children.Add(kbRow);

            // Minimize to tray toggle
            var trayRow = new Grid { Margin = new Thickness(0, 4, 0, 12) };
            trayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            trayRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var trayInfo = new StackPanel();
            trayInfo.Children.Add(MakeText("الإبقاء في الخلفية", 14, Colors.White, true));
            trayInfo.Children.Add(MakeText("إعداد اختياري لوضع ClearWin في الخلفية فقط؛ التصغير يظل ظاهرًا في شريط المهام.", 11, Color.FromArgb(136, 255, 255, 255), false));
            Grid.SetColumn(trayInfo, 0); trayRow.Children.Add(trayInfo);
            var trayToggle = MakeToggle(_minimizeToTray, (on) => { _minimizeToTray = on; SaveMinimizeToTraySetting(); });
            Grid.SetColumn(trayToggle, 1); trayRow.Children.Add(trayToggle);
            inner.Children.Add(trayRow);

            card.Child = inner; stack.Children.Add(card);

            // === Section: Overlay ===
            stack.Children.Add(MakeSectionHeader("\uE7F4", "عام — التراكب", Theme.Purple));
            var overCard = MakeCard(14, null); var overInner = new StackPanel { Margin = new Thickness(20) };

            overInner.Children.Add(MakeText("الموضع", 13, Colors.White, true));
            var posGrid = new Grid { Margin = new Thickness(0, 6, 0, 12) };
            for (int i = 0; i < 2; i++) posGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int i = 0; i < 2; i++) posGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            string[] posLabels = new[] { "Top Right", "Top Left", "Bottom Right", "Bottom Left" };
            OverlayPosition[] posVals = new[] { OverlayPosition.TopRight, OverlayPosition.TopLeft, OverlayPosition.BottomRight, OverlayPosition.BottomLeft };
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var btn = new Border
                {
                    Background = new SolidColorBrush(_overlayPosition == posVals[i] ? Color.FromArgb(50, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B) : Theme.SubCard),
                    CornerRadius = new CornerRadius(8), Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(3), Cursor = Cursors.Hand,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    BorderBrush = new SolidColorBrush(_overlayPosition == posVals[i] ? Color.FromArgb(80, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B) : Theme.BorderClr),
                    BorderThickness = new Thickness(1)
                };
                btn.Child = MakeText(posLabels[i], 11, _overlayPosition == posVals[i] ? Colors.White : Theme.TextSecondary, _overlayPosition == posVals[i]);
                int row = i / 2; int col = i % 2;
                Grid.SetRow(btn, row); Grid.SetColumn(btn, col); posGrid.Children.Add(btn);
                btn.MouseDown += (s3, e3) =>
                {
                    _overlayPosition = posVals[idx];
                    if (_overlayWindow != null) _overlayWindow.SetPosition(_overlayPosition);
                    SaveOverlaySettings();
                    foreach (var child in posGrid.Children) { var b = child as Border; if (b != null) { b.Background = new SolidColorBrush(Theme.SubCard); b.BorderBrush = new SolidColorBrush(Theme.BorderClr); } }
                    btn.Background = new SolidColorBrush(Color.FromArgb(50, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B));
                    btn.BorderBrush = new SolidColorBrush(Color.FromArgb(80, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B));
                };
            }
            overInner.Children.Add(posGrid);

            overInner.Children.Add(MakeText("الشفافية", 13, Colors.White, true));
            var opaStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 12) };
            var opaDown = new Border { Background = new SolidColorBrush(Theme.SubCard), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 4, 12, 4), Cursor = Cursors.Hand, BorderBrush = new SolidColorBrush(Theme.BorderClr), BorderThickness = new Thickness(1) };
            opaDown.Child = MakeText("-", 14, Colors.White, true);
            var opaText = MakeText(string.Format("{0:F0}%", _overlayOpacity * 100), 13, Colors.White, true);
            opaText.Margin = new Thickness(14, 0, 14, 0); opaText.Width = 40; opaText.HorizontalAlignment = HorizontalAlignment.Center;
            var opaUp = new Border { Background = new SolidColorBrush(Theme.SubCard), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 4, 12, 4), Cursor = Cursors.Hand, BorderBrush = new SolidColorBrush(Theme.BorderClr), BorderThickness = new Thickness(1) };
            opaUp.Child = MakeText("+", 14, Colors.White, true);
            opaDown.MouseDown += (s3, e3) => { _overlayOpacity = Math.Max(0.2, _overlayOpacity - 0.1); if (_overlayWindow != null) _overlayWindow.SetOpacity(_overlayOpacity); opaText.Text = string.Format("{0:F0}%", _overlayOpacity * 100); SaveOverlaySettings(); };
            opaUp.MouseDown += (s3, e3) => { _overlayOpacity = Math.Min(1.0, _overlayOpacity + 0.1); if (_overlayWindow != null) _overlayWindow.SetOpacity(_overlayOpacity); opaText.Text = string.Format("{0:F0}%", _overlayOpacity * 100); SaveOverlaySettings(); };
            opaStack.Children.Add(opaDown); opaStack.Children.Add(opaText); opaStack.Children.Add(opaUp);
            overInner.Children.Add(opaStack);

            overCard.Child = overInner; stack.Children.Add(overCard);

            // === Section: SmartAgent (automation toggles) ===
            stack.Children.Add(MakeSectionHeader("\uE945", "SmartAgent", Theme.Green));
            var saCard = MakeCard(14, null); var saInner = new StackPanel { Margin = new Thickness(20) };
            saInner.Children.Add(MakeText("تصرفات الأتمتة — تعمل فقط عندما يكون وضع الوكيل مفعلا (اقتراحات / أوتوبيوت)", 11, Color.FromArgb(136, 255, 255, 255), false));

            var apRow = new Grid { Margin = new Thickness(0, 4, 0, 12) };
            apRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            apRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var apInfo = new StackPanel();
            apInfo.Children.Add(MakeText("أولويات المعالجة", 14, Colors.White, true));
            apInfo.Children.Add(MakeText("ضبط أولوية النظام والعمليات تلقائا عند الضغط على الموارد", 11, Color.FromArgb(136, 255, 255, 255), false));
            Grid.SetColumn(apInfo, 0); apRow.Children.Add(apInfo);
            var apToggle = MakeToggle(_autoPriority, (on) => { _autoPriority = on; SaveAgentAutomationSetting(); });
            Grid.SetColumn(apToggle, 1); apRow.Children.Add(apToggle);
            saInner.Children.Add(apRow);

            var ramRow = new Grid { Margin = new Thickness(0, 4, 0, 12) };
            ramRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ramRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var ramInfo = new StackPanel();
            ramInfo.Children.Add(MakeText("تحسين الذاكرة تلقائا", 14, Colors.White, true));
            ramInfo.Children.Add(MakeText("تنظيف الذاكرة كل 5 دقائق تلقائا", 11, Color.FromArgb(136, 255, 255, 255), false));
            Grid.SetColumn(ramInfo, 0); ramRow.Children.Add(ramInfo);
            var ramToggle = MakeToggle(_autoRamClean, (on) => { _autoRamClean = on; SaveAutoRamCleanSetting(); });
            Grid.SetColumn(ramToggle, 1); ramRow.Children.Add(ramToggle);
            saInner.Children.Add(ramRow);

            var schedRow = new Grid { Margin = new Thickness(0, 4, 0, 12) };
            schedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            schedRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var schedInfo = new StackPanel();
            schedInfo.Children.Add(MakeText("المجدول الذكي", 14, Colors.White, true));
            schedInfo.Children.Add(MakeText("تنظيف RAM تلقائا عند تجاوز 85%", 11, Color.FromArgb(136, 255, 255, 255), false));
            Grid.SetColumn(schedInfo, 0); schedRow.Children.Add(schedInfo);
            var schedToggle = MakeToggle(_autoScheduler, (on) => { _autoScheduler = on; SaveSchedulerSetting(); if (on) InitScheduler(); });
            Grid.SetColumn(schedToggle, 1); schedRow.Children.Add(schedToggle);
            saInner.Children.Add(schedRow);

            var pilotRow = new Grid { Margin = new Thickness(0, 4, 0, 12) };
            pilotRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pilotRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var pilotInfo = new StackPanel();
            pilotInfo.Children.Add(MakeText("طيار الألعاب الآلي", 14, Colors.White, true));
            pilotInfo.Children.Add(MakeText("اكتشاف الألعاب النشطة تلقائا وتطبيق أفضل إعدادات لها عند بدء اللعب", 11, Color.FromArgb(136, 255, 255, 255), false));
            Grid.SetColumn(pilotInfo, 0); pilotRow.Children.Add(pilotInfo);
            var pilotToggle = MakeToggle(_autoPilot, (on) => { _autoPilot = on; SaveAgentAutomationSetting(); });
            Grid.SetColumn(pilotToggle, 1); pilotRow.Children.Add(pilotToggle);
            saInner.Children.Add(pilotRow);

            saCard.Child = saInner; stack.Children.Add(saCard);

            // === Section: Appearance ===
            stack.Children.Add(MakeSectionHeader("\uE790", "عام — المظهر", Theme.Cyan));
            var lookCard = MakeCard(14, null); var lookInner = new StackPanel { Margin = new Thickness(20) };

            lookInner.Children.Add(MakeText("اللغة", 13, Colors.White, true));
            var langBtn = new Border { Background = new SolidColorBrush(Theme.SubCard), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 8, 16, 8), Width = 160, Cursor = Cursors.Hand, Margin = new Thickness(0, 6, 0, 12), BorderBrush = new SolidColorBrush(Theme.BorderClr), BorderThickness = new Thickness(1) };
            string[] langLabels = new[] { "عربي", "English", "عربي+English" };
            langBtn.Child = MakeText(langLabels[_langMode], 12, Colors.White, true);
            langBtn.MouseDown += (s3, e3) => { _langMode = (_langMode + 1) % 3; langBtn.Child = MakeText(langLabels[_langMode], 12, Colors.White, true); RebuildUI(); };
            lookInner.Children.Add(langBtn);

            lookInner.Children.Add(MakeText("لون الواجهة", 13, Colors.White, true));
            var _colDesc = MakeText("اختر اللون الرئيسي للتطبيق", 11, Theme.TextSecondary, false);
            _colDesc.Margin = new Thickness(0, 4, 0, 10);
            lookInner.Children.Add(_colDesc);
            Color[] colorOpts = new[] { Theme.Accent, Theme.Purple, Theme.Green, Theme.Pink, Theme.Warning, Color.FromRgb(99, 102, 241) };
            var colorGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            for (int ci = 0; ci < 6; ci++) colorGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (int ci = 0; ci < colorOpts.Length; ci++)
            {
                int cIdx = ci;
                var dot = new Ellipse { Width = 32, Height = 32, Fill = new SolidColorBrush(colorOpts[ci]), Margin = new Thickness(3), Cursor = Cursors.Hand, StrokeThickness = 2.5 };
                dot.Stroke = _accentColor == colorOpts[ci] ? new SolidColorBrush(Colors.White) : new SolidColorBrush(Colors.Transparent);
                dot.Effect = _accentColor == colorOpts[ci] ? new DropShadowEffect { Color = colorOpts[ci], BlurRadius = 12, ShadowDepth = 0, Opacity = 0.5 } : null;
                dot.MouseDown += (s4, e4) => { _accentColor = colorOpts[cIdx]; foreach (var child in colorGrid.Children) { var e = child as Ellipse; if (e != null) { e.Stroke = new SolidColorBrush(Colors.Transparent); e.Effect = null; } } dot.Stroke = new SolidColorBrush(Colors.White); dot.Effect = new DropShadowEffect { Color = colorOpts[cIdx], BlurRadius = 12, ShadowDepth = 0, Opacity = 0.5 }; SaveColorSetting(); RebuildUI(); };
                Grid.SetColumn(dot, ci); colorGrid.Children.Add(dot);
            }
            lookInner.Children.Add(colorGrid);

            lookInner.Children.Add(MakeText("سرعة الحركات", 13, Colors.White, true));
            var animDesc = MakeText("التحكم في سرعة الرسوم المتحركة", 11, Theme.TextSecondary, false); animDesc.Margin = new Thickness(0, 4, 0, 8);
            lookInner.Children.Add(animDesc);
            var animGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            animGrid.ColumnDefinitions.Add(new ColumnDefinition()); animGrid.ColumnDefinitions.Add(new ColumnDefinition()); animGrid.ColumnDefinitions.Add(new ColumnDefinition());
            string[] animLabels = new[] { "بطيء", "عادي", "سريع" };
            for (int ai = 0; ai < 3; ai++)
            {
                int aIdx = ai;
                var ab = new Border {
                    Background = new SolidColorBrush(_animSpeed == aIdx ? Color.FromArgb(40, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B) : Theme.SubCard),
                    CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(3), Cursor = Cursors.Hand,
                    BorderBrush = new SolidColorBrush(_animSpeed == aIdx ? Color.FromArgb(70, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B) : Theme.BorderClr),
                    BorderThickness = new Thickness(1)
                };
                ab.Child = MakeText(animLabels[aIdx], 12, _animSpeed == aIdx ? Colors.White : Theme.TextSecondary, _animSpeed == aIdx);
                ab.MouseDown += (s5, e5) => { _animSpeed = aIdx; foreach (var child in animGrid.Children) { var b2 = child as Border; if (b2 != null) { b2.Background = new SolidColorBrush(Theme.SubCard); b2.BorderBrush = new SolidColorBrush(Theme.BorderClr); } } ab.Background = new SolidColorBrush(Color.FromArgb(40, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)); ab.BorderBrush = new SolidColorBrush(Color.FromArgb(70, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)); };
                Grid.SetColumn(ab, aIdx); animGrid.Children.Add(ab);
            }
            lookInner.Children.Add(animGrid);

            lookInner.Children.Add(MakeText("مؤشر الفأرة", 13, Colors.White, true));
            var curDesc = MakeText("تغيير شكل مؤشر الفأرة إلى مؤشر Windows 11 الأسود", 11, Theme.TextSecondary, false); curDesc.Margin = new Thickness(0, 4, 0, 8);
            lookInner.Children.Add(curDesc);
            var cursorRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            var w11Btn = new Border { Background = new SolidColorBrush(Color.FromArgb(25, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 8, 16, 8), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0), BorderBrush = new SolidColorBrush(Color.FromArgb(40, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)), BorderThickness = new Thickness(1) };
            w11Btn.Child = MakeText("Windows 11 أسود", 12, Theme.Accent, true);
            w11Btn.MouseDown += (sCur, eCur) => { bool ok = ApplyWindows11BlackCursor(); MessageBox.Show(ok ? "تم تطبيق مؤشر Windows 11 الأسود بنجاح!" : "فشل تطبيق المؤشر. تأكد من توفر ملفات المؤشر في C:\\Windows\\Cursors\\W11_dark_v2.2", "ClearWin - المؤشر"); };
            cursorRow.Children.Add(w11Btn);
            var defBtn = new Border { Background = new SolidColorBrush(Theme.SubCard), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 8, 16, 8), Cursor = Cursors.Hand, BorderBrush = new SolidColorBrush(Theme.BorderClr), BorderThickness = new Thickness(1) };
            defBtn.Child = MakeText("الافتراضي", 12, Theme.TextSecondary, true);
            defBtn.MouseDown += (sDef, eDef) => { bool ok = RestoreDefaultCursors(); MessageBox.Show(ok ? "تم استعادة المؤشر الافتراضي!" : "فشل استعادة المؤشر الافتراضي", "ClearWin - المؤشر"); };
            cursorRow.Children.Add(defBtn);
            lookInner.Children.Add(cursorRow);

            // === Section: Monitoring (AI control shortcuts) ===
            stack.Children.Add(MakeSectionHeader("\uE77A", "المراقبة", Theme.Cyan));
            var monCard = MakeCard(14, null); var monInner = new StackPanel { Margin = new Thickness(20) };

            var monKsRow = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            monKsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            monKsRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var monKsInfo = new StackPanel();
            monKsInfo.Children.Add(MakeText("Kill Switch", 13, Colors.White, true));
            monKsInfo.Children.Add(MakeText("إيقاف جميع عمليات الذكاء الاصطناعي فوراً", 11, Color.FromArgb(136, 255, 255, 255), false));
            monKsInfo.Children.Add(MakeText("Ctrl+Shift+F10", 10, Theme.Accent, false));
            Grid.SetColumn(monKsInfo, 0); monKsRow.Children.Add(monKsInfo);
            monInner.Children.Add(monKsRow);

            var monPauseRow = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            monPauseRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            monPauseRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var monPauseInfo = new StackPanel();
            monPauseInfo.Children.Add(MakeText("إيقاف مؤقت / استئناف", 13, Colors.White, true));
            monPauseInfo.Children.Add(MakeText("إيقاف مؤقت لمدة دقيقتين ثم استئناف تلقائي", 11, Color.FromArgb(136, 255, 255, 255), false));
            monPauseInfo.Children.Add(MakeText("Ctrl+Shift+F11", 10, Theme.Accent, false));
            Grid.SetColumn(monPauseInfo, 0); monPauseRow.Children.Add(monPauseInfo);
            monInner.Children.Add(monPauseRow);

            var monEmergRow = new Grid { Margin = new Thickness(0, 2, 0, 6) };
            monEmergRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            monEmergRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var monEmergInfo = new StackPanel();
            monEmergInfo.Children.Add(MakeText("إيقاف الطوارئ الكامل", 13, Colors.White, true));
            monEmergInfo.Children.Add(MakeText("إيقاف كل شيء وإرجاع الإعدادات الأصلية", 11, Color.FromArgb(136, 255, 255, 255), false));
            monEmergInfo.Children.Add(MakeText("Ctrl+Shift+F8", 10, Theme.Accent, false));
            Grid.SetColumn(monEmergInfo, 0); monEmergRow.Children.Add(monEmergInfo);
            monInner.Children.Add(monEmergRow);

            monCard.Child = monInner; stack.Children.Add(monCard);

            lookCard.Child = lookInner; stack.Children.Add(lookCard);

            // === Section: Safety ===
            stack.Children.Add(MakeSectionHeader("\uE72E", "السلامة", Theme.Error));
            var safeCard = MakeCard(14, null); var safeInner = new StackPanel { Margin = new Thickness(20) };
            safeInner.Children.Add(MakeText("التحكم البشري الكامل", 14, Colors.White, true));
            safeInner.Children.Add(MakeText("كل إجراء في ClearWin مرئي وقابل للمراجعة وموافق عليه يدوياً. الوكيل يراقب ويقيّم ويقترح — ولا ينفذ أي تغيير تلقائياً أبداً.", 11, Color.FromArgb(136, 255, 255, 255), false));
            var zeroRow = new Grid { Margin = new Thickness(0, 10, 0, 4) };
            zeroRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            zeroRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var zeroInfo = new StackPanel();
            zeroInfo.Children.Add(MakeText("أكبر عدد من الإجراءات التلقائية", 13, Colors.White, true));
            zeroInfo.Children.Add(MakeText("قراءة فقط — قيمة محكومة في المنتج ولا يمكن تغييرها", 11, Color.FromArgb(136, 255, 255, 255), false));
            Grid.SetColumn(zeroInfo, 0); zeroRow.Children.Add(zeroInfo);
            var zeroBadge = new Border { Background = new SolidColorBrush(Color.FromArgb(25, Theme.Error.R, Theme.Error.G, Theme.Error.B)), CornerRadius = new CornerRadius(10), Padding = new Thickness(18, 8, 18, 8), BorderBrush = new SolidColorBrush(Color.FromArgb(60, Theme.Error.R, Theme.Error.G, Theme.Error.B)), BorderThickness = new Thickness(1) };
            zeroBadge.Child = MakeText("0", 22, Theme.Error, true);
            zeroBadge.IsEnabled = false;
            Grid.SetColumn(zeroBadge, 1); zeroRow.Children.Add(zeroBadge);
            safeInner.Children.Add(zeroRow);
            safeCard.Child = safeInner; stack.Children.Add(safeCard);

            // === Sub-section: Danger Zone (within Safety group) ===
            stack.Children.Add(MakeSectionHeader("\uE7BA", "منطقة الخطر", Theme.Error));
            var resetCard = MakeCard(14, null); var resetInner = new StackPanel { Margin = new Thickness(20) };
            resetInner.Children.Add(MakeText("إعادة تعيين الإعدادات", 14, Colors.White, true));
            resetInner.Children.Add(MakeText("سيتم حذف جميع الإعدادات المحفوظة والعودة إلى الافتراضيات", 11, Theme.TextSecondary, false));
            var resetBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(30, Theme.Error.R, Theme.Error.G, Theme.Error.B)), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 8, 16, 8), Width = 140, Cursor = Cursors.Hand, Margin = new Thickness(0, 10, 0, 0), BorderBrush = new SolidColorBrush(Color.FromArgb(50, Theme.Error.R, Theme.Error.G, Theme.Error.B)), BorderThickness = new Thickness(1) };
            resetBtn.Child = MakeText("إعادة تعيين", 12, Theme.Error, true);
            resetBtn.MouseDown += (s8, e8) => { if (MessageBox.Show("هل أنت متأكد من إعادة تعيين جميع الإعدادات؟", "تأكيد", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { _accentColor = Color.FromRgb(46, 168, 255); _langMode = 0; _animSpeed = 1; _defaultTab = 0; _autoRamClean = false; _autoScheduler = false; _overlayEnabled = false; _overlayOpacity = 0.78; _overlayPosition = OverlayPosition.TopRight; _minimizeToTray = false; _autoPriority = false; _autoPilot = false; RebuildUI(); } };
            resetInner.Children.Add(resetBtn);
            resetCard.Child = resetInner; stack.Children.Add(resetCard);

            // === Section: Logs & Data ===
            stack.Children.Add(MakeSectionHeader("\uE74E", "السجلات والبيانات", Theme.Warning));
            var syncCard = MakeCard(14, null); var syncInner = new StackPanel { Margin = new Thickness(20) };
            var syncRow = new StackPanel { Orientation = Orientation.Horizontal };
            var exportBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(25, Theme.Green.R, Theme.Green.G, Theme.Green.B)), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 8, 16, 8), Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 8, 0), BorderBrush = new SolidColorBrush(Color.FromArgb(40, Theme.Green.R, Theme.Green.G, Theme.Green.B)), BorderThickness = new Thickness(1) };
            exportBtn.Child = MakeText("تصدير الإعدادات", 12, Theme.Green, true);
            exportBtn.MouseDown += (s6, e6) => SaveCloudSettings();
            syncRow.Children.Add(exportBtn);
            var importBtn = new Border { Background = new SolidColorBrush(Color.FromArgb(25, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)), CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 8, 16, 8), Cursor = Cursors.Hand, BorderBrush = new SolidColorBrush(Color.FromArgb(40, Theme.Accent.R, Theme.Accent.G, Theme.Accent.B)), BorderThickness = new Thickness(1) };
            importBtn.Child = MakeText("استيراد الإعدادات", 12, Theme.Accent, true);
            importBtn.MouseDown += (s7, e7) => LoadCloudSettings();
            syncRow.Children.Add(importBtn);
            syncInner.Children.Add(syncRow);
            syncCard.Child = syncInner; stack.Children.Add(syncCard);

            var dataCard = MakeCard(14, null); var dataInner = new StackPanel { Margin = new Thickness(20) };
            dataInner.Children.Add(MakeText("مواقع حفظ البيانات والسجلات", 14, Colors.White, true));
            dataInner.Children.Add(MakeText("الإعدادات العامة: سجل Windows في SOFTWARE\\ClearWin (تُحفظ تلقائياً عند كل تغيير)", 11, Theme.TextSecondary, false));
            dataInner.Children.Add(MakeText("بيانات تعلم الوكيل: مجلد %LOCALAPPDATA% الخاص بالتطبيق", 11, Theme.TextSecondary, false));
            dataInner.Children.Add(MakeText("سجلات التشغيل الداخلية: تُدوَّن في ملفات السجلات وتُدار تلقائياً", 11, Theme.TextMuted, false));
            dataCard.Child = dataInner; stack.Children.Add(dataCard);

            // === Section: About ===
            stack.Children.Add(MakeSectionHeader("\uE946", "حول التطبيق", Theme.Accent));
            var aboutCard = MakeCard(14, null); var aboutInner = new StackPanel { Margin = new Thickness(20) };
            aboutInner.Children.Add(MakeText("ClearWin", 22, Colors.White, true));
            aboutInner.Children.Add(MakeText("Smart Windows Optimizer v2.5", 13, Theme.TextSecondary, false));
            aboutInner.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Theme.BorderClr), Margin = new Thickness(0, 12, 0, 12) });
            aboutInner.Children.Add(MakeText("مطور بواسطة: Abdullah Mohamed", 12, Theme.TextSecondary, false));
            aboutInner.Children.Add(MakeText("Runtime: .NET Framework 4.8", 12, Theme.TextMuted, false));
            aboutInner.Children.Add(MakeText("Platform: " + Environment.OSVersion.VersionString, 12, Theme.TextMuted, false));
            aboutCard.Child = aboutInner; stack.Children.Add(aboutCard);

            return new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        }

        private void LoadColorSetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\ClearWin", false))
                {
                    if (rk != null)
                    {
                        object col = rk.GetValue("AccentColor");
                        if (col != null)
                        {
                            int cv = (int)col;
                            _accentColor = Color.FromRgb((byte)(cv >> 16), (byte)(cv >> 8), (byte)cv);
                        }
                        object ram = rk.GetValue("AutoRamClean");
                        if (ram != null) _autoRamClean = (int)ram == 1;
                        object spd = rk.GetValue("AnimSpeed");
                        if (spd != null) _animSpeed = Math.Max(0, Math.Min(2, (int)spd));
                        object sched = rk.GetValue("AutoScheduler");
                        if (sched != null) _autoScheduler = (int)sched == 1;
                        object pri = rk.GetValue("AutoPriority");
                        if (pri != null) _autoPriority = (int)pri == 1;
                        object pilot = rk.GetValue("AutoPilot");
                        if (pilot != null) _autoPilot = (int)pilot == 1;
                        object defTab = rk.GetValue("DefaultTab");
                        if (defTab != null) _defaultTab = Math.Max(0, Math.Min(2, (int)defTab));
                    }
                }
            } catch (Exception ex) { CWLog.Error("MainWindow.Settings", ex); }
        }

        private void SaveColorSetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ClearWin", true))
                {
                    int cv = (_accentColor.R << 16) | (_accentColor.G << 8) | _accentColor.B;
                    rk.SetValue("AccentColor", cv, RegistryValueKind.DWord);
                }
            } catch (Exception ex2) { CWLog.Error("MainWindow.Settings", ex2); }
        }

        private void LoadAutoRamCleanSetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\ClearWin", false))
                {
                    if (rk != null)
                    {
                        object ram = rk.GetValue("AutoRamClean");
                        if (ram != null) _autoRamClean = (int)ram == 1;
                    }
                }
            } catch (Exception ex3) { CWLog.Error("MainWindow.Settings", ex3); }
        }

        internal void LoadMinimizeToTraySetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\ClearWin", false))
                {
                    if (rk != null)
                    {
                        object val = rk.GetValue("MinimizeToTray");
                        if (val != null) _minimizeToTray = (int)val == 1;
                    }
                }
            } catch (Exception ex4) { CWLog.Error("MainWindow.Settings", ex4); }
        }

        internal void LoadStartWithWindowsSetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\ClearWin", false))
                {
                    if (rk != null)
                    {
                        object val = rk.GetValue("StartWithWindows");
                        if (val != null)
                        {
                            _startWithWindows = (int)val == 1;
                            return;
                        }
                    }
                }
                using (var runKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false))
                {
                    if (runKey != null && runKey.GetValue("ClearWin") != null)
                    {
                        _startWithWindows = true;
                    }
                }
            } catch (Exception exSw) { CWLog.Error("MainWindow.Settings.StartWithWindows", exSw); }
        }

        private void SaveAutoRamCleanSetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ClearWin", true))
                {
                    rk.SetValue("AutoRamClean", _autoRamClean ? 1 : 0, RegistryValueKind.DWord);
                }
            } catch (Exception ex5) { CWLog.Error("MainWindow.Settings", ex5); }
        }

        internal void SaveMinimizeToTraySetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ClearWin", true))
                {
                    rk.SetValue("MinimizeToTray", _minimizeToTray ? 1 : 0, RegistryValueKind.DWord);
                }
            } catch (Exception ex6) { CWLog.Error("MainWindow.Settings", ex6); }
        }

        internal void SaveStartWithWindowsSetting(bool enable)
        {
            _startWithWindows = enable;
            try
            {
                using (var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ClearWin", true))
                {
                    rk.SetValue("StartWithWindows", _startWithWindows ? 1 : 0, RegistryValueKind.DWord);
                }
            } catch (Exception exSw) { CWLog.Error("MainWindow.Settings.StartWithWindows", exSw); }

            SyncStartWithWindowsRegistry();
        }

        internal void SyncStartWithWindowsRegistry()
        {
            try
            {
                using (var runKey = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (runKey != null)
                    {
                        if (_startWithWindows)
                        {
                            string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
                            {
                                try { exePath = Process.GetCurrentProcess().MainModule.FileName; } catch { }
                            }
                            if (!string.IsNullOrEmpty(exePath))
                            {
                                string val = "\"" + exePath + "\" --minimized";
                                runKey.SetValue("ClearWin", val, RegistryValueKind.String);
                            }
                        }
                        else
                        {
                            if (runKey.GetValue("ClearWin") != null)
                            {
                                runKey.DeleteValue("ClearWin", false);
                            }
                        }
                    }
                }
            } catch (Exception exRun) { CWLog.Error("MainWindow.Settings.SyncRunKey", exRun); }
        }

        internal void LoadGameGpuPowerSetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\ClearWin", false))
                {
                    if (rk != null)
                    {
                        object val = rk.GetValue("GameGpuPower");
                        if (val != null) _gameGpuPower = (int)val == 1;
                    }
                }
            } catch (Exception ex) { CWLog.Error("MainWindow.Settings.GameGpuPower", ex); }
        }

        internal void SaveGameGpuPowerSetting(bool enable)
        {
            _gameGpuPower = enable;
            try
            {
                using (var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ClearWin", true))
                {
                    rk.SetValue("GameGpuPower", _gameGpuPower ? 1 : 0, RegistryValueKind.DWord);
                }
            } catch (Exception ex) { CWLog.Error("MainWindow.Settings.GameGpuPower", ex); }
        }

        private void SaveAnimSpeedSetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ClearWin", true))
                {
                    rk.SetValue("AnimSpeed", _animSpeed, RegistryValueKind.DWord);
                }
            } catch (Exception ex7) { CWLog.Error("MainWindow.Settings", ex7); }
        }

        private void SaveSchedulerSetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ClearWin", true))
                {
                    rk.SetValue("AutoScheduler", _autoScheduler ? 1 : 0, RegistryValueKind.DWord);
                }
            } catch (Exception ex8) { CWLog.Error("MainWindow.Settings", ex8); }
        }

        private void SaveAgentAutomationSetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ClearWin", true))
                {
                    rk.SetValue("AutoPriority", _autoPriority ? 1 : 0, RegistryValueKind.DWord);
                    rk.SetValue("AutoPilot", _autoPilot ? 1 : 0, RegistryValueKind.DWord);
                }
            } catch (Exception exAg) { CWLog.Error("MainWindow.Settings", exAg); }
        }

        private void SaveDefaultTabSetting()
        {
            try
            {
                using (var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ClearWin", true))
                {
                    rk.SetValue("DefaultTab", _defaultTab, RegistryValueKind.DWord);
                }
            } catch (Exception ex9) { CWLog.Error("MainWindow.Settings", ex9); }
        }

        private void RebuildUI()
        {
            try
            {
                _mainTimer.Stop();
                Content = null;
                HideOverlay();
                if (_pageCache != null) _pageCache.Clear();
                BuildUI();
                _mainTimer.Start();
                if (_currentTab != null) _mainContent.Content = BuildPage(_currentTab);
            } catch (Exception ex10) { CWLog.Error("MainWindow.Settings", ex10); }
        }

        private void SaveCloudSettings()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine(string.Format("AccentColor={0},{1},{2}", _accentColor.R, _accentColor.G, _accentColor.B));
                sb.AppendLine(string.Format("LangMode={0}", _langMode));
                sb.AppendLine(string.Format("AnimSpeed={0}", _animSpeed));
                sb.AppendLine(string.Format("DefaultTab={0}", _defaultTab));
                sb.AppendLine(string.Format("AutoRamClean={0}", _autoRamClean ? 1 : 0));
                sb.AppendLine(string.Format("AutoScheduler={0}", _autoScheduler ? 1 : 0));
                sb.AppendLine(string.Format("AutoPriority={0}", _autoPriority ? 1 : 0));
                sb.AppendLine(string.Format("AutoPilot={0}", _autoPilot ? 1 : 0));
                sb.AppendLine(string.Format("OverlayEnabled={0}", _overlayEnabled ? 1 : 0));
                sb.AppendLine(string.Format("OverlayPosition={0}", (int)_overlayPosition));
                sb.AppendLine(string.Format("OverlayOpacity={0}", (int)(_overlayOpacity * 100)));
                sb.AppendLine(string.Format("OverlayShowFPS={0}", _overlayShowFPS ? 1 : 0));
                sb.AppendLine(string.Format("OverlayShowCPU={0}", _overlayShowCPU ? 1 : 0));
                sb.AppendLine(string.Format("OverlayShowRAM={0}", _overlayShowRAM ? 1 : 0));
                sb.AppendLine(string.Format("OverlayShowGPU={0}", _overlayShowGPU ? 1 : 0));
                sb.AppendLine(string.Format("OverlayShowTemp={0}", _overlayShowTemp ? 1 : 0));
                sb.AppendLine(string.Format("OverlayShowClock={0}", _overlayShowClock ? 1 : 0));
                sb.AppendLine(string.Format("OverlayTextColor={0},{1},{2}", _overlayTextColor.R, _overlayTextColor.G, _overlayTextColor.B));
                sb.AppendLine(string.Format("MinimizeToTray={0}", _minimizeToTray ? 1 : 0));
                string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\ClearWin_Settings.cfg";
                File.WriteAllText(path, sb.ToString());
                MessageBox.Show("تم حفظ الإعدادات على سطح المكتب!", "Cloud Sync");
            }
            catch { MessageBox.Show("فشل حفظ الإعدادات", "خطأ"); }
        }

        private void LoadCloudSettings()
        {
            try
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop) + "\\ClearWin_Settings.cfg";
                if (!File.Exists(path)) { MessageBox.Show("ما لقينا ملف الإعدادات!", "Cloud Sync"); return; }
                foreach (string line in File.ReadAllLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || !line.Contains("=")) continue;
                    string key = line.Substring(0, line.IndexOf('='));
                    string val = line.Substring(line.IndexOf('=') + 1);
                    if (key == "AccentColor") { var pts = val.Split(','); if (pts.Length == 3) _accentColor = Color.FromRgb(byte.Parse(pts[0]), byte.Parse(pts[1]), byte.Parse(pts[2])); }
                    else if (key == "LangMode") _langMode = Math.Max(0, Math.Min(2, int.Parse(val)));
                    else if (key == "AnimSpeed") _animSpeed = Math.Max(0, Math.Min(2, int.Parse(val)));
                    else if (key == "DefaultTab") _defaultTab = Math.Max(0, Math.Min(2, int.Parse(val)));
                    else if (key == "AutoRamClean") _autoRamClean = int.Parse(val) == 1;
                    else if (key == "AutoScheduler") _autoScheduler = int.Parse(val) == 1;
                    else if (key == "AutoPriority") _autoPriority = int.Parse(val) == 1;
                    else if (key == "AutoPilot") _autoPilot = int.Parse(val) == 1;
                    else if (key == "OverlayEnabled") _overlayEnabled = int.Parse(val) == 1;
                    else if (key == "OverlayPosition") _overlayPosition = (OverlayPosition)Math.Max(0, Math.Min(3, int.Parse(val)));
                    else if (key == "OverlayOpacity") _overlayOpacity = Math.Max(0.2, Math.Min(1.0, int.Parse(val) / 100.0));
                    else if (key == "OverlayShowFPS") _overlayShowFPS = int.Parse(val) == 1;
                    else if (key == "OverlayShowCPU") _overlayShowCPU = int.Parse(val) == 1;
                    else if (key == "OverlayShowRAM") _overlayShowRAM = int.Parse(val) == 1;
                    else if (key == "OverlayShowGPU") _overlayShowGPU = int.Parse(val) == 1;
                    else if (key == "OverlayShowTemp") _overlayShowTemp = int.Parse(val) == 1;
                    else if (key == "OverlayShowClock") _overlayShowClock = int.Parse(val) == 1;
                    else if (key == "OverlayTextColor") { var pts = val.Split(','); if (pts.Length == 3) _overlayTextColor = Color.FromRgb(byte.Parse(pts[0]), byte.Parse(pts[1]), byte.Parse(pts[2])); }
                    else if (key == "MinimizeToTray") _minimizeToTray = int.Parse(val) == 1;
                }
                SaveColorSetting();
                SaveAutoRamCleanSetting();
                SaveSchedulerSetting();
                SaveOverlaySettings();
                SaveMinimizeToTraySetting();
                LoadAutoRamCleanSetting();
                LoadOverlaySettings();
                _navItems.Clear();
                _mainContent.Content = null;
                BuildUI();
                MessageBox.Show("تم تحميل الإعدادات بنجاح!", "Cloud Sync");
            }
            catch { MessageBox.Show("فشل تحميل الإعدادات", "خطأ"); }
        }

        private void LoadOverlaySettings()
        {
            try
            {
                using (var rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\ClearWin", false))
                {
                    if (rk != null)
                    {
                        object pos = rk.GetValue("OverlayPosition");
                        if (pos != null) { int p = (int)pos; if (p >= 0 && p <= 3) _overlayPosition = (OverlayPosition)p; }
                        object opa = rk.GetValue("OverlayOpacity");
                        if (opa != null) { int o = (int)opa; _overlayOpacity = Math.Max(0.2, Math.Min(1.0, o / 100.0)); }
                        object tc = rk.GetValue("OverlayTextColor");
                        if (tc != null)
                        {
                            int rgb = (int)tc;
                            _overlayTextColor = Color.FromRgb((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);
                        }
                    }
                }
            } catch (Exception ex11) { CWLog.Error("MainWindow.Settings", ex11); }
        }

        private void SaveOverlaySettings()
        {
            try
            {
                using (var rk = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\ClearWin", true))
                {
                    rk.SetValue("OverlayPosition", (int)_overlayPosition, RegistryValueKind.DWord);
                    rk.SetValue("OverlayOpacity", (int)(_overlayOpacity * 100), RegistryValueKind.DWord);
                    rk.SetValue("OverlayTextColor", (_overlayTextColor.R << 16) | (_overlayTextColor.G << 8) | _overlayTextColor.B, RegistryValueKind.DWord);
                }
            } catch (Exception ex12) { CWLog.Error("MainWindow.Settings", ex12); }
        }
    }
}