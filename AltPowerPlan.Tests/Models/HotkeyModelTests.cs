using AltPowerPlan.Models;
using FluentAssertions;
using System.Windows.Input;
using Xunit;

namespace AltPowerPlan.Tests.Models
{
    public class HotkeyModelTests
    {
        [Theory]
        [InlineData("Ctrl + Alt + P", ModifierKeys.Control | ModifierKeys.Alt, Key.P)]
        [InlineData("Alt + Shift + P", ModifierKeys.Alt | ModifierKeys.Shift, Key.P)]
        [InlineData("Win + Alt + P", ModifierKeys.Windows | ModifierKeys.Alt, Key.P)]
        [InlineData("Ctrl + Shift + P", ModifierKeys.Control | ModifierKeys.Shift, Key.P)]
        [InlineData("Ctrl + F1", ModifierKeys.Control, Key.F1)]
        public void Parse_ValidString_ReturnsExpectedModel(string input, ModifierKeys expectedModifiers, Key expectedKey)
        {
            var hotkey = HotkeyModel.Parse(input);

            hotkey.Modifiers.Should().Be(expectedModifiers);
            hotkey.Key.Should().Be(expectedKey);
            hotkey.IsValid.Should().BeTrue();
            hotkey.IsNone.Should().BeFalse();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("None")]
        [InlineData("Disabled")]
        public void Parse_NoneOrEmpty_ReturnsNoneModel(string? input)
        {
            var hotkey = HotkeyModel.Parse(input);

            hotkey.IsNone.Should().BeTrue();
            hotkey.IsValid.Should().BeFalse();
            hotkey.Key.Should().Be(Key.None);
            hotkey.Modifiers.Should().Be(ModifierKeys.None);
        }

        [Fact]
        public void ToString_DefaultHotkey_FormatsAsExpected()
        {
            var hotkey = new HotkeyModel(ModifierKeys.Control | ModifierKeys.Alt, Key.P);

            hotkey.ToString().Should().Be("Ctrl + Alt + P");
        }

        [Fact]
        public void ToString_NoneHotkey_FormatsAsNone()
        {
            var hotkey = HotkeyModel.None;

            hotkey.ToString().Should().Be("None");
        }

        [Fact]
        public void GetWin32Modifiers_CombinesBitmasksCorrectly()
        {
            var hotkey = new HotkeyModel(ModifierKeys.Alt | ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Windows, Key.A);

            // Alt = 0x0001, Ctrl = 0x0002, Shift = 0x0004, Win = 0x0008 -> 0x000F (15)
            uint bitmask = hotkey.GetWin32Modifiers();

            bitmask.Should().Be(15);
        }

        [Fact]
        public void Equals_SameValuesDifferentInstances_ReturnsTrue()
        {
            var a = new HotkeyModel(ModifierKeys.Control | ModifierKeys.Alt, Key.P);
            var b = new HotkeyModel(ModifierKeys.Control | ModifierKeys.Alt, Key.P);

            a.Should().Be(b);
            (a == b).Should().BeTrue();
            (a != b).Should().BeFalse();
            a.GetHashCode().Should().Be(b.GetHashCode());
        }

        [Fact]
        public void Equals_DifferentValues_ReturnsFalse()
        {
            var a = new HotkeyModel(ModifierKeys.Control | ModifierKeys.Alt, Key.P);
            var b = new HotkeyModel(ModifierKeys.Control | ModifierKeys.Shift, Key.P);

            a.Should().NotBe(b);
            (a == b).Should().BeFalse();
            (a != b).Should().BeTrue();
        }

        [Theory]
        [InlineData(HotkeyChoice.CtrlAltP, ModifierKeys.Control | ModifierKeys.Alt, Key.P)]
        [InlineData(HotkeyChoice.AltShiftP, ModifierKeys.Alt | ModifierKeys.Shift, Key.P)]
        [InlineData(HotkeyChoice.WinAltP, ModifierKeys.Windows | ModifierKeys.Alt, Key.P)]
        [InlineData(HotkeyChoice.CtrlShiftP, ModifierKeys.Control | ModifierKeys.Shift, Key.P)]
        [InlineData(HotkeyChoice.None, ModifierKeys.None, Key.None)]
        public void FromChoice_And_ToChoice_RoundTrips(HotkeyChoice choice, ModifierKeys expectedModifiers, Key expectedKey)
        {
            var model = HotkeyModel.FromChoice(choice);

            model.Modifiers.Should().Be(expectedModifiers);
            model.Key.Should().Be(expectedKey);

            var roundTripped = model.ToChoice();
            roundTripped.Should().Be(choice);
        }
    }
}
