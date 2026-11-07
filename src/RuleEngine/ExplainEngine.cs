using System;
using System.Collections.Generic;

namespace ClearWin.RuleEngine
{
    /// <summary>
    /// Explain Engine: produces clear, bilingual (English + Arabic)
    /// explanations for every decision so the user always understands
    /// WHY the system is acting, WHAT will happen, and WHICH alternatives
    /// were weighed.
    /// </summary>
    public class ExplainEngine
    {
        public ExpertExplanation Explain(CandidateAction action, ExpertDiagnosis diagnosis,
            List<string> reasoningChain, List<CandidateAction> alternatives)
        {
            var explanation = new ExpertExplanation { ActionId = action != null ? action.Id : "" };
            if (action == null)
            {
                explanation.Title = "No action";
                explanation.WhyNeeded = "The system is healthy; no optimization is required.";
                explanation.WhyNeededAr = "النظام بحالة جيدة؛ لا حاجة لأي تحسين.";
                return explanation;
            }

            string metricLine = diagnosis != null && !string.IsNullOrEmpty(diagnosis.Symptom)
                ? diagnosis.Symptom
                : "detected system pressure";
            string metricLineAr = SymptomAr(diagnosis);

            explanation.Title = action.Name;
            explanation.TitleAr = action.NameAr;
            explanation.WhyNeeded = string.Format("{0} because: {1}.", action.Description, metricLine);
            explanation.WhyNeededAr = string.Format("{0} لأن: {1}.", action.WhyItHelpsAr, metricLineAr);
            explanation.WhatWillHappen = string.Format("Apply '{0}' targeting {1}.", action.Name, action.Category);
            explanation.WhatWillHappenAr = string.Format("سيتم تنفيذ '{0}' لمعالجة {1}.", action.NameAr, CategoryAr(action.Category));
            explanation.ExpectedBenefit = string.Format("Expected improvement ~{0:F0}/100 with {1:F0}% historical confidence.",
                action.ExpectedImpact, action.Confidence * 100);
            explanation.ExpectedBenefitAr = string.Format("التحسن المتوقع ~{0:F0}/100 مع ثقة {1:F0}% من التاريخ.",
                action.ExpectedImpact, action.Confidence * 100);
            explanation.RiskAssessment = string.Format("{0} risk (score {1:F2}), {2}.",
                RiskLabel(action.Risk), action.Risk,
                action.Reversible ? "fully reversible" : "NOT reversible - needs approval");
            explanation.RiskAssessmentAr = string.Format("مخاطرة {0} (نسبة {1:F2})، {2}.",
                RiskLabelAr(action.Risk), action.Risk,
                action.Reversible ? "قابلة للتراجع بالكامل" : "غير قابلة للتراجع - تتطلب موافقة");
            explanation.Confidence = action.Confidence;
            explanation.ChosenReason = BuildChosenReason(action, diagnosis, alternatives);
            explanation.ChosenReasonAr = BuildChosenReasonAr(action, diagnosis, alternatives);

            if (alternatives != null)
            {
                for (int i = 0; i < alternatives.Count; i++)
                {
                    if (alternatives[i] != null && alternatives[i].Id != action.Id)
                        explanation.AlternativesConsidered.Add(alternatives[i].Name);
                }
            }

            if (reasoningChain != null)
            {
                for (int i = 0; i < reasoningChain.Count; i++)
                    explanation.ReasoningChain.Add(reasoningChain[i]);
            }
            if (diagnosis != null)
            {
                for (int i = 0; i < diagnosis.Evidence.Count && i < 6; i++)
                    explanation.Evidence.Add(diagnosis.Evidence[i]);
            }
            return explanation;
        }

        private string BuildChosenReason(CandidateAction action, ExpertDiagnosis diagnosis, List<CandidateAction> alternatives)
        {
            string best = "";
            double bestScore = -1;
            if (alternatives != null)
            {
                for (int i = 0; i < alternatives.Count; i++)
                {
                    if (alternatives[i] != null && alternatives[i].Score > bestScore)
                    {
                        bestScore = alternatives[i].Score;
                        best = alternatives[i].Name;
                    }
                }
            }
            string symptom = diagnosis != null && !string.IsNullOrEmpty(diagnosis.Symptom) ? diagnosis.Symptom : "detected pressure";
            string evidence = !string.IsNullOrEmpty(action.Evidence) ? action.Evidence : symptom;
            string baseReason = string.Format("Selected '{0}' because {1}. It targets {2} with {3:F0}% historical confidence and expected impact {4:F0}/100.",
                action.Name, LowerFirst(evidence), symptom, action.Confidence * 100, action.ExpectedImpact);
            if (bestScore >= 0)
                baseReason += string.Format(" It beat the nearest alternative '{0}' ({1:F0}/100) on overall score.", best, bestScore);
            return baseReason;
        }

        private string BuildChosenReasonAr(CandidateAction action, ExpertDiagnosis diagnosis, List<CandidateAction> alternatives)
        {
            string symptomAr = SymptomAr(diagnosis);
            string evidence = !string.IsNullOrEmpty(action.Evidence) ? action.Evidence : (diagnosis != null ? diagnosis.Symptom : "مشكلة مكتشفة");
            string baseReason = string.Format("تم اختيار '{0}' لأن {1}. يستهدف المشكلة ({2}) بنجاح تاريخي {3:F0}% وتأثير متوقع {4:F0}/100.",
                action.NameAr, LowerFirst(evidence), symptomAr, action.Confidence * 100, action.ExpectedImpact);
            if (alternatives != null && alternatives.Count > 0)
                baseReason += " وتفوّق على البدائل الأخرى في تقييم الفائدة مقابل المخاطرة.";
            return baseReason;
        }

        private static string LowerFirst(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToLowerInvariant(s[0]) + s.Substring(1);
        }

        /// <summary>Arabic label for the primary symptom (real translation, not a code string).</summary>
        public static string SymptomAr(ExpertDiagnosis diagnosis)
        {
            if (diagnosis == null || string.IsNullOrEmpty(diagnosis.Symptom)) return "انخفاض أداء النظام";
            string s = diagnosis.Symptom.ToLowerInvariant();
            if (s.IndexOf("cpu usage") >= 0 || s.IndexOf("cpu load") >= 0) return "ارتفاع استهلاك المعالج";
            if (s.IndexOf("thermal") >= 0 || s.IndexOf("heat") >= 0) return "إجهاد حراري";
            if (s.IndexOf("memory") >= 0 || s.IndexOf("ram") >= 0) return "ضغط على الذاكرة";
            if (s.IndexOf("gpu") >= 0) return "ارتفاع حمل كرت الشاشة";
            if (s.IndexOf("frame") >= 0 || s.IndexOf("fps") >= 0) return "انخفاض أو عدم ثبات معدل الإطارات";
            if (s.IndexOf("disk") >= 0) return "اختناق أو ضغط على القرص";
            if (s.IndexOf("battery") >= 0) return "استنزاف البطارية";
            if (s.IndexOf("network") >= 0) return "تشبع الشبكة";
            if (s.IndexOf("startup") >= 0) return "تضخم برامج بدء التشغيل";
            if (s.IndexOf("service") >= 0) return "مشاكل في الخدمات الحرجة";
            if (s.IndexOf("power plan") >= 0) return "خطة طاقة غير مثالية";
            return diagnosis.Symptom;
        }

        private static string CategoryAr(string category)
        {
            if (category == null) return "مشكلة النظام";
            string c = category.ToLowerInvariant();
            if (c.IndexOf("memory") >= 0) return "الذاكرة";
            if (c.IndexOf("thermal") >= 0) return "الحرارة";
            if (c.IndexOf("cpu") >= 0 || c.IndexOf("process") >= 0) return "المعالج";
            if (c.IndexOf("gpu") >= 0) return "كرت الشاشة";
            if (c.IndexOf("disk") >= 0) return "القرص";
            if (c.IndexOf("power") >= 0) return "الطاقة";
            if (c.IndexOf("network") >= 0) return "الشبكة";
            if (c.IndexOf("startup") >= 0) return "بدء التشغيل";
            if (c.IndexOf("service") >= 0) return "الخدمات";
            if (c.IndexOf("game") >= 0) return "الألعاب";
            return category;
        }

        public static string RiskLabel(double risk)
        {
            if (risk >= RuleEngineConstants.RiskCritical) return "Critical";
            if (risk >= RuleEngineConstants.RiskRequiresApproval) return "Moderate-High";
            if (risk >= 0.4) return "Moderate";
            if (risk >= 0.2) return "Low";
            return "Very low";
        }

        public static string RiskLabelAr(double risk)
        {
            if (risk >= RuleEngineConstants.RiskCritical) return "حرجة";
            if (risk >= RuleEngineConstants.RiskRequiresApproval) return "متوسطة-عالية";
            if (risk >= 0.4) return "متوسطة";
            if (risk >= 0.2) return "منخفضة";
            return "منخفضة جدًا";
        }
    }
}
