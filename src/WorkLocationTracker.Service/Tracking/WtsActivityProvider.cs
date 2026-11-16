using System.ComponentModel;
using System.Runtime.InteropServices;

namespace WorkLocationTracker.Service.Tracking;

public sealed class WtsActivityProvider : IActivityProvider
{
    private const uint NoActiveSession = 0xFFFFFFFF;
    private const int ActiveSessionState = 0;
    private const int LockedSessionFlag = 0;
    private static readonly IntPtr CurrentServerHandle = IntPtr.Zero;

    public ActivitySnapshot GetSnapshot()
    {
        var sessionId = NativeMethods.WTSGetActiveConsoleSessionId();
        if (sessionId == NoActiveSession)
        {
            return new ActivitySnapshot(false, TimeSpan.Zero);
        }

        if (!NativeMethods.WTSQuerySessionInformation(
                CurrentServerHandle,
                sessionId,
                WtsInfoClass.SessionInfoEx,
                out var buffer,
                out var bytesReturned))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not read the interactive Windows session.");
        }

        try
        {
            if (bytesReturned < Marshal.SizeOf<WtsInfoEx>())
            {
                throw new InvalidOperationException(
                    $"Windows returned {bytesReturned} session-information bytes; " +
                    $"{Marshal.SizeOf<WtsInfoEx>()} were expected.");
            }

            var sessionInfo = Marshal.PtrToStructure<WtsInfoEx>(buffer);
            var hasInteractiveUser =
                sessionInfo.Data.SessionState == ActiveSessionState &&
                sessionInfo.Data.SessionFlags != LockedSessionFlag &&
                !string.IsNullOrWhiteSpace(sessionInfo.Data.UserName);

            var maximumPlausibleIdleTime =
                TimeSpan.FromMilliseconds(Environment.TickCount64) + TimeSpan.FromMinutes(1);
            return CreateSnapshot(
                hasInteractiveUser,
                sessionInfo.Data.LogonTime,
                sessionInfo.Data.LastInputTime,
                sessionInfo.Data.CurrentTime,
                maximumPlausibleIdleTime);
        }
        finally
        {
            NativeMethods.WTSFreeMemory(buffer);
        }
    }

    internal static ActivitySnapshot CreateSnapshot(
        bool hasInteractiveUser,
        long logonTime,
        long lastInputTime,
        long currentTime,
        TimeSpan maximumPlausibleIdleTime)
    {
        if (!hasInteractiveUser)
        {
            return new ActivitySnapshot(false, TimeSpan.Zero);
        }

        // WTS can retain the previous session's last-input timestamp after a hibernate/resume
        // reconnect. A timestamp before the current logon is invalid for this session. As with a
        // missing timestamp, use the reliable lock/unlock state rather than marking the unlocked
        // desktop idle indefinitely.
        if (lastInputTime <= 0 || logonTime > 0 && lastInputTime < logonTime)
        {
            return new ActivitySnapshot(true, TimeSpan.Zero);
        }

        if (currentTime < lastInputTime)
        {
            throw new InvalidOperationException("Windows returned an invalid last-input timestamp.");
        }

        var idleTime = TimeSpan.FromTicks(currentTime - lastInputTime);
        if (idleTime > maximumPlausibleIdleTime)
        {
            throw new InvalidOperationException(
                $"Windows returned an implausible idle time of {idleTime.TotalDays:N1} days.");
        }

        return new ActivitySnapshot(true, idleTime);
    }

    private enum WtsInfoClass
    {
        SessionInfoEx = 25
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WtsInfoEx
    {
        public uint Level;
        public WtsInfoExLevel1 Data;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WtsInfoExLevel1
    {
        public uint SessionId;
        public int SessionState;
        public int SessionFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)]
        public string WinStationName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 21)]
        public string UserName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 18)]
        public string DomainName;

        public long LogonTime;
        public long ConnectTime;
        public long DisconnectTime;
        public long LastInputTime;
        public long CurrentTime;
        public uint IncomingBytes;
        public uint OutgoingBytes;
        public uint IncomingFrames;
        public uint OutgoingFrames;
        public uint IncomingCompressedBytes;
        public uint OutgoingCompressedBytes;
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll")]
        public static extern uint WTSGetActiveConsoleSessionId();

        [DllImport(
            "wtsapi32.dll",
            EntryPoint = "WTSQuerySessionInformationW",
            CharSet = CharSet.Unicode,
            ExactSpelling = true,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool WTSQuerySessionInformation(
            IntPtr serverHandle,
            uint sessionId,
            WtsInfoClass infoClass,
            out IntPtr buffer,
            out int bytesReturned);

        [DllImport("wtsapi32.dll")]
        public static extern void WTSFreeMemory(IntPtr memory);
    }
}
