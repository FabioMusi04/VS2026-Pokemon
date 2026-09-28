using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;

namespace VisualStudioPokemon.UI
{
    /// <summary>
    /// Hosts the status-bar Pokemon lane in a lightweight owned window.
    ///
    /// A normal WPF child placed in Visual Studio's main visual tree cannot be drawn over
    /// HWND-hosted tool windows (for example terminals/WebView content), regardless of ZIndex.
    /// Using a non-activating owned window avoids that WPF airspace limitation while keeping
    /// Visual Studio popups/context menus above the Pokemon overlay.
    /// </summary>
    internal static class VisualStudioStatusBarOverlay
    {
        private const double StatusBarOverlap = 20;

        private const int GwlExStyle = -20;
        private const long WsExNoActivate = 0x08000000L;
        private const long WsExToolWindow = 0x00000080L;

        private const int WmNcHitTest = 0x0084;
        private const int WmMouseActivate = 0x0021;
        private const int HtClient = 1;
        private const int HtTransparent = -1;
        private const int MaNoActivate = 3;

        private const uint SwpNoZOrder = 0x0004;
        private const uint SwpNoActivate = 0x0010;
        private const uint SwpNoOwnerZOrder = 0x0200;

        private static FrameworkElement? statusBar;
        private static Window? mainWindow;
        private static FrameworkElement? attachedElement;
        private static Window? overlayWindow;
        private static HwndSource? overlaySource;
        private static bool repositionPending;
        private static Rect lastDeviceBounds = Rect.Empty;

        internal static async Task<bool> AttachAsync(FrameworkElement element)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!await EnsureVisualStudioUiAsync() || mainWindow == null || statusBar == null)
            {
                return false;
            }

            if (attachedElement != null && !ReferenceEquals(attachedElement, element))
            {
                DetachElementFromOverlay();
            }

            attachedElement = element;
            EnsureOverlayWindow();
            if (overlayWindow == null)
            {
                return false;
            }

            if (!ReferenceEquals(overlayWindow.Content, element))
            {
                overlayWindow.Content = element;
            }

            SubscribeLayoutEvents();

            if (!overlayWindow.IsVisible)
            {
                overlayWindow.Show();
            }

            RequestReposition();
            return true;
        }

        internal static async Task DetachAsync(FrameworkElement element)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!ReferenceEquals(attachedElement, element))
            {
                return;
            }

            UnsubscribeLayoutEvents();
            DetachElementFromOverlay();
            CloseOverlayWindow();
            attachedElement = null;
            lastDeviceBounds = Rect.Empty;
        }

        internal static void RefreshPosition()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            RequestReposition();
        }

        private static void EnsureOverlayWindow()
        {
            if (overlayWindow != null || mainWindow == null)
            {
                return;
            }

            overlayWindow = new Window
            {
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                ShowActivated = false,
                Topmost = false,
                Focusable = false,
                Width = 1,
                Height = Math.Max(1, attachedElement?.Height ?? 1),
                Owner = mainWindow,
                SnapsToDevicePixels = true,
                UseLayoutRounding = true
            };

            overlayWindow.SourceInitialized += OverlayWindow_SourceInitialized;
            overlayWindow.Closed += OverlayWindow_Closed;
        }

        private static void OverlayWindow_SourceInitialized(object? sender, EventArgs e)
        {
            if (overlayWindow == null)
            {
                return;
            }

            overlaySource = PresentationSource.FromVisual(overlayWindow) as HwndSource;
            if (overlaySource == null)
            {
                return;
            }

            AddNoActivateStyles(overlaySource.Handle);
            overlaySource.AddHook(OverlayWindowHook);
            RequestReposition();
        }

        private static void OverlayWindow_Closed(object? sender, EventArgs e)
        {
            if (overlaySource != null)
            {
                overlaySource.RemoveHook(OverlayWindowHook);
            }

            overlaySource = null;
            overlayWindow = null;
        }

        private static IntPtr OverlayWindowHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WmMouseActivate)
            {
                handled = true;
                return new IntPtr(MaNoActivate);
            }

            if (msg != WmNcHitTest || attachedElement == null)
            {
                return IntPtr.Zero;
            }

            long packed = lParam.ToInt64();
            int screenX = unchecked((short)(packed & 0xFFFF));
            int screenY = unchecked((short)((packed >> 16) & 0xFFFF));

            try
            {
                Point localPoint = attachedElement.PointFromScreen(new Point(screenX, screenY));
                bool inside = localPoint.X >= 0 && localPoint.Y >= 0
                    && localPoint.X <= attachedElement.ActualWidth
                    && localPoint.Y <= attachedElement.ActualHeight;

                // Panels with Background=null are not hit-testable in their empty area. That makes
                // the overlay click-through everywhere except the actual Pokemon visuals.
                IInputElement? hit = inside ? attachedElement.InputHitTest(localPoint) : null;
                handled = true;
                return new IntPtr(hit == null ? HtTransparent : HtClient);
            }
            catch (InvalidOperationException)
            {
                handled = true;
                return new IntPtr(HtTransparent);
            }
        }

        private static void AddNoActivateStyles(IntPtr hwnd)
        {
            long styles = GetWindowLongPtr(hwnd, GwlExStyle).ToInt64();
            styles |= WsExNoActivate | WsExToolWindow;
            SetWindowLongPtr(hwnd, GwlExStyle, new IntPtr(styles));
        }

        private static void SubscribeLayoutEvents()
        {
            if (statusBar != null)
            {
                statusBar.SizeChanged -= StatusBar_SizeChanged;
                statusBar.SizeChanged += StatusBar_SizeChanged;
            }

            if (mainWindow != null)
            {
                mainWindow.LocationChanged -= MainWindow_Changed;
                mainWindow.LocationChanged += MainWindow_Changed;
                mainWindow.SizeChanged -= MainWindow_SizeChanged;
                mainWindow.SizeChanged += MainWindow_SizeChanged;
                mainWindow.StateChanged -= MainWindow_StateChanged;
                mainWindow.StateChanged += MainWindow_StateChanged;
            }
        }

        private static void UnsubscribeLayoutEvents()
        {
            if (statusBar != null)
            {
                statusBar.SizeChanged -= StatusBar_SizeChanged;
            }

            if (mainWindow != null)
            {
                mainWindow.LocationChanged -= MainWindow_Changed;
                mainWindow.SizeChanged -= MainWindow_SizeChanged;
                mainWindow.StateChanged -= MainWindow_StateChanged;
            }
        }

        private static void StatusBar_SizeChanged(object sender, SizeChangedEventArgs e) => RequestReposition();

        private static void MainWindow_Changed(object? sender, EventArgs e) => RequestReposition();

        private static void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e) => RequestReposition();

        private static void MainWindow_StateChanged(object? sender, EventArgs e) => RequestReposition();

        private static void RequestReposition()
        {
            if (repositionPending || overlayWindow == null)
            {
                return;
            }

            repositionPending = true;
            overlayWindow.Dispatcher.BeginInvoke(new Action(() =>
            {
                repositionPending = false;
                RepositionOverlay();
            }), DispatcherPriority.Render);
        }

        private static void RepositionOverlay()
        {
            if (overlayWindow == null || overlaySource == null || statusBar == null || attachedElement == null || mainWindow == null)
            {
                return;
            }

            if (!mainWindow.IsVisible || mainWindow.WindowState == WindowState.Minimized || !statusBar.IsVisible || statusBar.ActualWidth <= 1)
            {
                overlayWindow.Visibility = Visibility.Hidden;
                return;
            }

            if (overlayWindow.Visibility != Visibility.Visible)
            {
                overlayWindow.Visibility = Visibility.Visible;
            }

            Point statusLeft = statusBar.PointToScreen(new Point(0, 0));
            Point statusRight = statusBar.PointToScreen(new Point(statusBar.ActualWidth, 0));

            PresentationSource? source = PresentationSource.FromVisual(statusBar);
            Matrix toDevice = source?.CompositionTarget?.TransformToDevice ?? Matrix.Identity;
            double scaleY = Math.Abs(toDevice.M22) > 0.001 ? Math.Abs(toDevice.M22) : 1.0;

            double elementHeight = attachedElement.Height;
            if (Double.IsNaN(elementHeight) || elementHeight <= 0)
            {
                elementHeight = Math.Max(1, attachedElement.ActualHeight);
            }

            int x = (int)Math.Round(statusLeft.X);
            int width = Math.Max(1, (int)Math.Round(statusRight.X - statusLeft.X));
            int height = Math.Max(1, (int)Math.Ceiling(elementHeight * scaleY));
            int overlap = Math.Max(0, (int)Math.Round(StatusBarOverlap * scaleY));
            int y = (int)Math.Round(statusLeft.Y) - height + overlap;

            var bounds = new Rect(x, y, width, height);
            if (bounds == lastDeviceBounds)
            {
                return;
            }

            lastDeviceBounds = bounds;
            SetWindowPos(
                overlaySource.Handle,
                IntPtr.Zero,
                x,
                y,
                width,
                height,
                SwpNoZOrder | SwpNoActivate | SwpNoOwnerZOrder);
        }

        private static void DetachElementFromOverlay()
        {
            if (overlayWindow != null && ReferenceEquals(overlayWindow.Content, attachedElement))
            {
                overlayWindow.Content = null;
            }
        }

        private static void CloseOverlayWindow()
        {
            if (overlayWindow == null)
            {
                return;
            }

            Window window = overlayWindow;
            overlayWindow = null;

            if (overlaySource != null)
            {
                overlaySource.RemoveHook(OverlayWindowHook);
                overlaySource = null;
            }

            window.SourceInitialized -= OverlayWindow_SourceInitialized;
            window.Closed -= OverlayWindow_Closed;
            window.Close();
        }

        private static async Task<bool> EnsureVisualStudioUiAsync()
        {
            if (mainWindow != null && statusBar != null)
            {
                return true;
            }

            for (int attempt = 0; attempt < 10; attempt++)
            {
                mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    statusBar = FindNamedElement(mainWindow, "StatusBarPanel");
                    if (statusBar != null)
                    {
                        return true;
                    }
                }

                await Task.Delay(200);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            return false;
        }

        private static FrameworkElement? FindNamedElement(DependencyObject parent, string name)
        {
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is FrameworkElement element && String.Equals(element.Name, name, StringComparison.Ordinal))
                {
                    return element;
                }

                FrameworkElement? nested = FindNamedElement(child, name);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }

        private static IntPtr GetWindowLongPtr(IntPtr hwnd, int index)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hwnd, index) : new IntPtr(GetWindowLong32(hwnd, index));
        }

        private static IntPtr SetWindowLongPtr(IntPtr hwnd, int index, IntPtr value)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hwnd, index, value) : new IntPtr(SetWindowLong32(hwnd, index, value.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
        private static extern int GetWindowLong32(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hwnd, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hwnd, int index, int value);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hwnd, int index, IntPtr value);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(
            IntPtr hwnd,
            IntPtr hwndInsertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);
    }
}
