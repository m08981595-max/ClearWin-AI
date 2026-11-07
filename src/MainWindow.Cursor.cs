using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using System.IO;

namespace ClearWin
{
    public partial class MainWindow
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, string pvParam, uint fWinIni);

        const uint SPI_SETCURSORS = 0x0057;
        const uint SPIF_UPDATEINIFILE = 0x01;
        const uint SPIF_SENDCHANGE = 0x02;
        const uint SPIF_SENDWININICHANGE = 0x02;

        private const string W11_DARK_CUR_DIR = @"Cursors\W11_dark_v2.2";

        private string CursorFile(string name)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                W11_DARK_CUR_DIR, name);
        }

        private string AeroCursor(string name)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                "Cursors", name);
        }

        private bool ApplyWindows11BlackCursor()
        {
            try
            {
                if (!File.Exists(CursorFile("pointer.cur"))) return false;

                string scheme = string.Join(",",
                    CursorFile("pointer.cur"),    // Arrow
                    CursorFile("help.cur"),        // Help
                    AeroCursor("aero_working.ani"), // AppStarting
                    AeroCursor("aero_busy.ani"),    // Wait
                    CursorFile("precision.cur"),   // Crosshair
                    CursorFile("beam.cur"),        // IBeam
                    CursorFile("link.cur"),        // Hand
                    CursorFile("unavailable.cur"), // No
                    CursorFile("vert.cur"),        // SizeNS
                    CursorFile("horz.cur"),        // SizeWE
                    CursorFile("dgn1.cur"),        // SizeNWSE
                    CursorFile("dgn2.cur"),        // SizeNESW
                    CursorFile("move.cur"),        // SizeAll
                    CursorFile("alternate.cur"),   // UpArrow
                    CursorFile("link.cur"),        // Wait(dup, matches scheme layout)
                    CursorFile("person.cur"),      // Person
                    CursorFile("pin.cur"));        // Pin

                using (var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
                {
                    if (key == null) return false;
                    key.SetValue("", "W11 Cursors Dark HD v2.2 by Jepri Creations", RegistryValueKind.String);
                    key.SetValue("Arrow", CursorFile("pointer.cur"));
                    key.SetValue("Help", CursorFile("help.cur"));
                    key.SetValue("AppStarting", AeroCursor("aero_working.ani"));
                    key.SetValue("Wait", AeroCursor("aero_busy.ani"));
                    key.SetValue("Crosshair", CursorFile("precision.cur"));
                    key.SetValue("precisionhair", CursorFile("precision.cur"));
                    key.SetValue("Hand", CursorFile("link.cur"));
                    key.SetValue("IBeam", CursorFile("beam.cur"));
                    key.SetValue("No", CursorFile("unavailable.cur"));
                    key.SetValue("NWPen", CursorFile("handwriting.cur"));
                    key.SetValue("SizeAll", CursorFile("move.cur"));
                    key.SetValue("SizeNESW", CursorFile("dgn2.cur"));
                    key.SetValue("SizeNS", CursorFile("vert.cur"));
                    key.SetValue("SizeNWSE", CursorFile("dgn1.cur"));
                    key.SetValue("SizeWE", CursorFile("horz.cur"));
                    key.SetValue("UpArrow", CursorFile("alternate.cur"));
                    key.SetValue("Person", CursorFile("person.cur"));
                    key.SetValue("Pin", CursorFile("pin.cur"));
                }

                using (var schemes = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors\Schemes"))
                {
                    if (schemes != null)
                        schemes.SetValue("W11 Cursors Dark HD v2.2 by Jepri Creations", scheme, RegistryValueKind.ExpandString);
                }

                SystemParametersInfo(SPI_SETCURSORS, 0, null, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool RestoreDefaultCursors()
        {
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(@"Control Panel\Cursors"))
                {
                    if (key == null) return false;
                    key.SetValue("", "Windows Default (system scheme)", RegistryValueKind.String);
                    key.SetValue("Arrow", AeroCursor("aero_arrow.cur"));
                    key.SetValue("Help", AeroCursor("aero_helpsel.cur"));
                    key.SetValue("AppStarting", AeroCursor("aero_working.ani"));
                    key.SetValue("Wait", AeroCursor("aero_busy.ani"));
                    key.SetValue("Crosshair", "");
                    key.SetValue("precisionhair", "");
                    key.SetValue("Hand", AeroCursor("aero_link.cur"));
                    key.SetValue("IBeam", "");
                    key.SetValue("No", AeroCursor("aero_unavail.cur"));
                    key.SetValue("NWPen", AeroCursor("aero_pen.cur"));
                    key.SetValue("SizeAll", AeroCursor("aero_move.cur"));
                    key.SetValue("SizeNESW", AeroCursor("aero_nesw.cur"));
                    key.SetValue("SizeNS", AeroCursor("aero_ns.cur"));
                    key.SetValue("SizeNWSE", AeroCursor("aero_nwse.cur"));
                    key.SetValue("SizeWE", AeroCursor("aero_ew.cur"));
                    key.SetValue("UpArrow", AeroCursor("aero_up.cur"));
                    key.SetValue("Person", AeroCursor("aero_person.cur"));
                    key.SetValue("Pin", AeroCursor("aero_pin.cur"));
                }

                SystemParametersInfo(SPI_SETCURSORS, 0, null, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
