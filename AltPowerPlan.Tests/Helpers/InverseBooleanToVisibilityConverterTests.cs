using AltPowerPlan.Helpers;
using FluentAssertions;
using System.Globalization;
using System.Windows;
using Xunit;

namespace AltPowerPlan.Tests.Helpers
{
    public class InverseBooleanToVisibilityConverterTests
    {
        private readonly InverseBooleanToVisibilityConverter _converter = new();

        [Fact]
        public void Convert_True_ReturnsCollapsed()
        {
            var result = _converter.Convert(true, typeof(Visibility), null!, CultureInfo.InvariantCulture);

            result.Should().Be(Visibility.Collapsed);
        }

        [Fact]
        public void Convert_False_ReturnsVisible()
        {
            var result = _converter.Convert(false, typeof(Visibility), null!, CultureInfo.InvariantCulture);

            result.Should().Be(Visibility.Visible);
        }

        [Fact]
        public void ConvertBack_Collapsed_ReturnsTrue()
        {
            var result = _converter.ConvertBack(Visibility.Collapsed, typeof(bool), null!, CultureInfo.InvariantCulture);

            result.Should().Be(true);
        }

        [Fact]
        public void ConvertBack_Visible_ReturnsFalse()
        {
            var result = _converter.ConvertBack(Visibility.Visible, typeof(bool), null!, CultureInfo.InvariantCulture);

            result.Should().Be(false);
        }
    }
}
