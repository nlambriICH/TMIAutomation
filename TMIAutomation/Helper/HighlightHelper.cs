using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace TMIAutomation.Helpers
{
    public static class HighlightHelper
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.RegisterAttached(
                "Value",
                typeof(object),
                typeof(HighlightHelper),
                new PropertyMetadata(null, OnValueChanged));

        public static object GetValue(DependencyObject obj) => obj.GetValue(ValueProperty);
        public static void SetValue(DependencyObject obj, object value) => obj.SetValue(ValueProperty, value);

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 1. Ignore initial loading when OldValue is null or UnsetValue
            if (e.OldValue == null || e.OldValue == DependencyProperty.UnsetValue)
                return;

            // 2. Only trigger if the new value is actually different from the old value
            if (!Equals(e.OldValue, e.NewValue) && d is FrameworkElement element)
            {
                AnimateHighlight(element);
            }
        }

        private static void AnimateHighlight(FrameworkElement element)
        {
            Color highlightColor = (Color)ColorConverter.ConvertFromString("#81C784"); // Flash color (Green)

            if (element is TextBlock textBlock && textBlock.Foreground is SolidColorBrush textBrush)
            {
                var animatedBrush = textBrush.IsFrozen ? textBrush.Clone() : textBrush;
                textBlock.Foreground = animatedBrush;

                var animation = new ColorAnimation
                {
                    From = highlightColor,
                    To = animatedBrush.Color,
                    Duration = TimeSpan.FromSeconds(4.0)
                };

                animatedBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }
        }
    }
}