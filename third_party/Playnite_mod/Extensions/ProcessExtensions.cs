using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Windows.Wdk.System.Threading;
using Windows.Win32.System.Threading;
using Microsoft.Win32.SafeHandles;
using PInvokeWdk = Windows.Wdk.PInvoke;
using PInvokeWin32 = Windows.Win32.PInvoke;

namespace System.Diagnostics
{
    public static class ProcessExtensions
    {
        extension(Process process)
        {
            public bool TryGetMainModuleFileName(out string? fileName, int bufferSize = 1024)
            {
                fileName = null;
                var handle = PInvokeWin32.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)process.Id);
                if (handle == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    Span<char> buffer = stackalloc char[bufferSize];
                    var bufferLength = (uint)buffer.Length;
                    using var safeHandle = new SafeProcessHandle(handle, true);
                    var result = PInvokeWin32.QueryFullProcessImageName(safeHandle, 0, buffer, ref bufferLength);
                    fileName = result ? new string(buffer[..(int)bufferLength]) : null;
                    return result;
                }
                finally
                {
                    PInvokeWin32.CloseHandle(handle);
                }
            }

            public unsafe bool TryGetParentId(out int processId)
            {
                processId = 0;
                var handle = PInvokeWin32.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)process.Id);
                if (handle == IntPtr.Zero)
                {
                    return false;
                }

                try
                {
                    var info = default(PROCESS_BASIC_INFORMATION);
                    int status = PInvokeWdk.NtQueryInformationProcess(handle, PROCESSINFOCLASS.ProcessBasicInformation, &info,
                        (uint)Marshal.SizeOf(info), null);
                    if (status != 0)
                    {
                        return false;
                    }

                    processId = (int)info.InheritedFromUniqueProcessId;
                    return true;
                }
                finally
                {
                    PInvokeWin32.CloseHandle(handle);
                }
            }
        }

        public static bool IsRunning(string processPattern)
        {
            return Process.GetProcesses().FirstOrDefault(a => Regex.IsMatch(a.ProcessName, processPattern, RegexOptions.IgnoreCase)) !=
                   null;
        }
    }
}