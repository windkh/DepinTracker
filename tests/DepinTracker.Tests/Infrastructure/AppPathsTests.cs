namespace DepinTracker.Tests.Infrastructure;

using DepinTracker.Infrastructure.Paths;
using FluentAssertions;

public class AppPathsTests
{
    private static readonly string AppDir = Path.Combine(Path.GetTempPath(), "depintracker-app");

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Unset_data_root_defaults_to_data_beside_the_executable(string? configured)
    {
        var paths = AppPaths.FromSetting(configured, AppDir);

        paths.Root.Should().Be(Path.Combine(AppDir, "data"));
        paths.Data.Should().Be(Path.Combine(AppDir, "data", "data"));
    }

    [Fact]
    public void Relative_data_root_resolves_against_the_app_directory()
    {
        var paths = AppPaths.FromSetting("../shared", AppDir);

        paths.Root.Should().Be(Path.GetFullPath(Path.Combine(AppDir, "..", "shared")));
    }

    [Fact]
    public void Absolute_data_root_is_used_as_is_and_plugins_stay_beside_the_executable()
    {
        var root = Path.Combine(Path.GetTempPath(), "depintracker-elsewhere");

        var paths = AppPaths.FromSetting(root, AppDir);

        paths.Root.Should().Be(root);
        paths.Config.Should().Be(Path.Combine(root, "config"));
        paths.Plugins.Should().Be(Path.Combine(AppDir, "plugins"));
    }
}
