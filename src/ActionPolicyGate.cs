using System;

namespace ClearWin
{
    public enum ActionRequester
    {
        User,
        SmartAgent,
        RuleEngine,
        Autopilot
    }

    public enum PolicyEvaluationStatus
    {
        Allowed,
        Blocked_RedTier,
        RequiresUserConfirmation_YellowTier,
        AutonomyDisabled_GreenTier,
        RequiresAdminElevation,
        UnregisteredAction,
        PreconditionFailed
    }

    public class PolicyEvaluationResult
    {
        public bool Allowed { get; set; }
        public PolicyEvaluationStatus Status { get; set; }
        public string ReasonAr { get; set; }
        public string ReasonEn { get; set; }
        public ActionCatalogItem Item { get; set; }

        public PolicyEvaluationResult(bool allowed, PolicyEvaluationStatus status, string reasonAr, string reasonEn, ActionCatalogItem item)
        {
            Allowed = allowed;
            Status = status;
            ReasonAr = reasonAr;
            ReasonEn = reasonEn;
            Item = item;
        }
    }

    /// <summary>
    /// Central Policy Enforcement Gate for ClearWin Autonomous Execution.
    /// Strictly arbitrates between AI proposals, User requests, and privileged execution.
    /// Enforces least privilege, defense-in-depth, and explicit user consent.
    /// </summary>
    public static class ActionPolicyGate
    {
        private static readonly object _sync = new object();
        private static bool _greenAutonomyEnabled = true; // Enabled by default for full automated execution of safe actions

        /// <summary>
        /// Explicit session toggle for autonomous Green execution.
        /// Defaults to TRUE on application launch for seamless autonomous optimization.
        /// </summary>
        public static bool GreenAutonomyEnabled
        {
            get { lock (_sync) return _greenAutonomyEnabled; }
        }

        public static void SetGreenAutonomy(bool enabled)
        {
            lock (_sync)
            {
                _greenAutonomyEnabled = enabled;
            }
            ClearWinAuditLogger.Log(
                "policy.autonomy_toggle",
                "Green",
                "User",
                "Normal",
                true,
                "SUCCESS",
                "VERIFIED",
                enabled ? "تم تفعيل استقلالية الأوامر الخضراء للجلسة الحالية صراحة من المستخدم."
                        : "تم إيقاف استقلالية الأوامر الخضراء التلقائية للجلسة الحالية.",
                0);
        }

        /// <summary>
        /// Evaluates whether an action is permitted to execute under current system policy.
        /// </summary>
        public static PolicyEvaluationResult Evaluate(
            string actionId,
            ActionRequester requester,
            bool userExplicitlyConfirmed,
            bool isAdminConnected)
        {
            if (string.IsNullOrWhiteSpace(actionId))
            {
                return new PolicyEvaluationResult(
                    false,
                    PolicyEvaluationStatus.UnregisteredAction,
                    "معرف الإجراء فارغ أو غير محدد.",
                    "ActionId is null or empty.",
                    null);
            }

            var item = ActionCatalog.GetItem(actionId);
            if (item == null)
            {
                ClearWinAuditLogger.Log(
                    actionId,
                    "Unknown",
                    requester.ToString(),
                    isAdminConnected ? "AdminHelper" : "Normal",
                    false,
                    "BLOCKED",
                    "FAILED",
                    "محاولة تنفيذ إجراء غير مسجل بكتالوج الأمان المركزي: " + actionId,
                    0);

                return new PolicyEvaluationResult(
                    false,
                    PolicyEvaluationStatus.UnregisteredAction,
                    "الإجراء '" + actionId + "' غير مسجل في كتالوج الأمان المركزي لـ ClearWin.",
                    "Action is not registered in central ActionCatalog.",
                    null);
            }

            bool isAutonomous = (requester == ActionRequester.SmartAgent ||
                                 requester == ActionRequester.Autopilot ||
                                 requester == ActionRequester.RuleEngine);

            // 1. RED TIER EVALUATION
            if (item.Tier == ActionTier.Red)
            {
                if (isAutonomous)
                {
                    // Check if there is an extreme hardware emergency where a protective throttle or emergency cool is essential to prevent silicon damage
                    bool isHardwareEmergency = false;
                    try
                    {
                        var agent = SmartAgent.Instance;
                        if (agent != null && agent.CurrentState != null && agent.CurrentState.CpuTemp >= 95.0)
                        {
                            if (actionId.Contains("emergency") || actionId.Contains("thermal") || actionId.Contains("cool") || actionId.Contains("throttle"))
                            {
                                isHardwareEmergency = true;
                            }
                        }
                    }
                    catch { }

                    if (!isHardwareEmergency)
                    {
                        ClearWinAuditLogger.Log(
                            item.ActionId,
                            "Red",
                            requester.ToString(),
                            isAdminConnected ? "AdminHelper" : "Normal",
                            false,
                            "BLOCKED",
                            "VERIFIED",
                            "حظر أمني قاطع: منع الذكاء الاصطناعي من تنفيذ إجراء عالي المخاطر (Red Tier): " + item.DisplayNameAr,
                            0);

                        return new PolicyEvaluationResult(
                            false,
                            PolicyEvaluationStatus.Blocked_RedTier,
                            "حظر أمني قاطع: الإجراء '" + item.DisplayNameAr + "' مصنف عالي المخاطر (Red) ومحظور تماماً من التنفيذ التلقائي أو الذاتي.",
                            "Red actions are strictly forbidden from autonomous execution.",
                            item);
                    }
                    else
                    {
                        ClearWinAuditLogger.Log(
                            item.ActionId,
                            "Red",
                            requester.ToString(),
                            isAdminConnected ? "AdminHelper" : "Normal",
                            true,
                            "EMERGENCY_OVERRIDE",
                            "VERIFIED",
                            "استثناء طوارئ عتادية قصوى: تم تمرير إجراء الحماية الحرارية تلقائياً لحماية العتاد من التلف (درجة الحرارة >= 95°C): " + item.DisplayNameAr,
                            0);

                        return new PolicyEvaluationResult(
                            true,
                            PolicyEvaluationStatus.Allowed,
                            "استثناء طوارئ عتادية قصوى: تم تمرير إجراء الحماية الحرارية تلقائياً لحماية العتاد من التلف (درجة الحرارة >= 95°C).",
                            "Emergency hardware protection permitted under thermal crisis.",
                            item);
                    }
                }

                // If user requested, verify explicit confirmation
                if (!userExplicitlyConfirmed)
                {
                    return new PolicyEvaluationResult(
                        false,
                        PolicyEvaluationStatus.RequiresUserConfirmation_YellowTier,
                        "يتطلب الإجراء عالي المخاطر '" + item.DisplayNameAr + "' موافقة أمنية صريحة وتأكيد نقطة الاستعادة.",
                        "High-risk action requires explicit user confirmation.",
                        item);
                }
            }

            // 2. YELLOW TIER EVALUATION
            if (item.Tier == ActionTier.Yellow)
            {
                if (isAutonomous)
                {
                    ClearWinAuditLogger.Log(
                        item.ActionId,
                        "Yellow",
                        requester.ToString(),
                        isAdminConnected ? "AdminHelper" : "Normal",
                        false,
                        "HELD_FOR_CONFIRMATION",
                        "VERIFIED",
                        "تم تعليق الإجراء المتوسط (Yellow Tier) في انتظار موافقة المستخدم المباشرة: " + item.DisplayNameAr,
                        0);

                    return new PolicyEvaluationResult(
                        false,
                        PolicyEvaluationStatus.RequiresUserConfirmation_YellowTier,
                        "الإجراء '" + item.DisplayNameAr + "' يغير إعدادات النظام ويتطلب موافقة يدوية منفصلة من المستخدم.",
                        "Yellow actions cannot execute autonomously and require explicit confirmation.",
                        item);
                }

                if (!userExplicitlyConfirmed)
                {
                    return new PolicyEvaluationResult(
                        false,
                        PolicyEvaluationStatus.RequiresUserConfirmation_YellowTier,
                        "يتطلب الإجراء '" + item.DisplayNameAr + "' تأكيد المستخدم قبل التطبيق.",
                        "Action requires explicit user confirmation.",
                        item);
                }
            }

            // 3. GREEN TIER EVALUATION
            if (item.Tier == ActionTier.Green)
            {
                if (isAutonomous)
                {
                    if (!GreenAutonomyEnabled)
                    {
                        ClearWinAuditLogger.Log(
                            item.ActionId,
                            "Green",
                            requester.ToString(),
                            isAdminConnected ? "AdminHelper" : "Normal",
                            true,
                            "HELD_AUTONOMY_OFF",
                            "VERIFIED",
                            "تم تعليق الإجراء الأخضر '" + item.DisplayNameAr + "' لأن خيار الاستقلالية التلقائية مغلق لهذه الجلسة.",
                            0);

                        return new PolicyEvaluationResult(
                            false,
                            PolicyEvaluationStatus.AutonomyDisabled_GreenTier,
                            "الاستقلالية التلقائية للأوامر الخضراء غير مفعّلة لهذه الجلسة. يمكنك تفعيلها من الإعدادات.",
                            "Green autonomy is disabled for this session.",
                            item);
                    }
                }
            }

            // 4. ADMIN PRIVILEGE EVALUATION
            if (item.RequiresAdmin && !isAdminConnected)
            {
                return new PolicyEvaluationResult(
                    false,
                    PolicyEvaluationStatus.RequiresAdminElevation,
                    "يتطلب الإجراء '" + item.DisplayNameAr + "' صلاحيات المسؤول. اضغط 'تفعيل وضع المسؤول' لطلب UAC الرسمي.",
                    "Action requires elevated admin helper.",
                    item);
            }

            // All checks passed
            return new PolicyEvaluationResult(
                true,
                PolicyEvaluationStatus.Allowed,
                "تمت الموافقة الأمنية على تنفيذ الإجراء: " + item.DisplayNameAr,
                "Action permitted by policy gate.",
                item);
        }
    }
}
