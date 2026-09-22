using AltPowerPlan.Models;
using FluentAssertions;
using System.Text.Json;
using System.Windows.Input;
using Xunit;

namespace AltPowerPlan.Tests.Models
{
    public class HotkeyModelJsonConverterTests
    {
        private readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = false
        };

        [Fact]
        public void Serialize_HotkeyModel_OutputsValidJsonStringAndRoundTrips()
        {
            var model = new HotkeyModel(ModifierKeys.Control | ModifierKeys.Alt, Key.P);

            string json = JsonSerializer.Serialize(model, _options);

            // System.Text.Json escapes '+' as \u002B by default
            json.Should().Contain("Ctrl").And.Contain("Alt").And.Contain("P");

            var deserialized = JsonSerializer.Deserialize<HotkeyModel>(json, _options);
            deserialized.Should().NotBeNull();
            deserialized!.Modifiers.Should().Be(model.Modifiers);
            deserialized.Key.Should().Be(model.Key);
        }

        [Fact]
        public void Deserialize_StringRepresentation_RestoresModel()
        {
            string json = "\"Ctrl + Shift + X\"";

            var model = JsonSerializer.Deserialize<HotkeyModel>(json, _options);

            model.Should().NotBeNull();
            model!.Modifiers.Should().Be(ModifierKeys.Control | ModifierKeys.Shift);
            model.Key.Should().Be(Key.X);
        }

        [Fact]
        public void Deserialize_LegacyInteger_RestoresLegacyChoice()
        {
            // 0 = CtrlAltP, 1 = AltShiftP, 2 = WinAltP, 3 = CtrlShiftP, 4 = None
            string json = "1";

            var model = JsonSerializer.Deserialize<HotkeyModel>(json, _options);

            model.Should().NotBeNull();
            model!.Modifiers.Should().Be(ModifierKeys.Alt | ModifierKeys.Shift);
            model.Key.Should().Be(Key.P);
        }

        [Fact]
        public void Deserialize_StructuredObject_RestoresModel()
        {
            string json = "{\"Modifiers\": 3, \"Key\": 59}"; // Control(2) | Alt(1) = 3, Key.P = 59

            var model = JsonSerializer.Deserialize<HotkeyModel>(json, _options);

            model.Should().NotBeNull();
            model!.Modifiers.Should().Be(ModifierKeys.Control | ModifierKeys.Alt);
            model.Key.Should().Be(Key.P);
        }
    }
}
