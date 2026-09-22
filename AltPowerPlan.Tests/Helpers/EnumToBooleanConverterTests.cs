using AltPowerPlan.Helpers;
using AltPowerPlan.Models;
using FluentAssertions;
using System;
using System.Globalization;
using Xunit;

namespace AltPowerPlan.Tests.Helpers
{
    public class EnumToBooleanConverterTests
    {
        private readonly EnumToBooleanConverter _converter = new();

        [Fact]
        public void Convert_MatchingEnum_ReturnsTrue()
        {
            var result = _converter.Convert(ThemeChoice.Dark, typeof(bool), "Dark", CultureInfo.InvariantCulture);

            result.Should().Be(true);
        }

        [Fact]
        public void Convert_NonMatchingEnum_ReturnsFalse()
        {
            var result = _converter.Convert(ThemeChoice.Light, typeof(bool), "Dark", CultureInfo.InvariantCulture);

            result.Should().Be(false);
        }

        [Fact]
        public void Convert_NonStringParameter_ThrowsArgumentException()
        {
            Action act = () => _converter.Convert(ThemeChoice.Dark, typeof(bool), 123, CultureInfo.InvariantCulture);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public void ConvertBack_ValidString_ReturnsParsedEnum()
        {
            var result = _converter.ConvertBack(true, typeof(ThemeChoice), "System", CultureInfo.InvariantCulture);

            result.Should().Be(ThemeChoice.System);
        }
    }
}
