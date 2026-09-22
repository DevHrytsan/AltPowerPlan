using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Input;

namespace AltPowerPlan.Models
{
    [JsonConverter(typeof(HotkeyModelJsonConverter))]
    public class HotkeyModel : IEquatable<HotkeyModel>
    {
        public ModifierKeys Modifiers { get; set; }
        public Key Key { get; set; }

        public HotkeyModel()
        {
            Modifiers = ModifierKeys.None;
            Key = Key.None;
        }

        public HotkeyModel(ModifierKeys modifiers, Key key)
        {
            Modifiers = modifiers;
            Key = key;
        }

        public bool IsNone => Key == Key.None;

        public bool IsValid => Key != Key.None;

        public uint GetWin32Modifiers()
        {
            uint mod = 0;
            if (Modifiers.HasFlag(ModifierKeys.Alt)) mod |= 0x0001;
            if (Modifiers.HasFlag(ModifierKeys.Control)) mod |= 0x0002;
            if (Modifiers.HasFlag(ModifierKeys.Shift)) mod |= 0x0004;
            if (Modifiers.HasFlag(ModifierKeys.Windows)) mod |= 0x0008;
            return mod;
        }

        public uint GetWin32VirtualKey()
        {
            return (uint)KeyInterop.VirtualKeyFromKey(Key);
        }

        public override string ToString()
        {
            if (IsNone)
                return "None";

            var parts = new List<string>();
            if (Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
            if (Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
            if (Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
            if (Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");

            string keyName = FormatKeyName(Key);
            if (!string.IsNullOrEmpty(keyName))
                parts.Add(keyName);

            return string.Join(" + ", parts);
        }

        public static string FormatKeyName(Key key)
        {
            return key switch
            {
                Key.None => "",
                Key.D0 => "0",
                Key.D1 => "1",
                Key.D2 => "2",
                Key.D3 => "3",
                Key.D4 => "4",
                Key.D5 => "5",
                Key.D6 => "6",
                Key.D7 => "7",
                Key.D8 => "8",
                Key.D9 => "9",
                Key.NumPad0 => "Num 0",
                Key.NumPad1 => "Num 1",
                Key.NumPad2 => "Num 2",
                Key.NumPad3 => "Num 3",
                Key.NumPad4 => "Num 4",
                Key.NumPad5 => "Num 5",
                Key.NumPad6 => "Num 6",
                Key.NumPad7 => "Num 7",
                Key.NumPad8 => "Num 8",
                Key.NumPad9 => "Num 9",
                Key.OemTilde => "~",
                Key.OemMinus => "-",
                Key.OemPlus => "+",
                Key.OemOpenBrackets => "[",
                Key.OemCloseBrackets => "]",
                Key.OemQuotes => "'",
                Key.OemSemicolon => ";",
                Key.OemComma => ",",
                Key.OemPeriod => ".",
                Key.OemQuestion => "/",
                Key.OemBackslash => "\\",
                _ => key.ToString()
            };
        }

        public static HotkeyModel None => new(ModifierKeys.None, Key.None);
        public static HotkeyModel Default => new(ModifierKeys.Control | ModifierKeys.Alt, Key.P);

        public static HotkeyModel FromChoice(HotkeyChoice choice)
        {
            return choice switch
            {
                HotkeyChoice.CtrlAltP => new HotkeyModel(ModifierKeys.Control | ModifierKeys.Alt, Key.P),
                HotkeyChoice.AltShiftP => new HotkeyModel(ModifierKeys.Alt | ModifierKeys.Shift, Key.P),
                HotkeyChoice.WinAltP => new HotkeyModel(ModifierKeys.Windows | ModifierKeys.Alt, Key.P),
                HotkeyChoice.CtrlShiftP => new HotkeyModel(ModifierKeys.Control | ModifierKeys.Shift, Key.P),
                HotkeyChoice.None => None,
                _ => Default
            };
        }

        public HotkeyChoice ToChoice()
        {
            if (IsNone) return HotkeyChoice.None;
            if (Key == Key.P)
            {
                if (Modifiers == (ModifierKeys.Control | ModifierKeys.Alt)) return HotkeyChoice.CtrlAltP;
                if (Modifiers == (ModifierKeys.Alt | ModifierKeys.Shift)) return HotkeyChoice.AltShiftP;
                if (Modifiers == (ModifierKeys.Windows | ModifierKeys.Alt)) return HotkeyChoice.WinAltP;
                if (Modifiers == (ModifierKeys.Control | ModifierKeys.Shift)) return HotkeyChoice.CtrlShiftP;
            }
            return HotkeyChoice.Custom;
        }

        public static HotkeyModel Parse(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)
                || text.Equals("None", StringComparison.OrdinalIgnoreCase)
                || text.Equals("Disabled", StringComparison.OrdinalIgnoreCase))
            {
                return None;
            }

            if (Enum.TryParse<HotkeyChoice>(text, true, out var legacyChoice))
            {
                return FromChoice(legacyChoice);
            }

            var parts = text.Split(new[] { '+', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            ModifierKeys modifiers = ModifierKeys.None;
            Key key = Key.None;

            foreach (var rawPart in parts)
            {
                string part = rawPart.Trim();
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Control;
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Alt;
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Shift;
                else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase) || part.Equals("Windows", StringComparison.OrdinalIgnoreCase))
                    modifiers |= ModifierKeys.Windows;
                else
                {
                    if (Enum.TryParse<Key>(part, true, out var parsedKey))
                    {
                        key = parsedKey;
                    }
                    else if (part.Length == 1 && char.IsDigit(part[0]))
                    {
                        key = part[0] switch
                        {
                            '0' => Key.D0,
                            '1' => Key.D1,
                            '2' => Key.D2,
                            '3' => Key.D3,
                            '4' => Key.D4,
                            '5' => Key.D5,
                            '6' => Key.D6,
                            '7' => Key.D7,
                            '8' => Key.D8,
                            '9' => Key.D9,
                            _ => Key.None
                        };
                    }
                    else if (part.Length == 1 && char.IsLetter(part[0]))
                    {
                        if (Enum.TryParse<Key>(part.ToUpperInvariant(), true, out var letterKey))
                            key = letterKey;
                    }
                }
            }

            return new HotkeyModel(modifiers, key);
        }

        public bool Equals(HotkeyModel? other)
        {
            if (other is null) return false;
            return Modifiers == other.Modifiers && Key == other.Key;
        }

        public override bool Equals(object? obj) => Equals(obj as HotkeyModel);

        public override int GetHashCode() => HashCode.Combine(Modifiers, Key);

        public static bool operator ==(HotkeyModel? left, HotkeyModel? right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(HotkeyModel? left, HotkeyModel? right) => !(left == right);
    }

    public class HotkeyModelJsonConverter : JsonConverter<HotkeyModel>
    {
        public override HotkeyModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    int val = reader.GetInt32();
                    return val switch
                    {
                        0 => HotkeyModel.FromChoice(HotkeyChoice.CtrlAltP),
                        1 => HotkeyModel.FromChoice(HotkeyChoice.AltShiftP),
                        2 => HotkeyModel.FromChoice(HotkeyChoice.WinAltP),
                        3 => HotkeyModel.FromChoice(HotkeyChoice.CtrlShiftP),
                        4 => HotkeyModel.None,
                        _ => HotkeyModel.Default
                    };

                case JsonTokenType.String:
                    string? str = reader.GetString();
                    return HotkeyModel.Parse(str);

                case JsonTokenType.StartObject:
                    ModifierKeys modifiers = ModifierKeys.None;
                    Key key = Key.None;
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        if (reader.TokenType == JsonTokenType.PropertyName)
                        {
                            string? propName = reader.GetString();
                            reader.Read();
                            if (string.Equals(propName, "Modifiers", StringComparison.OrdinalIgnoreCase))
                            {
                                if (reader.TokenType == JsonTokenType.Number)
                                    modifiers = (ModifierKeys)reader.GetInt32();
                                else if (reader.TokenType == JsonTokenType.String && Enum.TryParse<ModifierKeys>(reader.GetString(), true, out var m))
                                    modifiers = m;
                            }
                            else if (string.Equals(propName, "Key", StringComparison.OrdinalIgnoreCase))
                            {
                                if (reader.TokenType == JsonTokenType.Number)
                                    key = (Key)reader.GetInt32();
                                else if (reader.TokenType == JsonTokenType.String && Enum.TryParse<Key>(reader.GetString(), true, out var k))
                                    key = k;
                            }
                        }
                    }
                    return new HotkeyModel(modifiers, key);

                default:
                    return HotkeyModel.Default;
            }
        }

        public override void Write(Utf8JsonWriter writer, HotkeyModel value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
