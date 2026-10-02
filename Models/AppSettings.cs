namespace ExpenseTracker.Models;

/// <summary>
/// Immutable application configuration. Because it is a record, a changed copy is
/// made with the "with" expression instead of modifying the original, e.g.
/// <c>settings = settings with { MonthlyBudget = 1500m };</c>
/// </summary>
public sealed record AppSettings(
    string AppName,
    string Version,
    string CurrencySymbol,
    decimal MonthlyBudget,
    string DataFilePath)
{
    public static AppSettings Default { get; } = new(
        AppName: "FinTrack Expense Tracker",
        Version: "1.0.0",
        CurrencySymbol: "$",
        MonthlyBudget: 2000m,
        DataFilePath: Path.Combine("Data", "expenses.json"));
}
