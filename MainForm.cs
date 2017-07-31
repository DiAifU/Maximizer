using Maximizer.Properties;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using WindowsDesktop;

namespace Maximizer
{
    public partial class MainForm : Form
    {

        private Dictionary<IntPtr, VirtualDesktop> OldDesktopsList;
        private object locker;
        private NotifyIcon nIcon;
        private IntPtr hWinEventHook;
        public MainForm()
        {
            InitializeComponent();

        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            BeginInvoke(new MethodInvoker(delegate
            {
                Hide();
            }));

            OldDesktopsList = new Dictionary<IntPtr, VirtualDesktop>();
            locker = new object();

            WinEventProc listener = new WinEventProc(EventCallback);
            //setting the window hook and writing the result to the console
            SetWinEventHook(EVENT_OBJECT_LOCATIONCHANGE, EVENT_OBJECT_LOCATIONCHANGE, IntPtr.Zero, listener, 0, 0, SetWinEventHookFlags.WINEVENT_OUTOFCONTEXT);

            nIcon = new NotifyIcon();
            Resources.Icon.MakeTransparent(Color.White);
            System.IntPtr icH = Resources.Icon.GetHicon();
            Icon ico = Icon.FromHandle(icH);
            nIcon.Icon = ico;
            nIcon.Visible = true;
            nIcon.ContextMenu = new ContextMenu(new MenuItem[] { new MenuItem("Close", (nS, nE) => {  this.Close(); })});
        }


        internal delegate void WinEventProc(IntPtr hWinEventHook, int iEvent, IntPtr hWnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime);
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWinEventHook(int eventMin, int eventMax, IntPtr hmodWinEventProc, WinEventProc lpfnWinEventProc, int idProcess, int idThread, SetWinEventHookFlags dwflags);

        const int EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        [DllImport("user32.dll", SetLastError = true)]
        internal static extern int UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowPlacement(
    IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [Serializable]
        [StructLayout(LayoutKind.Sequential)]
        internal struct WINDOWPLACEMENT
        {
            public int length;
            public int flags;
            public ShowWindowCommands showCmd;
            public System.Drawing.Point ptMinPosition;
            public System.Drawing.Point ptMaxPosition;
            public System.Drawing.Rectangle rcNormalPosition;
        }
        public enum ShowWindowCommands : int
        {
            /// <summary>
            /// Hides the window and activates another window.
            /// </summary>
            Hide = 0,
            /// <summary>
            /// Activates and displays a window. If the window is minimized or
            /// maximized, the system restores it to its original size and position.
            /// An application should specify this flag when displaying the window
            /// for the first time.
            /// </summary>
            Normal = 1,
            /// <summary>
            /// Activates the window and displays it as a minimized window.
            /// </summary>
            ShowMinimized = 2,
            /// <summary>
            /// Maximizes the specified window.
            /// </summary>
            Maximize = 3, // is this the right value?
                          /// <summary>
                          /// Activates the window and displays it as a maximized window.
                          /// </summary>      
            ShowMaximized = 3,
            /// <summary>
            /// Displays a window in its most recent size and position. This value
            /// is similar to <see cref="Win32.ShowWindowCommand.Normal"/>, except
            /// the window is not activated.
            /// </summary>
            ShowNoActivate = 4,
            /// <summary>
            /// Activates the window and displays it in its current size and position.
            /// </summary>
            Show = 5,
            /// <summary>
            /// Minimizes the specified window and activates the next top-level
            /// window in the Z order.
            /// </summary>
            Minimize = 6,
            /// <summary>
            /// Displays the window as a minimized window. This value is similar to
            /// <see cref="Win32.ShowWindowCommand.ShowMinimized"/>, except the
            /// window is not activated.
            /// </summary>
            ShowMinNoActive = 7,
            /// <summary>
            /// Displays the window in its current size and position. This value is
            /// similar to <see cref="Win32.ShowWindowCommand.Show"/>, except the
            /// window is not activated.
            /// </summary>
            ShowNA = 8,
            /// <summary>
            /// Activates and displays the window. If the window is minimized or
            /// maximized, the system restores it to its original size and position.
            /// An application should specify this flag when restoring a minimized window.
            /// </summary>
            Restore = 9,
            /// <summary>
            /// Sets the show state based on the SW_* value specified in the
            /// STARTUPINFO structure passed to the CreateProcess function by the
            /// program that started the application.
            /// </summary>
            ShowDefault = 10,
            /// <summary>
            ///  <b>Windows 2000/XP:</b> Minimizes a window, even if the thread
            /// that owns the window is not responding. This flag should only be
            /// used when minimizing windows from a different thread.
            /// </summary>
            ForceMinimize = 11
        }

        internal enum SetWinEventHookFlags
        {
            WINEVENT_INCONTEXT = 4,
            WINEVENT_OUTOFCONTEXT = 0,
            WINEVENT_SKIPOWNPROCESS = 2,
            WINEVENT_SKIPOWNTHREAD = 1
        }


        private void EventCallback(IntPtr hWinEventHook, int iEvent, IntPtr hWnd, int idObject, int idChild, int dwEventThread, int dwmsEventTime)
        {
            this.hWinEventHook = hWinEventHook;
            //callback function, called when message is intercepted
            WINDOWPLACEMENT wplcmt = new WINDOWPLACEMENT();
            wplcmt.length = Marshal.SizeOf(wplcmt);

            if (GetWindowPlacement(hWnd, ref wplcmt))
            {
                lock (locker)
                {
                    if ((wplcmt.showCmd == ShowWindowCommands.Maximize || wplcmt.showCmd == ShowWindowCommands.ShowMaximized) && !OldDesktopsList.Keys.Contains(hWnd))
                    {
                        try
                        {
                            VirtualDesktop currentDesktop = VirtualDesktop.FromHwnd(hWnd);
                            OldDesktopsList.Add(hWnd, currentDesktop);
                            VirtualDesktop newDesktop = VirtualDesktop.Create();
                            VirtualDesktopHelper.MoveToDesktop(hWnd, newDesktop);
                            newDesktop.Switch();
                        }
                        catch { }
                    }
                    else if ((wplcmt.showCmd == ShowWindowCommands.Hide || wplcmt.showCmd == ShowWindowCommands.Minimize ||
                        wplcmt.showCmd == ShowWindowCommands.ForceMinimize || wplcmt.showCmd == ShowWindowCommands.Restore ||
                        wplcmt.showCmd == ShowWindowCommands.Normal) && OldDesktopsList.Keys.Contains(hWnd))
                    {
                        if (VirtualDesktop.GetDesktops().Any(v => v.Id == OldDesktopsList[hWnd].Id))
                        {
                            VirtualDesktop newDesktop = VirtualDesktop.FromHwnd(hWnd);
                            VirtualDesktopHelper.MoveToDesktop(hWnd, OldDesktopsList[hWnd]);
                            OldDesktopsList[hWnd].Switch();
                            newDesktop.Remove();
                        }
                        OldDesktopsList.Remove(hWnd);

                    }
                }
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            nIcon.Visible = false;
            nIcon.Dispose();
            UnhookWinEvent(hWinEventHook);
        }
    }
}
