using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClearWin
{
    public class MetaProfile
    {
        public string ProfileKey;
        public int TotalTrials;
        public int TotalSuccesses;
        public double AvgOutcome;
        public string BestAction;
        public double BestScore;
        public List<double> RecentScores;
        public string PreferredEnsembleSeeds;
        public DateTime LastUpdated;

        public double SuccessRate { get { return TotalTrials > 0 ? (double)TotalSuccesses / TotalTrials : 0.5; } }

        public MetaProfile()
        {
            RecentScores = new List<double>();
            BestScore = 0;
            BestAction = "";
            AvgOutcome = 0.5;
            PreferredEnsembleSeeds = "";
        }
    }

    public partial class SmartAgent
    {
        private Dictionary<string, MetaProfile> _metaProfiles = new Dictionary<string, MetaProfile>();
        private List<string> _metaProfileKeys = new List<string>();
        private const int META_MAX_PROFILES = 50;
        private const int META_SAVE_INTERVAL = 120;

        public void InitializeMetaLearning()
        {
            LoadMetaProfiles();
        }

        private string GetMetaProfileKey()
        {
            int cpuB = (int)(_smoothCpu / 20);
            int ramB = (int)(_smoothRam / 20);
            int tempB = (int)(_smoothTemp / 15);
            int game = _currentState.IsGame ? 1 : 0;
            int bat = _isOnBattery ? 1 : 0;
            return string.Format("M{0}_{1}_{2}_G{3}_B{4}", cpuB, ramB, tempB, game, bat);
        }

        public void RecordMetaOutcome(string action, double score)
        {
            string key = GetMetaProfileKey();
            MetaProfile mp;
            if (!_metaProfiles.TryGetValue(key, out mp))
            {
                mp = new MetaProfile();
                mp.ProfileKey = key;
                _metaProfiles[key] = mp;
                _metaProfileKeys.Add(key);
                if (_metaProfileKeys.Count > META_MAX_PROFILES)
                {
                    string oldKey = _metaProfileKeys[0];
                    _metaProfileKeys.RemoveAt(0);
                    _metaProfiles.Remove(oldKey);
                }
            }

            mp.TotalTrials++;
            mp.TotalSuccesses += score >= 0.6 ? 1 : 0;
            mp.AvgOutcome = mp.AvgOutcome * 0.9 + score * 0.1;
            mp.LastUpdated = DateTime.Now;

            mp.RecentScores.Add(score);
            if (mp.RecentScores.Count > 20) mp.RecentScores.RemoveAt(0);

            if (score > mp.BestScore)
            {
                mp.BestScore = score;
                mp.BestAction = action;
            }

            TransferKnowledge(key, action, score);
        }

        public string GetMetaRecommendation()
        {
            string key = GetMetaProfileKey();
            MetaProfile mp;
            if (_metaProfiles.TryGetValue(key, out mp) && mp.TotalTrials >= 3 && mp.SuccessRate > 0.55)
                return mp.BestAction;

            // Try similar profiles
            string similar = FindSimilarProfile(key);
            if (similar != null)
            {
                MetaProfile sp;
                if (_metaProfiles.TryGetValue(similar, out sp) && sp.TotalTrials >= 5 && sp.SuccessRate > 0.6)
                    return sp.BestAction;
            }

            return null;
        }

        public double GetMetaConfidence()
        {
            string key = GetMetaProfileKey();
            MetaProfile mp;
            if (_metaProfiles.TryGetValue(key, out mp) && mp.TotalTrials >= 3)
            {
                double conf = mp.SuccessRate * 0.6 + Math.Min(1.0, mp.TotalTrials / 20.0) * 0.4;
                return Math.Max(0.3, Math.Min(0.98, conf));
            }
            return 0.3;
        }

        private string FindSimilarProfile(string key)
        {
            if (_metaProfileKeys.Count == 0) return null;
            string best = null; int bestSim = -1;
            string[] parts = key.Split('_', 'G', 'B');
            for (int i = 0; i < _metaProfileKeys.Count; i++)
            {
                string k = _metaProfileKeys[i];
                if (k == key) continue;
                string[] kp = k.Split('_', 'G', 'B');
                int sim = 0;
                for (int j = 0; j < Math.Min(parts.Length, kp.Length); j++)
                    if (parts[j] == kp[j]) sim++;
                if (sim > bestSim) { bestSim = sim; best = k; }
            }
            return best;
        }

        private void TransferKnowledge(string fromKey, string action, double score)
        {
            if (score < 0.7) return;
            foreach (var kv in _metaProfiles)
            {
                if (kv.Key == fromKey) continue;
                if (kv.Value.TotalTrials < 3 && score > 0.75)
                {
                    kv.Value.BestAction = action;
                    kv.Value.BestScore = Math.Max(kv.Value.BestScore, score * 0.9);
                }
            }
        }

        public string GetMetaSummary()
        {
            if (_metaProfiles.Count == 0) return "No meta-profiles yet";
            var sb = new StringBuilder();
            sb.AppendLine(string.Format("Meta-Profiles: {0}", _metaProfiles.Count));
            int count = 0;
            foreach (var kv in _metaProfiles)
            {
                if (count >= 5) break;
                count++;
                sb.AppendLine(string.Format("  {0}: rate={1:F1}% trials={2} best={3}",
                    kv.Key, kv.Value.SuccessRate * 100, kv.Value.TotalTrials, kv.Value.BestAction));
            }
            return sb.ToString();
        }

        private void SaveMetaProfiles()
        {
            try
            {
                string path = Path.Combine(_dataDir, "meta_profiles.dat");
                var sb = new StringBuilder();
                sb.AppendLine(_metaProfiles.Count.ToString());
                foreach (var kv in _metaProfiles)
                {
                    var mp = kv.Value;
                    sb.AppendLine(mp.ProfileKey);
                    sb.AppendLine(mp.TotalTrials.ToString());
                    sb.AppendLine(mp.TotalSuccesses.ToString());
                    sb.AppendLine(mp.AvgOutcome.ToString("F6"));
                    sb.AppendLine(mp.BestAction ?? "");
                    sb.AppendLine(mp.BestScore.ToString("F6"));
                    sb.AppendLine(mp.PreferredEnsembleSeeds ?? "");
                    sb.AppendLine(mp.LastUpdated.Ticks.ToString());
                    sb.AppendLine(mp.RecentScores.Count.ToString());
                    for (int i = 0; i < mp.RecentScores.Count; i++)
                        sb.AppendLine(mp.RecentScores[i].ToString("F6"));
                }
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex) { LogError("Meta.Save", ex); }
        }

        private void LoadMetaProfiles()
        {
            try
            {
                string path = Path.Combine(_dataDir, "meta_profiles.dat");
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                if (lines.Length < 1) return;
                int count = int.Parse(lines[0]); int idx = 1;
                for (int m = 0; m < count && idx < lines.Length; m++)
                {
                    var mp = new MetaProfile();
                    if (idx < lines.Length) mp.ProfileKey = lines[idx++]; else break;
                    if (idx < lines.Length) mp.TotalTrials = int.Parse(lines[idx++]); else break;
                    if (idx < lines.Length) mp.TotalSuccesses = int.Parse(lines[idx++]); else break;
                    if (idx < lines.Length) mp.AvgOutcome = double.Parse(lines[idx++]); else break;
                    if (idx < lines.Length) mp.BestAction = lines[idx++]; else break;
                    if (idx < lines.Length) mp.BestScore = double.Parse(lines[idx++]); else break;
                    if (idx < lines.Length) mp.PreferredEnsembleSeeds = lines[idx++]; else break;
                    if (idx < lines.Length) mp.LastUpdated = new DateTime(long.Parse(lines[idx++])); else break;
                    int rc = 0; if (idx < lines.Length) rc = int.Parse(lines[idx++]);
                    for (int r = 0; r < rc && idx < lines.Length; r++)
                        mp.RecentScores.Add(double.Parse(lines[idx++]));
                    _metaProfiles[mp.ProfileKey] = mp;
                    _metaProfileKeys.Add(mp.ProfileKey);
                }
            }
            catch (Exception ex) { LogError("Meta.Load", ex); }
        }
    }
}
