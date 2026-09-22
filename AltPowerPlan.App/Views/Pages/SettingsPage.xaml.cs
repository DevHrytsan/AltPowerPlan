using AltPowerPlan.ViewModels.Pages;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Abstractions.Controls;

namespace AltPowerPlan.Views.Pages
{
    public partial class SettingsPage : INavigableView<SettingsViewModel>
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;

            InitializeComponent();
        }

        private void OnHotkeyRecordButtonClicked(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleRecordingHotkey();
            if (ViewModel.IsRecordingHotkey)
            {
                HotkeyRecordButton.Focus();
            }
        }

        private void OnHotkeyRecordPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!ViewModel.IsRecordingHotkey)
                return;

            e.Handled = true;

            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (key == Key.ImeProcessed)
                key = e.ImeProcessedKey;

            if (key == Key.Escape)
            {
                ViewModel.CancelRecordingHotkey();
                return;
            }

            if (key == Key.Back || key == Key.Delete)
            {
                ViewModel.ClearHotkey();
                return;
            }

            bool isModifierOnly = key is Key.LeftCtrl or Key.RightCtrl
                                     or Key.LeftAlt or Key.RightAlt
                                     or Key.LeftShift or Key.RightShift
                                     or Key.LWin or Key.RWin;

            ModifierKeys modifiers = Keyboard.Modifiers;
            if (Keyboard.IsKeyDown(Key.LWin) || Keyboard.IsKeyDown(Key.RWin))
            {
                modifiers |= ModifierKeys.Windows;
            }

            if (isModifierOnly)
            {
                ViewModel.UpdateRecordingModifiers(modifiers);
                return;
            }

            ViewModel.ApplyRecordedHotkey(modifiers, key);
        }

        private void OnHotkeyRecordLostFocus(object sender, RoutedEventArgs e)
        {
            if (ViewModel.IsRecordingHotkey)
            {
                ViewModel.CancelRecordingHotkey();
            }
        }
    }
}
