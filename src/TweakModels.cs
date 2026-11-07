using System;
using System.Collections.Generic;

namespace ClearWin
{
    public class TweakKnowledgeEntry
    {
        public string Id;
        public string Name;
        public string NameAr;
        public string Category;         // Thermal, CPU, Memory, Network, Battery, GPU, I/O, Display, Security
        public string FileType;         // Registry, Script, DLL, EXE, PowerShell, Command, Config
        public string Function;         // What it does (English)
        public string FunctionAr;       // What it does (Arabic)
        public double SafetyScore;      // 0.0 = dangerous, 1.0 = completely safe
        public string SafetyLevel;      // Safe, Moderate, Risky, Dangerous
        public double PerformanceImpact; // -1.0 = hurts performance, 0 = neutral, +1.0 = boosts
        public string ImpactArea;       // Gaming, General, Battery, Thermal, Network
        public List<string> UseCases;   // Gaming, Normal, Battery, Productivity
        public string Platform;         // Windows, Android, Both
        public string WindowsEquivalent;
        public string AndroidEquivalent;
        public string SourceModule;     // VoidBattery, FrierenAI, ClearWin, Community
        public List<string> ConflictsWith; // IDs of conflicting tweaks
        public List<string> EnhancesWith;  // IDs of complementary tweaks
        public string Condition;        // When to apply (JSON-like condition string)
        public DateTime FirstSeen;
        public DateTime LastUpdated;
        public int UsageCount;          // How many times this tweak was applied
        public double SuccessRate;      // 0.0 - 1.0
        public string RiskDescription;  // What could go wrong
        public string RevertMethod;     // How to undo
        public bool UserConfirmed;      // User verified this is safe
        public bool AutoApproved;       // AI auto-approved without user confirmation
    }

    public class TweakConflict
    {
        public string TweakId1;
        public string TweakId2;
        public string Reason;
        public string Severity; // Low, Medium, High, Critical
    }

    public class TweakCategory
    {
        public string Name;
        public string NameAr;
        public string Description;
        public string Icon;
        public int TweakCount;
        public double AverageSafety;
    }

    public class TweakAnalysisResult
    {
        public string FilePath;
        public string FileType;
        public string DetectedType;     // Registry, Script, DLL, EXE, PowerShell, etc.
        public string Function;
        public string FunctionAr;
        public double Confidence;        // How confident the analyzer is
        public string Category;
        public double SafetyScore;
        public List<string> Warnings;
        public List<string> Tags;
        public DateTime AnalyzedAt;
    }
}
