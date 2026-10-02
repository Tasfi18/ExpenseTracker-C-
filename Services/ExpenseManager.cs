using ExpenseTracker.Exceptions;
using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

/// <summary>
/// Business logic for expenses: stores them in a List, categories in a Dictionary,
/// and answers every search, filter and report with LINQ and lambda expressions.
/// It never reads from or writes to the console, which keeps it easy to unit test.
/// </summary>
public class ExpenseManager : IExpenseManager
{
    private readonly List<Expense> _expenses = [];
    private readonly Dictionary<int, Category> _categories = [];
    private int _nextExpenseId = 1;

    public ExpenseManager()
        : this(Category.CreateDefaults())
    {
    }

    public ExpenseManager(IEnumerable<Category> categories)
    {
        foreach (Category category in categories)
        {
            _categories[category.Id] = category;
        }
    }

    public IReadOnlyList<Expense> Expenses => _expenses.AsReadOnly();

    public IReadOnlyDictionary<int, Category> Categories => _categories;

    // =====================================================================
    // Adding and removing
    // =====================================================================

    public Expense AddExpense(Expense expense)
    {
        ArgumentNullException.ThrowIfNull(expense);

        if (_expenses.Contains(expense))
        {
            throw new InvalidExpenseException($"Expense #{expense.Id} has already been added.");
        }

        // An Expense made with the parameterless constructor still has default
        // values (empty title, zero amount), so check the whole object again.
        expense.Validate();

        // Store the category with its official spelling ("food" -> "Food"),
        // creating the category if it is new.
        expense.Category = EnsureCategory(expense.Category).Name;

        expense.Id = _nextExpenseId++;
        _expenses.Add(expense);
        return expense;
    }

    public Expense AddExpense(string title, decimal amount, string category, DateTime date, string? notes = null) =>
        AddExpense(new Expense(title, amount, category, date, notes));

    public bool RemoveExpense(int id) => _expenses.RemoveAll(e => e.Id == id) > 0;

    public Expense? FindById(int id) => _expenses.FirstOrDefault(e => e.Id == id);

    public void ReplaceAll(IEnumerable<Expense> expenses)
    {
        ArgumentNullException.ThrowIfNull(expenses);

        // Validate everything before touching the current list, so a bad
        // record leaves the existing data unchanged.
        List<Expense> incoming = expenses.ToList();
        incoming.ForEach(e => e.Validate());

        // Keep the saved ids if they are usable; otherwise number the records 1, 2, 3...
        bool idsAreUsable = incoming.All(e => e.Id > 0)
                            && incoming.Select(e => e.Id).Distinct().Count() == incoming.Count;

        _expenses.Clear();
        int nextId = 1;
        foreach (Expense expense in incoming)
        {
            expense.Category = EnsureCategory(expense.Category).Name;
            if (!idsAreUsable)
            {
                expense.Id = nextId++;
            }

            _expenses.Add(expense);
        }

        _nextExpenseId = _expenses.Count == 0 ? 1 : _expenses.Max(e => e.Id) + 1;
    }

    // =====================================================================
    // Categories
    // =====================================================================

    public Category AddCategory(string name, string? description = null)
    {
        string cleanName = Category.ValidateName(name);
        if (FindCategory(cleanName) is not null)
        {
            throw new InvalidExpenseException($"A category called '{cleanName}' already exists.", nameof(Expense.Category));
        }

        int newId = _categories.Count == 0 ? 1 : _categories.Keys.Max() + 1;
        var category = new Category(newId, cleanName, description);
        _categories.Add(newId, category);
        return category;
    }

    public Category? FindCategory(string name) =>
        _categories.Values.FirstOrDefault(c => c.Matches(name));

    public Category? GetCategory(int id) =>
        _categories.TryGetValue(id, out Category? category) ? category : null;

    /// <summary>Returns the existing category with this name, or creates it.</summary>
    private Category EnsureCategory(string name) => FindCategory(name) ?? AddCategory(name);

    // =====================================================================
    // Search (three overloads), filter and sort
    // =====================================================================

    /// <summary>Finds expenses whose title, category or notes contain the keyword (case-insensitive).</summary>
    public IReadOnlyList<Expense> Search(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        string term = keyword.Trim();
        return _expenses
            .Where(e => e.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || e.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (e.Notes?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderByDescending(e => e.Date)
            .ThenBy(e => e.Id)
            .ToList();
    }

    /// <summary>Finds expenses with an amount between the two values (inclusive).</summary>
    public IReadOnlyList<Expense> Search(decimal minAmount, decimal maxAmount)
    {
        if (minAmount > maxAmount)
        {
            (minAmount, maxAmount) = (maxAmount, minAmount); // tuple swap if entered the wrong way round
        }

        return _expenses
            .Where(e => e.Amount >= minAmount && e.Amount <= maxAmount)
            .OrderByDescending(e => e.Amount)
            .ToList();
    }

    /// <summary>Finds expenses dated between the two dates (inclusive).</summary>
    public IReadOnlyList<Expense> Search(DateTime from, DateTime to)
    {
        DateTime start = from.Date;
        DateTime end = to.Date;
        if (start > end)
        {
            (start, end) = (end, start);
        }

        return _expenses
            .Where(e => e.Date >= start && e.Date <= end)
            .OrderBy(e => e.Date)
            .ThenBy(e => e.Id)
            .ToList();
    }

    public IReadOnlyList<Expense> FilterByCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
        {
            return [];
        }

        string name = category.Trim();
        return _expenses
            .Where(e => e.Category.Equals(name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Date)
            .ThenBy(e => e.Id)
            .ToList();
    }

    public IReadOnlyList<Expense> Sort(SortOption option) => option switch
    {
        SortOption.DateNewestFirst => _expenses.OrderByDescending(e => e.Date).ThenByDescending(e => e.Id).ToList(),
        SortOption.DateOldestFirst => _expenses.OrderBy(e => e.Date).ThenBy(e => e.Id).ToList(),
        SortOption.AmountHighestFirst => _expenses.OrderByDescending(e => e.Amount).ThenBy(e => e.Date).ToList(),
        SortOption.AmountLowestFirst => _expenses.OrderBy(e => e.Amount).ThenBy(e => e.Date).ToList(),
        SortOption.Title => _expenses.OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Date).ToList(),
        SortOption.Category => _expenses.OrderBy(e => e.Category).ThenByDescending(e => e.Amount).ToList(),
        _ => throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown sort option."),
    };

    // =====================================================================
    // Calculations and reports
    // =====================================================================

    public decimal CalculateTotalExpenses() => _expenses.Sum(e => e.Amount);

    public decimal CalculateTotalExpenses(string category) => FilterByCategory(category).Sum(e => e.Amount);

    public decimal CalculateAverageExpense() =>
        _expenses.Count == 0 ? 0m : decimal.Round(_expenses.Average(e => e.Amount), 2);

    public Expense? GetHighestExpense() => _expenses.MaxBy(e => e.Amount);

    public Expense? GetLowestExpense() => _expenses.MinBy(e => e.Amount);

    /// <summary>Totals per category, biggest first (written in LINQ query syntax).</summary>
    public IReadOnlyList<CategorySummary> GroupByCategory()
    {
        IEnumerable<CategorySummary> query =
            from expense in _expenses
            group expense by expense.Category into categoryGroup
            let total = categoryGroup.Sum(e => e.Amount)
            orderby total descending, categoryGroup.Key
            select new CategorySummary(
                Category: categoryGroup.Key,
                Count: categoryGroup.Count(),
                Total: total,
                Average: decimal.Round(total / categoryGroup.Count(), 2),
                Highest: categoryGroup.Max(e => e.Amount));

        return query.ToList();
    }

    /// <summary>Totals per calendar month, oldest first.</summary>
    public IReadOnlyList<MonthlyTotal> GetMonthlyTotals() =>
        _expenses
            .GroupBy(e => new { e.Date.Year, e.Date.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyTotal(g.Key.Year, g.Key.Month, g.Count(), g.Sum(e => e.Amount)))
            .ToList();

    public ExpenseSummary GetSummary(decimal monthlyBudget, DateTime? asOf = null)
    {
        DateTime today = (asOf ?? DateTime.Today).Date;

        decimal spentThisMonth = _expenses
            .Where(e => e.Date.Year == today.Year && e.Date.Month == today.Month)
            .Sum(e => e.Amount);

        // OfType<T>() picks out only the RecurringExpense objects from the list.
        decimal recurringPerMonth = _expenses
            .OfType<RecurringExpense>()
            .Sum(r => r.MonthlyEquivalent);

        return new ExpenseSummary(
            Count: _expenses.Count,
            Total: CalculateTotalExpenses(),
            Average: CalculateAverageExpense(),
            Highest: GetHighestExpense(),
            Lowest: GetLowestExpense(),
            MonthlyBudget: monthlyBudget,
            SpentThisMonth: spentThisMonth,
            RecurringPerMonth: recurringPerMonth);
    }
}
