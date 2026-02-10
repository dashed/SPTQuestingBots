using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;

namespace SPTQuestingBots.Client.Tests.ZoneMovement.Core;

[TestFixture]
public class AdvectionZoneConfigTests
{
    // --- GetDefaults ---

    [Test]
    public void GetDefaults_ContainsExpectedMaps()
    {
        var defaults = AdvectionZoneConfig.GetDefaults();

        Assert.Multiple(() =>
        {
            Assert.That(defaults, Contains.Key("bigmap"));
            Assert.That(defaults, Contains.Key("interchange"));
            Assert.That(defaults, Contains.Key("shoreline"));
            Assert.That(defaults, Contains.Key("woods"));
            Assert.That(defaults, Contains.Key("rezervbase"));
            Assert.That(defaults, Contains.Key("laboratory"));
            Assert.That(defaults, Contains.Key("factory4_day"));
            Assert.That(defaults, Contains.Key("factory4_night"));
        });
    }

    [Test]
    public void Customs_HasExpectedBuiltinZones()
    {
        var defaults = AdvectionZoneConfig.GetDefaults();
        var customs = defaults["bigmap"];

        Assert.Multiple(() =>
        {
            Assert.That(customs.BuiltinZones, Contains.Key("ZoneDormitory"));
            Assert.That(customs.BuiltinZones, Contains.Key("ZoneGasStation"));
            Assert.That(customs.BuiltinZones, Contains.Key("ZoneScavBase"));
            Assert.That(customs.BuiltinZones, Contains.Key("ZoneOldAZS"));
        });
    }

    [Test]
    public void Customs_Dormitory_HasExpectedValues()
    {
        var defaults = AdvectionZoneConfig.GetDefaults();
        var dorm = defaults["bigmap"].BuiltinZones["ZoneDormitory"];

        Assert.Multiple(() =>
        {
            Assert.That(dorm.ForceMin, Is.EqualTo(-0.5f));
            Assert.That(dorm.ForceMax, Is.EqualTo(1.5f));
            Assert.That(dorm.Radius, Is.EqualTo(250f));
            Assert.That(dorm.Decay, Is.EqualTo(1.0f));
            Assert.That(dorm.EarlyMultiplier, Is.EqualTo(1.5f));
            Assert.That(dorm.LateMultiplier, Is.EqualTo(0.5f));
        });
    }

    [Test]
    public void Interchange_ZoneCenter_HasBossAliveMultiplier()
    {
        var defaults = AdvectionZoneConfig.GetDefaults();
        var center = defaults["interchange"].BuiltinZones["ZoneCenter"];

        Assert.Multiple(() =>
        {
            Assert.That(center.BossAliveMultiplier, Is.EqualTo(1.5f));
            Assert.That(center.Decay, Is.EqualTo(0.75f));
            Assert.That(center.Radius, Is.EqualTo(500f));
        });
    }

    [Test]
    public void Shoreline_HasCustomZones()
    {
        var defaults = AdvectionZoneConfig.GetDefaults();
        var shoreline = defaults["shoreline"];

        Assert.Multiple(() =>
        {
            Assert.That(shoreline.BuiltinZones.Count, Is.EqualTo(0));
            Assert.That(shoreline.CustomZones.Count, Is.EqualTo(2));
            Assert.That(shoreline.CustomZones[0].X, Is.EqualTo(-250f));
            Assert.That(shoreline.CustomZones[0].Z, Is.EqualTo(-100f));
            Assert.That(shoreline.CustomZones[0].EarlyMultiplier, Is.EqualTo(1.3f));
        });
    }

    [Test]
    public void Woods_HasCustomZones()
    {
        var defaults = AdvectionZoneConfig.GetDefaults();
        var woods = defaults["woods"];

        Assert.Multiple(() =>
        {
            Assert.That(woods.BuiltinZones.Count, Is.EqualTo(0));
            Assert.That(woods.CustomZones.Count, Is.EqualTo(2));
        });
    }

    [Test]
    public void Rezervbase_HasBuiltinZones()
    {
        var defaults = AdvectionZoneConfig.GetDefaults();
        var reserve = defaults["rezervbase"];

        Assert.Multiple(() =>
        {
            Assert.That(reserve.BuiltinZones, Contains.Key("ZoneSubStorage"));
            Assert.That(reserve.BuiltinZones, Contains.Key("ZoneBarrack"));
        });
    }

    [Test]
    public void Laboratory_HasSingleCustomZone()
    {
        var defaults = AdvectionZoneConfig.GetDefaults();
        var lab = defaults["laboratory"];

        Assert.Multiple(() =>
        {
            Assert.That(lab.BuiltinZones.Count, Is.EqualTo(0));
            Assert.That(lab.CustomZones.Count, Is.EqualTo(1));
            Assert.That(lab.CustomZones[0].X, Is.EqualTo(0f));
            Assert.That(lab.CustomZones[0].Z, Is.EqualTo(0f));
            Assert.That(lab.CustomZones[0].Radius, Is.EqualTo(150f));
        });
    }

    [Test]
    public void FactoryMaps_HaveNoZones()
    {
        var defaults = AdvectionZoneConfig.GetDefaults();

        Assert.Multiple(() =>
        {
            Assert.That(defaults["factory4_day"].BuiltinZones.Count, Is.EqualTo(0));
            Assert.That(defaults["factory4_day"].CustomZones.Count, Is.EqualTo(0));
            Assert.That(defaults["factory4_night"].BuiltinZones.Count, Is.EqualTo(0));
            Assert.That(defaults["factory4_night"].CustomZones.Count, Is.EqualTo(0));
        });
    }

    // --- GetForMap ---

    [Test]
    public void GetForMap_KnownMap_ReturnsDefaults()
    {
        var result = AdvectionZoneConfig.GetForMap("bigmap", null);

        Assert.That(result.BuiltinZones, Contains.Key("ZoneDormitory"));
    }

    [Test]
    public void GetForMap_CaseInsensitive()
    {
        var result = AdvectionZoneConfig.GetForMap("BigMap", null);

        Assert.That(result.BuiltinZones, Contains.Key("ZoneDormitory"));
    }

    [Test]
    public void GetForMap_UnknownMap_ReturnsEmptyDefault()
    {
        var result = AdvectionZoneConfig.GetForMap("unknownmap123", null);

        Assert.Multiple(() =>
        {
            Assert.That(result.BuiltinZones.Count, Is.EqualTo(0));
            Assert.That(result.CustomZones.Count, Is.EqualTo(0));
        });
    }

    [Test]
    public void GetForMap_WithOverrides_UsesOverride()
    {
        var overrides = new Dictionary<string, AdvectionMapZones>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["bigmap"] = new AdvectionMapZones(
                new Dictionary<string, BuiltinZoneEntry>(System.StringComparer.OrdinalIgnoreCase)
                {
                    ["TestZone"] = new BuiltinZoneEntry("TestZone", 0f, 5.0f, 999f),
                },
                new System.Collections.Generic.List<CustomZoneEntry>()
            ),
        };

        var result = AdvectionZoneConfig.GetForMap("bigmap", overrides);

        Assert.Multiple(() =>
        {
            Assert.That(result.BuiltinZones, Contains.Key("TestZone"));
            Assert.That(result.BuiltinZones.ContainsKey("ZoneDormitory"), Is.False);
        });
    }

    [Test]
    public void GetForMap_OverrideDoesNotAffectOtherMaps()
    {
        var overrides = new Dictionary<string, AdvectionMapZones>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["bigmap"] = new AdvectionMapZones(),
        };

        var result = AdvectionZoneConfig.GetForMap("interchange", overrides);

        Assert.That(result.BuiltinZones, Contains.Key("ZoneCenter"));
    }

    // --- AdvectionZoneEntry defaults ---

    [Test]
    public void AdvectionZoneEntry_DefaultMultipliers_AreOne()
    {
        var entry = new AdvectionZoneEntry();

        Assert.Multiple(() =>
        {
            Assert.That(entry.EarlyMultiplier, Is.EqualTo(1.0f));
            Assert.That(entry.LateMultiplier, Is.EqualTo(1.0f));
            Assert.That(entry.BossAliveMultiplier, Is.EqualTo(1.0f));
            Assert.That(entry.Decay, Is.EqualTo(1.0f));
        });
    }

    [Test]
    public void BuiltinZoneEntry_StoresZoneName()
    {
        var entry = new BuiltinZoneEntry("TestZone", -1f, 2f, 100f);

        Assert.That(entry.ZoneName, Is.EqualTo("TestZone"));
    }

    [Test]
    public void CustomZoneEntry_StoresPosition()
    {
        var entry = new CustomZoneEntry(123.5f, -456.7f, 0f, 1f, 200f);

        Assert.Multiple(() =>
        {
            Assert.That(entry.X, Is.EqualTo(123.5f));
            Assert.That(entry.Z, Is.EqualTo(-456.7f));
        });
    }
}
