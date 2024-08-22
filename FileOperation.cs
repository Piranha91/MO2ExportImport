using System;
using System.IO;
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
        public const ushort FOF_NOCONFIRMATION = 0x0010;
        public const ushort FOF_NOERRORUI = 0x0400;
        public const ushort FOF_SILENT = 0x0004; // Suppress progress UI

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern int SHFileOperation(ref SHFILEOPSTRUCT FileOp);

        public static bool CopyFolderWithUI(string sourceDir, string destDir)
        {
            SHFILEOPSTRUCT shFile = new SHFILEOPSTRUCT
            {
                wFunc = FO_COPY,
                // Add trailing backslashes and double null-terminated strings
                pFrom = sourceDir.TrimEnd('\\') + @"\*.*" + '\0' + '\0',
                pTo = destDir.TrimEnd('\\') + @"\" + '\0' + '\0',
                fFlags = FOF_NOCONFIRMMKDIR // Additional flags can be added if needed
            };

            int result = SHFileOperation(ref shFile);
            return result == 0 && !shFile.fAnyOperationsAborted;
        }

        public static Task<bool> CopyFolderWithUIAsync(string sourceDir, string destDir)
        {
            return Task.Run(() => CopyFolderWithUI(sourceDir, destDir));
        }

        public static Task CopyFileWithUIAsync(string sourceFile, string destFile)
        {
            return Task.Run(() =>
            {
                SHFILEOPSTRUCT shFile = new SHFILEOPSTRUCT
                {
                    wFunc = FO_COPY,
                    pFrom = sourceFile + '\0' + '\0',
                    pTo = destFile + '\0' + '\0',
                    fFlags = FOF_NOCONFIRMMKDIR | FOF_NOCONFIRMATION | FOF_NOERRORUI // Flags to control behavior
                };

                int result = SHFileOperation(ref shFile);
                if (result != 0 || shFile.fAnyOperationsAborted)
                {
                    throw new IOException($"Failed to copy {sourceFile} to {destFile}");
                }
            });
        }
    }

}
