using System;
using System.Collections.Generic;

namespace ClearWin
{
    /// <summary>
    /// Represents the safety tier of an action under the ClearWin Autonomous Policy.
    /// Green: Low risk, reviewed, bounded, verifiable, reversible. Allowed for autonomous execution IF session autonomy is explicitly enabled.
    /// Yellow: Medium risk, system-altering. NEVER autonomous. Requires explicit per-action user confirmation.
    /// Red: High risk, irreversible, security-sensitive, or system-critical. NEVER autonomous under any circumstances.
    /// </summary>
    public enum ActionTier
    {
        Green,
        Yellow,
        Red
    }

    /// <summary>
    /// Metadata descriptor for an action in the ClearWin Action Catalog.
    /// </summary>
    public class ActionCatalogItem
    {
        public string ActionId { get; set; }
        public string DisplayNameAr { get; set; }
        public string DisplayNameEn { get; set; }
        public ActionTier Tier { get; set; }
        public bool RequiresAdmin { get; set; }
        public bool IsReversible { get; set; }
        public string Category { get; set; }
        public string DescriptionAr { get; set; }
        public string PreconditionDescription { get; set; }
        public string PostVerificationDescription { get; set; }

        public ActionCatalogItem(
            string actionId,
            string displayNameAr,
            string displayNameEn,
            ActionTier tier,
            bool requiresAdmin,
            bool isReversible,
            string category,
            string descriptionAr,
            string preconditionDescription,
            string postVerificationDescription)
        {
            ActionId = actionId;
            DisplayNameAr = displayNameAr;
            DisplayNameEn = displayNameEn;
            Tier = tier;
            RequiresAdmin = requiresAdmin;
            IsReversible = isReversible;
            Category = category;
            DescriptionAr = descriptionAr;
            PreconditionDescription = preconditionDescription;
            PostVerificationDescription = postVerificationDescription;
        }
    }

    /// <summary>
    /// Central, immutable catalog of all authorized ClearWin actions, their risk tiers,
    /// preconditions, admin requirements, and rollback capabilities.
    /// </summary>
    public static class ActionCatalog
    {
        private static readonly Dictionary<string, ActionCatalogItem> _catalog =
            new Dictionary<string, ActionCatalogItem>(StringComparer.OrdinalIgnoreCase);

        static ActionCatalog()
        {
            // =========================================================================
            // GREEN ACTIONS: Low Risk, Bounded, Verifiable, Reversible.
            // Eligible for autonomous execution ONLY IF session Green Autonomy is enabled.
            // =========================================================================

            Register(new ActionCatalogItem(
                "diagnostics.collect_metrics",
                "جمع مؤشرات النظام والحرارة",
                "Collect System Telemetry",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Diagnostics",
                descriptionAr: "قراءة مؤشرات المعالج، الذاكرة، القرص، والحرارة عبر WMI و Performance Counters بدون تعديل النظام.",
                preconditionDescription: "النظام قيد التشغيل ومتحكمات الأداء متاحة.",
                postVerificationDescription: "تم الحصول على قيم عددية صالحة للـ CPU والـ RAM."));

            Register(new ActionCatalogItem(
                "optimization.ram_clean",
                "تحرير ذاكرة العمل (EmptyWorkingSet)",
                "Trim Process Working Sets",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Memory",
                descriptionAr: "تقليص مساحات عمل العمليات غير النشطة وإعادة الصفحات غير المستخدمة لذاكرة النظام بدون إغلاق أي برنامج.",
                preconditionDescription: "الذاكرة المستخدمة أكبر من 40%.",
                postVerificationDescription: "انخفاض حجم الذاكرة المستخدمة أو زيادة الذاكرة المتاحة."));

            Register(new ActionCatalogItem(
                "optimization.standby_purge",
                "تفريغ الذاكرة الاحتياطية (PurgeStandbyList)",
                "Purge Standby Cache",
                ActionTier.Green,
                requiresAdmin: true,
                isReversible: true,
                category: "Memory",
                descriptionAr: "تفريغ ذاكرة التخزين المؤقت الاحتياطية بنظام ويندوز لحل مشكلات التقطيع في الألعاب الثقيلة.",
                preconditionDescription: "وجود صفحات احتياطية في الذاكرة ومساعد إداري متصل.",
                postVerificationDescription: "قراءة حجم الذاكرة الاحتياطية والتأكد من انخفاضه."));

            Register(new ActionCatalogItem(
                "network.dns_flush",
                "تفريغ مخزن أسماء النطاقات (DNS Flush)",
                "Flush DNS Resolver Cache",
                ActionTier.Green,
                requiresAdmin: true,
                isReversible: true,
                category: "Network",
                descriptionAr: "مسح الكاش المؤقت لـ DNS لحل مشاكل الاتصال وتحديث مسارات الشبكة.",
                preconditionDescription: "خدمة DNS Client قيد التشغيل ومساعد إداري متصل.",
                postVerificationDescription: "نجاح استدعاء DnsFlushResolverCache أو ipconfig /flushdns."));

            Register(new ActionCatalogItem(
                "optimization.desktop_smoothness",
                "تعزيز سلاسة الاستخدام المكتبي (Desktop Smoothness)",
                "Optimize Desktop Smoothness & Latency",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "ProcessPriority",
                descriptionAr: "رفع أولوية التطبيق النشط أمام المستخدم، وضبط مؤقت النواة على 1.0ms لسلاسة النوافذ والماوس، وخفض أولوية المهام الخلفية غير التفاعلية.",
                preconditionDescription: "وضع الاستخدام المكتبي نشط والنظام لا يشغل لعبة حالياً.",
                postVerificationDescription: "تم تطبيق مؤقت 1.0ms ورفع أولوية النافذة النشطة."));

            Register(new ActionCatalogItem(
                "optimization.trim_inactive_workingsets",
                "تقليص ذاكرة البرامج الخلفية الخاملة",
                "Trim Inactive Apps Working Sets",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Memory",
                descriptionAr: "تقليص استهلاك الذاكرة للتطبيقات المصغرة والخاملة بالخلفية بدون إغلاقها، مع الحفاظ الكامل على سرعة البرنامج النشط.",
                preconditionDescription: "الذاكرة المستخدمة أعلى من 50% ولا توجد لعبة نشطة.",
                postVerificationDescription: "تم تحرير ذاكرة التطبيقات الخاملة بنجاح."));

            Register(new ActionCatalogItem(
                "optimization.gpu_boost",
                "تعزيز أداء كارت الشاشة والرسوميات (GPU Boost & Scheduling)",
                "Optimize GPU Performance & Hardware Scheduling",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Graphics",
                descriptionAr: "رفع أولوية معالجة الرسوميات D3D/GPU للعبة والتطبيق النشط، وتوجيه المعالجة لكارت الشاشة المنفصل عالي الأداء مع جدولة HAGS.",
                preconditionDescription: "تطبيق ألعاب أو تطبيق رسومي نشط في المقدمة.",
                postVerificationDescription: "تم تعيين أولوية الرسوميات D3D GPU Priority وتوجيه المعالجة لكارت الشاشة عالي الأداء."));

            Register(new ActionCatalogItem(
                "optimization.gpu_vram_trim",
                "تحرير ذاكرة كارت الشاشة الخلفية (GPU VRAM Shedding)",
                "Shed Background GPU Video Memory",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Graphics",
                descriptionAr: "تحرير أسطح DirectX وحصص ذاكرة الفيديو (VRAM) المحجوزة للبرامج الخلفية والمتصفحات لمنح كامل مساحة كارت الشاشة للعبة النشطة.",
                preconditionDescription: "لعبة نشطة تعاني من ضغط ذاكرة الفيديو أو وجود برامج خلفية تستهلك أسطح رسومية.",
                postVerificationDescription: "تم تحرير مساحات عمل البرامج الرسومية الخلفية وأسطح العرض بنجاح."));

            // SmartAgent runtime action aliases (Green Tier - bounded, reversible runtime optimizations)
            string[] greenAgentAliases = new string[] {
                "LightClean", "AggressiveClean", "PreemptiveClean", "PurgeStandbyList",
                "BoostGamePriority", "UnparkAllCores", "LimitBackground", "SetGameAffinity",
                "SetLowIoPriority", "LimitBackgroundAffinity", "StabilizeFrames", "ReduceMicroStutter",
                "BatterySaver", "BalancedPerformance", "EmergencyThrottle", "ReduceThermal",
                "OptimizeDesktopSmoothness", "TrimInactiveWorkingSets", "OptimizeGpuPerformance",
                "ShedBackgroundGpuMemory", "PurgeGpuShaderCache", "CleanTempFiles", "SwitchPowerPlan",
                "SetHighPerformance", "SuggestClose", "ResumeSuspendedProcesses", "AutoRecovery",
                "IdleMemoryClean", "ContinuousMicroOptimization"
            };
            foreach (string alias in greenAgentAliases)
            {
                bool needsAdmin = (alias == "PurgeStandbyList");
                Register(new ActionCatalogItem(
                    alias,
                    "تحسين ذكي تلقائي (" + alias + ")",
                    "SmartAgent Runtime Action (" + alias + ")",
                    ActionTier.Green,
                    requiresAdmin: needsAdmin,
                    isReversible: true,
                    category: "SmartAgent",
                    descriptionAr: "إجراء تحسين ديناميكي آمن وقابل للتراجع مدار بواسطة محرك SmartAgent.",
                    preconditionDescription: "فحص الأمان والميزانية ومؤشر المخاطر أقل من الحد الحرج.",
                    postVerificationDescription: "التحقق من استقرار المؤشرات وعدم حدوث تدهور في الأداء."));
            }

            // SmartAgent Yellow Tier actions (require explicit user confirmation)
            string[] yellowAgentAliases = new string[] {
                "SuspendBloatProcesses", "DisableGameDvr", "StartupDisable"
            };
            foreach (string yAlias in yellowAgentAliases)
            {
                Register(new ActionCatalogItem(
                    yAlias,
                    "إجراء نظام متوسط الخطورة (" + yAlias + ")",
                    "SmartAgent Semi-Persistent Action (" + yAlias + ")",
                    ActionTier.Yellow,
                    requiresAdmin: false,
                    isReversible: true,
                    category: "SmartAgent",
                    descriptionAr: "إجراء يعدل حالة العمليات أو إعدادات المستخدم ويتطلب موافقة صريحة.",
                    preconditionDescription: "موافقة المستخدم الصريحة.",
                    postVerificationDescription: "التحقق من حفظ نسخة التراجع وتطبيق الإعداد."));
            }

            Register(new ActionCatalogItem(
                "optimization.gpu_shader_cache",
                "تطهير كاش التظليل لتفادي التقطيع (GPU Shader Cache Purge)",
                "Purge Stale GPU Shader Cache",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Graphics",
                descriptionAr: "تفريغ ملفات كاش التظليل المتراكمة والتالفة لـ DirectX و NVIDIA و AMD التي تسبب هبوط الفريمات (Micro-Stutter) في الألعاب.",
                preconditionDescription: "وجود ملفات كاش مؤقتة لـ D3D أو NVIDIA أو AMD غير مقفلة.",
                postVerificationDescription: "تم تنظيف ملفات الكاش غير المستخدمة بنجاح دون المساس بالملفات النشطة."));

            // Green Tweaks (Completely safe user preference / UI / scheduler tweaks)
            Register(new ActionCatalogItem(
                "tweak:enable-dark-mode",
                "تفعيل المظهر الداكن",
                "Enable Dark Mode",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Appearance",
                descriptionAr: "تبديل مظهر واجهة ويندوز والتطبيقات إلى الوضع الداكن المريح للعين والموفر للطاقة.",
                preconditionDescription: "مفاتيح سجل ثيمات المستخدم متاحة في HKCU.",
                postVerificationDescription: "التحقق من أن AppsUseLightTheme = 0 في سجل النظام."));

            Register(new ActionCatalogItem(
                "tweak:align-taskbar-left",
                "محاذاة شريط المهام لليسار",
                "Align Taskbar to Left",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Taskbar",
                descriptionAr: "محاذاة أيقونات شريط مهام ويندوز 11 إلى اليسار بدلاً من المنتصف.",
                preconditionDescription: "إصدار Windows 11 ومفتاح TaskbarAl متاح في HKCU.",
                postVerificationDescription: "التحقق من قيمة TaskbarAl = 0 في سجل النظام."));

            Register(new ActionCatalogItem(
                "tweak:revert-context-menu",
                "استعادة قائمة زر الفأرة الأيمن الكلاسيكية",
                "Restore Classic Context Menu",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Explorer",
                descriptionAr: "استعادة قائمة السياق الكاملة لنظام Windows 10 بدون شاشة 'إظهار المزيد من الخيارات'.",
                preconditionDescription: "مفتاح CLSID الخاص بقائمة ويندوز 11 متاح في HKCU.",
                postVerificationDescription: "التحقق من وجود مفتاح InprocServer32 الفارغ في السجل."));

            Register(new ActionCatalogItem(
                "tweak:enable-end-task-right-click",
                "إضافة إنهاء المهمة لشريط المهام",
                "Enable End Task on Taskbar",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Taskbar",
                descriptionAr: "إظهار خيار 'إنهاء المهمة' مباشرة عند الضغط بالزر الأيمن على أي تطبيق بشريط المهام.",
                preconditionDescription: "ويندوز 11 إصدار 23H2 فما فوق.",
                postVerificationDescription: "التحقق من قيمة TaskbarEndTask = 1 في HKCU."));

            Register(new ActionCatalogItem(
                "tweak:disable-lockscreen-tips",
                "تعطيل نصائح وإعلانات شاشة القفل",
                "Disable Lockscreen Tips",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Privacy",
                descriptionAr: "إيقاف النصائح الترويجية والإعلانات في شاشة قفل ويندوز.",
                preconditionDescription: "مفتاح ContentDeliveryManager متاح في HKCU.",
                postVerificationDescription: "التحقق من تعيين RotatingLockScreenOverlayEnabled = 0."));

            Register(new ActionCatalogItem(
                "tweak:disable-mouse-acceleration",
                "تعطيل تسريع الفأرة لتحسين دقة الألعاب",
                "Disable Mouse Acceleration",
                ActionTier.Green,
                requiresAdmin: false,
                isReversible: true,
                category: "Input",
                descriptionAr: "ضبط منحنى حركة مؤشر الفأرة بنسبة 1:1 للحد من التذبذب في التصويب.",
                preconditionDescription: "إعدادات الفأرة متاحة في HKCU\\Control Panel\\Mouse.",
                postVerificationDescription: "التحقق من أن MouseSpeed = 0 و SmoothMouseXCurve محايدة."));

            Register(new ActionCatalogItem(
                "tweak:detailed-bsod",
                "عرض تفاصيل شاشة التوقف الزرقاء",
                "Show Detailed BSOD",
                ActionTier.Green,
                requiresAdmin: true,
                isReversible: true,
                category: "Diagnostics",
                descriptionAr: "إظهار رمز الخطأ التفصيلي واسم المعالج بدلاً من رسالة ويندوز المبسطة عند الانهيار.",
                preconditionDescription: "مفتاح CrashControl في HKLM ومساعد إداري متصل.",
                postVerificationDescription: "التحقق من قيمة DisplayParameters = 1 في CrashControl."));

            Register(new ActionCatalogItem(
                "tweak:disable-gamebar",
                "تعطيل شريط ألعاب Xbox وتسجيل الخلفية",
                "Disable Xbox Game Bar",
                ActionTier.Green,
                requiresAdmin: true,
                isReversible: true,
                category: "Gaming",
                descriptionAr: "إيقاف تسجيل الشاشة التلقائي في الخلفية لتوفير موارد كارت الشاشة والمعالج.",
                preconditionDescription: "مفاتيح GameConfigStore و GameDVR متاحة في السجل.",
                postVerificationDescription: "التحقق من GameDVR_Enabled = 0 و AppCaptureEnabled = 0."));

            Register(new ActionCatalogItem(
                "tweak:ultimate-performance-plan",
                "تفعيل خطة الأداء الأقصى (Ultimate Performance)",
                "Enable Ultimate Performance Plan",
                ActionTier.Green,
                requiresAdmin: true,
                isReversible: true,
                category: "Power",
                descriptionAr: "إلغاء قيود توفير الطاقة على المعالج وكارت الشاشة للحصول على أقصى أداء متواصل.",
                preconditionDescription: "أداة powercfg متاحة بالنظام ومساعد إداري متصل.",
                postVerificationDescription: "التحقق من إضافة وتفعيل المخطط الأقصى بنجاح."));


            // =========================================================================
            // YELLOW ACTIONS: Medium Risk. Changes persistent system configuration.
            // NEVER executed autonomously. Requires explicit per-action user confirmation.
            // =========================================================================

            Register(new ActionCatalogItem(
                "tweak:disable-fast-startup",
                "تعطيل بدء التشغيل السريع (Fast Startup)",
                "Disable Fast Startup",
                ActionTier.Yellow,
                requiresAdmin: true,
                isReversible: true,
                category: "Power",
                descriptionAr: "إيقاف حفظ جلسة النواة في hiberfil.sys عند إيقاف التشغيل لحل مشاكل التعريفات والتحديثات.",
                preconditionDescription: "مفتاح HiberbootEnabled في HKLM ومساعد إداري متصل.",
                postVerificationDescription: "التحقق من قيمة HiberbootEnabled = 0."));

            Register(new ActionCatalogItem(
                "tweak:disable-hibernation",
                "تعطيل وضع الإسبات (Hibernation)",
                "Disable Hibernation",
                ActionTier.Yellow,
                requiresAdmin: true,
                isReversible: true,
                category: "Power",
                descriptionAr: "حذف ملف hiberfil.sys واستعادة مساحة تخزين تعادل حجم ذاكرة RAM بالكامل.",
                preconditionDescription: "مساعد إداري متصل وملف إسبات قابل للتعطيل.",
                postVerificationDescription: "التحقق من عدم وجود ملف C:\\hiberfil.sys."));

            Register(new ActionCatalogItem(
                "tweak:disable-copilot",
                "تعطيل مساعد ويندوز الذكي Copilot",
                "Disable Windows Copilot",
                ActionTier.Yellow,
                requiresAdmin: true,
                isReversible: true,
                category: "Privacy",
                descriptionAr: "إيقاف تكامل Copilot في شريط المهام وسطح المكتب لتوفير استهلاك الذاكرة والشبكة.",
                preconditionDescription: "مفاتيح WindowsCopilot في السجل ومساعد إداري متصل.",
                postVerificationDescription: "التحقق من TurnOffWindowsCopilot = 1 في سياسات ويندوز."));

            Register(new ActionCatalogItem(
                "tweak:disable-location-tracking",
                "تعطيل خدمات تتبع الموقع الجغرافي",
                "Disable Location Tracking",
                ActionTier.Yellow,
                requiresAdmin: true,
                isReversible: true,
                category: "Privacy",
                descriptionAr: "إيقاف خدمة تتبع الموقع ومستشعرات الجغرافيا لمنع استنزاف البطارية والمحافظة على الخصوصية.",
                preconditionDescription: "خدمة lfsvc ومفاتيح SensorPermissions متاحة.",
                postVerificationDescription: "التحقق من إيقاف خدمة lfsvc وتعطيل أذونات الموقع."));

            Register(new ActionCatalogItem(
                "tweak:disable-wifi-sense",
                "تعطيل ميزة استشعار الشبكات اللاسلكية Wi-Fi Sense",
                "Disable Wi-Fi Sense",
                ActionTier.Yellow,
                requiresAdmin: true,
                isReversible: true,
                category: "Network",
                descriptionAr: "منع الاتصال التلقائي بشبكات الواي فاي المقترحة ومشاركة بيانات الاعتماد.",
                preconditionDescription: "مفاتيح Wi-Fi Sense في HKLM ومساعد إداري متصل.",
                postVerificationDescription: "التحقق من تعطيل مشاركة الشبكات التلقائية."));

            Register(new ActionCatalogItem(
                "tweak:disable-background-ms-store-apps",
                "تعطيل عمل تطبيقات المتجر بالخلفية",
                "Disable Background Store Apps",
                ActionTier.Yellow,
                requiresAdmin: true,
                isReversible: true,
                category: "Performance",
                descriptionAr: "منع تطبيقات UWP من العمل في الخلفية واستهلاك موارد المعالج والذاكرة بدون علم المستخدم.",
                preconditionDescription: "مفتاح BackgroundAccessApplications في السجل ومساعد إداري متصل.",
                postVerificationDescription: "التحقق من تعيين GlobalUserDisabled = 1."));

            Register(new ActionCatalogItem(
                "tweak:optimize-nvidia-settings",
                "تحسين إعدادات تعريفات بطاقات NVIDIA",
                "Optimize NVIDIA Driver Settings",
                ActionTier.Yellow,
                requiresAdmin: true,
                isReversible: true,
                category: "Graphics",
                descriptionAr: "تعديل معلمات السجل لتعريفات نفيديا لتقليل تأخير إطارات الشاشة DPC Latency.",
                preconditionDescription: "وجود بطاقة شاشة NVIDIA مثبتة بنظام التشغيل.",
                postVerificationDescription: "التحقق من تحديث معلمات السجل في مسار كارت الشاشة."));

            Register(new ActionCatalogItem(
                "tweak:set-time-utc",
                "ضبط ساعة العتاد بنظام التوقيت العالمي (UTC)",
                "Set Hardware Clock to UTC",
                ActionTier.Yellow,
                requiresAdmin: true,
                isReversible: true,
                category: "System",
                descriptionAr: "مزامنة ساعة المذربورد بنظام UTC، وهو أمر هام جداً للأنظمة المزدوجة (Dual Boot مع Linux).",
                preconditionDescription: "مفتاح RealTimeIsUniversal في HKLM ومساعد إداري متصل.",
                postVerificationDescription: "التحقق من قيمة RealTimeIsUniversal = 1."));

            Register(new ActionCatalogItem(
                "startup.toggle_item",
                "تفعيل أو تعطيل برنامج عند بدء التشغيل",
                "Toggle Startup Application",
                ActionTier.Yellow,
                requiresAdmin: false,
                isReversible: true,
                category: "Startup",
                descriptionAr: "إيقاف أو إعادة تفعيل تشغيل أحد البرامج المسجلة مع إقلاع نظام ويندوز.",
                preconditionDescription: "وجود العنصر في مفاتيح Run بسجل النظام.",
                postVerificationDescription: "التحقق من نقل أو استعادة قيمة السجل في مفتاح التخزين الاحتياطي."));

            Register(new ActionCatalogItem(
                "apps.terminate_user_process",
                "إنهاء عملية مستخدم غير أساسية",
                "Terminate User Process",
                ActionTier.Yellow,
                requiresAdmin: false,
                isReversible: false,
                category: "Process",
                descriptionAr: "إغلاق برنامج أو عملية مستخدم تستهلك الموارد بشكل مفرط (مع حظر عمليات النظام الحرجة).",
                preconditionDescription: "العملية نشطة ومملوكة للمستخدم وليست خدمة نظام أساسية.",
                postVerificationDescription: "التحقق من انتهاء العملية بنجاح."));


            // =========================================================================
            // RED ACTIONS: High Risk / Irreversible / Security-Critical / Untested.
            // STRICTLY BLOCKED from autonomous execution by AI / RL2 under any circumstances.
            // Requires high-security user manual confirmation with restore point generation.
            // =========================================================================

            Register(new ActionCatalogItem(
                "tweak:disable-defender-rtp",
                "تعطيل الحماية الفورية لـ Windows Defender",
                "Disable Windows Defender RTP",
                ActionTier.Red,
                requiresAdmin: true,
                isReversible: true,
                category: "Security",
                descriptionAr: "إيقاف الحماية الفورية ضد الفيروسات. إجراء عالي الحساسية يقلل حماية النظام ومحظور استقلاليته.",
                preconditionDescription: "موافقة أمنية صريحة من المستخدم وإنشاء نقطة استعادة.",
                postVerificationDescription: "التحقق من حالة خدمة ومفتاح الحماية الفورية."));

            Register(new ActionCatalogItem(
                "tweak:disable-core-isolation",
                "تعطيل حماية عزل الذاكرة الأساسية (Core Isolation / HVCI)",
                "Disable Core Isolation (HVCI)",
                ActionTier.Red,
                requiresAdmin: true,
                isReversible: true,
                category: "Security",
                descriptionAr: "تعطيل حماية تكامل الذاكرة المعتمدة على المحاكاة الافتراضية. يقلل من حماية النواة ضد برمجيات الفدية.",
                preconditionDescription: "موافقة أمنية صريحة وإعادة تشغيل النظام لاحمة.",
                postVerificationDescription: "التحقق من قيمة HypervisorEnforcedCodeIntegrity = 0."));

            Register(new ActionCatalogItem(
                "tweak:debloat-windows",
                "حذف تطبيقات ويندوز المسبقة (Debloat Windows)",
                "Debloat Pre-installed Windows Apps",
                ActionTier.Red,
                requiresAdmin: true,
                isReversible: false,
                category: "Cleanup",
                descriptionAr: "إزالة حزم التطبيقات الترويجية المسبقة بنظام ويندوز نهائياً. عملية دائمة غير قابلة للتراجع التلقائي.",
                preconditionDescription: "موافقة صريحة على حذف الحزم ونقطة استعادة نظام كاملة.",
                postVerificationDescription: "التحقق من إزالة الحزم الترويجية المحددة فقط دون المساس بأدوات النظام."));

            Register(new ActionCatalogItem(
                "tweak:run-disk-cleanup",
                "تنظيف عميق للقرص ومخزن المكونات (WinSxS Cleanup)",
                "Run Deep Disk & WinSxS Cleanup",
                ActionTier.Red,
                requiresAdmin: true,
                isReversible: false,
                category: "Cleanup",
                descriptionAr: "حذف ملفات تحديثات ويندوز الملغاة من مخزن WinSxS وتفريغ سلة المهاملات. عملية حذف دائمة غير قابلة للاستعادة.",
                preconditionDescription: "موافقة صريحة من المستخدم ومساعد إداري متصل.",
                postVerificationDescription: "التحقق من اكتمال مهمة DISM /StartComponentCleanup."));

            Register(new ActionCatalogItem(
                "tweak:disable-dynamic-ticking",
                "تعطيل نبض المؤقت الديناميكي (Dynamic Ticking - BCD)",
                "Disable Dynamic Ticking (BCD)",
                ActionTier.Red,
                requiresAdmin: true,
                isReversible: true,
                category: "Boot",
                descriptionAr: "تعديل إعدادات إقلاع ويندوز BCD لتعطيل Dynamic Ticking. مصنف NOT TESTED ومحظور خارج بيئات الاختبار.",
                preconditionDescription: "بيئة اختبار معزولة (Sandbox/VM) وموافقة صريحة.",
                postVerificationDescription: "فحص مخرجات bcdedit /enum."));

            Register(new ActionCatalogItem(
                "tweak:disable-hpet",
                "تعطيل مؤقت الأحداث عالي الدقة (HPET - BCD)",
                "Disable HPET (BCD)",
                ActionTier.Red,
                requiresAdmin: true,
                isReversible: true,
                category: "Boot",
                descriptionAr: "تعديل منصة مؤقت الإقلاع BCD useplatformclock. مصنف NOT TESTED ومحظور إنتاجياً.",
                preconditionDescription: "بيئة اختبار معزولة وموافقة صريحة.",
                postVerificationDescription: "فحص مخرجات bcdedit."));

            Register(new ActionCatalogItem(
                "tweak:set-services-to-manual",
                "تحويل الخدمات غير الضرورية إلى الوضع اليدوي",
                "Set Services to Manual",
                ActionTier.Red,
                requiresAdmin: true,
                isReversible: true,
                category: "Services",
                descriptionAr: "تعديل إعدادات بدء تشغيل خدمات ويندوز بشكل مجمع. مصنف NOT TESTED ومؤجل لحماية استقرار الخدمات.",
                preconditionDescription: "بيئة معزولة ونقطة استعادة نظام كاملة.",
                postVerificationDescription: "التحقق من نوع بدء تشغيل كل خدمة على حدة."));

            Register(new ActionCatalogItem(
                "tweak:optimize-network-settings",
                "ضبط متقدم لمكدس شبكة TCP/IP",
                "Optimize Global TCP/IP Stack",
                ActionTier.Red,
                requiresAdmin: true,
                isReversible: true,
                category: "Network",
                descriptionAr: "تعديل معلمات Netsh المتقدمة لمكدس الشبكة العام. مصنف NOT TESTED ومؤجل لحماية اتصال الشبكة.",
                preconditionDescription: "بيئة اختبار معزولة ونسخة احتياطية من إعدادات netsh.",
                postVerificationDescription: "التحقق من استقرار اتصال محولات الشبكة."));

            Register(new ActionCatalogItem(
                "tweak:remove-gaming-apps",
                "حذف تطبيقات ألعاب ويندوز الإضافية",
                "Remove Gaming Apps",
                ActionTier.Red,
                requiresAdmin: true,
                isReversible: false,
                category: "Apps",
                descriptionAr: "إزالة حزم ألعاب ويندوز المسبقة. التراجع جزئي فقط ويتطلب اتصالاً بالإنترنت وإعادة تثبيت عبر Winget.",
                preconditionDescription: "موافقة صريحة على الاستعادة الجزئية ونقطة استعادة.",
                postVerificationDescription: "التحقق من إزالة الحزم."));

            Register(new ActionCatalogItem(
                "tweak:remove-onedrive",
                "إلغاء تثبيت برنامج OneDrive",
                "Uninstall Microsoft OneDrive",
                ActionTier.Red,
                requiresAdmin: true,
                isReversible: false,
                category: "Apps",
                descriptionAr: "إلغاء تثبيت OneDrive من النظام. عملية دائمة تتطلب إعادة تنزيل المثبت للتراجع عنها.",
                preconditionDescription: "موافقة صريحة على الاستعادة الجزئية.",
                postVerificationDescription: "التحقق من عدم وجود ملف OneDrive.exe في مجلدات النظام."));

            Register(new ActionCatalogItem(
                "hardware.emergency_cool",
                "تبريد طوارئ وقائي وحماية السيليكون",
                "Hardware Emergency Thermal Cool",
                ActionTier.Red,
                requiresAdmin: false,
                isReversible: true,
                category: "Hardware",
                descriptionAr: "إجراء طوارئ أوتوماتيكي استثنائي لتخفيف الضغط وحماية المعالج من التلف عندما تتجاوز الحرارة 95 درجة.",
                preconditionDescription: "حرارة المعالج تتجاوز 95 درجة مئوية.",
                postVerificationDescription: "انخفاض حرارة المعالج واستقرار النظام."));

            // Aliases for cross-module compatibility
            RegisterAlias("hardware.emergency_cool", "hardware.emergency_cool");
            RegisterAlias("system.emergency_cool", "hardware.emergency_cool");
            RegisterAlias("thermal.emergency_throttle", "hardware.emergency_cool");
            RegisterAlias("system.purge_standby", "optimization.standby_purge");
            RegisterAlias("optimization.standby_clean", "optimization.standby_purge");
            RegisterAlias("system.flush_dns", "network.dns_flush");
            RegisterAlias("tweak:flush-dns-cache", "network.dns_flush");
            RegisterAlias("flush-dns-cache", "network.dns_flush");
            RegisterAlias("tweak:disable-telemetry", "tweak:disable-location-tracking");
            RegisterAlias("disable-telemetry", "tweak:disable-location-tracking");
            RegisterAlias("OptimizeDesktopSmoothness", "optimization.desktop_smoothness");
            RegisterAlias("TrimInactiveWorkingSets", "optimization.trim_inactive_workingsets");
            RegisterAlias("OptimizeGpuPerformance", "optimization.gpu_boost");
            RegisterAlias("ShedBackgroundGpuMemory", "optimization.gpu_vram_trim");
            RegisterAlias("PurgeGpuShaderCache", "optimization.gpu_shader_cache");
            RegisterAlias("network.flush-dns", "network.dns_flush");
            RegisterAlias("startup.toggle", "startup.toggle_item");
            RegisterAlias("startup.review-toggle", "startup.toggle_item");
            RegisterAlias("apps.kill-process", "apps.terminate_user_process");

            Register(new ActionCatalogItem(
                "recovery.create-restore-point",
                "إنشاء نقطة استعادة نظام",
                "Create System Restore Point",
                ActionTier.Green,
                requiresAdmin: true,
                isReversible: true,
                category: "System",
                descriptionAr: "إنشاء نقطة استعادة جديدة لـ Windows لحماية إعدادات النظام قبل إجراء أي تعديلات.",
                preconditionDescription: "مساعد إداري متصل وخدمة حماية النظام مفعلة.",
                postVerificationDescription: "تم التحقق من نجاح إنشاء نقطة الاستعادة بنجاح."));
            RegisterAlias("create-restore", "recovery.create-restore-point");
            RegisterAlias("health:restore-point", "recovery.create-restore-point");
        }

        private static void RegisterAlias(string alias, string targetId)
        {
            ActionCatalogItem item;
            if (_catalog.TryGetValue(targetId, out item))
            {
                _catalog[alias] = item;
            }
        }

        private static void Register(ActionCatalogItem item)
        {
            _catalog[item.ActionId] = item;
            // Also register normalized key without prefix if starts with tweak:
            if (item.ActionId.StartsWith("tweak:", StringComparison.OrdinalIgnoreCase))
            {
                string shortKey = item.ActionId.Substring("tweak:".Length);
                if (!_catalog.ContainsKey(shortKey))
                {
                    _catalog[shortKey] = item;
                }
            }
        }

        public static bool IsRegistered(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId)) return false;
            return _catalog.ContainsKey(actionId.Trim());
        }

        public static ActionCatalogItem GetItem(string actionId)
        {
            if (string.IsNullOrWhiteSpace(actionId)) return null;
            ActionCatalogItem item;
            _catalog.TryGetValue(actionId.Trim(), out item);
            return item;
        }

        public static ActionTier GetTier(string actionId)
        {
            var item = GetItem(actionId);
            return item != null ? item.Tier : ActionTier.Red;
        }

        public static IEnumerable<ActionCatalogItem> GetAllItems()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in _catalog)
            {
                if (seen.Add(kvp.Value.ActionId))
                {
                    yield return kvp.Value;
                }
            }
        }
    }
}
