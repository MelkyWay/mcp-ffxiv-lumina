using McpLumina.Models;
using McpLumina.Validators;
using Xunit;

namespace McpLumina.Tests.Unit;

public sealed class InputValidatorTests
{
    // ── ValidateSheetName ──────────────────────────────────────────────────

    [Theory]
    [InlineData("Action")]
    [InlineData("ClassJob")]
    [InlineData("Item")]
    [InlineData("ContentFinderCondition")]
    [InlineData("Quest/QuestClassJob")]
    [InlineData("A1")]
    public void ValidateSheetName_Valid_DoesNotThrow(string name)
    {
        var ex = Record.Exception(() => InputValidator.ValidateSheetName(name));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateSheetName_NullOrEmpty_Throws(string? name)
    {
        Assert.Throws<ValidationException>(() => InputValidator.ValidateSheetName(name));
    }

    [Theory]
    [InlineData("Action Sheet")]   // space
    [InlineData("../etc/passwd")]  // path traversal
    [InlineData("Sheet;DROP")]     // injection attempt
    [InlineData("Sheet<script>")]  // XSS attempt
    public void ValidateSheetName_InvalidChars_Throws(string name)
    {
        Assert.Throws<ValidationException>(() => InputValidator.ValidateSheetName(name));
    }

    [Fact]
    public void ValidateSheetName_TooLong_Throws()
    {
        var name = new string('A', 129);
        Assert.Throws<ValidationException>(() => InputValidator.ValidateSheetName(name));
    }

    // ── ValidateLimit ─────────────────────────────────────────────────────

    [Fact]
    public void ValidateLimit_Null_ReturnsDefault()
    {
        Assert.Equal(50, InputValidator.ValidateLimit(null));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(200)]
    public void ValidateLimit_InRange_ReturnsValue(int limit)
    {
        Assert.Equal(limit, InputValidator.ValidateLimit(limit));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValidateLimit_TooLow_Throws(int limit)
    {
        Assert.Throws<ValidationException>(() => InputValidator.ValidateLimit(limit));
    }

    [Fact]
    public void ValidateLimit_TooHigh_Throws()
    {
        Assert.Throws<ValidationException>(() => InputValidator.ValidateLimit(201));
    }

    // ── ValidateOffset ────────────────────────────────────────────────────

    [Fact]
    public void ValidateOffset_Null_ReturnsZero()
    {
        Assert.Equal(0, InputValidator.ValidateOffset(null));
    }

    [Fact]
    public void ValidateOffset_Negative_Throws()
    {
        Assert.Throws<ValidationException>(() => InputValidator.ValidateOffset(-1));
    }

    [Fact]
    public void ValidateOffset_OverMax_Throws()
    {
        Assert.Throws<ValidationException>(() => InputValidator.ValidateOffset(10_001));
    }

    // ── ValidateBatchSize ─────────────────────────────────────────────────

    [Fact]
    public void ValidateBatchSize_Empty_Throws()
    {
        Assert.Throws<ValidationException>(() => InputValidator.ValidateBatchSize([]));
    }

    [Fact]
    public void ValidateBatchSize_OverMax_Throws()
    {
        var ids = Enumerable.Range(0, 101).Select(i => (uint)i).ToArray();
        Assert.Throws<ValidationException>(() => InputValidator.ValidateBatchSize(ids));
    }

    [Fact]
    public void ValidateBatchSize_AtMax_DoesNotThrow()
    {
        var ids = Enumerable.Range(0, 100).Select(i => (uint)i).ToArray();
        var ex  = Record.Exception(() => InputValidator.ValidateBatchSize(ids));
        Assert.Null(ex);
    }

    // ── ValidateDutyCategory ──────────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("dungeon")]
    [InlineData("trial")]
    [InlineData("raid")]
    [InlineData("ultimate")]
    [InlineData("criterion")]
    [InlineData("unreal")]
    public void ValidateDutyCategory_Valid_DoesNotThrow(string? category)
    {
        var ex = Record.Exception(() => InputValidator.ValidateDutyCategory(category));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("Dungeon")]  // wrong case handled via ToLower, so this should pass
    [InlineData("RAID")]
    public void ValidateDutyCategory_UpperCase_DoesNotThrow(string category)
    {
        var ex = Record.Exception(() => InputValidator.ValidateDutyCategory(category));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("pvp")]
    [InlineData("unknown_category")]
    [InlineData("")]
    public void ValidateDutyCategory_Invalid_Throws(string category)
    {
        Assert.Throws<ValidationException>(() => InputValidator.ValidateDutyCategory(category));
    }

    // ── ValidateLabelKind ─────────────────────────────────────────────────

    [Theory]
    [InlineData("jobs")]
    [InlineData("roles")]
    [InlineData("categories")]
    [InlineData("ultimates")]
    [InlineData("criterion")]
    [InlineData("unreal")]
    public void ValidateLabelKind_Valid_DoesNotThrow(string kind)
    {
        var ex = Record.Exception(() => InputValidator.ValidateLabelKind(kind));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData("JOBS")]
    [InlineData("Roles")]
    [InlineData("UNREAL")]
    public void ValidateLabelKind_UpperCase_DoesNotThrow(string kind)
    {
        var ex = Record.Exception(() => InputValidator.ValidateLabelKind(kind));
        Assert.Null(ex);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateLabelKind_NullOrEmpty_ThrowsValidationError(string? kind)
    {
        // Must throw ValidationException, not NullReferenceException, so the
        // error maps to ValidationError rather than InternalError in the tool contract.
        Assert.Throws<ValidationException>(() => InputValidator.ValidateLabelKind(kind!));
    }

    [Fact]
    public void ValidateLabelKind_Unknown_Throws()
    {
        Assert.Throws<ValidationException>(() => InputValidator.ValidateLabelKind("pvp"));
    }

    // ── ParseLanguages ────────────────────────────────────────────────────

    [Fact]
    public void ParseLanguages_Null_ReturnsEmpty()
    {
        Assert.Empty(InputValidator.ParseLanguages(null));
    }

    [Fact]
    public void ParseLanguages_CommaSeparated_ReturnsParsed()
    {
        var result = InputValidator.ParseLanguages("en,fr,de");
        Assert.Equal(["en", "fr", "de"], result);
    }

    [Fact]
    public void ParseLanguages_Whitespace_Trimmed()
    {
        var result = InputValidator.ParseLanguages(" en , ja ");
        Assert.Equal(["en", "ja"], result);
    }

    [Fact]
    public void ParseLanguages_UpperCase_Lowercased()
    {
        var result = InputValidator.ParseLanguages("EN,FR");
        Assert.Equal(["en", "fr"], result);
    }

    [Fact]
    public void ParseLanguages_Duplicates_Deduplicated()
    {
        var result = InputValidator.ParseLanguages("en,en,fr");
        Assert.Equal(["en", "fr"], result);
    }

    // ── ParseColumnFilters ────────────────────────────────────────────────

    [Fact]
    public void ParseColumnFilters_Null_ReturnsEmpty()
    {
        Assert.Empty(InputValidator.ParseColumnFilters(null));
    }

    [Fact]
    public void ParseColumnFilters_Empty_ReturnsEmpty()
    {
        Assert.Empty(InputValidator.ParseColumnFilters(""));
    }

    [Fact]
    public void ParseColumnFilters_ColumnNFormat_ReturnsParsed()
    {
        var result = InputValidator.ParseColumnFilters("Column_10=24");
        Assert.Equal(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["Column_10"] = 24 }, result);
    }

    [Fact]
    public void ParseColumnFilters_NamedField_ReturnsParsed()
    {
        var result = InputValidator.ParseColumnFilters("ClassJob=24");
        Assert.Equal(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["ClassJob"] = 24 }, result);
    }

    [Fact]
    public void ParseColumnFilters_MultipleFilters_ReturnsParsed()
    {
        var result = InputValidator.ParseColumnFilters("ClassJob=24,Column_12=2");
        Assert.Equal(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["ClassJob"] = 24, ["Column_12"] = 2 }, result);
    }

    [Fact]
    public void ParseColumnFilters_WithSpaces_Trimmed()
    {
        var result = InputValidator.ParseColumnFilters(" ClassJobLevel = 7 ");
        Assert.Equal(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["ClassJobLevel"] = 7 }, result);
    }

    [Fact]
    public void ParseColumnFilters_NegativeValue_ReturnsParsed()
    {
        var result = InputValidator.ParseColumnFilters("Column_0=-1");
        Assert.Equal(new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase) { ["Column_0"] = -1 }, result);
    }

    [Theory]
    [InlineData("Column_5")]         // missing = and value
    [InlineData("=24")]              // empty field name
    [InlineData("Column_5=abc")]     // non-integer value
    [InlineData("Column_5=1.5")]     // float value
    public void ParseColumnFilters_InvalidFormat_Throws(string input)
    {
        Assert.Throws<ValidationException>(() => InputValidator.ParseColumnFilters(input));
    }

    [Fact]
    public void ParseColumnFilters_DuplicateColumn_Throws()
    {
        Assert.Throws<ValidationException>(() =>
            InputValidator.ParseColumnFilters("Column_5=24,Column_5=99"));
    }

    // ── ValidateClassJobId ────────────────────────────────────────────────

    [Fact]
    public void ValidateClassJobId_Null_ReturnsNegativeOne()
    {
        Assert.Equal(-1, InputValidator.ValidateClassJobId(null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(25)]
    public void ValidateClassJobId_NonNegative_ReturnsValue(int id)
    {
        Assert.Equal(id, InputValidator.ValidateClassJobId(id));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public void ValidateClassJobId_Negative_Throws(int id)
    {
        Assert.Throws<ValidationException>(() => InputValidator.ValidateClassJobId(id));
    }
}
