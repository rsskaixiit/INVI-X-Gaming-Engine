using Invix.Services;
using Microsoft.Win32;
using Xunit;

namespace Invix.Tests;

public class TuningCatalogueTests
{
    private static readonly TuningService Service = new();

    [Fact]
    public void Catalogue_is_not_empty()
    {
        Assert.NotEmpty(Service.Descriptors);
    }

    [Fact]
    public void Mouse_acceleration_tweak_disables_all_three_values_for_the_user()
    {
        var tweak = Service.Descriptors.Single(d => d.Id == "mouse_acceleration");

        Assert.False(tweak.RequiresAdmin);
        Assert.Equal(3, tweak.GamingWrites.Count);
        Assert.All(tweak.GamingWrites, write => Assert.Equal("HKCU", write.Hive));
        Assert.All(tweak.GamingWrites, write => Assert.Equal(@"Control Panel\Mouse", write.KeyPath));
        Assert.All(tweak.GamingWrites, write => Assert.Equal(RegistryValueKind.String, write.Kind));
        Assert.All(tweak.GamingWrites, write => Assert.Equal("0", write.Value));
    }

    [Fact]
    public void Tweak_ids_are_unique()
    {
        var ids = Service.Descriptors.Select(d => d.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void Every_tweak_is_either_a_power_plan_or_a_set_of_registry_writes()
    {
        foreach (var tweak in Service.Descriptors)
        {
            var isPowerPlan = tweak.GamingPowerPlan is not null;
            var hasWrites = tweak.GamingWrites.Count > 0;
            Assert.True(isPowerPlan ^ hasWrites, $"{tweak.Id} must be power-plan XOR registry writes.");
        }
    }

    [Fact]
    public void Power_plan_tweak_declares_a_default_scheme()
    {
        var power = Service.Descriptors.Single(d => d.GamingPowerPlan is not null);
        Assert.NotNull(power.DefaultPowerPlan);
        Assert.NotEqual(power.GamingPowerPlan, power.DefaultPowerPlan);
    }

    [Fact]
    public void Admin_steps_only_target_hklm_and_user_steps_only_target_hkcu()
    {
        foreach (var tweak in Service.Descriptors.Where(d => d.GamingWrites.Count > 0))
        {
            var expectedHive = tweak.RequiresAdmin ? "HKLM" : "HKCU";
            Assert.All(tweak.GamingWrites, write => Assert.Equal(expectedHive, write.Hive));
            Assert.All(tweak.DefaultWrites, write => Assert.Equal(expectedHive, write.Hive));
        }
    }

    [Fact]
    public void Default_writes_target_exactly_the_same_values_as_gaming_writes()
    {
        foreach (var tweak in Service.Descriptors.Where(d => d.GamingWrites.Count > 0))
        {
            var gaming = tweak.GamingWrites.Select(w => (w.Hive, w.KeyPath, w.Name)).ToHashSet();
            var defaults = tweak.DefaultWrites.Select(w => (w.Hive, w.KeyPath, w.Name)).ToHashSet();
            Assert.Equal(gaming, defaults);
        }
    }

    [Fact]
    public void Every_tweak_has_a_human_readable_name_and_description()
    {
        Assert.All(Service.Descriptors, tweak =>
        {
            Assert.False(string.IsNullOrWhiteSpace(tweak.Name));
            Assert.False(string.IsNullOrWhiteSpace(tweak.Description));
        });
    }
}