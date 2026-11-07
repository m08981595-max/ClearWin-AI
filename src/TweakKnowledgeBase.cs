using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ClearWin
{
    public class TweakKnowledgeBase
    {
        private List<TweakKnowledgeEntry> _entries = new List<TweakKnowledgeEntry>();
        private List<TweakConflict> _conflicts = new List<TweakConflict>();
        private string _dataDir;
        private string _knowledgeFile;
        private string _conflictsFile;
        private DateTime _lastLoad = DateTime.MinValue;
        private bool _loaded = false;

        // Stats
        public int TotalEntries { get { return _entries.Count; } }
        public int TotalConflicts { get { return _conflicts.Count; } }

        public TweakKnowledgeBase(string dataDir)
        {
            _dataDir = dataDir;
            _knowledgeFile = Path.Combine(dataDir, "tweak_knowledge.json");
            _conflictsFile = Path.Combine(dataDir, "tweak_conflicts.json");
        }

        // === Load knowledge base from JSON ===
        public void Load()
        {
            try
            {
                if (File.Exists(_knowledgeFile))
                {
                    string json = File.ReadAllText(_knowledgeFile);
                    _entries = ParseKnowledgeJson(json);
                    _lastLoad = DateTime.Now;
                    _loaded = true;
                }
                if (File.Exists(_conflictsFile))
                {
                    string json = File.ReadAllText(_conflictsFile);
                    _conflicts = ParseConflictsJson(json);
                }
            }
            catch { _loaded = false; }
        }

        // === Save knowledge base to JSON ===
        public void Save()
        {
            try
            {
                string json = GenerateKnowledgeJson(_entries);
                File.WriteAllText(_knowledgeFile, json);
                string conflictJson = GenerateConflictsJson(_conflicts);
                File.WriteAllText(_conflictsFile, conflictJson);
            } catch (Exception ex) { CWLog.Error("TweakKnowledgeBase", ex); }
        }

        // === Query: Get all entries ===
        public List<TweakKnowledgeEntry> GetAll()
        {
            if (!_loaded) Load();
            return new List<TweakKnowledgeEntry>(_entries);
        }

        // === Query: Get by category ===
        public List<TweakKnowledgeEntry> GetByCategory(string category)
        {
            if (!_loaded) Load();
            return _entries.Where(e => e.Category == category).ToList();
        }

        // === Query: Get by safety level ===
        public List<TweakKnowledgeEntry> GetBySafety(string safetyLevel)
        {
            if (!_loaded) Load();
            return _entries.Where(e => e.SafetyLevel == safetyLevel).ToList();
        }

        // === Query: Get safe tweaks only ===
        public List<TweakKnowledgeEntry> GetSafeTweaks()
        {
            if (!_loaded) Load();
            return _entries.Where(e => e.SafetyScore >= 0.7).ToList();
        }

        // === Query: Get for gaming ===
        public List<TweakKnowledgeEntry> GetForGaming()
        {
            if (!_loaded) Load();
            return _entries.Where(e => e.UseCases != null && e.UseCases.Contains("Gaming")).ToList();
        }

        // === Query: Get for battery saving ===
        public List<TweakKnowledgeEntry> GetForBattery()
        {
            if (!_loaded) Load();
            return _entries.Where(e => e.UseCases != null && e.UseCases.Contains("Battery")).ToList();
        }

        // === Query: Search by name or function ===
        public List<TweakKnowledgeEntry> Search(string query)
        {
            if (!_loaded) Load();
            string q = query.ToLower();
            return _entries.Where(e =>
                (e.Name != null && e.Name.ToLower().Contains(q)) ||
                (e.NameAr != null && e.NameAr.Contains(query)) ||
                (e.Function != null && e.Function.ToLower().Contains(q)) ||
                (e.FunctionAr != null && e.FunctionAr.Contains(query))
            ).ToList();
        }

        // === Query: Get by ID ===
        public TweakKnowledgeEntry GetById(string id)
        {
            if (!_loaded) Load();
            return _entries.FirstOrDefault(e => e.Id == id);
        }

        // === Check: Is this tweak safe? ===
        public bool IsTweakSafe(string tweakId)
        {
            var entry = GetById(tweakId);
            return entry != null && entry.SafetyScore >= 0.7;
        }

        // === Check: Does this tweak conflict with any applied tweak? ===
        public List<TweakConflict> CheckConflicts(string tweakId, List<string> appliedTweaks)
        {
            if (!_loaded) Load();
            return _conflicts.Where(c =>
                (c.TweakId1 == tweakId && appliedTweaks.Contains(c.TweakId2)) ||
                (c.TweakId2 == tweakId && appliedTweaks.Contains(c.TweakId1))
            ).ToList();
        }

        // === Check: Is this tweak Windows-compatible? ===
        public bool IsWindowsCompatible(string tweakId)
        {
            var entry = GetById(tweakId);
            return entry != null && (entry.Platform == "Windows" || entry.Platform == "Both");
        }

        // === Add new knowledge entry ===
        public void AddEntry(TweakKnowledgeEntry entry)
        {
            if (!_loaded) Load();
            entry.FirstSeen = DateTime.Now;
            entry.LastUpdated = DateTime.Now;
            // Check if already exists
            var existing = GetById(entry.Id);
            if (existing != null)
            {
                // Update existing
                entry.FirstSeen = existing.FirstSeen;
                entry.UsageCount = existing.UsageCount;
                entry.SuccessRate = existing.SuccessRate;
                _entries.Remove(existing);
            }
            _entries.Add(entry);
            Save();
        }

        // === Update success rate after applying a tweak ===
        public void RecordApplication(string tweakId, bool success)
        {
            var entry = GetById(tweakId);
            if (entry == null) return;
            entry.UsageCount++;
            if (entry.UsageCount == 1)
                entry.SuccessRate = success ? 1.0 : 0.0;
            else
                entry.SuccessRate = (entry.SuccessRate * (entry.UsageCount - 1) + (success ? 1.0 : 0.0)) / entry.UsageCount;
            entry.LastUpdated = DateTime.Now;
            Save();
        }

        // === Get categories with counts ===
        public List<TweakCategory> GetCategories()
        {
            if (!_loaded) Load();
            var cats = new Dictionary<string, TweakCategory>();
            foreach (var e in _entries)
            {
                if (!cats.ContainsKey(e.Category))
                {
                    cats[e.Category] = new TweakCategory
                    {
                        Name = e.Category,
                        NameAr = GetCategoryArabic(e.Category),
                        TweakCount = 0,
                        AverageSafety = 0
                    };
                }
                cats[e.Category].TweakCount++;
                cats[e.Category].AverageSafety += e.SafetyScore;
            }
            foreach (var cat in cats.Values)
            {
                if (cat.TweakCount > 0) cat.AverageSafety /= cat.TweakCount;
            }
            return cats.Values.ToList();
        }

        // === Get recommendations for current system state ===
        public List<TweakKnowledgeEntry> GetRecommendations(double cpuUsage, double ramUsage, double temp, bool isGame, bool isOnBattery)
        {
            if (!_loaded) Load();
            var results = new List<TweakKnowledgeEntry>();
            foreach (var e in _entries)
            {
                if (e.SafetyScore < 0.5) continue; // Skip dangerous tweaks
                bool relevant = false;
                if (isGame && e.UseCases != null && e.UseCases.Contains("Gaming")) relevant = true;
                if (isOnBattery && e.UseCases != null && e.UseCases.Contains("Battery")) relevant = true;
                if (temp > 80 && e.Category == "Thermal") relevant = true;
                if (ramUsage > 80 && e.Category == "Memory") relevant = true;
                if (cpuUsage > 80 && e.Category == "CPU") relevant = true;
                if (relevant) results.Add(e);
            }
            results.Sort((a, b) => b.SafetyScore.CompareTo(a.SafetyScore));
            return results;
        }

        // === Initialize with default knowledge from FrierenAI + VoidBattery ===
        public void InitializeDefaults()
        {
            if (_loaded && _entries.Count > 0) return;

            // === THERMAL ===
            AddEntry(new TweakKnowledgeEntry { Id = "thermal_governor", Name = "Temperature Governor", NameAr = "محدد الحرارة", Category = "Thermal", FileType = "Script", Function = "Caps CPU/GPU max frequency when temperature exceeds thresholds", FunctionAr = "يحد أقصى تردد للمعالج والكرت عند تجاوز درجة الحرارة", SafetyScore = 0.85, SafetyLevel = "Safe", PerformanceImpact = -0.2, ImpactArea = "Thermal", UseCases = new List<string> { "Gaming", "Normal", "Battery" }, Platform = "Both", WindowsEquivalent = "powercfg processor max state", AndroidEquivalent = "scaling_max_freq", SourceModule = "VoidBattery", ConflictsWith = new List<string> { "performanceGovernor" }, RiskDescription = "May reduce performance during thermal events", RevertMethod = "Restore original max frequency" });
            AddEntry(new TweakKnowledgeEntry { Id = "smart_thermal_predict", Name = "Smart Thermal Prediction", NameAr = "التنبؤ الحراري الذكي", Category = "Thermal", FileType = "Script", Function = "Detects temperature trend and throttles before hitting critical thresholds", FunctionAr = "يكتشف اتجاه الحرارة ويحد الأداء قبل الوصول للحد الحرج", SafetyScore = 0.80, SafetyLevel = "Safe", PerformanceImpact = -0.15, ImpactArea = "Thermal", UseCases = new List<string> { "Gaming", "Normal" }, Platform = "Both", SourceModule = "VoidBattery", RiskDescription = "Predictive throttling may reduce performance preemptively", RevertMethod = "Remove thermal prediction rules" });
            AddEntry(new TweakKnowledgeEntry { Id = "thermal_alert", Name = "Thermal Alert System", NameAr = "نظام تنبيه الحرارة", Category = "Thermal", FileType = "Script", Function = "Monitors temperature and generates alerts at thresholds", FunctionAr = "يراقب درجة الحرارة ويولّد تنبيهات عند الحدود", SafetyScore = 0.95, SafetyLevel = "Safe", PerformanceImpact = 0, ImpactArea = "Thermal", UseCases = new List<string> { "Gaming", "Normal", "Battery" }, Platform = "Both", SourceModule = "ClearWin", RiskDescription = "No risk - monitoring only", RevertMethod = "Disable alerts" });

            // === CPU ===
            AddEntry(new TweakKnowledgeEntry { Id = "performanceGovernor", Name = "Performance Governor", NameAr = "محدد الأداء", Category = "CPU", FileType = "Script", Function = "Sets CPU governor to performance mode for maximum speed", FunctionAr = "يضبط المعالج على وضع الأداء الأقصى", SafetyScore = 0.70, SafetyLevel = "Moderate", PerformanceImpact = 0.8, ImpactArea = "Gaming", UseCases = new List<string> { "Gaming" }, Platform = "Both", WindowsEquivalent = "powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c", AndroidEquivalent = "scaling_governor performance", SourceModule = "FrierenAI", ConflictsWith = new List<string> { "thermal_governor" }, RiskDescription = "Increases power consumption and heat generation", RevertMethod = "Restore original governor" });
            AddEntry(new TweakKnowledgeEntry { Id = "cpu_affinity", Name = "CPU Affinity Optimization", NameAr = "تحسين ارتباط المعالج", Category = "CPU", FileType = "Script", Function = "Assigns game/process to performance cores, background to efficiency cores", FunctionAr = "يخصص أنوية الأداء للألعاب والأنوية الكفؤة للعمليات الخلفية", SafetyScore = 0.80, SafetyLevel = "Safe", PerformanceImpact = 0.4, ImpactArea = "Gaming", UseCases = new List<string> { "Gaming", "Productivity" }, Platform = "Windows", SourceModule = "ClearWin", RiskDescription = "May cause process scheduling issues if misconfigured", RevertMethod = "Reset affinity to all cores" });
            AddEntry(new TweakKnowledgeEntry { Id = "screen_off_cpu_cap", Name = "Screen Off CPU Cap", NameAr = "حد المعالج عند إغلاق الشاشة", Category = "CPU", FileType = "Script", Function = "Caps CPU max frequency when screen is off (50-75%)", FunctionAr = "يحد أقصى تردد المعالج عند إغلاق الشاشة", SafetyScore = 0.85, SafetyLevel = "Safe", PerformanceImpact = 0.3, ImpactArea = "Battery", UseCases = new List<string> { "Battery" }, Platform = "Both", WindowsEquivalent = "powercfg processor idle throttle", SourceModule = "VoidBattery", RiskDescription = "Background tasks may run slower", RevertMethod = "Restore original max frequency" });

            // === MEMORY ===
            AddEntry(new TweakKnowledgeEntry { Id = "memory_clean", Name = "Memory Clean", NameAr = "تنظيف الذاكرة", Category = "Memory", FileType = "Command", Function = "Clears working set of background processes to free RAM", FunctionAr = "يُفرغ مجموعة العمل للعمليات الخلفية لتحرير الذاكرة", SafetyScore = 0.75, SafetyLevel = "Safe", PerformanceImpact = 0.3, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal", "Battery" }, Platform = "Windows", SourceModule = "ClearWin", RiskDescription = "Background apps may restart (uses more CPU temporarily)", RevertMethod = "Let apps restart naturally" });
            AddEntry(new TweakKnowledgeEntry { Id = "memory_pressure_drop", Name = "Memory Pressure Drop", NameAr = "تخفيف ضغط الذاكرة", Category = "Memory", FileType = "Script", Function = "Trims app caches, drops page cache, compacts memory", FunctionAr = "يقص ذاكرة التخزين المؤقت ويُفرغ ذاكرة الصفحة", SafetyScore = 0.65, SafetyLevel = "Moderate", PerformanceImpact = 0.4, ImpactArea = "General", UseCases = new List<string> { "Battery" }, Platform = "Both", SourceModule = "VoidBattery", RiskDescription = "Aggressive - may cause app reloads and temporary slowdown", RevertMethod = "Let system rebuild caches naturally" });
            AddEntry(new TweakKnowledgeEntry { Id = "purge_standby", Name = "Purge Standby List", NameAr = "تطهير قائمة الانتظار", Category = "Memory", FileType = "Command", Function = "Purges the standby memory list to free more RAM", FunctionAr = "يُطهر قائمة الذاكرة المحجوزة لتحرير مزيد من RAM", SafetyScore = 0.70, SafetyLevel = "Moderate", PerformanceImpact = 0.2, ImpactArea = "General", UseCases = new List<string> { "Gaming" }, Platform = "Windows", SourceModule = "ClearWin", RiskDescription = "Frequently accessed files may need to be reloaded from disk", RevertMethod = "Let system repopulate standby list" });

            // === NETWORK ===
            AddEntry(new TweakKnowledgeEntry { Id = "network_low_latency", Name = "Low Latency Network Profile", NameAr = "ملف الشبكة منخفض التأخير", Category = "Network", FileType = "Command", Function = "Optimizes TCP settings for minimum latency (gaming)", FunctionAr = "يحسّن إعدادات TCP لأقل تأخير (للألعاب)", SafetyScore = 0.80, SafetyLevel = "Safe", PerformanceImpact = 0.3, ImpactArea = "Gaming", UseCases = new List<string> { "Gaming" }, Platform = "Windows", SourceModule = "ClearWin", RiskDescription = "May increase power usage of network adapter", RevertMethod = "Restore balanced TCP settings" });
            AddEntry(new TweakKnowledgeEntry { Id = "dns_cache_boost", Name = "DNS Cache Boost", NameAr = "تعزيز ذاكرة DNS المؤقتة", Category = "Network", FileType = "Script", Function = "Increases DNS cache size and TTL to reduce DNS lookups", FunctionAr = "يزيّد حجم ذاكرة DNS المؤقتة لتقليل عمليات البحث", SafetyScore = 0.90, SafetyLevel = "Safe", PerformanceImpact = 0.1, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal" }, Platform = "Both", SourceModule = "VoidBattery", RiskDescription = "Stale DNS entries may persist longer", RevertMethod = "Flush DNS cache and restore defaults" });
            AddEntry(new TweakKnowledgeEntry { Id = "network_sleep_policy", Name = "Network Sleep Policy", NameAr = "سياسة إ mạng الشبكة", Category = "Network", FileType = "Script", Function = "Increases WiFi scan intervals and enables WiFi power-save", FunctionAr = "يزيد فترات فحص الواي فاي ويُفعّل وضع توفير الطاقة", SafetyScore = 0.80, SafetyLevel = "Safe", PerformanceImpact = 0.2, ImpactArea = "Battery", UseCases = new List<string> { "Battery" }, Platform = "Both", SourceModule = "VoidBattery", RiskDescription = "WiFi may reconnect slower after sleep", RevertMethod = "Restore default scan interval" });
            AddEntry(new TweakKnowledgeEntry { Id = "tcp_optimization", Name = "TCP Buffer Optimization", NameAr = "تحسين ذاuffman TCP", Category = "Network", FileType = "Command", Function = "Tunes TCP buffer sizes, congestion control, and window scaling", FunctionAr = "يُحسّن أحجام ذاكرة TCP والتحكم بالازدحام", SafetyScore = 0.82, SafetyLevel = "Safe", PerformanceImpact = 0.25, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal" }, Platform = "Windows", SourceModule = "FrierenAI", RiskDescription = "Non-standard TCP settings may cause issues with some firewalls", RevertMethod = "Restore default TCP settings" });

            // === BATTERY ===
            AddEntry(new TweakKnowledgeEntry { Id = "battery_saver", Name = "Battery Saver Mode", NameAr = "وضع توفير البطارية", Category = "Battery", FileType = "Command", Function = "Enables Windows battery saver with reduced brightness and background", FunctionAr = "يُفعّل وضع توفير البطارية مع تقليل السطوع والعمليات الخلفية", SafetyScore = 0.95, SafetyLevel = "Safe", PerformanceImpact = -0.3, ImpactArea = "Battery", UseCases = new List<string> { "Battery" }, Platform = "Windows", SourceModule = "ClearWin", RiskDescription = "Reduces performance and notification frequency", RevertMethod = "Disable battery saver" });
            AddEntry(new TweakKnowledgeEntry { Id = "doze_optimization", Name = "Doze/Standby Optimization", NameAr = "تحسين وضع السبات", Category = "Battery", FileType = "Script", Function = "Configures aggressive idle/standby modes to save battery", FunctionAr = "يُكوّن أوضاع السبات لتوفير البطارية", SafetyScore = 0.85, SafetyLevel = "Safe", PerformanceImpact = 0.1, ImpactArea = "Battery", UseCases = new List<string> { "Battery" }, Platform = "Both", SourceModule = "VoidBattery", RiskDescription = "Notifications may be delayed", RevertMethod = "Restore default standby settings" });
            AddEntry(new TweakKnowledgeEntry { Id = "app_hibernate", Name = "App Hibernate", NameAr = "إسبات التطبيقات", Category = "Battery", FileType = "Script", Function = "Force-stops background apps and sets them to rare standby bucket", FunctionAr = "يُوقف العمليات الخلفية ويضعها في سلة نادرة", SafetyScore = 0.75, SafetyLevel = "Safe", PerformanceImpact = 0.3, ImpactArea = "Battery", UseCases = new List<string> { "Battery" }, Platform = "Both", SourceModule = "VoidBattery", RiskDescription = "Apps may not receive notifications until opened", RevertMethod = "Open the app to restore it" });

            // === GPU ===
            AddEntry(new TweakKnowledgeEntry { Id = "gpu_performance", Name = "GPU Performance Mode", NameAr = "وضع أداء كرت الشاشة", Category = "GPU", FileType = "Script", Function = "Sets GPU to performance governor and max frequency", FunctionAr = "يضبط كرت الشاشة على وضع الأداء الأقصى", SafetyScore = 0.70, SafetyLevel = "Moderate", PerformanceImpact = 0.6, ImpactArea = "Gaming", UseCases = new List<string> { "Gaming" }, Platform = "Both", SourceModule = "FrierenAI", RiskDescription = "Increases GPU power consumption and heat", RevertMethod = "Restore original GPU governor" });
            AddEntry(new TweakKnowledgeEntry { Id = "render_optimization", Name = "Render Mode Optimization", NameAr = "تحسين وضع الرندر", Category = "GPU", FileType = "Script", Function = "Switches between Vulkan/SkiaGL/Hybrid rendering for best performance", FunctionAr = "يُبدّل بين أوضاع الرندر لأفضل أداء", SafetyScore = 0.75, SafetyLevel = "Moderate", PerformanceImpact = 0.4, ImpactArea = "Gaming", UseCases = new List<string> { "Gaming" }, Platform = "Android", SourceModule = "FrierenAI", RiskDescription = "Some apps may not work with certain render modes", RevertMethod = "Switch back to default render mode" });

            // === I/O ===
            AddEntry(new TweakKnowledgeEntry { Id = "io_scheduler", Name = "I/O Scheduler Optimization", NameAr = "تحسين جدولة الإدخال/الإخراج", Category = "I/O", FileType = "Script", Function = "Sets optimal I/O scheduler (deadline/fiops) and read-ahead", FunctionAr = "يضبط جدولة I/O المثلى", SafetyScore = 0.82, SafetyLevel = "Safe", PerformanceImpact = 0.2, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal" }, Platform = "Both", SourceModule = "FrierenAI", RiskDescription = "Non-optimal scheduler for specific workloads", RevertMethod = "Restore original I/O scheduler" });
            AddEntry(new TweakKnowledgeEntry { Id = "fstrim", Name = "Periodic FSTRIM", NameAr = "تنظيفPeriodي للتخزين", Category = "I/O", FileType = "Script", Function = "Periodically trims SSD/flash storage for sustained write performance", FunctionAr = "يُنظّف التخزين بشكل دوري للأداء المستدام", SafetyScore = 0.90, SafetyLevel = "Safe", PerformanceImpact = 0.15, ImpactArea = "General", UseCases = new List<string> { "Normal" }, Platform = "Both", SourceModule = "VoidBattery", RiskDescription = "Minor I/O overhead during trim operation", RevertMethod = "Disable periodic trim" });

            // === DISPLAY ===
            AddEntry(new TweakKnowledgeEntry { Id = "animation_reduce", Name = "Reduce Animations", NameAr = "تقليل الرسوم المتحركة", Category = "Display", FileType = "Command", Function = "Reduces or disables UI animations for snappier feel", FunctionAr = "يُقلل أو يُعطّل الرسوم المتحركة", SafetyScore = 0.95, SafetyLevel = "Safe", PerformanceImpact = 0.15, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal", "Battery" }, Platform = "Both", SourceModule = "VoidBattery", RiskDescription = "UI may feel less smooth without animations", RevertMethod = "Restore animation scales" });
            AddEntry(new TweakKnowledgeEntry { Id = "refresh_rate", Name = "Refresh Rate Control", NameAr = "تحكم معدل التحديث", Category = "Display", FileType = "Command", Function = "Sets display refresh rate to optimal value (60/120/144Hz)", FunctionAr = "يضبط معدل تحديث الشاشة للقيمة المثلى", SafetyScore = 0.90, SafetyLevel = "Safe", PerformanceImpact = 0.2, ImpactArea = "Gaming", UseCases = new List<string> { "Gaming" }, Platform = "Windows", SourceModule = "ClearWin", RiskDescription = "Higher refresh rate uses more battery", RevertMethod = "Restore default refresh rate" });

            // === SECURITY ===
            AddEntry(new TweakKnowledgeEntry { Id = "panic_disable", Name = "Disable Kernel Panic", NameAr = "تعطيل ذعر النواة", Category = "Security", FileType = "Script", Function = "Disables kernel panic on oops/warn to prevent boot loops", FunctionAr = "يُعطّل ذعر النواة لمنع حلقات البوت", SafetyScore = 0.60, SafetyLevel = "Moderate", PerformanceImpact = 0, ImpactArea = "General", UseCases = new List<string> { "Normal" }, Platform = "Both", SourceModule = "FrierenAI", RiskDescription = "System may hang instead of rebooting on critical errors", RevertMethod = "Re-enable kernel panic settings" });

            // === GENERAL ===
            AddEntry(new TweakKnowledgeEntry { Id = "power_plan_switch", Name = "Auto Power Plan Switch", NameAr = "تبديل خطة الطاقة التلقائي", Category = "CPU", FileType = "Command", Function = "Switches between power plans based on context (game/idle/battery)", FunctionAr = "يُبدّل بين خطط الطاقة حسب السياق", SafetyScore = 0.90, SafetyLevel = "Safe", PerformanceImpact = 0.3, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal", "Battery" }, Platform = "Windows", SourceModule = "ClearWin", RiskDescription = "May switch plan at unexpected times", RevertMethod = "Set fixed power plan" });

            // === ATLASX ZRAM: Adaptive Memory Compression ===
            AddEntry(new TweakKnowledgeEntry { Id = "zram_adaptive_sizing", Name = "Adaptive ZRAM Sizing", NameAr = "تحديد حجم ZRAM التكيفي", Category = "Memory", FileType = "Script", Function = "Sizes ZRAM dynamically based on physical RAM (50% for 4GB, 25% for 8GB, 15% for 16GB)", FunctionAr = "يحدد حجم ZRAM ديناميكياً حسب RAM الفعلية (50% لـ 4GB، 25% لـ 8GB، 15% لـ 16GB)", SafetyScore = 0.85, SafetyLevel = "Safe", PerformanceImpact = 0.4, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal", "Battery" }, Platform = "Android", SourceModule = "ATLASX_ZRAM", RiskDescription = "Over-provisioning ZRAM can reduce available RAM for apps", RevertMethod = "Reset ZRAM size to default" });
            AddEntry(new TweakKnowledgeEntry { Id = "zram_lz4_compression", Name = "LZ4 Fast Compression", NameAr = "ضغط LZ4 السريع", Category = "Memory", FileType = "Script", Function = "Uses lz4 algorithm for ZRAM - fastest compression/decompression ratio for real-time use", FunctionAr = "يستخدم خوارزمية lz4 لـ ZRAM - أسرع نسبة ضغط/فك ضغط للاستخدام الفوري", SafetyScore = 0.90, SafetyLevel = "Safe", PerformanceImpact = 0.3, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal" }, Platform = "Android", SourceModule = "ATLASX_ZRAM", RiskDescription = "Lower compression ratio than lzzo/zstd means more ZRAM space used", RevertMethod = "Switch to default compression algorithm" });
            AddEntry(new TweakKnowledgeEntry { Id = "vm_swappiness_tune", Name = "VM Swappiness Tuning", NameAr = "ضبط ميل الذاكرة الافتراضية", Category = "Memory", FileType = "Registry", Function = "Adjusts vm.swappiness to control how aggressively kernel swaps pages (Android: 60-100, Windows: concept maps to pagefile usage)", FunctionAr = "يضبط vm.swappiness للتحكم في مدى عدوانية النواة في تبديل الصفحات", SafetyScore = 0.80, SafetyLevel = "Safe", PerformanceImpact = 0.25, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal", "Battery" }, Platform = "Both", WindowsEquivalent = "HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Memory Management\\SwapfileUsage", SourceModule = "ATLASX_ZRAM", RiskDescription = "Too low swappiness causes premature OOM kills; too high causes excessive swapping", RevertMethod = "Restore default swappiness value" });
            AddEntry(new TweakKnowledgeEntry { Id = "vm_dirty_ratio", Name = "VM Dirty Ratio Control", NameAr = "تحكم نسبة البيانات المؤقتة", Category = "Memory", FileType = "Script", Function = "Controls vm.dirty_ratio and vm.dirty_background_ratio to balance write-back frequency vs memory pressure", FunctionAr = "يتحكم في vm.dirty_ratio و vm.dirty_background_ratio لتوازن تكرار الكتابة مقابل ضغط الذاكرة", SafetyScore = 0.75, SafetyLevel = "Moderate", PerformanceImpact = 0.2, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal" }, Platform = "Android", SourceModule = "ATLASX_ZRAM", RiskDescription = "Aggressive dirty ratio can cause I/O storms during heavy writes", RevertMethod = "Restore default dirty ratio values" });
            AddEntry(new TweakKnowledgeEntry { Id = "lmk_minfree_tuning", Name = "LMK Minfree Thresholds", NameAr = "عتبات LMK الدنيا", Category = "Memory", FileType = "Script", Function = "Tunes Low Memory Killer minfree thresholds to kill apps at optimal memory pressure points", FunctionAr = "يضبط عتبات القاتل منخفض الذاكرة لقتل التطبيقات عند نقاط ضغط الذاكرة المثلى", SafetyScore = 0.70, SafetyLevel = "Moderate", PerformanceImpact = 0.3, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal" }, Platform = "Android", SourceModule = "ATLASX_ZRAM", RiskDescription = "Too aggressive LMK kills foreground apps; too conservative causes OOM", RevertMethod = "Restore default LMK thresholds" });
            AddEntry(new TweakKnowledgeEntry { Id = "vm_overcommit_tune", Name = "VM Overcommit Settings", NameAr = "إعدادات التزام الذاكرة الزائد", Category = "Memory", FileType = "Script", Function = "Configures vm.overcommit_memory and vm.overcommit_ratio for optimal memory allocation strategy", FunctionAr = "يُكوّن vm.overcommit_memory و vm.overcommit_ratio لاستراتيجية تخصيص الذاكرة المثلى", SafetyScore = 0.65, SafetyLevel = "Moderate", PerformanceImpact = 0.15, ImpactArea = "General", UseCases = new List<string> { "Normal" }, Platform = "Android", SourceModule = "ATLASX_ZRAM", RiskDescription = "Incorrect overcommit can cause allocation failures or OOM kills", RevertMethod = "Restore default overcommit settings" });
            AddEntry(new TweakKnowledgeEntry { Id = "health_monitor_drift", Name = "Settings Drift Detection", NameAr = "كشف انحراف الإعدادات", Category = "Memory", FileType = "Script", Function = "Periodically verifies optimization settings haven't been overridden by system/other modules, auto-remediates if drift detected", FunctionAr = "يتحقق دورياً من أن إعدادات التحسين لم يتم تجاوزها، ويُعيد تطبيقها إذا اكتشف انحرافاً", SafetyScore = 0.95, SafetyLevel = "Safe", PerformanceImpact = 0.05, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal", "Battery" }, Platform = "Both", SourceModule = "ATLASX_ZRAM", RiskDescription = "Minimal - just monitoring and re-applying safe settings", RevertMethod = "Disable health monitoring" });
            AddEntry(new TweakKnowledgeEntry { Id = "profile_based_adaptation", Name = "Hardware Profile Adaptation", NameAr = "تكييف ملفات الأجهزة", Category = "Memory", FileType = "Script", Function = "Detects RAM size and selects optimal preset profile (Conservative/Balanced/Aggressive) for memory management", FunctionAr = "يكتشف حجم RAM ويختار الملف المثالي مسبقاً (تحفظي/متوازن/عدواني) لإدارة الذاكرة", SafetyScore = 0.85, SafetyLevel = "Safe", PerformanceImpact = 0.3, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal", "Battery" }, Platform = "Both", SourceModule = "ATLASX_ZRAM", RiskDescription = "Wrong profile for workload type may sub-optimize", RevertMethod = "Override with manual profile selection" });
            AddEntry(new TweakKnowledgeEntry { Id = "memory_compression_win", Name = "Windows Memory Compression", NameAr = "ضغط الذاكرة في ويندوز", Category = "Memory", FileType = "Command", Function = "Manages Windows built-in memory compression service - trims working sets, adjusts compression aggressiveness", FunctionAr = "يُدير خدمة ضغط الذاكرة المدمجة في ويندوز - يقص مجموعات العمل ويضبط عدوانية الضغط", SafetyScore = 0.80, SafetyLevel = "Safe", PerformanceImpact = 0.25, ImpactArea = "General", UseCases = new List<string> { "Gaming", "Normal", "Battery" }, Platform = "Windows", SourceModule = "ATLASX_ZRAM", RiskDescription = "Disabling compression can increase RAM usage but reduce CPU overhead", RevertMethod = "Re-enable memory compression service" });

            // === Default conflicts ===
            _conflicts.Add(new TweakConflict { TweakId1 = "performanceGovernor", TweakId2 = "thermal_governor", Reason = "Performance governor pushes clocks high while thermal governor tries to cap them", Severity = "Medium" });
            _conflicts.Add(new TweakConflict { TweakId1 = "memory_clean", TweakId2 = "memory_pressure_drop", Reason = "Both target memory - running together may cause excessive app restarts", Severity = "Low" });
            _conflicts.Add(new TweakConflict { TweakId1 = "gpu_performance", TweakId2 = "thermal_governor", Reason = "GPU performance mode generates heat that triggers thermal throttling", Severity = "Medium" });
            // ATLASX ZRAM conflicts
            _conflicts.Add(new TweakConflict { TweakId1 = "zram_adaptive_sizing", TweakId2 = "memory_pressure_drop", Reason = "ZRAM sizing and aggressive memory trim can compete - ZRAM needs free pages to compress into", Severity = "Medium" });
            _conflicts.Add(new TweakConflict { TweakId1 = "lmk_minfree_tuning", TweakId2 = "memory_clean", Reason = "Both kill/trim processes - running together may cause excessive app restarts", Severity = "Low" });
            _conflicts.Add(new TweakConflict { TweakId1 = "vm_swappiness_tune", TweakId2 = "purge_standby", Reason = "Low swappiness + standby purge = aggressive memory reclamation that may hurt multitasking", Severity = "Low" });

            Save();
        }

        // === Helper: Get Arabic category name ===
        private string GetCategoryArabic(string category)
        {
            switch (category)
            {
                case "Thermal": return "الحرارة";
                case "CPU": return "المعالج";
                case "Memory": return "الذاكرة";
                case "Network": return "الشبكة";
                case "Battery": return "البطارية";
                case "GPU": return "كرت الشاشة";
                case "I/O": return "الإدخال/الإخراج";
                case "Display": return "العرض";
                case "Security": return "الأمان";
                default: return category;
            }
        }

        // === JSON Serialization (C# 5 compatible) ===
        private string GenerateKnowledgeJson(List<TweakKnowledgeEntry> entries)
        {
            var lines = new List<string>();
            lines.Add("[");
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                lines.Add("  {");
                lines.Add("    \"Id\": \"" + EscapeJson(e.Id) + "\",");
                lines.Add("    \"Name\": \"" + EscapeJson(e.Name) + "\",");
                lines.Add("    \"NameAr\": \"" + EscapeJson(e.NameAr) + "\",");
                lines.Add("    \"Category\": \"" + EscapeJson(e.Category) + "\",");
                lines.Add("    \"FileType\": \"" + EscapeJson(e.FileType) + "\",");
                lines.Add("    \"Function\": \"" + EscapeJson(e.Function) + "\",");
                lines.Add("    \"FunctionAr\": \"" + EscapeJson(e.FunctionAr) + "\",");
                lines.Add("    \"SafetyScore\": " + e.SafetyScore.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ",");
                lines.Add("    \"SafetyLevel\": \"" + EscapeJson(e.SafetyLevel) + "\",");
                lines.Add("    \"PerformanceImpact\": " + e.PerformanceImpact.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + ",");
                lines.Add("    \"ImpactArea\": \"" + EscapeJson(e.ImpactArea) + "\",");
                lines.Add("    \"UseCases\": [" + ToJsonArray(e.UseCases) + "],");
                lines.Add("    \"Platform\": \"" + EscapeJson(e.Platform) + "\",");
                lines.Add("    \"WindowsEquivalent\": \"" + EscapeJson(e.WindowsEquivalent) + "\",");
                lines.Add("    \"AndroidEquivalent\": \"" + EscapeJson(e.AndroidEquivalent) + "\",");
                lines.Add("    \"SourceModule\": \"" + EscapeJson(e.SourceModule) + "\",");
                lines.Add("    \"ConflictsWith\": [" + ToJsonArray(e.ConflictsWith) + "],");
                lines.Add("    \"RiskDescription\": \"" + EscapeJson(e.RiskDescription) + "\",");
                lines.Add("    \"RevertMethod\": \"" + EscapeJson(e.RevertMethod) + "\",");
                lines.Add("    \"UsageCount\": " + e.UsageCount + ",");
                lines.Add("    \"SuccessRate\": " + e.SuccessRate.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));
                if (i < entries.Count - 1) lines[lines.Count - 1] += ",";
                lines.Add("  }");
            }
            lines.Add("]");
            return string.Join("\n", lines.ToArray());
        }

        private string GenerateConflictsJson(List<TweakConflict> conflicts)
        {
            var lines = new List<string>();
            lines.Add("[");
            for (int i = 0; i < conflicts.Count; i++)
            {
                var c = conflicts[i];
                lines.Add("  {");
                lines.Add("    \"TweakId1\": \"" + EscapeJson(c.TweakId1) + "\",");
                lines.Add("    \"TweakId2\": \"" + EscapeJson(c.TweakId2) + "\",");
                lines.Add("    \"Reason\": \"" + EscapeJson(c.Reason) + "\",");
                lines.Add("    \"Severity\": \"" + EscapeJson(c.Severity) + "\"");
                lines.Add("  }" + (i < conflicts.Count - 1 ? "," : ""));
            }
            lines.Add("]");
            return string.Join("\n", lines.ToArray());
        }

        private string EscapeJson(string s)
        {
            if (s == null) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");
        }

        private string ToJsonArray(List<string> list)
        {
            if (list == null || list.Count == 0) return "";
            var items = new string[list.Count];
            for (int i = 0; i < list.Count; i++)
                items[i] = "\"" + EscapeJson(list[i]) + "\"";
            return string.Join(", ", items);
        }

        // === JSON Deserialization (simple parser, C# 5 compatible) ===
        private List<TweakKnowledgeEntry> ParseKnowledgeJson(string json)
        {
            var result = new List<TweakKnowledgeEntry>();
            // Simple line-by-line parser for our known format
            var entries = json.Split(new[] { "\"Id\":" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < entries.Length; i++)
            {
                var entry = new TweakKnowledgeEntry();
                entry.Id = ExtractString(entries[i], "Id");
                entry.Name = ExtractString(entries[i], "Name");
                entry.NameAr = ExtractString(entries[i], "NameAr");
                entry.Category = ExtractString(entries[i], "Category");
                entry.FileType = ExtractString(entries[i], "FileType");
                entry.Function = ExtractString(entries[i], "Function");
                entry.FunctionAr = ExtractString(entries[i], "FunctionAr");
                entry.SafetyScore = ExtractDouble(entries[i], "SafetyScore");
                entry.SafetyLevel = ExtractString(entries[i], "SafetyLevel");
                entry.PerformanceImpact = ExtractDouble(entries[i], "PerformanceImpact");
                entry.ImpactArea = ExtractString(entries[i], "ImpactArea");
                entry.Platform = ExtractString(entries[i], "Platform");
                entry.WindowsEquivalent = ExtractString(entries[i], "WindowsEquivalent");
                entry.AndroidEquivalent = ExtractString(entries[i], "AndroidEquivalent");
                entry.SourceModule = ExtractString(entries[i], "SourceModule");
                entry.RiskDescription = ExtractString(entries[i], "RiskDescription");
                entry.RevertMethod = ExtractString(entries[i], "RevertMethod");
                entry.UsageCount = (int)ExtractDouble(entries[i], "UsageCount");
                entry.SuccessRate = ExtractDouble(entries[i], "SuccessRate");
                result.Add(entry);
            }
            return result;
        }

        private List<TweakConflict> ParseConflictsJson(string json)
        {
            var result = new List<TweakConflict>();
            var entries = json.Split(new[] { "\"TweakId1\":" }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < entries.Length; i++)
            {
                var c = new TweakConflict();
                c.TweakId1 = ExtractString(entries[i], "TweakId1");
                c.TweakId2 = ExtractString(entries[i], "TweakId2");
                c.Reason = ExtractString(entries[i], "Reason");
                c.Severity = ExtractString(entries[i], "Severity");
                result.Add(c);
            }
            return result;
        }

        private string ExtractString(string json, string key)
        {
            string search = "\"" + key + "\": \"";
            int start = json.IndexOf(search);
            if (start < 0) return "";
            start += search.Length;
            int end = json.IndexOf("\"", start);
            if (end < 0) return "";
            return json.Substring(start, end - start).Replace("\\\"", "\"").Replace("\\n", "\n");
        }

        private double ExtractDouble(string json, string key)
        {
            string search = "\"" + key + "\": ";
            int start = json.IndexOf(search);
            if (start < 0) return 0;
            start += search.Length;
            int end = start;
            while (end < json.Length && (char.IsDigit(json[end]) || json[end] == '.' || json[end] == '-'))
                end++;
            double val;
            if (double.TryParse(json.Substring(start, end - start), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out val))
                return val;
            return 0;
        }
    }
}
