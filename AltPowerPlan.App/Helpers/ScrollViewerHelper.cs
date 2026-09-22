using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace AltPowerPlan.Helpers
{
    /// <summary>
    /// Attached behavior for ScrollViewer to control mouse wheel scrolling sensitivity and step size.
    /// Follows AGENTS.md zero-allocation and performance rules in hot paths.
    /// </summary>
    public static class ScrollViewerHelper
    {
        public static readonly DependencyProperty UseReducedScrollSensitivityProperty =
            DependencyProperty.RegisterAttached(
                "UseReducedScrollSensitivity",
                typeof(bool),
                typeof(ScrollViewerHelper),
                new PropertyMetadata(false, OnUseReducedScrollSensitivityChanged));

        public static bool GetUseReducedScrollSensitivity(DependencyObject obj) =>
            (bool)obj.GetValue(UseReducedScrollSensitivityProperty);

        public static void SetUseReducedScrollSensitivity(DependencyObject obj, bool value) =>
            obj.SetValue(UseReducedScrollSensitivityProperty, value);

        public static readonly DependencyProperty ScrollStepProperty =
            DependencyProperty.RegisterAttached(
                "ScrollStep",
                typeof(double),
                typeof(ScrollViewerHelper),
                new PropertyMetadata(24.0));

        public static double GetScrollStep(DependencyObject obj) =>
            (double)obj.GetValue(ScrollStepProperty);

        public static void SetScrollStep(DependencyObject obj, double value) =>
            obj.SetValue(ScrollStepProperty, value);

        private static void OnUseReducedScrollSensitivityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer scrollViewer)
                return;

            if ((bool)e.NewValue)
            {
                scrollViewer.PreviewMouseWheel += OnScrollViewerPreviewMouseWheel;
            }
            else
            {
                scrollViewer.PreviewMouseWheel -= OnScrollViewerPreviewMouseWheel;
            }
        }

        private static void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
                return;

            // If the mouse wheel event originated inside a nested child ScrollViewer, let the child handle it
            if (e.OriginalSource is DependencyObject source)
            {
                DependencyObject? parent = VisualTreeHelper.GetParent(source);
                while (parent != null && parent != scrollViewer)
                {
                    if (parent is ScrollViewer)
                        return;
                    parent = VisualTreeHelper.GetParent(parent);
                }
            }

            double step = GetScrollStep(scrollViewer);
            if (step <= 0)
                step = 24.0;

            // e.Delta is positive for wheel up, negative for wheel down
            double deltaUnits = e.Delta / 120.0;
            double offsetChange = deltaUnits * step;
            double currentOffset = scrollViewer.VerticalOffset;
            double targetOffset = currentOffset - offsetChange;

            // Clamp offset within valid bounds [0, ScrollableHeight]
            if (targetOffset < 0.0)
                targetOffset = 0.0;
            else if (targetOffset > scrollViewer.ScrollableHeight)
                targetOffset = scrollViewer.ScrollableHeight;

            // Only scroll and mark handled if an actual displacement occurred
            if (Math.Abs(currentOffset - targetOffset) > 0.001)
            {
                scrollViewer.ScrollToVerticalOffset(targetOffset);
                e.Handled = true;
            }
        }
    }
}
