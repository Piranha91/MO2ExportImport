using System;
using System.Runtime.InteropServices;

namespace MO2ExportImport
{
    public class FileOperation
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct SHFILEOPSTRUCT
        {
            public IntPtr hwnd;
            public uint wFunc;
            public string pFrom;
            public string pTo;
            public ushort fFlags;
            public bool fAnyOperationsAborted;
            public IntPtr hNameMappings;
            public string lpszProgressTitle;
        }

        public const uint FO_COPY = 0x0002;
        public const ushort FOF_NOCONFIRMMKDIR = 0x0200;
        public const ushort FOF_SILENT = 0x0004; // Suppress progress UI

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        public static bool CopyFolderWithUI(string sourceDir, string destDir)
        {
            SHFILEOPSTRUCT shFile = new SHFILEOPSTRUCT
            {
                wFunc = FO_COPY,
                pFrom = sourceDir + '\0' + '\0',
                pTo = destDir + '\0' + '\0',
                fFlags = FOF_NOCONFIRMMKDIR | FOF_SILENT, // Add other flags as needed
            };

            int result = SHFileOperation(ref shFile);
            return result == 0 && !shFile.fAnyOperationsAborted;
        }
    }

}
