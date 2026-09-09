using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell;

namespace VisualStudioPokemon.UI
{
    /// <summary>
    /// Places a transparent, interactive surface immediately above Visual Studio's status bar.
    /// Empty areas remain transparent to mouse input; only Pokemon visuals handle clicks.
    /// </summary>
    internal static class VisualStudioStatusBarOverlay
    {
        private const double StatusBarOverlap = 6;
        private static FrameworkElement? statusBar;
        private static Grid? rootGrid;
        private static FrameworkElement? attachedElement;

        internal static async Task<bool> AttachAsync(FrameworkElement element)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (!await EnsureVisualStudioUiAsync())
            {
                return false;
            }

            if (rootGrid == null || statusBar == null)
            {
                return false;
            }

            if (attachedElement != null && !ReferenceEquals(attachedElement, element))
            {
                rootGrid.Children.Remove(attachedElement);
            }

            attachedElement = element;
            ConfigurePosition();
            if (!rootGrid.Children.Contains(element))
            {
                Panel.SetZIndex(element, 10000);
                Grid.SetRowSpan(element, 100);
                Grid.SetColumnSpan(element, 100);
                rootGrid.Children.Add(element);
                statusBar.SizeChanged += StatusBar_SizeChanged;
            }

            return true;
        }

        internal static async Task DetachAsync(FrameworkElement element)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (rootGrid != null)
            {
                rootGrid.Children.Remove(element);
            }

            if (statusBar != null)
            {
                statusBar.SizeChanged -= StatusBar_SizeChanged;
            }

            if (ReferenceEquals(attachedElement, element))
            {
                attachedElement = null;
            }
        }

        private static void StatusBar_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            ConfigurePosition();
        }

        private static void ConfigurePosition()
        {
            if (attachedElement == null)
            {
                return;
            }

            double statusBarHeight = statusBar?.ActualHeight > 0 ? statusBar.ActualHeight : 22;
            attachedElement.HorizontalAlignment = HorizontalAlignment.Stretch;
            attachedElement.VerticalAlignment = VerticalAlignment.Bottom;
            attachedElement.Margin = new Thickness(0, 0, 0, Math.Max(0, statusBarHeight - StatusBarOverlap));
        }

        private static async Task<bool> EnsureVisualStudioUiAsync()
        {
            if (rootGrid != null && statusBar != null)
            {
                return true;
            }

            for (int attempt = 0; attempt < 10; attempt++)
            {
                Window? mainWindow = Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    statusBar = FindNamedElement(mainWindow, "StatusBarPanel");
                    rootGrid = FindRootGrid(mainWindow);
                    if (statusBar != null && rootGrid != null)
                    {
                        return true;
                    }
                }

                await Task.Delay(200);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            }

            return false;
        }

        private static Grid? FindRootGrid(DependencyObject parent)
        {
            if (parent is Window window && window.Content is Grid contentGrid)
            {
                return contentGrid;
            }

            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int index = 0; index < childCount; index++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, index);
                if (child is Grid grid)
                {
                    return grid;
                }

                Grid? nested = FindRootGrid(child);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
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
    }
}
