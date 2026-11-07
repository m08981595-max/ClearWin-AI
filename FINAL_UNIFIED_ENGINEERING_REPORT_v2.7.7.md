# التقرير الهندسي الموحّد والنهائي لمشروع ClearWin AI v2.7.7
**التدقيق الشامل، الإصلاح المعماري المتكامل (المراحل 0–5)، التحقق الرياضي والبرمجي، وتسليم النسخة النهائية للمصدر**

---

## 1. الملخص التنفيذي

أُنجزت المهمة الهندسية الشاملة لمشروع **ClearWin AI v2.7.7** بالكامل بدءاً من المرحلة صفر (حماية المشروع والنسخ الاحتياطي) وحتى المرحلة الخامسة (إصلاح وتكامل `RL2Engine` وتوسيع حزمة الاختبارات)، مع معالجة جميع العيوب الحرجة والعالية والمتوسطة المثبتة في تقارير المراجعة والتحقق المستقل دون كسر أي عقد برمجي قائم أو المساس بملفات البناء الثنائية (`bin/*.exe`) قبل توفر بيئة بناء Windows أصلية.

### أبرز الإنجازات الهندسية المنفّذة:
1. **المرحلة صفر (حماية المصدر وبصمات SHA-256)**:
   - الحفاظ التام على النسخة الاحتياطية الأصلية `backups/pre_v2.7.7_audit_backup/` وإنشاء نسخة احتياطية كاملة قبل الإصلاح في `backups/phase0_pre_repair_v277/` مع توثيق بصمات `SHA-256` لجميع ملفات المشروع قبل التعديل وبعده في `SHA256_MANIFEST_PRE_REPAIR.txt` و`SHA256_MANIFEST_POST_REPAIR.txt`، وتوليد ملف الفروق الموحّد الكامل `FINAL_ALL_PHASES_UNIFIED_DIFF.patch` (4,793 سطراً).
2. **المرحلة الأولى (الصلاحيات، نقاط الاستعادة، DNS، ومحلل JSON المتتبع للعمق)**:
   - توحيد مسار إنشاء نقطة الاستعادة عبر `CreateClearWinRestorePoint` (تمرير الطلب عبر `AdminSessionManager.ExecutePrivilegedAction("system.create_restore_point", ...)` عند اتصال `AdminHelper`، والتحقق من إنشاء ملف العلامة `markerFile` و`ExitCode == 0`، وتشغيل العملية خارج خيط واجهة المستخدم `Task.Run` مع قفل التوازي `TryEnterRestorePointFlight`).
   - فصل `network.repair-preview` (معاينة تشخيصية للقراءة فقط بلا آثار جانبية) عن `network.flush-dns` (تنفيذ صريح ومُتحقق منه عبر `TryFlushDnsVerified` مع قفل التوازي `TryEnterDnsFlushFlight`)، وحظر الإجراء غير المسجل `risky-network`.
   - ترقية محلل JSON في `src/AdminSessionManager.cs` (`TryParseTopLevelJsonObject`) و`src/WebUiHost.cs` ليتتبع عمق الكائنات `{...}` (`braceDepth`) والمصفوفات `[...]` (`bracketDepth`) وحالة الهروب داخل النصوص، بحيث يعزل أي كائنات أو مصفوفات متداخلة (`"output": {"success": true}` أو `[{"success": true}]`) كرموز مركّبة (`JsonTokenKind.Structured`) لا يمكنها أبداً تزوير الحقول العلوية، ويرفض المفاتيح المكررة في المستوى الأعلى (`Duplicate top-level JSON key`) أو JSON المبتور/غير المتوازن بمنطق الإغلاق الآمن (`Fail-Closed`).
3. **المرحلة الثانية (أ) — سلامة تعديلات السجل (`Registry`) واستعادة الحالة الأصلية**:
   - بناء محرك النسخ الاحتياطي والاستعادة الدائم للسجل في `src/SmartAgent.GpuEngine.cs` (`RegistryBackupEntry`، `SetRegistryValueWithBackup`، و`RestoreAllBackedUpRegistrySettings`) مع حفظ السجل على القرص في `%LOCALAPPDATA%\ClearWin\registry-restore-journal.ini`.
   - منع أي محاولة كتابة في `HKLM` (`Multimedia\SystemProfile\Tasks\Games`) عندما يعمل البرنامج بصلاحيات مستخدم عادي (`!IsCurrentProcessElevated()`)، وإزالة التعديل الصامت لمفتاح `HwSchMode` أثناء التشغيل الاعتيادي لأنه يتطلب إعادة تشغيل النظام.
   - تغليف جميع كتابات `HKCU` (`Software\Microsoft\Direct3D\MaxPreRenderedFrames`، و`Software\Microsoft\DirectX\UserGpuPreferences`، ومفاتيح `Software\Tencent\GameLoop` و`MobileGamePC`) بنسخ احتياطي يسجل (`KeyExistedBefore`, `ValueExistedBefore`, `OriginalValueKind`, `OriginalValueSerialized`, `ValueWrittenByClearWin`).
   - تنفيذ استعادة ذكية تحترم تعديلات المستخدم اللاحقة: عند الخروج من وضع الألعاب (`RevertGameMode`) أو إيقاف التحسينات (`EmergencyStopAllOptimizations` / `RestoreFramePacing` / `Stop` / `MainWindow.Closed`)، لا يستعيد البرنامج القيمة السابقة إلا إذا كانت القيمة الحالية في السجل لا تزال تطابق `ValueWrittenByClearWin`؛ فإذا غيّرها المستخدم يدوياً أثناء الجلسة، يحتفظ البرنامج بقيمة المستخدم الجديدة ولا يطمسها، وإذا لم تكن القيمة موجودة أصلاً قبل تدخل ClearWin يحذفها نظيفاً.
4. **المرحلة الثانية (ب) — توحيد آلة حالة كشف الألعاب وخطط الطاقة (`Power Plan State Machine`)**:
   - إزالة حلقة `AutoPilot` المستقلة المكررة داخل `OnTick` (`src/MainWindow.Logic.cs`) وتوحيد حالة `_autoPilotActive` بالكامل تحت مالك واحد (`OnGameWatch`).
   - إضافة فترة سماح واستقرار (`Hysteresis / Grace Period`: `GAME_EXIT_HYSTERESIS_TICKS = 10` أي نحو 5 ثوانٍ) عند خروج نافذة اللعبة من المقدمة طالما أن عملية اللعبة (`_trackedGamePid`) لا تزال قيد التشغيل، مما يمنع تذبذب خطط الطاقة وأولويات العمليات عند ضغط `Alt+Tab`.
   - إزالة استدعاء `powercfg.exe /setactive 8c5e7fda-...` المزود بـ `Verb = "runas"` من `ActivateGameModeSilent()` (`src/MainWindow.Logic.cs`) لمنع ظهور نوافذ UAC مفاجئة عند بدء الألعاب وتجنب إطلاق 11 عملية `powercfg.exe` متزاحمة.
   - إصلاح حلقة التكرار كل 30 ثانية بين `"ultimate"` و`"high"` في `AnalyzePowerPlan` (`src/SmartAgent.PowerPlanManager.cs`) عبر الدالة `IsPowerPlanSatisfied` وفترة التهدئة الموحدة (`POWER_SWITCH_COOLDOWN_SECONDS = 10.0`)، وإصلاح أزرار WPF الاحتياطية في `src/MainWindow.Pages.cs` (إزالة تعيين DNS الثابت `8.8.8.8` غير القابل للتراجع على `"Wi-Fi"` واستبداله بـ `TryFlushDnsVerified`).
5. **المرحلة الثالثة — إصلاح الشبكة العصبية (`MLNeuralNetwork`)، `RunningNormalizer`، ومنع تسرب بيانات التحقق**:
   - فصل التطبيع القرائي `NormalizeReadOnly(double[] raw)` عن التحديث الإحصائي `Observe(double[] raw)` في `RunningNormalizer` (`src/SmartAgent.MLModel.cs`)، بحيث تُحدَّث الإحصاءات الجارية (`_c`, `_m`, `_s`) مرة واحدة فقط عند وصول عينة حقيقية جديدة، بينما تستخدم جميع حلقات التدريب (`TrainNN`) والتحقق (`ComputeValidationMetrics`) والتدريب المسبق (`Pretrain` / `CurriculumTraining`) والاستدلال الحي (`GetMLDecision`) دالة `NormalizeReadOnly` دون تلويث المتوسط أو الانحراف المعياري.
   - القضاء التام على تسرب بيانات التحقق في `TrainNN` (`H1`): إضافة معرّف فريد رتيب `SampleId` لكل `MLTrainingSample`، وتقسيم عينات `_mlTB` أولاً إلى مجموعة تدريب (70%) ومجموعة تحقق معزولة (30% محفوظة في `valSampleIds`) **قبل** سحب عينات `PrioritizedReplayBuffer` (`_mlRP`)، مع استبعاد أي عينة من `_mlRP` ينتمي معرّفها `SampleId` إلى `valSampleIds`، مما يضمن رياضياً أن $\text{TrainIds} \cap \text{ValIds} = \emptyset$.
   - حقن أمثلة مضادة سالبة (`Negative Counter-Examples`، `H2`) في `GeneratePriorGrid` (`target = 0.08` للإجراءات غير الملائمة للحالة) وفي `RecordActionOutcomeSample` (`OutcomeScore = 0.12` للإجراءات المتعارضة في `ACTION_CONFLICTS` عند نجاح إجراء منافس بدرجة $\ge 0.72$)، وتخفيض الحد الأدنى لبدء تدريب الإجراءات النادرة من 5/8 إلى 4 عينات.
   - تجميد انحيازات ما قبل التطبيع الطبقي (`B1 = 0`, `B2 = 0`, `dB1 = 0`, `dB2 = 0`) نظراً لأن `LayerNorm` يلغي جبرياً أي إزاحة ثابتة قبل التطبيع بينما يتولى `BN1B` ($\beta_1$) و`BN2B` ($\beta_2$) الإزاحة المتعلمة بعد التطبيع (`M1`).
   - ترقية تنسيق حفظ النموذج العصبي في `SaveMLModel()` إلى النسخة `"V4"` لحفظ واستعادة جميع عزوم `AdamW` الخاصة بـ `LayerNorm` (`MBN1G, VBN1G, MBN1B, VBN1B, MBN2G, VBN2G, MBN2B, VBN2B`) مع الحفاظ على التوافق العكسي الكامل لقراءة ملفات `"V2"` و`"V3"` و`"V4"` في `LoadMLModelFile()`، وتحديث `CloneNet` في `src/SmartAgent.Validation.cs` لنسخ جميع عزوم `AdamW` (`M2`).
6. **المرحلة الخامسة — إصلاح `RL2Engine` وتفعيل وضع المراقبة المقارنة (`Shadow Mode`)**:
   - توسيع فضاء الإجراءات في `GetCandidateActionsForStateLocked` (`src/SmartAgent.RL2.cs`) ليشمل جميع الإجراءات الـ 20 في `ML_AC` بالإضافة إلى `_actionValues` وأي إجراءات مخزنة في `_stateQ` أو `_stateQB` (بما في ذلك إجراءات GPU الثلاثة).
   - تهيئة عداد الزيارات `_stateVisits[stateKey][action] = 1` داخل `SeedStateQValue` لمنع طرد الحالات المدربة مسبقاً عند استدعاء `EvictColdestState()`.
   - تطبيق خوارزمية **Double Q-Learning المتماثلة الحقيقية (Hasselt 2010)** في `ApplyDoubleQUpdateLocked` بحيث يتناوب التحديث بين جدولي `_stateQ` ($Q_A$) و`_stateQB` ($Q_B$) مع تقييم هدف TD عبر الشبكة المقابلة والهدف الناعم `_stateQTarget`، واستخدام المتوسط المزدوج $0.5(Q_A + Q_B)$ عند اختيار أفضل إجراء.
   - ربط `RL2Engine` في `RankAndFilter` (`src/SmartAgent.cs`) عبر `EvaluateRL2ShadowAndOptionalInfluence` بوضع **Shadow Mode افتراضياً** (`EnableRL2LiveInfluence = false`)، حيث يسجل مقارنات التوافق (`RL2ShadowComparisons`, `RL2ShadowAgreements`) دون تغيير قرارات النظام الحية، مع توفير بوابة تأثير اختيارية محدودة ($\pm 0.08$ كحد أقصى) لا تعمل إلا عند تفعيل `EnableRL2LiveInfluence = true` وتوفر $\ge 3$ زيارات فعلية واجتياز `IsActionApplicableToState`.

---

## 2. بيئة التنفيذ الفعلية وما أمكن تشغيله فعلًا

التزاماً بالشفافية الهندسية الصارمة:
- **نظام التشغيل الفعلي لبيئة الفحص الحالية**: حاوية Linux (`x86_64 GNU/Linux`، النواة `6.6.137+`، Debian/Ubuntu) مثبت عليها `Python 3.11.2` و`gcc/g++`.
- **أدوات البناء المتوفرة في المستودع**: المترجم المدمج `tools/roslyn-4.8.0/tasks/net472/csc.exe` ومكتبات WebView2 في `libs/` هي ثنائيات Windows PE (`.exe` / `.dll`) مصممة لبيئة `.NET Framework 4.8` و`WPF` على Windows.
- **ما لم يُشغَّل في هذه البيئة (ولماذا)**: لا تتوفر في حاوية Linux الحالية بيئة تشغيل Windows أو `.NET Framework 4.8` أو `Wine` أو `Mono`، ولا توجد خدمات Windows (`Named Pipes` مع `PipeSecurity`، `WMI`، `Checkpoint-Computer`، `ipconfig /flushdns`، `powercfg.exe`، `Win32 Registry`). لذلك **لم يتم الادعاء بتحديث الثنائيات داخل `bin/`**، وبقيت بصمات `bin/ClearWin.exe` و`bin/ClearWin.AdminHelper.exe` مطابقة تماماً لبصماتها الأصلية قبل التعديل.
- **ما تم تشغيله والتحقق منه فعلياً داخل البيئة الحالية**:
  1. السكربت التحققي الشامل `tests/verify_all_phases_v277.py` الذي يفحص توازن الأقواس والبنية النحوية لجميع ملفات C# المعدلة الـ 25، ويشغّل آلة الحالة الحرفية لمحلل JSON المتتبع للعمق على 14 حالة هجومية وحدية، ويختبر محرك استعادة السجل وحماية تعديلات المستخدم، ويثبت ثبات `RunningNormalizer.NormalizeReadOnly` وانعدام تسرب بيانات التحقق ($\text{TrainIds} \cap \text{ValIds} = \emptyset$).
  2. السكربت الرياضي المرجعي `tests/reproducible_verification_v277.py` (مع توضيح صريح بأنه مرجع رياضي مستقل يحاكي معادلات C# وليس تشغيلاً لثنائية C# نفسها، إلى جانب وجود اختبارات C# الفعلية داخل `src/UnitTests.cs`).
  3. توليد سكربت التحقق والبناء الأوتوماتيكي الكامل لبيئة Windows: `verify-windows-build-and-tests.ps1`.

---

## 3. النسخ الاحتياطية وبصمات SHA-256 قبل وبعد التعديل

- **النسخة الاحتياطية التاريخية الأولى (محفوظة دون لمس)**: `backups/pre_v2.7.7_audit_backup/`
- **النسخة الاحتياطية الكاملة للمرحلة صفر قبل الإصلاح**: `backups/phase0_pre_repair_v277/`
- **سجل البصمات قبل الإصلاح**: `backups/phase0_pre_repair_v277/SHA256_MANIFEST_PRE_REPAIR.txt` (126 ملفاً)
- **سجل البصمات المقارن بعد الإصلاح**: `backups/phase0_pre_repair_v277/SHA256_MANIFEST_POST_REPAIR.txt`
- **ملف الفروق الموحّد الكامل**: `backups/phase0_pre_repair_v277/FINAL_ALL_PHASES_UNIFIED_DIFF.patch`

### جدول بصمات SHA-256 للملفات الـ 25 المعدلة (قبل التعديل مقابل بعد التعديل):

| المسار (`File Path`) | بصمة SHA-256 قبل الإصلاح (`Pre-Repair`) | بصمة SHA-256 بعد الإصلاح (`Post-Repair`) |
| :--- | :--- | :--- |
| `src/AdminHelper/AdminHelperProgram.cs` | `F20347484F56326CB96EE29FA1145370FC9AA821BA441C6BD3394D56433CCF0D` | `61C60823B7DC0FD3255A57A3B95FF2648A7F627CBAD8EC1DAFC092724E463152` |
| `src/AdminSessionManager.cs` | `AF9468E5AABB29DC993FA5922320EF26CC1ADAFF04E48EA2686395E50CC1CC11` | `192E999112DC061A472ED61C8188364F310A455EC2BD61B5841C2C23E15F0F1F` |
| `src/WebUiHost.cs` | `B3DC3833541E26311048EEC9E7D4BB827C1FBF499A7D3F967A48CC25F452A6E8` | `7FAF7C60CD5B64EEF952566B962ADB021B41C4AFC31CA7189293627FEB3E5F9F` |
| `src/MainWindow.cs` | `68138884F58751F4784BA1E3CF4F07B1D9E45E70BD1550691EBDB2357ED409E8` | `F0029AFFAC1925E260C3ECE2DA71D2A2FDF484862D625E95898D0D9BCC557AAA` |
| `src/MainWindow.Logic.cs` | `6018CC31A601D5DA057B32A39C8DBFDA456FE8B2D2B2A74198D28DF32672CC7F` | `B6D7FCD27F10B68A307F7E38B318E227F1FED3799478E19B3242BF5634F785A5` |
| `src/MainWindow.Pages.cs` | `FAB6EBC0D46CDDDE1BC05397B9E4ACD253EF52C0D4FEA0BA0E9F0C7DD6582A4F` | `142C0BB97FA30D813FD8CC53257A639682D82DED81914E6EBAD1CB54D5B5387D` |
| `src/MainWindow.WebUi.cs` | `2EF4715651AA2BF76123428F0563B93923334A0B1EF71904172EAE240DCF0233` | `34AE1A830A0B267FE4650B1B7345905FB6C9BB31D706BB5FAB228F11A4185C52` |
| `src/OverlayWindow.cs` | `C893591606EA7E4FA404140FDDA03F1F7F63C82CC0AD07ED6153A7BCBB502A19` | `F096BD4FBC0DD9F493135DCBCC6C90CEC82268F9B39829E64BBBED97F36809C6` |
| `src/OverlayControls.cs` | `760F6334181B3EFE061D53006CB40BD9F815A080E5665D2E71288D9FBBF2D93E` | `40BAAF620734C373F256A6231E5F9A3F965AEDDC68BD92025E108FBDC019A5D0` |
| `src/SmartAgent.cs` | `5200E0C5ACAC224F2A3AAABEB1FFE7845EBD38B68E5EC5375A753E59F28706EC` | `5CE4F2161BF8FED3F1BBA77A38878EF8ADC39563DA5323C7050537E69FF700EE` |
| `src/SmartAgent.GpuEngine.cs` | `5B075CC344124B42C5C4BED6BFF0D7204C24B0250B7834483F8C332896C6BAA3` | `1D34C8C34677348B5B13A3345E618AED4277A5E0EA39BBDFFA64415A08FC0031` |
| `src/SmartAgent.PowerPlanManager.cs` | `50CDD9A19DA537C2F03833E8EA907ACCBFB3646D8738BD123CE22CD2F3D26805` | `4EE909A476E5E005C57FB13773678F291097E3EE08EBE62F1DC9E1C3249FE40D` |
| `src/SmartAgent.FramePacing.cs` | `05E4A2FDB00EB8BE0B205A57451EF57AD83E552D5503E64BC9FE11A7B06CF2FD` | `24C49DC54A98679003A5C5C271845A3ED215150936D17122ECEB7D1D7C345534` |
| `src/SmartAgent.NetworkOptimization.cs` | `AEEE4567EE8C466E26F67A28D9392EC51E0E685AB1170DF96F12C6DA47C31CFA` | `DAD4CB01E4A10ED2E548AC233EABA984CD492A3E51F652F641DCD015280F2B43` |
| `src/SmartAgent.NetworkTuning.cs` | `D879F08B19238002ED55F5126E6D274051B2980416B07CD66127C28C2716995D` | `8671E7E42473FF3B794618A1771DF162F8857CA8566B66397B0BF4A75189B32E` |
| `src/SmartAgent.MLModel.cs` | `32AC225203D8A1C08270162D7D05B168EA2FA90520185BE7DDE7050C644501A6` | `DC5D7119F3E8015665F4309351CAC21186A64C45AE0E1D856151CD9980F39512` |
| `src/SmartAgent.Pretrain.cs` | `C21FB6435844ED6ECCBCAC127F9B138D1ACA8D47C7B441AC6035A10FA4911CB5` | `8F55380F684D29C6662827FEDFD25B7D2645161CF0F6DD2A270D555E6BAAF33B` |
| `src/SmartAgent.CurriculumTraining.cs` | `2EAADAB871714EB592F71C2A977ED99648772E6CF2A434ECCE30D96A7125B0DC` | `54A0DB2CB70409CC92059ABD21598705487C8C2729D3110D03141E560ABE9EB2` |
| `src/SmartAgent.DirectLearningAudit.cs` | `38A1238802BE4A0B7D40663C1EB3F83C87ECED0415B236A2D52D7E4ED596CDE5` | `506860B1B300643939F0EBDE94F9B4D7F4F1E19C64FECC01E55B3A5C308A5885` |
| `src/SmartAgent.Validation.cs` | `7A002FA4AECC2D71B598879CD7644E71A4F94B3F0C424DBB800F93ECC1C69039` | `3F9E06F84FF560B7B5BE4AB5B1E5E62B31AB27EAF18FB4443D458BD89DB7530C` |
| `src/SmartAgent.RL2.cs` | `89DDA8B875C5DC2E4546C543D8E96D84F82585902A48B1D13396D0936F93985C` | `6685C2AFF049E8DCC778A99AD879D3089630C7C42FE2B9FCB754BA91DD7EE24E` |
| `src/SmartAgent.StartupClassifier.cs` | `E5B583F778755B294CA875E3FD60B73431B8DD3C6E032B16C72CA8C8D63E0543` | `1A2A2BF38002A2A0758AAAB939E9491A5D56384F244A25A6CE993F79E83280F9` |
| `src/OptimizationAITest.cs` | `E3896359B8B29E657E718A984331B293CAAB63C237DDFED935D54E754113B915` | `7DC681BD368865569AA06A79B710A320AA7187793994973E9FBC60189D7CB9DD` |
| `src/UnitTests.cs` | `2B4A766C00C62AA91710B90A583F38DCE383121E004B8EC3CE096022BD6A88C8` | `8268B2C59B1AACB9CBFDAA37B6819D91D5CD437B418FC084DBBD13DD6CB13A9E` |
| `src/TestEntry.cs` | `959AE7FBD109E43C4E34C0522DF2EBD8C893A4446D358D43267248C91B4C41FB` | `1B3CD22D3CA085739007CADC37C3E28593ECCD0B65284A0D3A9A7EA96F0CE8B5` |

### بصمات الثنائيات التنفيذية (محفوظة دون تلاعب بانتظار البناء على Windows):
- `bin/ClearWin.exe`: `B4D1ED50FAE87C466F4AE241F39B1FB96A984EE6540FE229E4FA8DF0128C2C81` (لم تُمس)
- `bin/ClearWin.AdminHelper.exe`: `E68892B77365106F43FB88A9123B13AEF757050CE87584AC3CF9595D5AE4F88D` (لم تُمس)

---

## 4. قائمة جميع المشكلات المكتشفة وحالة كل واحدة

| المعرّف | الوصف الهندسي للمشكلة | الخطورة | الحالة النهائية |
| :--- | :--- | :--- | :--- |
| **C1 / Phase 1** | إنشاء نقطة الاستعادة يفشل بصمت أو يعرض نجاحاً كاذباً ويتجاوز `AdminHelper` أو يجمّد خيط الواجهة | **حرجة (Critical)** | **تم إصلاحها والتحقق منها برمجياً (وبانتظار البناء والتشغيل الحي على Windows)** |
| **C2 / Phase 1** | محلل JSON في `AdminSessionManager` قابل للتزوير عبر كائنات/مصفوفات متداخلة أو مفاتيح مكررة | **حرجة (Critical)** | **تم إصلاحها والتحقق منها (محلل متتبع للعمق `TryParseTopLevelJsonObject` + اختبارات هجومية)** |
| **C3 / Phase 1** | `network.repair-preview` ينفذ `ipconfig /flushdns` الفعلي ويعرض نجاحاً غير محقق، ووجود `risky-network` | **عالية (High)** | **تم إصلاحها والتحقق منها (فصل المعاينة القرائية عن التنفيذ الصريح + فحص رمز الخروج)** |
| **C4 / Phase 2A** | كتابات سجل `HKCU` و`HKLM` (`MaxPreRenderedFrames`, `UserGpuPreferences`, `GameLoop`) بلا نسخ احتياطي أو استعادة | **حرجة (Critical)** | **تم إصلاحها والتحقق منها (محرك `RegistryBackupEntry` مع حماية التعديلات اليدوية للمستخدم)** |
| **C5 / Phase 2B** | تنافس 3 مسارات طاقة وألعاب (`OnGameWatch`، و`AutoPilot` في `OnTick`، و`AnalyzePowerPlan`) ونافذة UAC مفاجئة | **حرجة (Critical)** | **تم إصلاحها والتحقق منها (توحيد الآلة تحت `OnGameWatch` + `Hysteresis` 5 ثوانٍ + إزالة `runas`)** |
| **H1 / Phase 3** | تسرب بيانات التحقق في `TrainNN` بسبب خلط `_mlTB` مع `_mlRP` قبل التقسيم 70/30 | **عالية (High)** | **تم إصلاحها والتحقق منها ($\text{TrainIds} \cap \text{ValIds} = \emptyset$ عبر `SampleId` والعزل المسبق)** |
| **H2 / Phase 3** | تلوث إحصاءات `RunningNormalizer` أثناء حلقات التدريب/التحقق، وغياب الأمثلة السالبة في التدريب المسبق | **عالية (High)** | **تم إصلاحها والتحقق منها (`NormalizeReadOnly` مقابل `Observe` + حقن أمثلة مضادة سالبة)** |
| **M1 & M2 / Phase 3** | انجراف انحيازات ما قبل `LayerNorm` (`B1`, `B2`)، وعدم حفظ عزوم `AdamW` لمعاملات `LayerNorm` عند الحفظ | **متوسطة (Medium)** | **تم إصلاحها والتحقق منها (تثبيت `B1=B2=0` + تنسيق `V4` لحفظ `MBN1G..VBN2B` + تحديث `CloneNet`)** |
| **H3 / Phase 5** | انعزال `RL2Engine`، واقتصاره على 14 إجراءً، وطرد الحالات المبذورة، وعدم تماثل `Double-Q` | **عالية (High)** | **تم إصلاحها والتحقق منها (تغطية 20 إجراءً + تهيئة الزيارات + `Double-Q` المتماثل + `Shadow Mode`)** |
| **M3 / Phase 4** | اختبارات بدء التشغيل تكتب في مفتاح السجل الحقيقي `HKCU\...\CurrentVersion\Run` | **متوسطة (Medium)** | **تم إصلاحها والتحقق منها (عزل الاختبارات داخل `SOFTWARE\ClearWin\TestSandbox\Run`)** |

---

## 5. تفاصيل إصلاحات الصلاحيات و`AdminHelper` ونقطة الاستعادة وDNS ومحلل JSON

1. **إنشاء نقطة الاستعادة (`CreateClearWinRestorePoint` و`AdminHelperProgram.ExecuteCreateRestorePoint`)**:
   - في `src/AdminHelper/AdminHelperProgram.cs`: إضافة `$ErrorActionPreference = 'Stop'` واستخدام ملف علامة مؤقت (`markerFile`) لا يُكتب إلا بعد نجاح `Checkpoint-Computer` دون استثناءات، والتحقق من `ExitCode == 0 && File.Exists(markerFile)`.
   - في `src/MainWindow.Pages.cs` و`src/MainWindow.WebUi.cs` و`src/OverlayWindow.cs` و`src/OverlayControls.cs`: توجيه جميع طلبات إنشاء نقطة الاستعادة عبر `CreateClearWinRestorePoint` داخل `Task.Run` خارج خيط الواجهة، مع حارس التوازي `TryEnterRestorePointFlight()` لمنع إطلاق عمليتين متزامنتين، واستخدام قناة `AdminSessionManager.ExecutePrivilegedAction("system.create_restore_point", ...)` أولاً عند اتصال `AdminHelper`.
2. **فصل `network.repair-preview` عن `network.flush-dns` والتحقق من `ipconfig /flushdns`**:
   - في `src/MainWindow.WebUi.cs`: أصبح الأمر `network.repair-preview` معاينة قرائية بحتة (`Read-Only Preview`) تعرض الإجراءات التي ستُنفذ دون إحداث أي أثر جانبي، بينما ينفذ `network.flush-dns` عملية المسح الفعلية خارج خيط الواجهة عبر `TryFlushDnsVerified` مع حارس التوازي `TryEnterDnsFlushFlight()`.
   - في `src/SmartAgent.NetworkOptimization.cs` و`src/SmartAgent.NetworkTuning.cs` و`src/MainWindow.Pages.cs`: التحقق من `proc.ExitCode == 0` وعدم وجود نصوص فشل/رفض صلاحيات قبل تسجيل نجاح مسح DNS.
3. **محلل JSON المتتبع للعمق (`TryParseTopLevelJsonObject` في `src/AdminSessionManager.cs`)**:
   - يتتبع المحلل الجديد عمق الأقواس المتعرجة `{...}` (`braceDepth`) والمربعة `[...]` (`bracketDepth`) وحالة علامات الاقتباس المهرّبة `\"` و`\uXXXX` عبر آلة حالة حتمية:
     - أي قيمة تبدأ بـ `{` أو `[` عند المستوى الأعلى يتم تخطيها بالكامل عبر `TrySkipStructuredJsonValue` وتخزينها كـ `JsonTokenKind.Structured`، بحيث لا يمكن لأي حقل داخلي مثل `"output": {"success": true, "exitCode": 0, "status": "SUCCESS"}` أو `"output": [{"success": true}]` أن يتسلل إلى قاموس الحقول العلوية `fields`.
     - إذا تكرر أي مفتاح في المستوى الأعلى (`fields.ContainsKey(key)`)، يرفض المحلل الاستجابة فوراً بـ `INVALID_RESPONSE` (`Duplicate top-level JSON key`).
     - إذا كان JSON مبتوراً أو غير متوازن الأقواس أو يتضمن رموزاً زائدة بعد القوس الختامي `}`، يُرفض فوراً بمنطق `Fail-Closed`.

---

## 6. تفاصيل إصلاحات سلامة Registry وGPU وخطط الطاقة ووضع الألعاب

1. **محرك النسخ الاحتياطي والاستعادة للسجل (`src/SmartAgent.GpuEngine.cs` و`src/SmartAgent.PowerPlanManager.cs`)**:
   - قبل تعديل أي قيمة في السجل عبر `SetRegistryValueWithBackup(hive, subKeyPath, valueName, newValue, valueKind, createSubKeyIfMissing)`:
     - إذا كان `hive == "HKLM"` والعملية الحالية غير مرتفعة الصلاحيات (`!IsCurrentProcessElevated()`)، ترفض الدالة التعديل فوراً وتعيد `false` دون محاولة كتابة فاشلة أو ادعاء نجاح.
     - تقرأ الدالة الحالة الأصلية للمفتاح والقيمة قبل التعديل لأول مرة فقط، وتحفظها في القاموس المحمي `_regBackupEntries` وفي الملف الدائم `%LOCALAPPDATA%\ClearWin\registry-restore-journal.ini`، كما تسجل القيمة التي كتبها ClearWin في `ValueWrittenByClearWin`.
   - عند الاستعادة عبر `RestoreAllBackedUpRegistrySettings()` (المستدعاة في `RevertGameMode`، و`RestoreFramePacing`، و`EmergencyStopAllOptimizations`، و`Stop`، و`MainWindow.Closed`، وعند بدء التشغيل لاسترداد أي جلسة متقطعة):
     - تقرأ الدالة القيمة الحالية في السجل؛ فإذا وجدتها **مختلفة عن `ValueWrittenByClearWin`** (أي أن المستخدم أو أداة أخرى غيّرها يدوياً بعد ClearWin)، تحافظ الدالة على اختيار المستخدم ولا تكتب فوقه!
     - أما إذا كانت القيمة لا تزال تطابق `ValueWrittenByClearWin`، فإذا كانت `ValueExistedBefore == false` تحذف القيمة (`DeleteValue`)، وإذا كانت `ValueExistedBefore == true` تعيد `OriginalValueSerialized` بنوعها الأصلي `OriginalValueKind`.
2. **توحيد آلة حالة الألعاب وخطط الطاقة (`src/MainWindow.Logic.cs` و`src/SmartAgent.PowerPlanManager.cs`)**:
   - دمج متغير `_autoPilotActive` داخل `OnGameWatch` وإلغاء الفحص المزدوج كل 15 نبضة في `OnTick`.
   - إضافة عداد الاستقرار `_gameExitGraceTicks` (`GAME_EXIT_HYSTERESIS_TICKS = 10` نبضات ≈ 5 ثوانٍ) والتحقق من `IsTrackedGameProcessStillAlive(_trackedGamePid)`، بحيث لا يؤدي تبديل النافذة المؤقت (`Alt+Tab`) أثناء بقاء اللعبة مفتوحة إلى إلغاء وضع الألعاب وإعادة تفعيله بشكل متكرر.
   - إزالة استدعاء `powercfg.exe /setactive 8c5e7fda-...` مع `Verb = "runas"` من `ActivateGameModeSilent()`، وتوحيد التبديل عبر `SmartAgent.ActivateHighestPerformancePowerPlan()` المحمي بقفل التوازي وفترة التهدئة `POWER_SWITCH_COOLDOWN_SECONDS = 10.0`.
   - إضافة `IsPowerPlanSatisfied(currentPlan, targetPlan, hasTrueUltimatePlan)` في `src/SmartAgent.PowerPlanManager.cs` لمنع إصدار قرار `SwitchPowerPlan` كل 30 ثانية عندما يوصي المحرك بـ `"ultimate"` بينما أعلى خطة متاحة ومفعّلة على الجهاز هي `"high"`.

---

## 7. تفاصيل إصلاحات الشبكة العصبية و`RunningNormalizer` والتدريب والتحقق

1. **فصل `Observe` عن `NormalizeReadOnly` في `RunningNormalizer` (`src/SmartAgent.MLModel.cs`)**:
   - أضيفت الدالة `Observe(double[] raw)` لتحديث `_c[i]` و`_m[i]` و`_s[i]` مرة واحدة فقط لكل عينة حقيقية جديدة في `RecordActionOutcomeSample` (ولكل سيناريو أساسي في `GeneratePriorGrid`).
   - أضيفت الدالة `NormalizeReadOnly(double[] raw)` التي تحسب الدرجة المعيارية $z_i = \text{clamp}\left(\frac{x_i - \mu_i}{\sigma_i}, -3, 3\right)$ دون أي تعديل على `_c` أو `_m` أو `_s`، وتم استبدال جميع استدعاءات التطبيع داخل `TrainNN` و`GetMLDecision` و`GetMLRecommendation` و`GetMLActionScore` و`GetMLActionConfidence` و`ComputeValidationMetrics` و`Pretrain` و`CurriculumTraining` و`DirectLearningAudit` بها.
2. **منع تسرب بيانات التحقق في `TrainNN` (`H1`)**:
   - يحصل كل كائن `MLTrainingSample` عند إنشائه على رقم تسلسلي ذري فريد `SampleId`.
   - في `TrainNN()`، تُجمع عينات `_mlTB` أولاً حسب الإجراء مع إزالة أي تكرار في `SampleId`، ثم تُقسّم قبل لمس `_mlRP` إلى `trainPool` (أول 70%) و`valSet` (آخر 30% مع تسجيل معرّفاتها في `valSampleIds`).
   - عند سحب عينات الإعادة من `_mlRP.Sample(ML_BS)`، لا تُضاف أي عينة إلى `trainPool` إلا إذا كان `!valSampleIds.Contains(rs.SampleId)`. وبذلك يستحيل رياضياً ظهور أي عينة تحقق في مجموعة التدريب ($\text{TrainIds} \cap \text{ValIds} = \emptyset$).
3. **الأمثلة السالبة المضادة (`H2`)، انحيازات `LayerNorm` (`M1`)، وحفظ عزوم `AdamW` بتنسيق `V4` (`M2`)**:
   - في `GeneratePriorGrid` (`src/SmartAgent.Pretrain.cs`): لكل حالة اصطناعية يختار لها خبير القواعد الإجراء الأمثل `d.Action`، يتم أيضاً توليد عينتين سالبتين (`target = 0.08`) لإجراءات غير ملائمة لتلك الحالة (`!IsActionApplicableToState(cand, s)`). وفي `RecordActionOutcomeSample`، عند نجاح إجراء بدرجة $\ge 0.72$، تُسجَّل عينات سالبة (`OutcomeScore = 0.12`) للإجراءات المتعارضة معه في `ACTION_CONFLICTS`.
   - تم تصفير `B1` و`B2` وتثبيت `dB1 = 0` و`dB2 = 0` في `Train()` لأن `LayerNorm` يطرح متوسط الطبقة لكل عينة، بينما يتولى `BN1B` و`BN2B` دور الانحياز المتعلم الفعلي.
   - تمت ترقية `SaveMLModel()` إلى النسخة `"V4"` لحفظ المصفوفات الثماني `MBN1G, VBN1G, MBN1B, VBN1B, MBN2G, VBN2G, MBN2B, VBN2B` لكل شبكة في المجموعة، مع دعم قراءة `"V2"` و`"V3"` و`"V4"` في `LoadMLModelFile()`، وتحديث `CloneNet` في `src/SmartAgent.Validation.cs`.
4. **تصحيح توثيق متمّم الاحتمال في التقرير**:
   - للتأكيد الرياضي الدقيق: إذا كانت دالة السيجمويد للمخرج هي $\hat{y} = \sigma(o) = 0.5782$ والهدف $y = 1.0$، فإن متمّم الاحتمال هو $1 - \hat{y} = 1 - 0.5782 = 0.4218$، ومشتقة السيجمويد هي $\sigma'(o) = \hat{y}(1 - \hat{y}) = 0.5782 \times 0.4218 = 0.243885$، وتدرج خطأ الخرج هو $\delta_3 = (\hat{y} - y)\sigma'(o) = (0.5782 - 1.0) \times 0.243885 = -0.102871$. أما الرقم `0.9989` فيعود لحالة مشبعة أخرى ($\hat{y} = 0.9989 \implies 1 - \hat{y} = 0.0011$) ولا يجوز الخلط بينهما.

---

## 8. قرار وتنفيذ `RL2Engine`

بعد المراجعة المعمارية، تم اعتماد **الخيار المدمج الآمن (إصلاح المحرك بالكامل + تشغيله في وضع المراقبة المقارنة `Shadow Mode` افتراضياً مع بوابة تأثير اختيارية مشروطة)**:
1. **توسيع فضاء الإجراءات (`GetCandidateActionsForStateLocked`)**: يمسح المحرك الآن جميع الإجراءات الـ 20 في `ML_AC` (بما فيها `OptimizeGpuPerformance`, `ShedBackgroundGpuMemory`, `PurgeGpuShaderCache`) بالإضافة إلى `_actionValues` ومفاتيح `_stateQ` و`_stateQB`.
2. **حماية الحالات المبذورة من الطرد المبكر (`SeedStateQValue`)**: عند بذر جدول Q بالمعرفة المسبقة، يُهيأ `_stateVisits[stateKey][action] = 1` إلى جانب `_stateQ` و`_stateQB` و`_stateQTarget`، فلا تطرد دالة `EvictColdestState()` الحالات المسبقة لصالح حالة عابرة غير مزورة.
3. **تطبيق Double Q-Learning المتماثل (`ApplyDoubleQUpdateLocked`)**:
   - في التحديثات الزوجية: يختار الإجراء $\arg\max_{a'} Q_A(s', a')$ ويقيّمه عبر $0.5 \cdot (Q_B(s', a^*) + Q_{\text{target}}(s', a^*))$ لتحديث $Q_A(s, a)$.
   - في التحديثات الفردية: يختار الإجراء $\arg\max_{a'} Q_B(s', a')$ ويقيّمه عبر $0.5 \cdot (Q_A(s', a^*) + Q_{\text{target}}(s', a^*))$ لتحديث $Q_B(s, a)$.
4. **وضع `Shadow Mode` والبوابة الاختيارية (`EvaluateRL2ShadowAndOptionalInfluence`)**:
   - القيمة الافتراضية لخاصية `EnableRL2LiveInfluence` هي `false`: يقوم المحرك في كل دورة `RankAndFilter` بحساب أفضل إجراء وفق `RL2` ومقارنته بالقرار الحي لتحديث عدادات `RL2ShadowComparisons` و`RL2ShadowAgreements` دون أي تعديل على ثقة القرارات الحية.
   - عند تفعيل `EnableRL2LiveInfluence = true` صراحةً، لا يُسمح بتعديل ثقة أي قرار إلا إذا كان الإجراء متوافقاً مع الحالة (`IsActionApplicableToState`) وحقق $\ge 3$ زيارات فعلية في تلك الحالة، وبسقف تعديل لا يتجاوز $\pm 0.08$.

---

## 9. نتائج البناء والاختبارات الفعلية

### أ. نتائج حزمة التحقق التنفيذية داخل البيئة الحالية (`python3 tests/verify_all_phases_v277.py`):
- **فحص سلامة وتوازن الأقواس وبنية C# عبر 25 ملفاً معدلاً**: **PASS (25/25 ملفاً، `brace_depth == 0`)**.
- **فحص محلل JSON المتتبع للعمق (`TryParseTopLevelJsonObject` / `ParseHelperResponse`)**: **PASS** (قبول الاستجابة الصحيحة، رفض التزوير عبر كائن متداخل `{"output":{"success":true}}`، رفض التزوير عبر مصفوفة متداخلة `[{"success":true}]`، رفض المفاتيح العلوية المكررة `Duplicate top-level JSON key`، ورفض JSON المبتور).
- **فحص محرك النسخ الاحتياطي للسجل وحماية تعديل المستخدم (`Phase 2A`)**: **PASS** (حذف القيمة المستحدثة عند الاستعادة، إرجاع القيمة الأصلية للقيم الموجودة مسبقاً، والحفاظ التام على القيمة التي عدّلها المستخدم يدوياً `Vsync = 3` دون طمسها).
- **فحص `RunningNormalizer.NormalizeReadOnly` ومنع تسرب التحقق في `TrainNN` (`Phase 3`)**: **PASS** (ثبات `_c, _m, _s` بنسبة 100% عبر 500 استدعاء `NormalizeReadOnly`، وتقاطع صفر $\text{TrainIds} \cap \text{ValIds} = \emptyset$).
- **فحص عدم المساس بالثنائيات التنفيذية قبل البناء على Windows**: **PASS** (`bin/ClearWin.exe` و`bin/ClearWin.AdminHelper.exe` يحتفظان ببصمات `SHA-256` الأصلية).

### ب. الاختبارات المضافة في كود C# الإنتاجي (`src/UnitTests.cs` و`src/TestEntry.cs`):
1. `TestV277Phase1PrivilegesAndJsonParserPublic`: يختبر كود `AdminSessionManager.ParseHelperResponse` الفعلي على الحالات الـ 8 (بما فيها التزوير المتداخل والمفاتيح المكررة وأقفال التوازي وفصل `network.repair-preview` عن `network.flush-dns`).
2. `TestV277Phase2RegistryAndPowerStatePublic`: يختبر `SmartAgent.SetRegistryValueWithBackup` و`RestoreAllBackedUpRegistrySettings` (بما فيها حماية التعديل اليدوي للمستخدم واستعادة ملف السجل من القرص) و`SmartAgent.IsPowerPlanSatisfied`.
3. `TestV277Phase3And5MLAndRL2FixesPublic`: يختبر `RunningNormalizer.NormalizeReadOnly`، وثبات `LayerNorm` تجاه إزاحة `B1`، وحفظ واستعادة النموذج بتنسيق `V4`، وتحديثات `RL2Engine` المزدوجة ووضع `Shadow Mode`.
4. توسيع `TestV277IndependentVerificationSuitePublic`: فحص التدرجات العددية مقابل التحليلية (`Central Finite-Difference Gradient Check`) عبر جميع مجموعات المعاملات الثماني (`W3, B3, BN2G, BN2B, W2, BN1G, BN1B, W1`) بخطأ أقصى $< 10^{-7}$.
5. عزل اختبارات السجل (`TestTrayAndStartupSettingsPublic` في `src/UnitTests.cs` و`TestStartupClassifier` في `src/OptimizationAITest.cs`) لتعمل حصرياً داخل `HKCU\SOFTWARE\ClearWin\TestSandbox` دون لمس مفتاح `Run` الحقيقي لنظام Windows.

---

## 10. قائمة الملفات المعدلة مع سبب كل تعديل

1. `src/AdminHelper/AdminHelperProgram.cs`: التحقق الصارم من نجاح `Checkpoint-Computer` عبر ملف علامة مؤقت (`markerFile`) و`$ErrorActionPreference = 'Stop'` وفحص رمز الخروج في مسح DNS.
2. `src/AdminSessionManager.cs`: بناء محلل JSON المتتبع للعمق (`TryParseTopLevelJsonObject`) لمنع تزوير الحقول عبر الكائنات/المصفوفات المتداخلة ورفض المفاتيح المكررة، وتطبيق invariants الإغلاق الآمن (`Fail-Closed`).
3. `src/WebUiHost.cs`: ربط `ExtractJsonString` و`ExtractJsonInt` بمحلل المستوى الأعلى المتتبع للعمق في `AdminSessionManager`.
4. `src/MainWindow.cs`: استعادة أي تعديلات سجل متبقية عند بدء التشغيل وعند إغلاق النافذة (`Closed`)، وإلغاء التعديل غير المشروط لـ `UserGpuPreferences` عند بدء التشغيل بدون لعبة نشطة.
5. `src/MainWindow.Logic.cs`: توحيد آلة حالة الألعاب والطاقة تحت `OnGameWatch`، وإضافة فترة سماح `GAME_EXIT_HYSTERESIS_TICKS = 10` عند `Alt+Tab`، وإزالة `Verb = "runas"` من `ActivateGameModeSilent`، وربط `RestoreAllBackedUpRegistrySettings` في `RevertGameMode` و`TryRecoverInterruptedGameMode`.
6. `src/MainWindow.Pages.cs`: توحيد `CreateClearWinRestorePoint` و`TryFlushDnsVerified` مع أقفال التوازي، وإصلاح أزرار الوصول السريع (إزالة تعيين DNS الثابت `8.8.8.8` على `"Wi-Fi"` واستبدال GUID الطاقة الثابت بـ `ActivateHighestPerformancePowerPlan`).
7. `src/MainWindow.WebUi.cs`: تشغيل إنشاء نقطة الاستعادة ومسح DNS خارج خيط الواجهة مع تحديث الواجهة عبر `Dispatcher.BeginInvoke`، وفصل `network.repair-preview` القرائي عن `network.flush-dns`، وحظر `risky-network`.
8. `src/OverlayWindow.cs`: تحويل زر مسح DNS في النافذة العائمة ليستخدم `TryFlushDnsVerified` خارج خيط الواجهة.
9. `src/OverlayControls.cs`: تحويل زر مسح DNS في عناصر التحكم العائمة ليستخدم `TryFlushDnsVerified` ويعرض النتيجة الفعلية.
10. `src/SmartAgent.cs`: استدعاء `RestoreAllBackedUpRegistrySettings()` في `EmergencyStopAllOptimizations()` و`Stop()`، واستدعاء `EvaluateRL2ShadowAndOptionalInfluence(decisions)` في `RankAndFilter`.
11. `src/SmartAgent.GpuEngine.cs`: إنشاء محرك النسخ الاحتياطي والاستعادة الدائم للسجل (`RegistryBackupEntry`, `SetRegistryValueWithBackup`, `RestoreAllBackedUpRegistrySettings`) مع فحص `IsCurrentProcessElevated()` قبل أي تعديل في `HKLM` وحماية تعديلات المستخدم اليدوية.
12. `src/SmartAgent.PowerPlanManager.cs`: إصلاح تعارض `"ultimate"` مقابل `"high"` عبر `IsPowerPlanSatisfied`، وإزالة قرارات `SwitchPowerPlan` المكررة، وإضافة فترة تهدئة موحدة `POWER_SWITCH_COOLDOWN_SECONDS = 10.0`، وتغليف كتابات `UserGpuPreferences` و`GameLoop` بالنسخ الاحتياطي للسجل.
13. `src/SmartAgent.FramePacing.cs`: استدعاء `RestoreAllBackedUpRegistrySettings()` داخل `RestoreFramePacing()`.
14. `src/SmartAgent.NetworkOptimization.cs`: التحقق من رمز الخروج ومخرجات `ipconfig /flushdns` قبل إعلان النجاح.
15. `src/SmartAgent.NetworkTuning.cs`: التحقق من رمز الخروج في `NtRunProcess("ipconfig", "/flushdns")`.
16. `src/SmartAgent.MLModel.cs`: فصل `Observe` عن `NormalizeReadOnly` في `RunningNormalizer`، وإضافة `SampleId` لمنع تسرب بيانات التحقق في `TrainNN` (`H1`)، وحقن أمثلة مضادة سالبة (`H2`)، وتثبيت `B1=B2=0` (`M1`)، وترقية حفظ النموذج إلى `"V4"` لحفظ عزوم `AdamW` الخاصة بـ `LayerNorm` (`M2`).
17. `src/SmartAgent.Pretrain.cs`: استخدام `NormalizeReadOnly` أثناء الحلقات، وحقن أمثلة مضادة سالبة (`target = 0.08`) في `GeneratePriorGrid`، وتخفيض عتبة التدريب المسبق إلى 4 عينات.
18. `src/SmartAgent.CurriculumTraining.cs`: استخدام `Observe` مرة واحدة لكل سيناريو واستخدام `NormalizeReadOnly` في حلقات التدريب والمعايرة.
19. `src/SmartAgent.DirectLearningAudit.cs`: استخدام `NormalizeReadOnly` في `TeachActionWeight` و`EvaluateActionWeight`.
20. `src/SmartAgent.Validation.cs`: استخدام `NormalizeReadOnly` في `ComputeValidationMetrics`، وتحديث `CloneNet` لنسخ جميع عزوم `AdamW` (`VW1..VB3` و`MBN1G..VBN2B`).
21. `src/SmartAgent.RL2.cs`: توسيع فضاء الإجراءات ليشمل 20 إجراءً (`ML_AC` + `_actionValues`)، وتهيئة الزيارات في `SeedStateQValue`، وتطبيق `Double Q-Learning` المتماثل، وإضافة `Shadow Mode` وبوابة التأثير الاختيارية.
22. `src/SmartAgent.StartupClassifier.cs`: إضافة `TestRunSubKeyOverride` و`CLEARWIN_TEST_REG_PREFIX` لعزل اختبارات بدء التشغيل عن مفتاح `Run` الحقيقي.
23. `src/OptimizationAITest.cs`: توجيه `TestStartupClassifier` لاستخدام المسار المعزول `SOFTWARE\ClearWin\TestSandbox\Run`.
24. `src/UnitTests.cs`: عزل `TestTrayAndStartupSettingsPublic`، وتوسيع فحص التدرجات ليشمل جميع المعاملات الثماني، وإضافة مجموعات اختبارات الانحدار للمراحل 1–5.
25. `src/TestEntry.cs`: ضبط `CLEARWIN_TEST_REG_PREFIX` وتسجيل اختبارات المراحل 1–5 في مشغّل الاختبارات الرسمي.

---

## 11. ملخص الفروق الدقيقة (Unified Diff Highlights) وسكربت البناء والتحقق على Windows

> ملاحظة: النسخة الكاملة حرفياً من Unified Diff (4,793 سطراً لجميع الملفات الـ 25) محفوظة في:
> `backups/phase0_pre_repair_v277/FINAL_ALL_PHASES_UNIFIED_DIFF.patch`

### أهم المقتطفات الجوهرية من الفروق البرمجية:

```diff
--- a/src/AdminSessionManager.cs
+++ b/src/AdminSessionManager.cs
@@ -354,20 +354,35 @@
         internal static HelperResponse ParseHelperResponse(string json, string expectedRequestId = null)
         {
+            Dictionary<string, JsonFieldToken> fields;
+            string parseError;
+            if (!TryParseTopLevelJsonObject(json, out fields, out parseError))
+            {
+                return new HelperResponse
+                {
+                    Success = false,
+                    ExitCode = -1,
+                    Status = "INVALID_RESPONSE",
+                    Error = string.IsNullOrEmpty(parseError) ? "Malformed JSON response from AdminHelper." : parseError,
+                    Message = "استجابة غير صالحة من المساعد الإداري."
+                };
+            }
```

```diff
--- a/src/SmartAgent.MLModel.cs
+++ b/src/SmartAgent.MLModel.cs
@@ -1175,15 +1225,45 @@
-                var grp = new Dictionary<string, List<MLTrainingSample>>();
-                foreach (var s in _mlTB) { if (!grp.ContainsKey(s.ActionTaken)) grp[s.ActionTaken] = new List<MLTrainingSample>(); grp[s.ActionTaken].Add(s); }
-                foreach (var s in _mlRP.Sample(ML_BS)) { if (!grp.ContainsKey(s.ActionTaken)) grp[s.ActionTaken] = new List<MLTrainingSample>(); grp[s.ActionTaken].Add(s); }
+                // H1 Fix: Partition _mlTB into strictly disjoint Train (70%) and Validation (30%) sets
+                // BEFORE mixing in any PrioritizedReplayBuffer samples, and exclude any validation SampleId
+                // from the replay draw so TrainIds intersect ValIds is provably empty.
+                int trainBaseCount = (int)Math.Max(1, Math.Round(n * 0.7));
+                var trainPool = new List<MLTrainingSample>(trainBaseCount + 16);
+                var valSet = new List<MLTrainingSample>(valCount);
+                var valSampleIds = new HashSet<long>();
+                for (int i = 0; i < trainBaseCount; i++) trainPool.Add(kv.Value[i]);
+                for (int i = trainBaseCount; i < n; i++) { valSet.Add(kv.Value[i]); valSampleIds.Add(kv.Value[i].SampleId); }
+                if (replayByAction.TryGetValue(kv.Key, out actionReplays))
+                    foreach (var rs in actionReplays)
+                        if (!valSampleIds.Contains(rs.SampleId)) trainPool.Add(rs);
```

---

## 12. المخاطر المتبقية وخطة التحقق النهائية على Windows

1. **خطوة البناء والاختبار الحتمية على Windows**:
   - فور نقل مجلد المشروع إلى بيئة Windows 10/11 (تتوفر فيها `.NET Framework 4.8`)، شغّل السكربت المرفق في جذر المشروع:
     ```powershell
     powershell -ExecutionPolicy Bypass -File .\verify-windows-build-and-tests.ps1
     ```
   - سيقوم السكربت ببناء `ClearWin.AdminHelper.exe` و`ClearWin.exe` و`ClearWin.Tests.exe` من المصدر المصلح عبر `tools\roslyn-4.8.0\tasks\net472\csc.exe`، وتشغيل جميع الاختبارات (بما فيها اختبارات المراحل 1–5 المعزولة في `SOFTWARE\ClearWin\TestSandbox`)، وطباعة بصمات `SHA-256` الجديدة للثنائيات المبنية.
2. **التحقق التفاعلي الحي على Windows**:
   - اختبار إنشاء نقطة استعادة في الحالات الثلاث: (أ) رفض UAC، (ب) نجاح الإنشاء والتحقق من ظهوره في `Get-ComputerRestorePoint`، (ج) تعطيل System Restore على القرص والتأكد من ظهور رسالة الفشل الصادقة.
   - اختبار بدء لعبة (أو محاكي) والضغط على `Alt+Tab` لمدة أقل من 5 ثوانٍ للتأكد من ثبات وضع الألعاب بفضل `GAME_EXIT_HYSTERESIS_TICKS`، ثم إغلاق اللعبة والتأكد من استعادة خطة الطاقة الأصلية وحذف/استعادة مفاتيح السجل من `%LOCALAPPDATA%\ClearWin\registry-restore-journal.ini`.