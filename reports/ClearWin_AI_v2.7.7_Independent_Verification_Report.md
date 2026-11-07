# التقرير الهندسي النهائي للتحقق المستقل وتدقيق الرياضيات والأمان لمشروع ClearWin AI (الإصدار v2.7.7)

**تاريخ التحقق النهائي:** 23 أكتوبر 2026  
**الإصدار في ملفات المصدر والبيان:** `v2.7.7` (`2.7.7.0`)  
**حالة الملفات التنفيذية (`bin/*.exe`):** محتفظ بالنسخ التنفيذية الأصلية دون استبدال (لم تُبنَ في الحاوية الحالية لعدم وجود نظام Windows ومترجم `.NET Framework 4.x`).

---

## أولًا: إصلاح توثيق النتائج وتصحيح جدول الاحتمالات ومتمّماتها ($1 - p$)

### 1.1 تصحيح الخلط في صياغة متمّم الاحتمال للعينة السالبة $x_{\text{neg}}$
في الملخص السابق، وردت عبارة مختزلة بخصوص المتجه السالب $x_{\text{neg}}$ جمعت بين القيمة الابتدائية قبل التدريب (`0.5782`) ومتمّم القيمة النهائية بعد التدريب (`0.9989`) في جملة واحدة مما أوحى خطأً بأن `0.9989` هو متمّم `0.5782`.

**التصحيح الرياضي الدقيق:**
الشبكة العصبية `MLNeuralNetwork` تملك **مخرج Sigmoid قياسيًا واحدًا فقط** لكل إجراء:
$$p(x) = f_\theta(x) = \sigma(z_3(x)) = \frac{1}{1 + e^{-z_3(x)}} \in (0, 1)$$
يمثل $p(x)$ احتمالية ملاءمة الإجراء لحالة الدخل $x$، ويكون **متمّم الاحتمال لنفس المتجه $x$ في نفس اللحظة الزمنية** هو:
$$q(x) = 1 - p(x)$$

يوضح الجدول الآتي القيم الفعلية المحسوبة من التجربة (`seed = 123`، `lr = 0.12`، 25 دورة متناوبة بواقع 50 تحديثًا، دالة الخسارة $L(o, y) = \frac{1}{2}(o - y)^2$) ومتمّم كل قيمة على حدة قبل التدريب وبعده، سواء مع التقريب القديم (`Identity-BN`) أو بعد تطبيق الاشتقاق التحليلي الكامل لـ `LayerNorm`:

| وضع الاشتقاق الخلفي | متجه الدخل (18 ميزة) | الهدف $y$ | المخرج الابتدائي قبل التدريب $p_0(x)$ | متمّم المخرج الابتدائي $1 - p_0(x)$ | المخرج النهائي بعد 25 دورة $p_{25}(x)$ | متمّم المخرج النهائي $1 - p_{25}(x)$ |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: |
| **التمرير المحايد القديم (`dBN = dA`)** | المتجه الموجب $x_{\text{pos}}$ | `1.0` | `0.3985` | **`0.6015`** ($1 - 0.3985$) | `0.9982` | **`0.0018`** ($1 - 0.9982$) |
| **التمرير المحايد القديم (`dBN = dA`)** | المتجه السالب $x_{\text{neg}}$ | `0.0` | `0.5782` | **`0.4218`** ($1 - 0.5782$) | `0.0011` | **`0.9989`** ($1 - 0.0011$) |
| **اشتقاق `LayerNorm` التحليلي الكامل (`v2.7.7`)** | المتجه الموجب $x_{\text{pos}}$ | `1.0` | `0.3985` | **`0.6015`** ($1 - 0.3985$) | `1.0000` (`0.99996`) | **`0.0000`** (`0.00004`) |
| **اشتقاق `LayerNorm` التحليلي الكامل (`v2.7.7`)** | المتجه السالب $x_{\text{neg}}$ | `0.0` | `0.5782` | **`0.4218`** ($1 - 0.5782$) | `0.0001` (`0.00006`) | **`0.9999`** (`0.99994`) |

**خلاصة التفسير الرياضي:**
1. متمّم القيمة الابتدائية للعينة السالبة $p_0(x_{\text{neg}}) = 0.5782$ هو **`0.4218`** ($1 - 0.5782 = 0.4218$).
2. متمّم القيمة النهائية للعينة السالبة بعد التدريب $p_{25}(x_{\text{neg}}) = 0.0011$ هو **`0.9989`** ($1 - 0.0011 = 0.9989$).
3. لا يساوي $p_{25}(x_{\text{neg}}) = 0.0011$ متمّم $p_{25}(x_{\text{pos}}) = 1 - 0.9982 = 0.0018$ لأن كلاً منهما محسوب عند **متجه دخل مختلف** ($x_{\text{pos}} \neq x_{\text{neg}}$).
4. هذه التجربة على متجهين ثابتين تقيس **تقارب التحسين (In-Sample Optimization Convergence)** فقط، ولا تُعدّ دليلًا على التعميم (الذي يُقاس حصريًا في القسم 3.3 على مجموعة اختبار مستقلة).

---

## ثانيًا: طبيعة سكربت `tests/reproducible_verification_v277.py` مقابل كود C# الإنتاجي

عملاً بالشفافية الهندسية الصارمة:
1. **طبيعة الملف `tests/reproducible_verification_v277.py`:**
   - هذا الملف هو **نموذج مرجعي رياضي مستقل مكتوب بلغة Python 3 (`ReferenceMLNeuralNetwork`)** يحاكي معادلات `src/SmartAgent.MLModel.cs` و`src/SmartAgent.RL2.cs`، **ولا يقوم بتشغيل كود C# الإنتاجي المترجم نفسه**.
   - السبب: بيئة التدقيق الحالية تعمل على حاوية Linux (`x86_64 GNU/Linux`) لا تتضمن وقت تشغيل `.NET Framework 4.x` ولا مترجم Windows `csc.exe`.
   - لذلك، فإن نجاح `tests/reproducible_verification_v277.py` يثبت **الصحة الرياضية للخوارزميات والمشتقات التحليلية**، لكنه **لا يُعدّ بديلًا عن تشغيل اختبار C# المترجم على Windows**.
2. **اختبار التنفيذ الإنتاجي الفعلي المضاف في C# (`src/UnitTests.cs`):**
   - أضفنا الدالة العامة `ComputeAnalyticalGradients(...)` داخل الكلاس الإنتاجي الفعلي `MLNeuralNetwork` في `src/SmartAgent.MLModel.cs` (الأسطر 152–207).
   - وأضفنا في `src/UnitTests.cs` (ضمن `TestV277IndependentVerificationSuitePublic` المسجلة في `src/TestEntry.cs`) اختبارًا يستدعي كلاس C# الإنتاجي `MLNeuralNetwork` مباشرةً ويقارن تدرجاته التحليلية بالتدرجات العددية (`eps = 1e-6`) ويتحقق من `RL2Engine` وحراس `GpuEngine`؛ وينتظر هذا الاختبار تشغيله فعليًا على بيئة Windows عبر `.\build-tests.ps1`.

---

## ثالثًا: مراجعة `LayerNorm` واشتقاق المشتقات التحليلية واختبار الفروق المحدودة (Finite-Difference Check)

### 3.1 الخلل الرياضي الذي كشفه التدقيق في `LayerNorm` داخل الكود الأصلي
في `src/SmartAgent.MLModel.cs`:
- يحسب الانتشار الأمامي `BN` لكل طبقة مخفية ($N \in \{32, 16\}$):
  $$\mu = \frac{1}{N}\sum_{i=1}^N z_i, \quad \sigma^2 = \frac{1}{N}\sum_{i=1}^N (z_i - \mu)^2, \quad \text{invStd} = \frac{1}{\sqrt{\sigma^2 + \epsilon}}, \quad \hat{z}_i = (z_i - \mu)\,\text{invStd}, \quad n_i = \gamma_i \hat{z}_i + \beta_i$$
- لكن في الانتشار الخلفي القديم (`Train`)، كان الكود يضع `dBN2[i] = dA2[i]` و`dBN1[i] = dA1[i]` (تمرير محايد يتجاهل القسمة على $\sqrt{\sigma^2 + \epsilon}$ وطرح المتوسط $\mu$)، ولم يكن يحسب تدرجات معاملات القياس $\gamma$ (`BN1G`, `BN2G`) والانحياز $\beta$ (`BN1B`, `BN2B`)، كما لم يكن يصفر مشتقة `LeakyReLU` عند تشبع سقف التفعيل `ACAP = 4.0`.

### 3.2 الصيغة التحليلية الدقيقة التي تم تطبيقها في `src/SmartAgent.MLModel.cs`
بفرض أن $g_i = \frac{\partial L}{\partial n_i}$ هو التدرج الوارد من تفعيل `LeakyReLU` المقيد بـ `ACAP` (بحيث $g_i = 0$ إذا كانت الوحدة مسقطة بـ Dropout أو كانت $\text{LReLU}(n_i) > \text{ACAP}$):
1. **تدرجات معاملات القياس ($\gamma$) والانحياز ($\beta$):**
   $$\frac{\partial L}{\partial \gamma_i} = g_i \hat{z}_i, \qquad \frac{\partial L}{\partial \beta_i} = g_i$$
2. **تدرجات المدخلات قبل التطبيع ($z_i$) عبر مصفوفة جاكوبيان الكاملة لـ LayerNorm:**
   $$d\hat{z}_i = g_i \gamma_i, \qquad \frac{\partial L}{\partial z_i} = \text{invStd} \cdot \left( d\hat{z}_i - \frac{1}{N}\sum_{k=1}^N d\hat{z}_k - \hat{z}_i \cdot \frac{1}{N}\sum_{k=1}^N d\hat{z}_k \hat{z}_k \right)$$
تم تنفيذ هذه المعادلة في الدالة `LayerNormBackward` (`src/SmartAgent.MLModel.cs:131–149`) مع تحديث معاملات `BN1G`, `BN1B`, `BN2G`, `BN2B` بواسطة `AdamWStep`.

### 3.3 نتائج اختبار الفروق المحدودة المركزية (Central Finite-Difference Gradient Check)
باستخدام الفروق المحدودة المركزية خطوة $\varepsilon = 10^{-6}$:
$$g_{\text{num}}(\theta_k) = \frac{L(\theta_k + \varepsilon) - L(\theta_k - \varepsilon)}{2\varepsilon}$$
على الشبكة الكاملة (`18 -> 32 -> 16 -> 1`، بإجمالي `1,249` معاملًا، `seed = 77`)، جاءت مقارنة أقصى خطأ مطلق (`MaxAbsErr`) بين التدرج التحليلي والتدرج العددي قبل الإصلاح وبعده كالآتي:

| مجموعة المعاملات | عدد المعاملات | أقصى خطأ مطلق قديمًا (`Identity-BN`) | أقصى خطأ مطلق بعد الإصلاح (`Exact LayerNorm`) | حالة التطابق مع المشتقة العددية |
| :--- | :---: | :---: | :---: | :---: |
| **`W3`** (أوزان طبقة المخرج) | `16` | `2.718e-11` | **`2.718e-11`** | مطابق لدقة الآلة ($< 10^{-10}$) |
| **`W2`** (أوزان الطبقة المخفية الثانية) | `512` | `7.494e-02` (خطأ جسيم) | **`4.229e-11`** | مطابق لدقة الآلة ($< 10^{-10}$) |
| **`W1`** (أوزان الطبقة المخفية الأولى) | `576` | `6.657e-02` (خطأ جسيم) | **`6.267e-11`** | مطابق لدقة الآلة ($< 10^{-10}$) |
| **`BN2G`** (معامل قياس $\gamma_2$) | `16` | `3.313e-02` (كان مهملاً = 0) | **`3.450e-11`** | مطابق لدقة الآلة ($< 10^{-10}$) |
| **`BN2B`** (معامل انحياز $\beta_2$) | `16` | `3.310e-02` (كان مهملاً = 0) | **`2.599e-11`** | مطابق لدقة الآلة ($< 10^{-10}$) |
| **`BN1G`** (معامل قياس $\gamma_1$) | `32` | `3.909e-02` (كان مهملاً = 0) | **`3.232e-11`** | مطابق لدقة الآلة ($< 10^{-10}$) |
| **`BN1B`** (معامل انحياز $\beta_1$) | `32` | `4.186e-02` (كان مهملاً = 0) | **`4.076e-11`** | مطابق لدقة الآلة ($< 10^{-10}$) |
| **`B2`** (انحياز ما قبل التطبيع 2) | `16` | `4.786e-02` (خطأ جسيم) | **`3.536e-11`** | مطابق لدقة الآلة ($\sum dZ_2 = 0$) |
| **`B1`** (انحياز ما قبل التطبيع 1) | `32` | `5.121e-02` (خطأ جسيم) | **`3.733e-11`** | مطابق لدقة الآلة ($\sum dZ_1 = 0$) |

### 3.4 إعادة اختبار التعميم على بيانات مستقلة بعد تصحيح مشتقات `LayerNorm`
في النموذج المرجعي (`Test 3` في `tests/reproducible_verification_v277.py`)، أعيد اختبار `MLEnsemble` (3 شبكات، 12 حقبة، `lr = 0.08`) مع الفصل التام بين مجموعة التدريب (`N = 80, seed = 2026`) ومجموعة الاختبار المستقلة (`N = 80, seed = 99999`) وتجميد إحصاءات `RunningNormalizer`:
- **مجموعة التدريب (`N = 80`):** ارتفعت الدقة من `47.5%` (الخسارة `0.1345`) إلى **`100.0%`** (الخسارة `0.0007`، `TP=40, FP=0, TN=40, FN=0`).
- **مجموعة الاختبار المستقلة (`N = 80`):** ارتفعت الدقة من `43.8%` (الخسارة `0.1313`) إلى **`100.0%`** (الخسارة `0.0013`، `TP=40, FP=0, TN=40, FN=0`)، حيث اختفى الخطأ الموجب الكاذب الوحيد (`FP = 1 -> 0`) الذي كان يظهر قبل تصحيح مشتقة `LayerNorm`.

---

## رابعًا: التوثيق الدقيق لحالة `RL2Engine` وعزله عن مسار القرار الحي

1. **مسار القرار الحي لا يقرأ جدول `_stateQ` حاليًا:**
   - أثبت فحص جميع استدعاءات `_stateQ` و`GetStateQValue` و`SelectBestStateAction` عبر كامل ملفات `src/*.cs` أن `AnalyzeAndDecide()` و`RankAndFilter()` و`GetExperienceModifier()` **لا تستدعي أي دالة تقرأ من `_stateQ`**.
   - الاستدعاء الوحيد لـ `SelectBestStateAction` في كود التشغيل يقع داخل `GetRL2Summary()` (`src/SmartAgent.RL2.cs:306`) لأغراض العرض النصي فقط.
   -したがって، **لا يؤثر `RL2Engine` حاليًا في قرارات تحسين ClearWin الحية**. وقد وثقنا ذلك باختبار صريح في `src/UnitTests.cs` (`TestV277IndependentVerificationSuitePublic` — البند 5) يبذر قيمة قصوى `SeedStateQValue(liveStateKey, "BatterySaver", 99.0)` ويثبت أنه بينما تتغير مخرجات `SelectBestStateAction(liveStateKey)` إلى `"BatterySaver"`، فإن قرار `GetOptimizationDecisionForState(simState)` لا يتغير إطلاقًا.
2. **تتبّع قيمة $\gamma$ وتحديث واستخدام `Q1` (`_stateQ`) و`Q2` (`_stateQB`) و`QTarget` (`_stateQTarget`):**
   - المعامل `GAMMA` معرّف في `src/SmartAgent.RL2.cs:28` بالقيمة **`0.9`**.
   - **الجدول الأول `Q1` (`_stateQ`):** يُحدّث في `RecordRLExperience` (السطر 183) وفي `ReplayTrainRL2` (السطر 247) باستخدام هدف الفروق الزمنية (TD Target) بمعامل خصم $\gamma = 0.9$:
     $$\text{tdTarget} = r + 0.9 \cdot Q_{\text{target}}\!\left(s', \arg\max_{a' \in \text{\_actionValues}} Q_1(s', a')\right)$$
     $$Q_1(s, a) \leftarrow Q_1(s, a) + \alpha \cdot (\text{tdTarget} - Q_1(s, a))$$
     ويُستخدم `Q1` في `GetArgMaxAction(nextStateKey)` وفي `SelectBestStateAction(stateKey)`.
   - **جدول الهدف `QTarget` (`_stateQTarget`):** يُحدّث كل `25` استدعاءً لـ `RecordRLExperience` (`UpdateStateQTargets`، السطر 266) عبر المتوسط المتحرك الأسي بمعامل $\tau_2 = 0.01$:
     $$Q_{\text{target}}(s, a) \leftarrow 0.99 \cdot Q_{\text{target}}(s, a) + 0.01 \cdot Q_1(s, a)$$
     ويُستخدم لتقييم الفعل المختار $a^*$ في الحالة التالية $s'$.
   - **الجدول الثاني `Q2` (`_stateQB`):** يُحدّث في السطر 187 نحو المكافأة الفورية فقط (أي بمعامل خصم فعلي $\gamma = 0$ ودون تبديل عشوائي):
     $$Q_2(s, a) \leftarrow Q_2(s, a) + \alpha \cdot (r - Q_2(s, a))$$
     **استخدام `Q2` الفعلي:** يُقرأ فقط في السطر 186 لحساب التحديث الذاتي وفي السطر 326 لحفظه في ملف `rl2.dat`، **ولا يُقرأ في أي دالة اختيار أو تقييم أخرى**.

---

## خامسًا: سلامة الاختبارات ومنع التغييرات الفعلية على نظام Windows أثناء الاختبار

تم فحص جميع ملفات الاختبار (`src/UnitTests.cs`, `src/OptimizationAITest.cs`, `src/NeuralNetworkTest.cs`, `src/TestEntry.cs`) للتأكد من عدم تنفيذ أي تعديلات ضارة أو دائمة على نظام Windows عند تشغيل `build-tests.ps1`:

1. **حماية كاش التظليل (`Shader Cache`) وذاكرة الفيديو (`VRAM`) وخطة الطاقة (`powercfg`) وسجل الألعاب:**
   - في `src/SmartAgent.GpuEngine.cs` (الأسطر 36، 151، 268): تتحقق الدوال `ExecuteOptimizeGpuPerformance` و`ExecuteShedBackgroundGpuMemory` و`ExecutePurgeGpuShaderCache` في بدايتها من الشرط:
     ```csharp
     if (DisableRealExecution || _simulationMode) { ... return true; }
     ```
     فلا يتم لمس أي ملف في `D3DSCache` أو `NVIDIA\DXCache` أو `AMD\DxCache`، ولا يتم استدعاء `EmptyWorkingSet`، ولا يتم تعديل `HKLM` أو `HKCU\Software\Microsoft\Direct3D` أو `UserGpuPreferences` أثناء المحاكاة والاختبارات.
   - في `src/SmartAgent.PowerPlanManager.cs` (الأسطر 483، 553، 646، 691): تتحقق الدوال `RestoreBalancedOrPreviousPowerPlan` و`ActivateHighestPerformancePowerPlan` و`OptimizeGameLoopSettings` و`EnforceGameDedicatedGpuAndPower` من `if (DisableRealExecution)` فتمنع تشغيل `powercfg.exe` وتمنع تعديل `UserGpuPreferences` أثناء الاختبار. كما استُبدل `CreateSubKey` بـ `OpenSubKey` في `OptimizeGameLoopSettings` لمنع إنشاء مفاتيح `HKCU\Software\Tencent\*` على الأجهزة التي لا تحتوي على GameLoop.
   - في `src/UnitTests.cs`: تم تفعيل `SmartAgent.DisableRealExecution = true` و`agent.EnableSimulation(...)` داخل `try ... finally` في اختبارات `TestGameDedicatedGpuAndUltimatePowerPublic` و`TestGpuBoostAndVramTrimExecutionPublic` و`TestShaderCachePurgeSafetyPublic`.
2. **حماية اختبارات سجل بدء التشغيل (`HKCU\...\Run`) من ترك مخلفات عند الفشل:**
   - في `src/UnitTests.cs:2898` (`TestTrayAndStartupSettingsPublic`) وفي `src/OptimizationAITest.cs:811` (`TestStartupClassifier`): تم تغليف إنشاء وحذف القيمة الاختبارية المؤقتة (`ClearWin_Test_Startup` / `ClearWin_Test_<guid>`) بكتلة `try ... finally` تضمن حذف القيمة الاختبارية من `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Run` ومن `HKCU\Software\ClearWin\StartupBackup\HKCU` حتى لو فشل أي شرط تحقق داخل الاختبار.
3. **حماية اختبارات سكربتات PowerShell (`TestTweakScriptsSafetyAndBackupEngine` في `src/UnitTests.cs:2150`):**
   - تعمل جميع اختبارات السكربتات مع تعيين `CLEARWIN_TEST_MODE = "1"` و`CLEARWIN_TEST_REG_PREFIX` إلى مسار سجل معزول مؤقت `HKCU:\Software\ClearWin_AutomatedTest_Sandbox_<Guid>` ويُحذف بالكامل في كتلة `finally`.

---

## سادسًا: مصفوفة التحقق النهائية (التفريق الصارم بين المراحل الأربع)

يفصل الجدول التالي بدقة بين: **(1) فحص المصدر (Source Audit)**، **(2) الاختبار التنفيذي (Executable Test)**، **(3) البناء (Build)**، و**(4) التشغيل الفعلي على Windows (Live Windows Runtime)**:

| المكون / الإصلاح | 1. فحص المصدر (Source Audit) | 2. الاختبار التنفيذي (Executable Verification) | 3. البناء (`build.ps1` / `csc.exe`) | 4. التشغيل الفعلي على Windows |
| :--- | :--- | :--- | :--- | :--- |
| **النسخة الاحتياطية `backups/pre_v2.7.7_audit_backup/` وعدم المساس بـ `bin/*.exe`** | **مُتحقق** (`130` ملف C# + جميع الموارد) | **مُتحقق فعليًا** (مطابقة بصمات SHA-256 بنسبة 100% مع الأرشيف الأصلي) | لا ينطبق | جاهز للاستعادة الفورية عند الحاجة |
| **اشتقاق `LayerNorm` التحليلي الكامل وتحديث `BN1G/B` و`BN2G/B` وحماية `NaN`/`Infinity`** | **مُتحقق في المصدر** (`src/SmartAgent.MLModel.cs:103–240`) | **مُتحقق في النموذج الرياضي المرجعي** (`tests/reproducible_verification_v277.py`: خطأ الفروق المحدودة $< 6.3 \times 10^{-11}$، ودقة التعميم `100.0%` على `N=80` عينة اختبار مستقلة). **كود C# في `UnitTests.cs` ينتظر البناء على Windows** | **لم يُنفّذ في الحاوية الحالية** (يتطلب Windows + `.NET Framework 4.x`) | **يحتاج إلى تشغيل `.\build-tests.ps1` على Windows** |
| **توثيق وعزل `RL2Engine` ($\gamma=0.9$ لـ `Q1`، $\gamma=0$ لـ `Q2`، سقف `400` حالة، وعدم قراءته في `RankAndFilter`)** | **مُتحقق في المصدر** (`src/SmartAgent.RL2.cs:20–270`, `SmartAgent.cs:3079`) | **مُتحقق في النموذج المرجعي** (طرد الحالات الباردة عند `400` حالة) + **مُصاغ في `UnitTests.cs` بانتظار البناء على Windows** | **لم يُنفّذ في الحاوية الحالية** | **يحتاج إلى تشغيل `.\build-tests.ps1` على Windows** |
| **بوابات الأمان (`WebUiHost`, `LocalWebBridge`, `ActionCatalog`, `AdminHelperProgram`)** | **مُتحقق في المصدر** (`130/130` ملف C# خالٍ من أخطاء الترقيم، و`node --check` لواجهة WebUI بـ `0` أخطاء) | **مُتحقق منطقيًا في Python/Node.js** + **مُصاغ في `UnitTests.cs` بانتظار البناء على Windows** | **لم يُنفّذ في الحاوية الحالية** | **يحتاج إلى تشغيل `.\build.ps1` و`.\build-tests.ps1` على Windows** |
| **حماية `GpuEngine` و`PowerPlanManager` وسلامة اختبارات `UnitTests.cs` من تعديل النظام أو حذف `Shader Cache`** | **مُتحقق في المصدر** (`SmartAgent.GpuEngine.cs`, `PowerPlanManager.cs`, `UnitTests.cs`, `OptimizationAITest.cs`) | **مُتحقق بالكود المصدري عبر حراس `DisableRealExecution` و`_simulationMode` وكتل `try/finally`** | **لم يُنفّذ في الحاوية الحالية** | **يحتاج إلى تشغيل `.\build-tests.ps1` على Windows** |
| **سكربتات التعديلات والنسخ الاحتياطي (`TweakBackupEngine.ps1`, `ultimate-performance-plan`, `remove-onedrive`, `install.ps1`)** | **مُتحقق في المصدر** (`86/86` سكربت PowerShell مفحوص التركيب) | **لم يُشغّل محرك PowerShell الفعلي** لعدم وجود `powershell.exe` / `pwsh` في حاوية Linux | لا يتطلب ترجمة (تُقرأ نصيًا من `Resources/tweaks/`) | **يحتاج إلى تجربة على Windows حقيقي** |
