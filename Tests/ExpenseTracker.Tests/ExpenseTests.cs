using ExpenseTracker.Exceptions;
using ExpenseTracker.Models;

namespace ExpenseTracker.Tests;

/// <summary>Tests for the Expense and RecurringExpense models and their validation rules.</summary>
public class ExpenseTests
{
    private static readonly DateTime SampleDate = new(2026, 9, 15);

    [Fact]
    public void Constructor_WithValidData_SetsAllProperties()
    {
        var expense = new Expense("  Groceries  ", 54.30m, "Food", SampleDate, "Weekly shop");

        Assert.Equal("Groceries", expense.Title); // trimmed
        Assert.Equal(54.30m, expense.Amount);
        Assert.Equal("Food", expense.Category);
        Assert.Equal(SampleDate, expense.Date);
        Assert.Equal("Weekly shop", expense.Notes);
        Assert.Equal("One-time", expense.ExpenseType);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyTitle_ThrowsInvalidExpenseException(string title)
    {
        var ex = Assert.Throws<InvalidExpenseException>(() => new Expense(title, 10m, "Food", SampleDate));

        Assert.Equal(nameof(Expense.Title), ex.FieldName);
    }

    [Fact]
    public void Constructor_WithTooLongTitle_ThrowsInvalidExpenseException()
    {
        string longTitle = new('a', Expense.MaxTitleLength + 1);

        Assert.Throws<InvalidExpenseException>(() => new Expense(longTitle, 10m, "Food", SampleDate));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-250.75)]
    public void Constructor_WithZeroOrNegativeAmount_ThrowsInvalidExpenseException(double amount)
    {
        var ex = Assert.Throws<InvalidExpenseException>(
            () => new Expense("Coffee", (decimal)amount, "Food", SampleDate));

        Assert.Equal(nameof(Expense.Amount), ex.FieldName);
        Assert.Contains("greater than zero", ex.Message);
    }

    [Fact]
    public void Amount_WithMoreThanTwoDecimalPlaces_ThrowsInvalidExpenseException()
    {
        var expense = new Expense("Coffee", 3.50m, "Food", SampleDate);

        Assert.Throws<InvalidExpenseException>(() => expense.Amount = 3.555m);
        Assert.Equal(3.50m, expense.Amount); // the old value is kept
    }

    [Fact]
    public void Date_InTheFuture_ThrowsInvalidExpenseException()
    {
        DateTime tomorrow = DateTime.Today.AddDays(1);

        Assert.Throws<InvalidExpenseException>(() => new Expense("Concert", 40m, "Entertainment", tomorrow));
    }

    [Fact]
    public void Category_WhenEmpty_ThrowsInvalidExpenseException()
    {
        Assert.Throws<InvalidExpenseException>(() => new Expense("Coffee", 3m, " ", SampleDate));
    }

    [Fact]
    public void Notes_WhenBlank_AreStoredAsNull()
    {
        var expense = new Expense("Coffee", 3m, "Food", SampleDate, "   ");

        Assert.Null(expense.Notes);
    }

    [Fact]
    public void Validate_OnExpenseWithDefaultValues_ThrowsInvalidExpenseException()
    {
        var empty = new Expense(); // parameterless constructor leaves the title empty

        Assert.Throws<InvalidExpenseException>(() => empty.Validate());
    }

    [Theory]
    [InlineData(RecurrenceFrequency.Weekly, 12, 52)]   // 12 * 52 / 12 = 52.00
    [InlineData(RecurrenceFrequency.Monthly, 15.49, 15.49)]
    [InlineData(RecurrenceFrequency.Yearly, 120, 10)]
    public void RecurringExpense_MonthlyEquivalent_DependsOnFrequency(
        RecurrenceFrequency frequency, double amount, double expectedPerMonth)
    {
        var recurring = new RecurringExpense("Subscription", (decimal)amount, "Entertainment", SampleDate, frequency);

        Assert.Equal((decimal)expectedPerMonth, recurring.MonthlyEquivalent);
    }

    [Fact]
    public void RecurringExpense_GetNextDueDate_ReturnsFirstDateAfterGivenDate()
    {
        var rent = new RecurringExpense("Rent", 850m, "Housing", new DateTime(2026, 1, 31), RecurrenceFrequency.Monthly);

        // Calculated from the original date, so the 31st does not drift to the 28th after February.
        Assert.Equal(new DateTime(2026, 2, 28), rent.GetNextDueDate(new DateTime(2026, 2, 1)));
        Assert.Equal(new DateTime(2026, 3, 31), rent.GetNextDueDate(new DateTime(2026, 3, 1)));
    }

    [Fact]
    public void RecurringExpense_OverridesDisplayMethods()
    {
        Expense expense = new RecurringExpense("Netflix", 15.49m, "Entertainment", SampleDate, RecurrenceFrequency.Monthly);

        // Called through a base-class reference, the overridden versions still run.
        Assert.Equal("Monthly", expense.ExpenseType);
        Assert.Contains("repeats monthly", expense.GetDisplayText());
        Assert.Contains("[repeats Monthly]", expense.ToString());
    }

    [Fact]
    public void Category_Matches_IsCaseInsensitive()
    {
        var category = new Category(1, "Food");

        Assert.True(category.Matches("food"));
        Assert.True(category.Matches("  FOOD "));
        Assert.False(category.Matches("Fun"));
    }

    [Fact]
    public void ExpenseSummary_IsARecordWithValueEquality()
    {
        var first = new ExpenseSummary(2, 100m, 50m, null, null, 500m, 100m, 0m);
        var second = new ExpenseSummary(2, 100m, 50m, null, null, 500m, 100m, 0m);
        ExpenseSummary changed = first with { SpentThisMonth = 600m };

        Assert.Equal(first, second);
        Assert.Equal(400m, first.RemainingBudget);
        Assert.False(first.IsOverBudget);
        Assert.True(changed.IsOverBudget);
        Assert.Equal(-100m, changed.RemainingBudget);
    }
}
