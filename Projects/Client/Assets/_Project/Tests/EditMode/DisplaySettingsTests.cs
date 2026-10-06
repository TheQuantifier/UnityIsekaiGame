using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityIsekaiGame.UI;

namespace UnityIsekaiGame.Tests
{
    public sealed class DisplaySettingsTests
    {
        [Test]
        public void Resolution_options_are_unique_monitor_bounded_and_include_native_size()
        {
            IReadOnlyList<GameDisplayResolution> options = ClientDisplaySettings.BuildResolutionOptions(
                new[]
                {
                    new GameDisplayResolution(1920, 1080),
                    new GameDisplayResolution(1920, 1080),
                    new GameDisplayResolution(1280, 720),
                    new GameDisplayResolution(3840, 2160)
                },
                2560,
                1440);

            Assert.That(options.Count, Is.EqualTo(3));
            Assert.That(options[0], Is.EqualTo(new GameDisplayResolution(2560, 1440)));
            Assert.That(options[1], Is.EqualTo(new GameDisplayResolution(1920, 1080)));
            Assert.That(options[2], Is.EqualTo(new GameDisplayResolution(1280, 720)));
        }

        [Test]
        public void Closest_resolution_uses_both_dimensions()
        {
            var options = new[]
            {
                new GameDisplayResolution(2560, 1440),
                new GameDisplayResolution(1920, 1080),
                new GameDisplayResolution(1280, 720)
            };

            GameDisplayResolution closest = ClientDisplaySettings.FindClosestResolution(
                new GameDisplayResolution(1800, 1000),
                options);

            Assert.That(closest, Is.EqualTo(new GameDisplayResolution(1920, 1080)));
        }

        [Test]
        public void Display_modes_map_to_exclusive_borderless_and_standard_windows()
        {
            Assert.That(
                ClientDisplaySettings.ToUnityMode(GameDisplayMode.ExclusiveFullscreen),
                Is.EqualTo(FullScreenMode.ExclusiveFullScreen));
            Assert.That(
                ClientDisplaySettings.ToUnityMode(GameDisplayMode.BorderlessFullscreen),
                Is.EqualTo(FullScreenMode.FullScreenWindow));
            Assert.That(
                ClientDisplaySettings.ToUnityMode(GameDisplayMode.Windowed),
                Is.EqualTo(FullScreenMode.Windowed));
        }
    }
}
