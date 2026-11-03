using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using System.Runtime.CompilerServices;

namespace AllInsurance.Desktop.Controls;

public static class ButtonMotion
{
    private static readonly ConditionalWeakTable<ButtonBase, MotionState> States = new();

    public static void Attach(ButtonBase button)
    {
        if (States.TryGetValue(button, out _)) return;
        var scale = new ScaleTransform { ScaleX = 1, ScaleY = 1 };
        button.RenderTransformOrigin = new Windows.Foundation.Point(0.5, 0.5);
        button.RenderTransform = scale;
        var state = new MotionState(button, scale);
        States.Add(button, state);
        button.PointerEntered += (_, _) => state.Animate(1.025, 1);
        button.PointerExited += (_, _) => state.Animate(1, 1);
        button.PointerPressed += (_, _) => state.Animate(0.96, 0.84);
        button.PointerReleased += (_, _) => state.Animate(1.025, 1);
        button.PointerCanceled += (_, _) => state.Animate(1, 1);
        button.LostFocus += (_, _) => state.Animate(1, 1);
    }

    public static void AttachTree(DependencyObject root)
    {
        if (root is ButtonBase button) Attach(button);
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            AttachTree(VisualTreeHelper.GetChild(root, index));
    }

    private sealed class MotionState(ButtonBase button, ScaleTransform scale)
    {
        private Storyboard? _current;

        public void Animate(double target, double opacity)
        {
            if (!button.IsEnabled) return;
            _current?.Stop();
            var story = new Storyboard();
            AddAnimation(story, scale, "ScaleX", scale.ScaleX, target);
            AddAnimation(story, scale, "ScaleY", scale.ScaleY, target);
            AddAnimation(story, button, "Opacity", button.Opacity, opacity);
            _current = story;
            story.Begin();
        }

        private static void AddAnimation(Storyboard story, DependencyObject target,
            string property, double from, double to)
        {
            var animation = new DoubleAnimation { From = from, To = to,
                Duration = new Duration(TimeSpan.FromMilliseconds(140)),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, property);
            story.Children.Add(animation);
        }
    }
}
