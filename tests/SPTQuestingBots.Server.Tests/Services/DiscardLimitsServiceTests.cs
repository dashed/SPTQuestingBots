using System.Reflection;
using NUnit.Framework;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTQuestingBots.Server.Services;

namespace SPTQuestingBots.Server.Tests.Services;

[TestFixture]
public class DiscardLimitsServiceTests
{
    // Valid 24-char hex strings for MongoId construction
    private const string Id1 = "000000000000000000000001";
    private const string Id2 = "000000000000000000000002";
    private const string Id3 = "000000000000000000000003";
    private const string Id4 = "000000000000000000000004";
    private const string Id5 = "000000000000000000000005";

    // ── MarkDiscardLimitItemsAsUninsurable ─────────────────────────

    [Test]
    public void MarkDiscardLimitItems_WithDiscardLimit_SetsInsuranceDisabled()
    {
        var items = MakeItems(
            new TemplateItemProperties
            {
                DiscardLimit = 1,
                InsuranceDisabled = false,
                IsAlwaysAvailableForInsurance = false,
            }
        );

        var count = DiscardLimitsService.MarkDiscardLimitItemsAsUninsurable(items);

        Assert.That(count, Is.EqualTo(1));
        Assert.That(items.Values.First().Properties!.InsuranceDisabled, Is.True);
    }

    [Test]
    public void MarkDiscardLimitItems_WithDiscardLimitZero_SetsInsuranceDisabled()
    {
        var items = MakeItems(
            new TemplateItemProperties
            {
                DiscardLimit = 0,
                InsuranceDisabled = false,
                IsAlwaysAvailableForInsurance = false,
            }
        );

        var count = DiscardLimitsService.MarkDiscardLimitItemsAsUninsurable(items);

        Assert.That(count, Is.EqualTo(1));
        Assert.That(items.Values.First().Properties!.InsuranceDisabled, Is.True);
    }

    [Test]
    public void MarkDiscardLimitItems_WithNegativeDiscardLimit_DoesNotMark()
    {
        var items = MakeItems(
            new TemplateItemProperties
            {
                DiscardLimit = -1,
                InsuranceDisabled = false,
                IsAlwaysAvailableForInsurance = false,
            }
        );

        var count = DiscardLimitsService.MarkDiscardLimitItemsAsUninsurable(items);

        Assert.That(count, Is.EqualTo(0));
        Assert.That(items.Values.First().Properties!.InsuranceDisabled, Is.False);
    }

    [Test]
    public void MarkDiscardLimitItems_AlwaysAvailableForInsurance_DoesNotMark()
    {
        var items = MakeItems(
            new TemplateItemProperties
            {
                DiscardLimit = 1,
                InsuranceDisabled = false,
                IsAlwaysAvailableForInsurance = true,
            }
        );

        var count = DiscardLimitsService.MarkDiscardLimitItemsAsUninsurable(items);

        Assert.That(count, Is.EqualTo(0));
        Assert.That(items.Values.First().Properties!.InsuranceDisabled, Is.False);
    }

    [Test]
    public void MarkDiscardLimitItems_NullProperties_SkipsItem()
    {
        var items = new Dictionary<MongoId, TemplateItem> { [(MongoId)Id1] = new TemplateItem { Properties = null } };

        var count = DiscardLimitsService.MarkDiscardLimitItemsAsUninsurable(items);

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void MarkDiscardLimitItems_EmptyDictionary_ReturnsZero()
    {
        var items = new Dictionary<MongoId, TemplateItem>();

        var count = DiscardLimitsService.MarkDiscardLimitItemsAsUninsurable(items);

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void MarkDiscardLimitItems_MultipleItems_CountsCorrectly()
    {
        var items = new Dictionary<MongoId, TemplateItem>
        {
            [(MongoId)Id1] = new TemplateItem
            {
                Properties = new TemplateItemProperties
                {
                    DiscardLimit = 1,
                    InsuranceDisabled = false,
                    IsAlwaysAvailableForInsurance = false,
                },
            },
            [(MongoId)Id2] = new TemplateItem
            {
                Properties = new TemplateItemProperties
                {
                    DiscardLimit = -1,
                    InsuranceDisabled = false,
                    IsAlwaysAvailableForInsurance = false,
                },
            },
            [(MongoId)Id3] = new TemplateItem
            {
                Properties = new TemplateItemProperties
                {
                    DiscardLimit = 3,
                    InsuranceDisabled = false,
                    IsAlwaysAvailableForInsurance = true,
                },
            },
            [(MongoId)Id4] = new TemplateItem { Properties = null },
            [(MongoId)Id5] = new TemplateItem
            {
                Properties = new TemplateItemProperties
                {
                    DiscardLimit = 0,
                    InsuranceDisabled = false,
                    IsAlwaysAvailableForInsurance = false,
                },
            },
        };

        var count = DiscardLimitsService.MarkDiscardLimitItemsAsUninsurable(items);

        // Only Id1 (limit=1, not always insurable) and Id5 (limit=0, not always insurable) get marked.
        Assert.That(count, Is.EqualTo(2));
    }

    // ── Injectable attribute priority ──────────────────────────────

    [Test]
    public void Injectable_TypePriority_IsAfterDatabaseImport()
    {
        var attr = typeof(DiscardLimitsService)
            .GetCustomAttributes(inherit: false)
            .FirstOrDefault(a => a.GetType().Name.Contains("Injectable"));

        Assert.That(attr, Is.Not.Null, "DiscardLimitsService must have [Injectable] attribute");

        var priorityProp = attr!.GetType().GetProperty("TypePriority");
        Assert.That(priorityProp, Is.Not.Null, "Injectable attribute must have TypePriority property");

        var priority = (int)priorityProp!.GetValue(attr)!;

        // PostDBModLoader = 400_000. Must run after Database import (200_000).
        Assert.That(priority, Is.GreaterThan(200_000), "TypePriority must be after Database import (200,000)");
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static Dictionary<MongoId, TemplateItem> MakeItems(TemplateItemProperties props)
    {
        return new Dictionary<MongoId, TemplateItem> { [(MongoId)Id1] = new TemplateItem { Properties = props } };
    }
}
