using ExpenseTracker.Exceptions;

namespace ExpenseTracker.Models;

/// <summary>
/// A spending category such as Food or Transport.
/// Categories are kept in a Dictionary&lt;int, Category&gt; inside the ExpenseManager,
/// so the user can pick one by its number.
/// </summary>
public class Category
{
    public const int MaxNameLength = 30;

    public Category(int id, string name, string? description = null)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Category id must be a positive number.");
        }

        Id = id;
        Name = ValidateName(name);
        Description = description?.Trim() ?? string.Empty;
    }

    public int Id { get; }

    public string Name { get; }

    public string Description { get; }

    /// <summary>Checks a category name and returns it trimmed, or throws InvalidExpenseException.</summary>
    public static string ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidExpenseException("Category name cannot be empty.", nameof(Expense.Category));
        }

        string trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new InvalidExpenseException(
                $"Category name cannot be longer than {MaxNameLength} characters.", nameof(Expense.Category));
        }

        return trimmed;
    }

    /// <summary>Case-insensitive name comparison, so "food" matches "Food".</summary>
    public bool Matches(string? name) =>
        string.Equals(Name, name?.Trim(), StringComparison.OrdinalIgnoreCase);

    /// <summary>The categories every new tracker starts with.</summary>
    public static IEnumerable<Category> CreateDefaults() =>
    [
        new Category(1, "Food", "Groceries, restaurants and snacks"),
        new Category(2, "Transport", "Fuel, bus, train and taxi"),
        new Category(3, "Housing", "Rent, repairs and furniture"),
        new Category(4, "Utilities", "Electricity, water, internet and phone"),
        new Category(5, "Entertainment", "Streaming, games, cinema and events"),
        new Category(6, "Health", "Medicine, doctor visits and gym"),
        new Category(7, "Shopping", "Clothes, electronics and household items"),
        new Category(8, "Other", "Anything that does not fit elsewhere"),
    ];

    public override string ToString() =>
        string.IsNullOrEmpty(Description) ? $"{Id}. {Name}" : $"{Id}. {Name} - {Description}";
}
