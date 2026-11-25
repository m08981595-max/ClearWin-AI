using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ClearWin.ViewModels
{
    /// <summary>
    /// DataContext for the runtime-loaded Views/GameBoostPage.xaml.
    /// The XAML is parsed with XamlReader.Load (no BAML / InitializeComponent).
    /// </summary>
    public sealed class GameBoostViewModel : INotifyPropertyChanged
    {
        private string _status = "Ready — Hybrid SmartAgent standing by.";
        private string _lastReport = "No boost has been executed yet.";
        private bool _aggressiveMode;
        private bool _busy;

        public event PropertyChangedEventHandler PropertyChanged;

        private void Raise(string name)
        {
            if (PropertyChanged != null) PropertyChanged(this, new PropertyChangedEventArgs(name));
        }

        public string Status
        {
            get { return _status; }
            private set { _status = value; Raise("Status"); }
        }

        public string LastReport
        {
            get { return _lastReport; }
            private set { _lastReport = value; Raise("LastReport"); }
        }

        public bool AggressiveMode
        {
            get { return _aggressiveMode; }
            set { if (_aggressiveMode != value) { _aggressiveMode = value; Raise("AggressiveMode"); } }
        }

        public bool Busy
        {
            get { return _busy; }
            private set { _busy = value; Raise("Busy"); CommandManager.InvalidateRequerySuggested(); }
        }

        public ICommand BoostCommand { get; private set; }
        public ICommand CancelCommand { get; private set; }
        public ICommand RestoreAiCommand { get; private set; }

        public GameBoostViewModel()
        {
            BoostCommand = new AsyncRelayCommand(delegate { return RunBoostAsync(); }, delegate { return !Busy; });
            CancelCommand = new RelayCommand(delegate
            {
                SmartAgentService.Instance.EndGameBoost();
                Status = "Game Boost stopped — services restored.";
            });
            RestoreAiCommand = new AsyncRelayCommand(delegate { return RestoreAiAsync(); }, delegate { return !Busy; });
        }

        private async Task RunBoostAsync()
        {
            Busy = true;
            Status = "Running Hybrid Game Boost — 80% rules, 20% frozen neural inference…";
            var progress = new Progress<string>(delegate(string message) { Status = message; });
            try
            {
                var report = await SmartAgentService.Instance.RunGameBoostAsync(
                    AggressiveMode, progress, CancellationToken.None);
                LastReport = report == null ? "No report returned." : report.Summary();
                Status = report == null
                    ? "Boost finished with no report."
                    : string.Format("Boost complete — {0} rule kills, {1} neural kills.",
                                    report.RuleKillCount, report.NeuralKillCount);
            }
            catch (Exception ex)
            {
                Status = "Boost failed: " + ex.Message;
                LastReport = ex.ToString();
            }
            finally
            {
                Busy = false;
            }
        }

        private async Task RestoreAiAsync()
        {
            Busy = true;
            Status = "Restoring audited AI baseline…";
            try
            {
                var report = await Task.Run(() => SmartAgentService.Instance.RestoreDefaultAI());
                LastReport = report == null ? "No report returned." : report.Summary();
                Status = report != null && report.Success
                    ? "Default AI restored — learning frozen."
                    : "Restore failed — see report.";
            }
            catch (Exception ex)
            {
                Status = "Restore failed: " + ex.Message;
                LastReport = ex.ToString();
            }
            finally
            {
                Busy = false;
            }
        }
    }

    /// <summary>Synchronous ICommand used by the Stop button.</summary>
    internal sealed class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException("execute");
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) { return _canExecute == null || _canExecute(); }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object parameter) { _execute(); }
    }

    /// <summary>Async ICommand — never blocks the WPF UI thread.</summary>
    internal sealed class AsyncRelayCommand : ICommand
    {
        private readonly Func<Task> _execute;
        private readonly Func<bool> _canExecute;
        private bool _running;

        public AsyncRelayCommand(Func<Task> execute, Func<bool> canExecute = null)
        {
            if (execute == null) throw new ArgumentNullException("execute");
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return !_running && (_canExecute == null || _canExecute());
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public async void Execute(object parameter)
        {
            if (!CanExecute(parameter)) return;
            _running = true;
            CommandManager.InvalidateRequerySuggested();
            try { await _execute(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine("AsyncRelayCommand: " + ex.Message); }
            finally
            {
                _running = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}
