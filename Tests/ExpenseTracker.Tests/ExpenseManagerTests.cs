using ExpenseTracker.Exceptions;
using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests;

/// <summary>
/// Tests for ExpenseManager: adding, totals, searching, filtering, grouping and exceptions.
/// xUnit creates a new instance of this class for every test, so each test gets a fresh manager.
/// </summary>
public class ExpenseManagerTests
{
    private readonly IExpenseManager _manager = new ExpenseManager();

    /// <summary>Adds a small, known set of expenses (total 1,010.00).</summary>
    private void AddSampleExpenses()
    {
        _manager.AddExpense("Groceries at FreshMart", 50.00m, "Food", new DateTime(2026, 9, 3));
        _manager.AddExpense("Dinner with friends", 40.00m, "Food", new DateTime(2026, 9, 12), "Birthday dinner");
        _manager.AddExpense("Bus pass", 30.00m, "Transport", new DateTime(2026, 9, 2));
        _manager.AddExpense(new RecurringExpense(
            "Apartment rent", 800.00m, "Housing", new DateTime(2026, 9, 1), RecurrenceFrequency.Monthly));
        _manager.AddExpense("Running shoes", 90.00m, "Shopping", new DateTime(2026, 8, 20));
    }

    // ---------------------------------------------------------------------
    // Adding expenses
    // ---------------------------------------------------------------------

    [Fact]
    public void AddExpense_ValidExpense_IsStoredWithNewId()
    {
        Expense added = _manager.AddExpense("Coffee", 3.80m, "Food", new DateTime(2026, 9, 24));

        Assert.Single(_manager.Expenses);
        Assert.Equal(1, added.Id);
        Assert.Same(added, _manager.FindById(1));
    }

    [Fact]
    public void AddExpense_SeveralExpenses_GetIncreasingIds()
    {
        AddSampleExpenses();

        Assert.Equal(5, _manager.Expenses.Count);
        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, _manager.Expenses.Select(e => e.Id).ToArray());
    }

    [Fact]
    public void AddExpense_CategoryInDifferentCase_UsesExistingCategoryName()
    {
        Expense added = _manager.AddExpense("Coffee", 3.80m, "fOoD", new DateTime(2026, 9, 24));

        Assert.Equal("Food", added.Category);
    }

    [Fact]
    public void AddExpense_UnknownCategory_CreatesCategory()
    {
        int categoriesBefore = _manager.Categories.Count;

        _manager.AddExpense("Dog food", 25m, "Pets", new DateTime(2026, 9, 10));

        Assert.Equal(categoriesBefore + 1, _manager.Categories.Count);
        Assert.NotNull(_manager.FindCategory("pets"));
    }

    [Fact]
    public void AddExpense_RecurringExpense_KeepsDerivedType()
    {
        AddSampleExpenses();

        RecurringExpense rent = Assert.IsType<RecurringExpense>(_manager.FindById(4));
        Assert.Equal(RecurrenceFrequency.Monthly, rent.Frequency);
    }

    // ---------------------------------------------------------------------
    // Exception handling
    // ---------------------------------------------------------------------

    [Fact]
    public void AddExpense_ExpenseWithDefaultValues_ThrowsInvalidExpenseException()
    {
        Assert.Throws<InvalidExpenseException>(() => _manager.AddExpense(new Expense()));
        Assert.Empty(_manager.Expenses);
    }

    [Fact]
    public void AddExpense_NegativeAmount_ThrowsInvalidExpenseException()
    {
        var ex = Assert.Throws<InvalidExpenseException>(
            () => _manager.AddExpense("Refund?", -20m, "Other", new DateTime(2026, 9, 1)));

        Assert.Equal("Amount", ex.FieldName);
        Assert.Empty(_manager.Expenses);
    }

    [Fact]
    public void AddExpense_SameObjectTwice_ThrowsInvalidExpenseException()
    {
        var coffee = new Expense("Coffee", 3.80m, "Food", new DateTime(2026, 9, 24));
        _manager.AddExpense(coffee);

        Assert.Throws<InvalidExpenseException>(() => _manager.AddExpense(coffee));
        Assert.Single(_manager.Expenses);
    }

    [Fact]
    public void AddExpense_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _manager.AddExpense(null!));
    }

    [Fact]
    public void AddCategory_DuplicateName_ThrowsInvalidExpenseException()
    {
        Assert.Throws<InvalidExpenseException>(() => _manager.AddCategory("food"));
    }

    // ---------------------------------------------------------------------
    // Totals and statistics
    // ---------------------------------------------------------------------

    [Fact]
    public void CalculateTotalExpenses_NoExpenses_ReturnsZero()
    {
        Assert.Equal(0m, _manager.CalculateTotalExpenses());
        Assert.Equal(0m, _manager.CalculateAverageExpense());
        Assert.Null(_manager.GetHighestExpense());
    }

    [Fact]
    public void CalculateTotalExpenses_ReturnsSumOfAllAmounts()
    {
        AddSampleExpenses();

        Assert.Equal(1010.00m, _manager.CalculateTotalExpenses());
    }

    [Fact]
    public void CalculateTotalExpenses_ForCategory_ReturnsCategorySum()
    {
        AddSampleExpenses();

        Assert.Equal(90.00m, _manager.CalculateTotalExpenses("food"));
        Assert.Equal(0m, _manager.CalculateTotalExpenses("Health"));
    }

    [Fact]
    public void CalculateTotalExpenses_UpdatesWhenExpenseIsRemoved()
    {
        AddSampleExpenses();

        bool removed = _manager.RemoveExpense(4); // the 800.00 rent

        Assert.True(removed);
        Assert.Equal(210.00m, _manager.CalculateTotalExpenses());
        Assert.Equal(4, _manager.Expenses.Count);
    }

    [Fact]
    public void RemoveExpense_UnknownId_ReturnsFalse()
    {
        AddSampleExpenses();

        Assert.False(_manager.RemoveExpense(99));
        Assert.Equal(5, _manager.Expenses.Count);
    }

    [Fact]
    public void CalculateAverageExpense_ReturnsRoundedAverage()
    {
        AddSampleExpenses();

        Assert.Equal(202.00m, _manager.CalculateAverageExpense()); // 1010 / 5
    }

    [Fact]
    public void GetHighestAndLowestExpense_ReturnExpectedExpenses()
    {
        AddSampleExpenses();

        Assert.Equal("Apartment rent", _manager.GetHighestExpense()?.Title);
        Assert.Equal("Bus pass", _manager.GetLowestExpense()?.Title);
    }

    // ---------------------------------------------------------------------
    // Searching (three overloads)
    // ---------------------------------------------------------------------

    [Fact]
    public void Search_ByKeyword_IsCaseInsensitiveAndChecksTitleCategoryAndNotes()
    {
        AddSampleExpenses();

        Assert.Single(_manager.Search("GROCERIES"));        // title
        Assert.Equal(2, _manager.Search("food").Count);     // category
        Assert.Single(_manager.Search("birthday"));         // notes
    }

    [Fact]
    public void Search_ByKeyword_NoMatchOrBlank_ReturnsEmpty()
    {
        AddSampleExpenses();

        Assert.Empty(_manager.Search("holiday"));
        Assert.Empty(_manager.Search("   "));
    }

    [Fact]
    public void Search_ByAmountRange_IncludesBoundaries()
    {
        AddSampleExpenses();

        IReadOnlyList<Expense> results = _manager.Search(30m, 50m);

        Assert.Equal(3, results.Count);
        Assert.All(results, e => Assert.InRange(e.Amount, 30m, 50m));
    }

    [Fact]
    public void Search_ByAmountRange_WithBoundsReversed_StillWorks()
    {
        AddSampleExpenses();

        Assert.Equal(_manager.Search(30m, 50m).Count, _manager.Search(50m, 30m).Count);
    }

    [Fact]
    public void Search_ByDateRange_ReturnsExpensesInRangeOldestFirst()
    {
        AddSampleExpenses();

        IReadOnlyList<Expense> results = _manager.Search(new DateTime(2026, 9, 1), new DateTime(2026, 9, 3));

        Assert.Equal(new[] { "Apartment rent", "Bus pass", "Groceries at FreshMart" }, results.Select(e => e.Title).ToArray());
    }

    // ---------------------------------------------------------------------
    // Filtering, sorting and grouping
    // ---------------------------------------------------------------------

    [Fact]
    public void FilterByCategory_ReturnsOnlyThatCategory()
    {
        AddSampleExpenses();

        IReadOnlyList<Expense> food = _manager.FilterByCategory("food");

        Assert.Equal(2, food.Count);
        Assert.All(food, e => Assert.Equal("Food", e.Category));
    }

    [Fact]
    public void FilterByCategory_UnknownOrEmptyCategory_ReturnsEmpty()
    {
        AddSampleExpenses();

        Assert.Empty(_manager.FilterByCategory("Travel"));
        Assert.Empty(_manager.FilterByCategory(""));
    }

    [Theory]
    [InlineData(SortOption.AmountHighestFirst, "Apartment rent")]
    [InlineData(SortOption.AmountLowestFirst, "Bus pass")]
    [InlineData(SortOption.DateNewestFirst, "Dinner with friends")]
    [InlineData(SortOption.DateOldestFirst, "Running shoes")]
    [InlineData(SortOption.Title, "Apartment rent")]
    public void Sort_PutsExpectedExpenseFirst(SortOption option, string expectedFirstTitle)
    {
        AddSampleExpenses();

        Assert.Equal(expectedFirstTitle, _manager.Sort(option)[0].Title);
    }

    [Fact]
    public void GroupByCategory_ReturnsTotalsBiggestFirst()
    {
        AddSampleExpenses();

        IReadOnlyList<CategorySummary> groups = _manager.GroupByCategory();

        Assert.Equal(4, groups.Count);
        Assert.Equal("Housing", groups[0].Category);
        CategorySummary food = Assert.Single(groups, g => g.Category == "Food");
        Assert.Equal(2, food.Count);
        Assert.Equal(90m, food.Total);
        Assert.Equal(45m, food.Average);
        Assert.Equal(_manager.CalculateTotalExpenses(), groups.Sum(g => g.Total));
    }

    [Fact]
    public void GetMonthlyTotals_GroupsByCalendarMonth()
    {
        AddSampleExpenses();

        IReadOnlyList<MonthlyTotal> months = _manager.GetMonthlyTotals();

        Assert.Equal(2, months.Count);
        Assert.Equal(new MonthlyTotal(2026, 8, 1, 90m), months[0]);
        Assert.Equal(new MonthlyTotal(2026, 9, 4, 920m), months[1]);
    }

    [Fact]
    public void GetSummary_CalculatesBudgetFiguresForTheMonth()
    {
        AddSampleExpenses();

        ExpenseSummary summary = _manager.GetSummary(monthlyBudget: 1000m, asOf: new DateTime(2026, 9, 30));

        Assert.Equal(5, summary.Count);
        Assert.Equal(1010m, summary.Total);
        Assert.Equal(920m, summary.SpentThisMonth);   // August's shoes are not counted
        Assert.Equal(80m, summary.RemainingBudget);
        Assert.Equal(92m, summary.BudgetUsedPercent);
        Assert.Equal(800m, summary.RecurringPerMonth);
        Assert.False(summary.IsOverBudget);
    }

    // ---------------------------------------------------------------------
    // Replacing all expenses (used after loading from a file)
    // ---------------------------------------------------------------------

    [Fact]
    public void ReplaceAll_WithoutUsableIds_RenumbersAndContinuesNumbering()
    {
        // Expenses built with the constructor have Id 0 until a manager numbers them.
        var loaded = new List<Expense>
        {
            new("Tea", 2m, "Food", new DateTime(2026, 9, 1)),
            new("Cake", 4m, "Food", new DateTime(2026, 9, 2)),
        };

        _manager.ReplaceAll(loaded);
        Expense next = _manager.AddExpense("Juice", 3m, "Food", new DateTime(2026, 9, 3));

        Assert.Equal(new[] { 1, 2, 3 }, _manager.Expenses.Select(e => e.Id).ToArray());
        Assert.Equal(3, next.Id);
    }

    [Fact]
    public void ReplaceAll_WithInvalidExpense_KeepsExistingData()
    {
        AddSampleExpenses();
        var bad = new List<Expense> { new() }; // empty title, zero amount

        Assert.Throws<InvalidExpenseException>(() => _manager.ReplaceAll(bad));
        Assert.Equal(5, _manager.Expenses.Count);
    }
}
