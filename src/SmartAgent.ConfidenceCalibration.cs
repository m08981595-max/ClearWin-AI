using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClearWin
{
    public partial class SmartAgent
    {
        private Dictionary<string, CalibrationBucket> _calibrationBuckets = new Dictionary<string, CalibrationBucket>();
        private DateTime _lastCalibration = DateTime.MinValue;
        private const int CALIBRATION_INTERVAL_SECONDS = 300;
        private double _overallCalibrationError = 0;
        private int _calibrationSamples = 0;
        private List<CalibrationRecord> _calibrationHistory = new List<CalibrationRecord>();

        public double GetCalibrationError() { return _overallCalibrationError; }
        public int GetCalibrationSamples() { return _calibrationSamples; }
        public List<CalibrationRecord> GetCalibrationHistory() { return new List<CalibrationRecord>(_calibrationHistory); }

        public void ResetCalibrationData()
        {
            _calibrationBuckets.Clear();
            _calibrationSamples = 0;
            _calibrationHistory.Clear();
            _overallCalibrationError = 0;
        }

        public void RecordConfidenceSample(string action, double predictedConfidence, bool actualSuccess)
        {
            int bucketIndex = (int)Math.Floor(predictedConfidence * 10 + 1e-6);
            bucketIndex = Math.Max(0, Math.Min(9, bucketIndex));
            string bucketKey = action + "_" + bucketIndex;

            CalibrationBucket bucket;
            if (!_calibrationBuckets.TryGetValue(bucketKey, out bucket))
            {
                bucket = new CalibrationBucket();
                bucket.Action = action;
                bucket.BucketIndex = bucketIndex;
                bucket.PredictedConfidence = (bucketIndex + 0.5) * 0.1;
                bucket.TotalSamples = 0;
                bucket.SuccessCount = 0;
                bucket.LastUpdated = DateTime.Now;
                _calibrationBuckets[bucketKey] = bucket;
            }

            bucket.TotalSamples++;
            if (actualSuccess) bucket.SuccessCount++;
            bucket.ActualSuccessRate = (double)bucket.SuccessCount / bucket.TotalSamples;
            bucket.CalibrationError = Math.Abs(bucket.PredictedConfidence - bucket.ActualSuccessRate);

            _calibrationSamples++;

            var record = new CalibrationRecord();
            record.Timestamp = DateTime.Now;
            record.Action = action;
            record.PredictedConfidence = predictedConfidence;
            record.ActualSuccess = actualSuccess;
            record.BucketError = bucket.CalibrationError;
            _calibrationHistory.Add(record);
            if (_calibrationHistory.Count > 500) _calibrationHistory.RemoveAt(0);
        }

        public double GetCalibratedConfidence(string action, double rawConfidence)
        {
            int bucketIndex = (int)Math.Floor(rawConfidence * 10 + 1e-6);
            bucketIndex = Math.Max(0, Math.Min(9, bucketIndex));
            string bucketKey = action + "_" + bucketIndex;

            CalibrationBucket bucket;
            if (_calibrationBuckets.TryGetValue(bucketKey, out bucket) && bucket.TotalSamples >= 5)
            {
                double adjustment = bucket.ActualSuccessRate - bucket.PredictedConfidence;
                return Math.Max(0.05, Math.Min(0.95, rawConfidence + adjustment * 0.5));
            }
            return rawConfidence;
        }

        public void RunCalibrationUpdate()
        {
            if ((DateTime.Now - _lastCalibration).TotalSeconds < CALIBRATION_INTERVAL_SECONDS) return;

            // Decay old calibration bucket counts (prevents permanent bias from early data)
            foreach (var kv in _calibrationBuckets)
            {
                double hoursSinceUpdate = (DateTime.Now - kv.Value.LastUpdated).TotalHours;
                double decayFactor = Math.Exp(-hoursSinceUpdate / 72.0); // half-life ~50 hours
                kv.Value.TotalSamples = Math.Max(1, (int)(kv.Value.TotalSamples * decayFactor));
                kv.Value.SuccessCount = Math.Max(0, (int)(kv.Value.SuccessCount * decayFactor));
                kv.Value.ActualSuccessRate = kv.Value.TotalSamples > 0
                    ? (double)kv.Value.SuccessCount / kv.Value.TotalSamples : 0.5;
                kv.Value.CalibrationError = Math.Abs(kv.Value.PredictedConfidence - kv.Value.ActualSuccessRate);
                kv.Value.LastUpdated = DateTime.Now;
            }

            double totalError = 0;
            int errorCount = 0;
            foreach (var kv in _calibrationBuckets)
            {
                if (kv.Value.TotalSamples >= 5)
                {
                    totalError += kv.Value.CalibrationError;
                    errorCount++;
                }
            }
            _overallCalibrationError = errorCount > 0 ? totalError / errorCount : 0;

            int wellCalibrated = 0;
            int poorlyCalibrated = 0;
            foreach (var kv in _calibrationBuckets)
            {
                if (kv.Value.TotalSamples >= 5)
                {
                    if (kv.Value.CalibrationError < 0.1) wellCalibrated++;
                    else poorlyCalibrated++;
                }
            }

            AddLog("CALIBRATE", "ConfidenceCalibration",
                string.Format("Error={0:F3} | Well-calibrated: {1} | Poorly-calibrated: {2} | Samples: {3}",
                _overallCalibrationError, wellCalibrated, poorlyCalibrated, _calibrationSamples));

            SaveCalibrationData();
            _lastCalibration = DateTime.Now;
        }

        private void SaveCalibrationData()
        {
            try
            {
                string path = Path.Combine(_dataDir, "calibration.txt");
                var sb = new StringBuilder();
                foreach (var kv in _calibrationBuckets)
                {
                    if (kv.Value.TotalSamples >= 3)
                    {
                        sb.AppendLine(string.Format("{0}|{1}|{2}|{3}|{4}",
                            kv.Key, kv.Value.PredictedConfidence, kv.Value.ActualSuccessRate,
                            kv.Value.TotalSamples, kv.Value.CalibrationError));
                    }
                }
                File.WriteAllText(path, sb.ToString());
            }
            catch (Exception ex) { LogError("SaveCalibrationData", ex); }
        }

        public void LoadCalibrationData()
        {
            try
            {
                string path = Path.Combine(_dataDir, "calibration.txt");
                if (!File.Exists(path)) return;
                string[] lines = File.ReadAllLines(path);
                foreach (string line in lines)
                {
                    string[] parts = line.Split('|');
                    if (parts.Length >= 5)
                    {
                        var bucket = new CalibrationBucket();
                        string[] keyParts = parts[0].Split('_');
                        if (keyParts.Length >= 2)
                        {
                            bucket.Action = keyParts[0];
                            int.TryParse(keyParts[1], out bucket.BucketIndex);
                        }
                        bucket.PredictedConfidence = double.Parse(parts[1]);
                        bucket.ActualSuccessRate = double.Parse(parts[2]);
                        int.TryParse(parts[3], out bucket.TotalSamples);
                        bucket.CalibrationError = double.Parse(parts[4]);
                        bucket.SuccessCount = (int)(bucket.ActualSuccessRate * bucket.TotalSamples);
                        // Stamp a fresh timestamp so the first refresh does not
                        // decay a just-loaded calibration into nothing (Math.Exp on
                        // a default(DateTime) gap is ~0 -> samples collapse to 1).
                        bucket.LastUpdated = DateTime.Now;
                        _calibrationBuckets[parts[0]] = bucket;
                    }
                }
                AddLog("CALIBRATE", "ConfidenceCalibration", "Loaded calibration data");
            }
            catch (Exception ex) { LogError("LoadCalibrationData", ex); }
        }

        public string GetCalibrationSummary()
        {
            return string.Format("Calibration error: {0:F3} | Samples: {1} | Buckets: {2}",
                _overallCalibrationError, _calibrationSamples, _calibrationBuckets.Count);
        }
    }
}
