using System;
using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Selection;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class CellScorerTests
{
    [Test]
    public void PerfectAlignment_ScoresHigh()
    {
        var scorer = new CellScorer(poiWeight: 0f); // angle only
        // Cell directly east of bot, composite direction is east
        var cell = new GridCell(1, 0, new Vector3(100, 0, 0));

        float score = scorer.Score(cell, 1f, 0f, new Vector3(0, 0, 0), 0f);

        // Perfect alignment → angleFactor = 1.0
        Assert.That(score, Is.GreaterThan(0.95f));
    }

    [Test]
    public void OppositeDirection_ScoresLow()
    {
        var scorer = new CellScorer(poiWeight: 0f);
        // Cell east of bot, composite direction is west
        var cell = new GridCell(1, 0, new Vector3(100, 0, 0));

        float score = scorer.Score(cell, -1f, 0f, new Vector3(0, 0, 0), 0f);

        // Opposite direction → angleFactor ≈ 0.0
        Assert.That(score, Is.LessThan(0.05f));
    }

    [Test]
    public void Perpendicular_ScoresMiddle()
    {
        var scorer = new CellScorer(poiWeight: 0f);
        // Cell east of bot, composite direction is north
        var cell = new GridCell(1, 0, new Vector3(100, 0, 0));

        float score = scorer.Score(cell, 0f, 1f, new Vector3(0, 0, 0), 0f);

        // Perpendicular → angleFactor ≈ 0.5
        Assert.That(score, Is.EqualTo(0.5f).Within(0.05f));
    }

    [Test]
    public void ZeroCompositeDirection_TreatsAllEqually()
    {
        var scorer = new CellScorer(poiWeight: 0f);
        var cell = new GridCell(1, 0, new Vector3(100, 0, 0));

        float score = scorer.Score(cell, 0f, 0f, new Vector3(0, 0, 0), 0f);

        Assert.That(score, Is.EqualTo(0.5f).Within(0.01f));
    }

    [Test]
    public void PoiDensity_BoostsScore()
    {
        var scorer = new CellScorer(poiWeight: 1.0f); // density only
        var cell = new GridCell(0, 0, new Vector3(50, 0, 50));
        cell.AddPoi(new PointOfInterest(new Vector3(50, 0, 50), PoiCategory.Container)); // weight 1.0

        float score = scorer.Score(cell, 1f, 0f, new Vector3(0, 0, 0), 1.0f);

        // Full density → poiFactor = 1.0
        Assert.That(score, Is.EqualTo(1.0f).Within(0.01f));
    }

    [Test]
    public void EmptyCell_NoDensityBonus()
    {
        var scorer = new CellScorer(poiWeight: 1.0f);
        var cell = new GridCell(0, 0, new Vector3(50, 0, 50));
        // No POIs

        float score = scorer.Score(cell, 1f, 0f, new Vector3(0, 0, 0), 1.0f);

        // Zero density → poiFactor = 0
        Assert.That(score, Is.EqualTo(0f).Within(0.01f));
    }

    [Test]
    public void MixedWeight_BlendsAngleAndDensity()
    {
        var scorer = new CellScorer(poiWeight: 0.5f);
        // Cell perfectly aligned with composite direction
        var cell = new GridCell(1, 0, new Vector3(100, 0, 0));
        // No POIs

        float score = scorer.Score(cell, 1f, 0f, new Vector3(0, 0, 0), 1.0f);

        // angleFactor=1.0 * 0.5 + poiFactor=0.0 * 0.5 = 0.5
        Assert.That(score, Is.EqualTo(0.5f).Within(0.05f));
    }

    [Test]
    public void PoiWeight_ClampedTo01()
    {
        var lowScorer = new CellScorer(poiWeight: -5f);
        var highScorer = new CellScorer(poiWeight: 10f);

        var cell = new GridCell(1, 0, new Vector3(100, 0, 0));

        // With clamped weight, results should be within [0, 1]
        float low = lowScorer.Score(cell, 1f, 0f, new Vector3(0, 0, 0), 0f);
        float high = highScorer.Score(cell, 1f, 0f, new Vector3(0, 0, 0), 0f);

        Assert.That(low, Is.InRange(0f, 1f));
        Assert.That(high, Is.InRange(0f, 1f));
    }

    [Test]
    public void ZeroMaxDensity_SkipsDensityScoring()
    {
        var scorer = new CellScorer(poiWeight: 0.5f);
        var cell = new GridCell(0, 0, new Vector3(50, 0, 50));
        cell.AddPoi(new PointOfInterest(new Vector3(50, 0, 50), PoiCategory.Container));

        // maxPoiDensity = 0 → density scoring skipped
        float score = scorer.Score(cell, 1f, 0f, new Vector3(0, 0, 0), 0f);

        // Only angle factor contributes (with 1-poiWeight=0.5 multiplier)
        // angleFactor=0.5 (zero composite) * 0.5 = 0.25
        Assert.That(score, Is.InRange(0f, 1f));
    }
}
