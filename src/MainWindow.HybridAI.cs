// ============================================================================
// ClearWin — Hybrid AI UI integration (code-behind)
// Adds three controls to the Smart Agent page:
//   * Restore Default AI  -> RestoreDefaultAI_Click
//   * Hybrid Game Boost   -> HybridGameBoost_Click
//   * Freeze AI (inference mode) -> FreezeAi_Click
// All heavy work is offloaded with Task.Run so the WPF UI thread stays at 60fps.
// ============================================================================

using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ClearWin
{
    public partial class MainWindow
    {
        private TextBlock _hybridAiStatusText;

        /// <summary>
        /// Build the hybrid controls. Called from BuildAgentPage() in
        // MainWindow.AI.cs so the buttons live inside the existing Agent page.
        /// </summary>
        private void AddHybridAiControls(StackPanel host)
        {
            if (host == null) return;
            try
            {
                host.Children.Add(UI.SectionHeader("الذكاء الهجين / Hybrid AI", Theme.Cyan,
                    "قواعد ثابتة 80% + استدلال عصبي مجمد 20%"));

                var card = UI.GlassCard(14, Theme.Cyan, new Thickness(0, 0, 0, 20));
                var panel = new StackPanel { Margin = new Thickness(18) };

                _hybridAiStatusText = UI.TextWrapped(_agent != null ? _agent.GetHybridAiStatus() : "Agent unavailable",
                                                     12, Theme.TextPrimary, false);
                _hybridAiStatusText.Margin = new Thickness(0, 0, 0, 12);
                panel.Children.Add(_hybridAiStatusText);

                var row = new WrapPanel();

                // The WPF button the user asked for.
                var restoreBtn = new Button
                {
                    Name = "RestoreDefaultAIButton",
                    Content = "Restore Default AI",
                    Height = 34,
                    Padding = new Thickness(14, 6, 14, 6),
                    Margin = new Thickness(0, 0, 10, 8),
                    Cursor = Cursors.Hand,
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Theme.Accent),
                    BorderThickness = new Thickness(0)
                };
                restoreBtn.Click += RestoreDefaultAI_Click;
                row.Children.Add(restoreBtn);

                var boostBtn = new Button
                {
                    Name = "HybridGameBoostButton",
                    Content = "Hybrid Game Boost",
                    Height = 34,
                    Padding = new Thickness(14, 6, 14, 6),
                    Margin = new Thickness(0, 0, 10, 8),
                    Cursor = Cursors.Hand,
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Theme.Green),
                    BorderThickness = new Thickness(0)
                };
                boostBtn.Click += HybridGameBoost_Click;
                row.Children.Add(boostBtn);

                var freezeBtn = new Button
                {
                    Name = "FreezeAiButton",
                    Content = "Freeze AI (Inference Mode)",
                    Height = 34,
                    Padding = new Thickness(14, 6, 14, 6),
                    Margin = new Thickness(0, 0, 10, 8),
                    Cursor = Cursors.Hand,
                    Foreground = Brushes.White,
                    Background = new SolidColorBrush(Theme.Blue),
                    BorderThickness = new Thickness(0)
                };
                freezeBtn.Click += FreezeAi_Click;
                row.Children.Add(freezeBtn);

                panel.Children.Add(row);
                card.Child = panel;
                host.Children.Add(card);
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.HybridAI", ex);
            }
        }

        /// <summary>
        /// "Restore Default AI" — reset the network to the audited baseline
        /// weights and re-freeze learning. Never blocks the UI thread.
        /// </summary>
        private async void RestoreDefaultAI_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;
            try
            {
                if (_agent == null) { MessageBox.Show("SmartAgent is not initialised.", "ClearWin"); return; }

                var report = await Task.Run(() => _agent.RestoreDefaultAI());
                UpdateHybridStatus();

                MessageBox.Show(report.Summary(),
                    report.Success ? "Default AI restored" : "Restore failed",
                    MessageBoxButton.OK,
                    report.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.HybridAI.RestoreDefaultAI_Click", ex);
                MessageBox.Show("Restore failed: " + ex.Message, "ClearWin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (button != null) button.IsEnabled = true;
            }
        }

        /// <summary>
        /// Game Boost: 80% deterministic rules execute first; the frozen NN may
        /// only touch up to 20% of unknown third-party processes afterwards.
        /// </summary>
        private async void HybridGameBoost_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null) button.IsEnabled = false;
            try
            {
                if (_agent == null) { MessageBox.Show("SmartAgent is not initialised.", "ClearWin"); return; }

                var report = await Task.Run(() => _agent.ExecuteGameBoost());
                UpdateHybridStatus();
                MessageBox.Show(report.Summary() + Environment.NewLine +
                                "Rules: " + string.Join(", ", report.RuleActions) + Environment.NewLine +
                                "Neural: " + (report.NeuralActions.Count == 0 ? "(none)" : string.Join(", ", report.NeuralActions)),
                                "Hybrid Game Boost", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                CWLog.Error("MainWindow.HybridAI.HybridGameBoost_Click", ex);
                MessageBox.Show("Game Boost failed: " + ex.Message, "ClearWin", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (button != null) button.IsEnabled = true;
            }
        }

        /// <summary>Explicitly freeze the network (learning rate = 0).</summary>
        private void FreezeAi_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_agent == null) return;
                _agent.SetInferenceMode(true);
                UpdateHybridStatus();
            }
            catch (Exception ex) { CWLog.Error("MainWindow.HybridAI.FreezeAi_Click", ex); }
        }

        /// <summary>Update the status label from any worker thread.</summary>
        private void UpdateHybridStatus()
        {
            if (_hybridAiStatusText == null || _agent == null) return;
            string status = _agent.GetHybridAiStatus();
            try
            {
                if (Dispatcher.CheckAccess()) _hybridAiStatusText.Text = status;
                else Dispatcher.Invoke(new Action(() => _hybridAiStatusText.Text = status));
            }
            catch (Exception ex) { CWLog.Error("MainWindow.HybridAI.UpdateHybridStatus", ex); }
        }
    }
}
