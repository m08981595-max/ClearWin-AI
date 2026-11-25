// ============================================================================
// Native WPF-UI integration — no MSBuild, no BAML, no InitializeComponent().
// The three Views/*.xaml files are plain XAML parsed at runtime with
// XamlReader.Load and bound to a ViewModel through DataContext.
// ============================================================================

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Navigation;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace ClearWin
{
    public partial class MainWindow
    {
        /// <summary>
        /// Apply the DWM-only Mica backdrop. NOTE: WPF-UI 4.0.3's real signature
        /// is Apply(ApplicationTheme, Wpf.Ui.Controls.WindowBackdropType, bool) —
        /// WindowBackdropType lives in Wpf.Ui.Controls, not Wpf.Ui.Appearance.
        /// </summary>
        private void ApplyNativeBackdrop()
        {
            try
            {
                ApplicationThemeManager.Apply(
                    ApplicationTheme.Dark,
                    WindowBackdropType.Mica,
                    true);
                CWLog.Info("UI.Native", "WPF-UI Mica backdrop applied (DWM hardware accelerated).");
            }
            catch (Exception ex)
            {
                CWLog.Error("UI.Native.Mica", ex);
            }
        }

        /// <summary>
        /// Parse a loose XAML file from bin\Views at runtime. x:Class must be
        /// absent — XamlReader.Load rejects compiled-page markup.
        /// </summary>
        private object LoadView(string viewName, object dataContext)
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Views", viewName);
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                var element = (FrameworkElement)XamlReader.Load(fs);
                element.DataContext = dataContext;
                return element;
            }
        }

        /// <summary>
        /// A Page may only be hosted by a Window or a Frame. Wrapping in a
        /// Frame avoids "Page can have only Window or Frame as parent".
        /// </summary>
        private UIElement HostView(string viewName, object dataContext)
        {
            object element = LoadView(viewName, dataContext);
            var page = element as Page;
            if (page != null)
                return new Frame
                {
                    Content = page,
                    NavigationUIVisibility = NavigationUIVisibility.Hidden,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
            return element as UIElement;
        }

        /// <summary>Dispose the WebView2 host so it releases RAM immediately.</summary>
        private void DisposeWebUi()
        {
            if (_webUiHost == null) return;
            try { _webUiHost.Dispose(); }
            catch (Exception ex) { CWLog.Error("UI.Native.WebUiDispose", ex); }
            _webUiHost = null;
        }

        /// <summary>Show the native Game Boost page as the main workspace.</summary>
        private void ShowNativeGameBoost()
        {
            try
            {
                DisposeWebUi();
                var view = HostView("GameBoostPage.xaml", new ViewModels.GameBoostViewModel());
                if (_mainContent == null)
                    _mainContent = new ContentControl { Margin = new Thickness(12) };
                _mainContent.Content = view;
                _mainContent.Visibility = Visibility.Visible;
                CWLog.Info("UI.Native", "GameBoostPage.xaml loaded via XamlReader and bound to GameBoostViewModel.");
            }
            catch (Exception ex)
            {
                CWLog.Error("UI.Native.LoadView", ex);
                // Keep the previous WebView2 experience as a safety net if a
                // loose XAML file is missing or has a markup error.
                ShowWebUiFallback("Native view could not be loaded: " + ex.Message);
            }
        }

        /// <summary>Show the native Smart Agent settings page.</summary>
        private void ShowNativeAiSettings()
        {
            try
            {
                var view = HostView("AiSettingsPage.xaml", new ViewModels.GameBoostViewModel());
                if (_mainContent != null) _mainContent.Content = view;
            }
            catch (Exception ex) { CWLog.Error("UI.Native.AiSettings", ex); }
        }

        /// <summary>Show the native Settings page.</summary>
        private void ShowNativeSettings()
        {
            try
            {
                var view = HostView("SettingsPage.xaml", null);
                if (_mainContent != null) _mainContent.Content = view;
            }
            catch (Exception ex) { CWLog.Error("UI.Native.Settings", ex); }
        }
    }
}
