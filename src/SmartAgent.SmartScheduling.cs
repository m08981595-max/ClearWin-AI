using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ClearWin
{
    // === SMART SCHEDULING ===
    // Schedules optimizations when system is idle or low usage
    // Learns optimal times for different maintenance tasks
    public partial class SmartAgent
    {
        private Dictionary<string, SmartScheduledTask> _smartScheduledTasks = new Dictionary<string, SmartScheduledTask>();
        private Dictionary<string, List<TaskExecutionRecord>> _taskHistory = new Dictionary<string, List<TaskExecutionRecord>>();
        private DateTime _lastScheduleUpdate = DateTime.MinValue;
        private const int SCHEDULE_UPDATE_INTERVAL = 120;
        private const int MAX_TASK_HISTORY = 50;
        private const double IDLE_THRESHOLD = 0.25;

        public void InitializeSmartScheduling()
        {
            _smartScheduledTasks.Clear();
            _taskHistory.Clear();

            _smartScheduledTasks["MemoryCleanup"] = new SmartScheduledTask
            {
                Name = "MemoryCleanup",
                Priority = TaskPriority.Medium,
                MinIntervalMinutes = 10,
                MaxIntervalMinutes = 30,
                PreferredCpuBelow = 30,
                PreferredRamAbove = 60
            };

            _smartScheduledTasks["DiskCleanup"] = new SmartScheduledTask
            {
                Name = "DiskCleanup",
                Priority = TaskPriority.Low,
                MinIntervalMinutes = 360,
                MaxIntervalMinutes = 720,
                PreferredCpuBelow = 20,
                PreferredRamBelow = 50
            };

            _smartScheduledTasks["ProcessOptimization"] = new SmartScheduledTask
            {
                Name = "ProcessOptimization",
                Priority = TaskPriority.Medium,
                MinIntervalMinutes = 15,
                MaxIntervalMinutes = 60,
                PreferredCpuBelow = 40,
                PreferredRamAbove = 50
            };

            _smartScheduledTasks["ThermalManagement"] = new SmartScheduledTask
            {
                Name = "ThermalManagement",
                Priority = TaskPriority.High,
                MinIntervalMinutes = 5,
                MaxIntervalMinutes = 15,
                PreferredCpuBelow = 50,
                PreferredTempAbove = 75
            };

            _smartScheduledTasks["GameOptimization"] = new SmartScheduledTask
            {
                Name = "GameOptimization",
                Priority = TaskPriority.High,
                MinIntervalMinutes = 2,
                MaxIntervalMinutes = 5,
                PreferredCpuBelow = 60,
                PreferredRamAbove = 40
            };
        }

        public SmartScheduleResult UpdateSmartScheduling()
        {
            SmartScheduleResult result = new SmartScheduleResult();

            try
            {
                if ((DateTime.Now - _lastScheduleUpdate).TotalSeconds < SCHEDULE_UPDATE_INTERVAL)
                    return result;
                _lastScheduleUpdate = DateTime.Now;

                double cpuUsage = GetCurrentCpuUsage();
                double ramUsage = GetCurrentRamUsage();
                double temp = GetCurrentCpuTemp();
                bool isIdle = cpuUsage < IDLE_THRESHOLD;

                List<string> readyTasks = GetReadyTasks();
                List<string> rescheduledTasks = new List<string>();

                for (int i = 0; i < readyTasks.Count; i++)
                {
                    string taskName = readyTasks[i];
                    SmartScheduledTask task = _smartScheduledTasks[taskName];

                    bool canExecute = CanExecuteTask(task, cpuUsage, ramUsage, temp, isIdle);
                    if (canExecute)
                    {
                        result.ReadyTasks.Add(task);
                        Debug.WriteLine("[SmartScheduling] Task ready: " + taskName);
                    }
                    else
                    {
                        rescheduledTasks.Add(taskName);
                    }
                }

                // Check if idle time can be used for low-priority tasks
                if (isIdle && rescheduledTasks.Count > 0)
                {
                    for (int i = 0; i < rescheduledTasks.Count; i++)
                    {
                        string taskName = rescheduledTasks[i];
                        SmartScheduledTask task = _smartScheduledTasks[taskName];
                        if (task.Priority == TaskPriority.Low)
                        {
                            result.ReadyTasks.Add(task);
                            Debug.WriteLine("[SmartScheduling] Idle time task: " + taskName);
                        }
                    }
                }

                // Update task priorities based on history
                UpdateTaskPriorities();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SmartScheduling] Error: " + ex.Message);
            }

            return result;
        }

        private bool CanExecuteTask(SmartScheduledTask task, double cpuUsage, double ramUsage, double temp, bool isIdle)
        {
            DateTime lastExecution = GetLastExecutionTime(task.Name);
            double minutesSinceLastExecution = (DateTime.Now - lastExecution).TotalMinutes;

            if (minutesSinceLastExecution < task.MinIntervalMinutes)
                return false;

            if (cpuUsage > task.PreferredCpuBelow + 20)
                return false;

            if (task.PreferredRamAbove > 0 && ramUsage < task.PreferredRamAbove - 10)
                return false;

            if (task.PreferredRamBelow > 0 && ramUsage > task.PreferredRamBelow + 10)
                return false;

            if (task.PreferredTempAbove > 0 && temp < task.PreferredTempAbove - 5)
                return false;

            return true;
        }

        private List<string> GetReadyTasks()
        {
            List<string> ready = new List<string>();

            foreach (var task in _smartScheduledTasks.Values)
            {
                DateTime lastExec = GetLastExecutionTime(task.Name);
                double minutesSince = (DateTime.Now - lastExec).TotalMinutes;

                if (minutesSince >= task.MinIntervalMinutes)
                    ready.Add(task.Name);
            }

            ready.Sort((a, b) => _smartScheduledTasks[b].Priority.CompareTo(_smartScheduledTasks[a].Priority));

            return ready;
        }

        private DateTime GetLastExecutionTime(string taskName)
        {
            if (!_taskHistory.ContainsKey(taskName) || _taskHistory[taskName].Count == 0)
                return DateTime.MinValue.AddHours(1);

            List<TaskExecutionRecord> history = _taskHistory[taskName];
            return history[history.Count - 1].ExecutedAt;
        }

        public void RecordTaskExecution(string taskName, bool success, double durationMs)
        {
            if (!_taskHistory.ContainsKey(taskName))
                _taskHistory[taskName] = new List<TaskExecutionRecord>();

            TaskExecutionRecord record = new TaskExecutionRecord
            {
                TaskName = taskName,
                ExecutedAt = DateTime.Now,
                Success = success,
                DurationMs = durationMs
            };

            _taskHistory[taskName].Add(record);
            if (_taskHistory[taskName].Count > MAX_TASK_HISTORY)
                _taskHistory[taskName].RemoveAt(0);

            if (_smartScheduledTasks.ContainsKey(taskName))
            {
                _smartScheduledTasks[taskName].LastExecution = DateTime.Now;
                _smartScheduledTasks[taskName].ExecutionCount++;
                if (success)
                    _smartScheduledTasks[taskName].SuccessCount++;
            }
        }

        private void UpdateTaskPriorities()
        {
            foreach (var task in _smartScheduledTasks.Values)
            {
                if (task.ExecutionCount > 5)
                {
                    double successRate = (double)task.SuccessCount / task.ExecutionCount;
                    if (successRate > 0.9)
                    {
                        if (task.Priority < TaskPriority.High)
                            task.Priority++;
                    }
                    else if (successRate < 0.5)
                    {
                        if (task.Priority > TaskPriority.Low)
                            task.Priority--;
                    }
                }
            }
        }

        public string GetSmartScheduleSummary()
        {
            SmartScheduleResult result = UpdateSmartScheduling();
            string summary = "Smart Schedule:\n";

            foreach (var task in _smartScheduledTasks.Values)
            {
                DateTime lastExec = GetLastExecutionTime(task.Name);
                double minutesSince = (DateTime.Now - lastExec).TotalMinutes;
                summary += string.Format("  {0}: Priority={1}, Last={2:F0}min ago, Execs={3}, Success={4}\n",
                    task.Name, task.Priority, minutesSince, task.ExecutionCount, task.SuccessCount);
            }

            if (result.ReadyTasks.Count > 0)
            {
                summary += "Ready tasks: ";
                for (int i = 0; i < result.ReadyTasks.Count; i++)
                    summary += result.ReadyTasks[i].Name + " ";
            }

            return summary;
        }
    }

    public class SmartScheduledTask
    {
        public string Name;
        public TaskPriority Priority;
        public double MinIntervalMinutes;
        public double MaxIntervalMinutes;
        public double PreferredCpuBelow;
        public double PreferredCpuAbove;
        public double PreferredRamAbove;
        public double PreferredRamBelow;
        public double PreferredTempAbove;
        public DateTime LastExecution;
        public int ExecutionCount;
        public int SuccessCount;
    }

    public enum TaskPriority { Low = 0, Medium = 1, High = 2 }

    public class SmartScheduleResult
    {
        public List<SmartScheduledTask> ReadyTasks = new List<SmartScheduledTask>();
    }

    public class TaskExecutionRecord
    {
        public string TaskName;
        public DateTime ExecutedAt;
        public bool Success;
        public double DurationMs;
    }
}
