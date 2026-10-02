namespace ExpenseTracker.Models;

/// <summary>
/// Immutable snapshot of the spending figures shown on the summary screen.
/// Records give value equality and a readable ToString() for free.
/// </summary>
public sealed record ExpenseSummary(
    int Count,
    decimal Total,
    decimal Average,
    Expense? Highest,
    Expense? Lowest,
    decimal MonthlyBudget,
    decimal SpentThisMonth,
    decimal RecurringPerMonth)
{
    /// <summary>Budget minus what was spent in the current month.</summary>
    public decimal RemainingBudget => MonthlyBudget - SpentThisMonth;

    public bool IsOverBudget => RemainingBudget < 0;

    /// <summary>Share of the monthly budget already used, as a percentage.</summary>
    public decimal BudgetUsedPercent => MonthlyBudget > 0 ? SpentThisMonth / MonthlyBudget * 100 : 0;
}

/// <summary>Totals for one category, produced by a LINQ group-by query.</summary>
public sealed record CategorySummary(string Category, int Count, decimal Total, decimal Average, decimal Highest)
{
    /// <summary>This category's share of the grand total, as a percentage.</summary>
    public decimal ShareOf(decimal grandTotal) => grandTotal > 0 ? Total / grandTotal * 100 : 0;
}

/// <summary>Totals for one calendar month.</summary>
public sealed record MonthlyTotal(int Year, int Month, int Count, decimal Total)
{
    public string Label => new DateTime(Year, Month, 1).ToString("MMM yyyy");
}
