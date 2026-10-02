using ExpenseTracker.Models;

namespace ExpenseTracker.Interfaces;

/// <summary>
/// Everything the application can do with expenses and categories.
/// Program.cs only talks to this interface, so the implementation can be
/// swapped (for example for a database version) without touching the UI.
/// </summary>
public interface IExpenseManager
{
    IReadOnlyList<Expense> Expenses { get; }

    IReadOnlyDictionary<int, Category> Categories { get; }

    // ---- Adding and removing -------------------------------------------

    Expense AddExpense(Expense expense);

    Expense AddExpense(string title, decimal amount, string category, DateTime date, string? notes = null);

    bool RemoveExpense(int id);

    Expense? FindById(int id);

    /// <summary>Replaces all expenses, e.g. with the ones loaded from a file.</summary>
    void ReplaceAll(IEnumerable<Expense> expenses);

    // ---- Categories ----------------------------------------------------

    Category AddCategory(string name, string? description = null);

    Category? FindCategory(string name);

    Category? GetCategory(int id);

    // ---- Searching, filtering and sorting (overloaded Search) ----------

    IReadOnlyList<Expense> Search(string keyword);

    IReadOnlyList<Expense> Search(decimal minAmount, decimal maxAmount);

    IReadOnlyList<Expense> Search(DateTime from, DateTime to);

    IReadOnlyList<Expense> FilterByCategory(string category);

    IReadOnlyList<Expense> Sort(SortOption option);

    // ---- Calculations and reports --------------------------------------

    decimal CalculateTotalExpenses();

    decimal CalculateTotalExpenses(string category);

    decimal CalculateAverageExpense();

    Expense? GetHighestExpense();

    Expense? GetLowestExpense();

    IReadOnlyList<CategorySummary> GroupByCategory();

    IReadOnlyList<MonthlyTotal> GetMonthlyTotals();

    ExpenseSummary GetSummary(decimal monthlyBudget, DateTime? asOf = null);
}
