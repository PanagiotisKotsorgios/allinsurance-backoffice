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
        button.PointerEntered += (_, _) => { state.SetHover(true); state.Animate(1.025, 1); };
        button.PointerExited += (_, _) => { state.SetHover(false); state.Animate(1, 1); };
        button.PointerPressed += (_, _) => state.Animate(0.96, 0.84);
        button.PointerReleased += (_, _) => state.Animate(1.025, 1);
        button.PointerCanceled += (_, _) => { state.SetHover(false); state.Animate(1, 1); };
        button.LostFocus += (_, _) => { state.SetHover(false); state.Animate(1, 1); };
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
        private SolidColorBrush? _originalBrush;
        private SolidColorBrush? _hoverBrush;

        public void SetHover(bool hovering)
        {
            if (!hovering)
            {
                if (_hoverBrush is not null && ReferenceEquals(button.Background, _hoverBrush))
                    button.Background = _originalBrush;
                _originalBrush = null;
                _hoverBrush = null;
                return;
            }
            if (button.Background is not SolidColorBrush original || original.Color.A < 160) return;
            _originalBrush = original;
            var color = original.Color;
            var dark = button.ActualTheme == ElementTheme.Dark;
            byte Adjust(byte component) => (byte)Math.Clamp(Math.Round(dark
                ? component + (255 - component) * 0.12 : component * 0.89), 0, 255);
            _hoverBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(color.A,
                Adjust(color.R), Adjust(color.G), Adjust(color.B)));
            button.Background = _hoverBrush;
        }

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
