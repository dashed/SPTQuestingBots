using NUnit.Framework;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers;

[TestFixture]
public class LootClusterHelperTests
{
    [Test]
    public void TryFindNearestCluster_EmptyArray_ReturnsFalse()
    {
        bool found = LootClusterScorer.TryFindNearestCluster(0, 0, 0, 100, System.Array.Empty<LootClusterData>(), out _);
        Assert.That(found, Is.False);
    }

    [Test]
    public void TryFindNearestCluster_NullArray_ReturnsFalse()
    {
        bool found = LootClusterScorer.TryFindNearestCluster(0, 0, 0, 100, null, out _);
        Assert.That(found, Is.False);
    }

    [Test]
    public void TryFindNearestCluster_SingleClusterInRange_ReturnsIt()
    {
        var clusters = new[]
        {
            new LootClusterData
            {
                CenterX = 10,
                CenterY = 0,
                CenterZ = 10,
                Radius = 5,
                ValueScore = 500,
                ClusterId = 1,
                IsLooted = false,
            },
        };

        bool found = LootClusterScorer.TryFindNearestCluster(0, 0, 0, 50, clusters, out var nearest);

        Assert.That(found, Is.True);
        Assert.That(nearest.ClusterId, Is.EqualTo(1));
    }

    [Test]
    public void TryFindNearestCluster_ClusterOutOfRange_ReturnsFalse()
    {
        var clusters = new[]
        {
            new LootClusterData
            {
                CenterX = 200,
                CenterY = 0,
                CenterZ = 200,
                Radius = 5,
                ValueScore = 500,
                ClusterId = 1,
                IsLooted = false,
            },
        };

        bool found = LootClusterScorer.TryFindNearestCluster(0, 0, 0, 50, clusters, out _);
        Assert.That(found, Is.False);
    }

    [Test]
    public void TryFindNearestCluster_SkipsLootedClusters()
    {
        var clusters = new[]
        {
            new LootClusterData
            {
                CenterX = 5,
                CenterY = 0,
                CenterZ = 5,
                ClusterId = 1,
                IsLooted = true,
            },
            new LootClusterData
            {
                CenterX = 20,
                CenterY = 0,
                CenterZ = 20,
                ClusterId = 2,
                IsLooted = false,
            },
        };

        bool found = LootClusterScorer.TryFindNearestCluster(0, 0, 0, 100, clusters, out var nearest);

        Assert.That(found, Is.True);
        Assert.That(nearest.ClusterId, Is.EqualTo(2));
    }

    [Test]
    public void TryFindNearestCluster_ReturnsClosest()
    {
        var clusters = new[]
        {
            new LootClusterData
            {
                CenterX = 50,
                CenterY = 0,
                CenterZ = 50,
                ClusterId = 1,
                IsLooted = false,
            },
            new LootClusterData
            {
                CenterX = 10,
                CenterY = 0,
                CenterZ = 10,
                ClusterId = 2,
                IsLooted = false,
            },
        };

        bool found = LootClusterScorer.TryFindNearestCluster(0, 0, 0, 100, clusters, out var nearest);

        Assert.That(found, Is.True);
        Assert.That(nearest.ClusterId, Is.EqualTo(2));
    }

    [Test]
    public void TryFindNearestCluster_AllLooted_ReturnsFalse()
    {
        var clusters = new[]
        {
            new LootClusterData
            {
                CenterX = 10,
                CenterY = 0,
                CenterZ = 10,
                ClusterId = 1,
                IsLooted = true,
            },
            new LootClusterData
            {
                CenterX = 20,
                CenterY = 0,
                CenterZ = 20,
                ClusterId = 2,
                IsLooted = true,
            },
        };

        bool found = LootClusterScorer.TryFindNearestCluster(0, 0, 0, 100, clusters, out _);
        Assert.That(found, Is.False);
    }

    // ── ComputeLootDensity tests ──

    [Test]
    public void ComputeLootDensity_NullArray_ReturnsZero()
    {
        float density = LootClusterScorer.ComputeLootDensity(0, 0, null, 100);
        Assert.That(density, Is.EqualTo(0f));
    }

    [Test]
    public void ComputeLootDensity_EmptyArray_ReturnsZero()
    {
        float density = LootClusterScorer.ComputeLootDensity(0, 0, System.Array.Empty<LootClusterData>(), 100);
        Assert.That(density, Is.EqualTo(0f));
    }

    [Test]
    public void ComputeLootDensity_ClusterAtCenter_MaxFalloff()
    {
        var clusters = new[]
        {
            new LootClusterData
            {
                CenterX = 0,
                CenterZ = 0,
                ValueScore = 10000,
                LootPointCount = 5,
                IsLooted = false,
            },
        };

        float density = LootClusterScorer.ComputeLootDensity(0, 0, clusters, 100);

        // At center: falloff = 1.0, score = 10000 * 1.0 * 5 = 50000, normalized = 50000/100000 = 0.5
        Assert.That(density, Is.GreaterThan(0f));
        Assert.That(density, Is.EqualTo(0.5f).Within(0.01f));
    }

    [Test]
    public void ComputeLootDensity_ClusterOutOfRange_NoContribution()
    {
        var clusters = new[]
        {
            new LootClusterData
            {
                CenterX = 200,
                CenterZ = 200,
                ValueScore = 10000,
                LootPointCount = 5,
                IsLooted = false,
            },
        };

        float density = LootClusterScorer.ComputeLootDensity(0, 0, clusters, 50);
        Assert.That(density, Is.EqualTo(0f));
    }

    [Test]
    public void ComputeLootDensity_LootedCluster_Ignored()
    {
        var clusters = new[]
        {
            new LootClusterData
            {
                CenterX = 0,
                CenterZ = 0,
                ValueScore = 10000,
                LootPointCount = 5,
                IsLooted = true,
            },
        };

        float density = LootClusterScorer.ComputeLootDensity(0, 0, clusters, 100);
        Assert.That(density, Is.EqualTo(0f));
    }

    [Test]
    public void ComputeLootDensity_FalloffDecreasesWithDistance()
    {
        var clusters = new[]
        {
            new LootClusterData
            {
                CenterX = 0,
                CenterZ = 0,
                ValueScore = 10000,
                LootPointCount = 3,
                IsLooted = false,
            },
        };

        float atCenter = LootClusterScorer.ComputeLootDensity(0, 0, clusters, 100);
        float at50m = LootClusterScorer.ComputeLootDensity(50, 0, clusters, 100);

        Assert.That(atCenter, Is.GreaterThan(at50m));
    }

    [Test]
    public void ComputeLootDensity_CappedAtOne()
    {
        // Create extremely high-value clusters to exceed normalization cap
        var clusters = new[]
        {
            new LootClusterData
            {
                CenterX = 0,
                CenterZ = 0,
                ValueScore = 1000000,
                LootPointCount = 100,
                IsLooted = false,
            },
        };

        float density = LootClusterScorer.ComputeLootDensity(0, 0, clusters, 100);
        Assert.That(density, Is.EqualTo(1f));
    }

    [Test]
    public void ComputeLootDensity_MultipleClusters_Additive()
    {
        var singleCluster = new[]
        {
            new LootClusterData
            {
                CenterX = 10,
                CenterZ = 10,
                ValueScore = 5000,
                LootPointCount = 3,
                IsLooted = false,
            },
        };

        var twoClusters = new[]
        {
            new LootClusterData
            {
                CenterX = 10,
                CenterZ = 10,
                ValueScore = 5000,
                LootPointCount = 3,
                IsLooted = false,
            },
            new LootClusterData
            {
                CenterX = -10,
                CenterZ = -10,
                ValueScore = 5000,
                LootPointCount = 3,
                IsLooted = false,
            },
        };

        float single = LootClusterScorer.ComputeLootDensity(0, 0, singleCluster, 100);
        float dual = LootClusterScorer.ComputeLootDensity(0, 0, twoClusters, 100);

        Assert.That(dual, Is.GreaterThan(single));
    }
}
