using CrmSync.Core.Model;
using CrmSync.Core.Source;
using CrmSync.Core.Target;
using static CrmSync.Core.Tests.TestData;

namespace CrmSync.Core.Tests;

public class NormalizeTests
{
    [Fact]
    public void Email_is_trimmed_and_lower_cased() => Assert.Equal("ada@example.com", Normalize.Email("  Ada@Example.COM "));

    [Theory]
    [InlineData("(555) 010-2030", "5550102030")]
    [InlineData("+1 555 010 2030", "+15550102030")]
    [InlineData(" +44 (0)20 7946 0018 ", "+4402079460018")]
    [InlineData("n/a", null)]
    [InlineData("", null)]
    public void Phone_keeps_digits_and_a_leading_plus(string input, string? expected) => Assert.Equal(expected, Normalize.Phone(input));

    [Theory]
    [InlineData("ada@example.com", true)]
    [InlineData("ada@example", false)]
    [InlineData("ada-at-example.com", false)]
    [InlineData("ada lovelace@example.com", false)]
    [InlineData(null, false)]
    public void Email_validation(string? email, bool valid) => Assert.Equal(valid, Normalize.IsValidEmail(email));
}

public class AlphaAdapterTests
{
    [Fact]
    public void Maps_and_normalizes_a_valid_record()
    {
        var result = AlphaAdapter.ToCanonical(Alpha("A-1", " Ada ", "Lovelace", "ADA@Example.com", "(555) 010-2030", " Analytical Co ", "customer"));

        Assert.Null(result.Error);
        Assert.Equal(new Contact("A-1", "Ada", "Lovelace", "ada@example.com", "5550102030", "Analytical Co", LifecycleStage.Customer), result.Contact);
    }

    [Fact]
    public void Missing_lifecycle_defaults_to_lead() =>
        Assert.Equal(LifecycleStage.Lead, AlphaAdapter.ToCanonical(Alpha("A-1", "Ada", "Lovelace", "ada@example.com", lifecycle: null)).Contact?.Stage);

    [Theory]
    [InlineData("ada-at-example.com", "lead", "Invalid email")]
    [InlineData("ada@example.com", "partner", "Unknown lifecycle")]
    public void Rejects_records_it_cannot_map_safely(string email, string lifecycle, string expectedError)
    {
        var result = AlphaAdapter.ToCanonical(Alpha("A-1", "Ada", "Lovelace", email, lifecycle: lifecycle));

        Assert.Null(result.Contact);
        Assert.StartsWith(expectedError, result.Error);
    }

    [Fact]
    public void Rejects_a_record_with_no_name() =>
        Assert.Equal("Missing name", AlphaAdapter.ToCanonical(Alpha("A-1", " ", "", "ada@example.com")).Error);
}

public class BetaAdapterTests
{
    [Fact]
    public void Joins_names_and_maps_stage_to_the_target_vocabulary()
    {
        var fields = BetaAdapter.ToFields(new Contact("A-1", "Ada", "Lovelace", "ada@example.com", null, "Analytical Co", LifecycleStage.Customer));

        Assert.Equal(new BetaFields("Ada Lovelace", "ada@example.com", null, "Analytical Co", "Client"), fields);
    }

    [Fact]
    public void Diff_reports_only_the_fields_that_changed()
    {
        var current = new BetaFields("Ada Lovelace", "ada@example.com", "5550100000", "Analytical Co", "Prospect");
        var desired = current with { PhoneNumber = "5550102030", Stage = "Client" };

        var diff = BetaAdapter.Diff(current, desired);

        Assert.Equal(
            [new FieldChange("PhoneNumber", "5550100000", "5550102030"), new FieldChange("Stage", "Prospect", "Client")],
            diff);
    }

    [Fact]
    public void Diff_is_empty_for_identical_fields()
    {
        var fields = new BetaFields("Ada Lovelace", "ada@example.com", null, null, "Prospect");

        Assert.Empty(BetaAdapter.Diff(fields, fields with { }));
    }
}
