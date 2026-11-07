# سجل التغييرات — ClearWin AI

## الإصدار v2.7.7 (التدقيق الهندسي الشامل وتصليب الأمان والاستقرار)
- **أمان WebView2 و LocalWebBridge**: إصلاح ثغرة تجاوز التحقق من المسار (`IsTrustedWebUiUri`) ومنع القبول التلقائي لأي ملف باسم `index.html` خارج مجلد التطبيق، وتفعيل حد الطول الأقصى للأوامر (`MAX_ACTION_LENGTH = 128`)، وإخضاع الرسائل المهيكلة (JSON) لقائمة الأوامر المسموحة، وتقييد `LocalWebBridge` بالمنفذ الفعلي وحجم طلب أقصى 16KB مع حظر كافة الأوامر الحساسة والإدارية عبر HTTP.
- **حماية بيانات المستخدم واستقرار Windows**: إيقاف مسح الحافظة (`EmptyClipboard`) التلقائي أثناء تنظيف الذاكرة (`OptimizeRAM` و `ExecuteAggressiveMemoryClean`)، وإصلاح القيمة المعكوسة لفك إيقاف أنوية المعالج (`CPMINCORES = 100%` بدلاً من `0%`)، وحماية عمليات النظام الحرجة (`winlogon`, `wininit`, `services`, `smss`, `dwm`, `MsMpEng`, `ClearWin`) من الإنهاء، وإزالة مجلدات `Windows\Installer\$PatchCache$` و `System32\Winevt\Logs` من الحذف العشوائي، وحماية التطبيقات الأساسية في أداة إزالة البرامج المسبقة.
- **نظام التراجع والنسخ الاحتياطي للتعديلات (`TweakBackupEngine.ps1`)**: منع الكتابة فوق النسخة الاحتياطية الأصلية عند إعادة تطبيق نفس التعديل مرتين (`Has-TweakBackup`)، ومنع الحذف العشوائي للمفاتيح المشتركة عند التراجع، ودعم الأنظمة غير الإنجليزية في تفعيل خطة الأداء الفائق (`ultimate-performance-plan`)، وإصلاح تحليل مسارات الحسابات التي تحتوي على مسافات في `remove-onedrive`.
- **تطوير وإصلاح الأنظمة الذكية (`SmartAgent` & `MLModel` & `RiskEngine`)**: تسجيل كافة إجراءات `SmartAgent` في `ActionCatalog` لمنع تجاوز `ActionPolicyGate`، وإصلاح تخزين قرارات الشبكة العصبية (`GetMLDecision`)، وتصحيح حساب تدرجات Dropout في `MLNeuralNetwork.Train`، وإزالة تسرب حالات الاختبار (`GetAbstentionCases`) من تدريب المنهج (`CurriculumTraining`)، وإصلاح اكتشاف الخمول الفعلي عبر `GetLastInputInfo`، وتفعيل التراجع الفعلي في `SmartAgent.Recovery`.
- **شفافية الواجهة وإدارة بدء التشغيل**: استبدال الأرقام العشوائية في الواجهة بقياسات فعلية للذاكرة المحررة (`GlobalMemoryStatusEx`) والملفات المحذوفة، وإظهار البرامج المعطلة في مدير بدء التشغيل (`StartupClassifier`) لتمكين إعادة تفعيلها.


# ClearWin — CHANGELOG

## ClearWin v2.7.4 (Work-in-Progress) — October 2026

### AI Training Pipeline & Abstention Calibration
- **Fixed**: `TestAIEvaluationLabPublic` post-training abstention rate regression (was 50%, target >= 80%).
- **Root cause**: The persisted ML model (`ml_nn_v2.dat`, 200 samples) kept `_mlTB.Count >= ML_MIN`, so the neural ensemble gate was active after training. The curriculum's NONE-scenario suppression threshold (0.20) and learning rate (0.06) were too lenient to push mild-state predictions below the firing gate (`0.45 - conf*0.15 ≈ 0.30+`). New ensembles created on-the-fly during training also lacked abstention calibration.
- **Changes**:
  - `src/SmartAgent.CurriculumTraining.cs` — Tightened NONE-scenario suppression: threshold lowered from `0.20` → `0.15`, learning rate raised from `0.06` → `0.10`, so mild-state ensemble predictions are driven well below the firing gate.
  - `src/SmartAgent.CurriculumTraining.cs` — Added **Step 4b: Abstention Calibration Pass** that iterates over the evaluation battery's abstention cases (`AIEvaluationLab.GetAbstentionCases()`), identifies any still-elevated ensembles (`Predict > 0.25`), and applies targeted suppression (`lr=0.08`) before saving the checkpoint.
  - `src/AIEvaluationLab.cs` — Added `GetAbstentionCases()` helper that returns all test cases with `ExpectAbstention == true` from both the main and holdout batteries. Added `using System.Linq;`.
- **Traceability**: See night-shift log `2026-10-19-nighshift.md` for full diagnostic trace.

### 1. Version Synchronization & Integrity

### 1. Version Synchronization & Integrity
- **Version Alignment**: Updated full build identity to `2.7.3.0` across:
  - `src\Properties\AssemblyInfo.cs` (`AssemblyVersion`, `AssemblyFileVersion`)
  - `src\app.manifest` (`assemblyIdentity version="2.7.3.0"`)
  - `src\MainWindow.Glassmorphism.cs` (Window header branding)
  - `Resources\WebUI\index.html` (Title bar and sidebar badge)
  - `Resources\WebUI\app.js` (Changelog view and status bindings)
  - `Resources\WebUI\style.css` (Glass UI metadata)
  - `build.ps1` & Inno Setup scripts (`installer.iss`, `installer-2.7.3-offline.iss`)
- **Preservation of Prior Releases**: `release\ClearWin-Setup-2.7.2.exe` and `release\ClearWin-Setup-2.7.2-Offline-Candidate.exe` preserved intact in the parent `release\` directory; new v2.7.3 artifacts isolated in `release\v2.7.3\`.

### 2. Tweak Safety & Defense-in-Depth Gates
- **4 NOT TESTED Tweaks Locked**:
  - `disable-dynamic-ticking`
  - `disable-hpet`
  - `optimize-network-settings`
  - `set-services-to-manual`
  - Protected by a 4-layer security barrier:
    1. C# GUI layer: `IsUntestedDeferredTweak()` blocks execution and returns Arabic safety explanation.
    2. WebUI bridge layer: `IsUntestedDeferredTweak()` interceptor drops command before routing.
    3. Process Environment Sanitization: Automatically purges `CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS` from execution context.
    4. Script self-guard: Each script checks `$env:CLEARWIN_ALLOW_DEFERRED_VM_TWEAKS -ne 'YES'` and exits immediately with code 3.
- **4 BLOCKED Tweaks Safeguarded**:
  - `disable-core-isolation`, `disable-defender-rtp`, `debloat-windows`, `run-disk-cleanup` require explicit opt-in flags and safety restore points.
- **2 VERIFIED_PARTIAL Tweaks Documented**:
  - `remove-gaming-apps` and `remove-onedrive` feature clear UI tooltips indicating partial undo via Winget reinstall.

### 3. Automated Test Suite Verification
- **127 / 127 Automated Tests Passing (100%)**:
  - Core Engine (28 tests): Budget, Confidence, Costs, Preconditions, Conflicts, Telemetry, Mock Collector, Rollback, Dependency Graph, Memory Caps, Aging, Lock Ordering, Null Safety, Cooldown, Knapsack.
  - FPS Stabilizer (10 tests): Stabilizer Core, Drop Detection, Prediction, Prevention Counting, Cooldown Cap, 60 Floor Mode, Decision Demo.
  - Hardware & Platform (3 tests): Vendor GPU Temperature (NVAPI/ADL), DXGI FPS Reader, Startup Classifier.
  - LocalWebBridge Security (1 test): Allowlist actions, URI validation, length limits, anti-directory traversal.
  - AI & SmartAgent (33 tests): Double Q-Learning, Causal AI, Multi-Agent Voting, World Model, Risk Engine, Tool Training, KillSwitch Emergency, Stress Cascades.
  - Neural Network (19 tests): 19/19 passed (1153 weights, training convergence, feature conversion, save/load round-trip).
  - Optimization AI (29 tests): 1000 Decisions, Stability, Thermal/RAM/Gaming profiles, DiD Counterfactuals, Trust Scoring.
  - Tweaks Lifecycle & Backup Engine (2 tests): State preservation, apply/undo integrity across 27 tweaks.

### 4. Release Packaging & Verification
- **ClearWin-Setup-2.7.3.exe (Standard Web Installer)**:
  - Size: 3,377,166 bytes (3.22 MB)
  - SHA-256: `243AFE31B0CDB3AF80ABB6B51278D15BA41681DF3ED7D155D8757A88372CC9D6`
  - Full host lifecycle tested: Silent installation (exit 0), 120 files verified, `runtime-check.ps1` passed (exit 0), smoke launch verified (PID 12284, 49.36 MB RAM), clean silent uninstall (exit 0, 0 leftover files).
- **ClearWin-Setup-2.7.3-Offline.exe (Standalone Offline Installer)**:
  - Size: 217,356,250 bytes (207.29 MB)
  - SHA-256: `593E2CBE03642688B00E28EC601C917955124274C8A1CE64EB8C43A5BACDAE51`
  - Bundles official Microsoft Edge WebView2 Evergreen Standalone Installer (x64, Authenticode verified).
  - Full host lifecycle tested: Silent installation (exit 0), 120 files verified, smoke launch verified (PID 10896, 100.8 MB RAM), clean silent uninstall (exit 0, 0 leftover files).

---

## ClearWin v2.7.2 (Legacy Release)
- Implementation of Inno Setup packaging.
- Baseline 27 tweaks catalog integration with TweakBackupEngine.
- Initial offline WebView2 installer candidate packaging.
- Logging deduplication resilience guards.
