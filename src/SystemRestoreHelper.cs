using System;
using System.Management;

namespace ClearWin
{
    /// <summary>
    /// شبكة الأمان قبل أي تعديل للنظام - نقطة استعادة النظام
    /// يتطلب صلاحيات Admin - يتم استدعاؤه قبل Apply / SmartAgent.Activate()
    /// </summary>
    internal static class SystemRestoreHelper
    {
        public static bool TryCreateRestorePoint(string description = "ClearWin - Before Optimization")
        {
            try
            {
                var scope = new ManagementScope(@"\\.\root\default");
                var path = new ManagementPath("SystemRestore");
                using (var cls = new ManagementClass(scope, path, null))
                {
                    var parms = cls.GetMethodParameters("CreateRestorePoint");
                    parms["Description"] = description;
                    parms["RestorePointType"] = 12; // MODIFY_SETTINGS
                    parms["EventType"] = 100; // BEGIN_SYSTEM_CHANGE
                    var result = cls.InvokeMethod("CreateRestorePoint", parms, null);
                    uint ret = (uint)result["ReturnValue"];
                    // 0 = Success, 1058 = Disabled, 1460 = Already in progress
                    if (ret == 0) { CWLog.Info("SystemRestoreHelper", "Restore point created: " + description); return true; }
                    CWLog.Error("SystemRestoreHelper", new Exception("CreateRestorePoint returned " + ret + " for " + description));
                    return false;
                }
            }
            catch (Exception ex) { CWLog.Error("SystemRestoreHelper", ex); return false; }
        }

        public static bool IsSystemRestoreEnabled()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\SystemRestore"))
                    return key != null && key.GetValue("RPSessionInterval") != null;
            }
            catch (Exception ex) { CWLog.Error("SystemRestoreHelper", ex); return false; }
        }
    }
}
