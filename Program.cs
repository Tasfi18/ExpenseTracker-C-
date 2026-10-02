using System.Diagnostics;
using System.Globalization;
using ExpenseTracker.Exceptions;
using ExpenseTracker.Interfaces;
using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker;

/// <summary>
/// Entry point and console menu for the FinTrack Expense Tracker.
/// Program.cs only handles input and output; all business rules live in
/// ExpenseManager (through IExpenseManager) and all file work in FileService.
/// </summary>
public static class Program
{
    private const int TableWidth = 84;

    private static readonly string[] DateFormats = ["yyyy-MM-dd", "yyyy/MM/dd", "dd-MM-yyyy", "dd/MM/yyyy"];

    private static readonly (string Key, string Label)[] MenuItems =
    [
        ("1", "Add Expense"),
        ("2", "View Expenses"),
        ("3", "Search Expenses"),
        ("4", "Filter by Category"),
        ("5", "View Summary"),
        ("6", "Save Expenses"),
        ("7", "Load Expenses"),
        ("8", "Delete Expense"),
        ("0", "Exit"),
    ];

    private static readonly IExpenseManager Manager = new ExpenseManager();
    private static AppSettings _settings = AppSettings.Default;
    private static FileService _fileService = new(AppSettings.Default.DataFilePath);
    private static bool _hasUnsavedChanges;

    public static async Task Main(string[] args)
    {
        // Use the same number and date format on every computer (12.50 and 2026-09-30).
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

        // Optional custom data file: dotnet run -- path/to/file.json
        string dataFile = args.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a)) ?? _settings.DataFilePath;
        _settings = _settings with { DataFilePath = dataFile };
        _fileService = new FileService(_settings.DataFilePath);

        try
        {
            DisplayWelcome();

            decimal budget = ReadValidAmount(
                $"Enter your monthly budget (press Enter for {Money(_settings.MonthlyBudget)}): ",
                _settings.MonthlyBudget);
            _settings = _settings with { MonthlyBudget = budget };
            WriteSuccess($"Monthly budget set to {Money(budget)}.");

            await RunMenuLoopAsync();
        }
        catch (InputClosedException)
        {
            Console.WriteLine();
            WriteWarning(_hasUnsavedChanges
                ? "Input ended. Unsaved changes were not saved."
                : "Input ended.");
        }

        Console.WriteLine();
        WriteColored($"Thank you for using {_settings.AppName}. Goodbye!", ConsoleColor.Cyan);
    }

    // =====================================================================
    // Menu
    // =====================================================================

    private static async Task RunMenuLoopAsync()
    {
        bool isRunning = true;
        while (isRunning)
        {
            DisplayMenu();
            string choice = ReadInput("Choose an option: ").Trim();

            try
            {
                isRunning = await HandleMenuChoiceAsync(choice);
            }
            catch (InvalidExpenseException ex)
            {
                WriteError($"Invalid expense: {ex.Message}");
            }
            catch (Exception ex) when (ex is not InputClosedException)
            {
                // Last safety net: report the problem and keep the application running.
                WriteError($"Something went wrong: {ex.Message}");
            }
        }
    }

    private static void DisplayMenu()
    {
        Console.WriteLine();
        WriteColored("=================== MAIN MENU ===================", ConsoleColor.Cyan);
        foreach ((string key, string label) in MenuItems)
        {
            Console.WriteLine($"  {key}. {label}");
        }

        Console.WriteLine("-------------------------------------------------");
        string saveState = _hasUnsavedChanges ? "unsaved changes" : "all changes saved";
        Console.WriteLine(
            $"  {Manager.Expenses.Count} expense(s) | total {Money(Manager.CalculateTotalExpenses())} | {saveState}");
    }

    /// <summary>Runs the chosen option. Returns false when the application should close.</summary>
    private static async Task<bool> HandleMenuChoiceAsync(string choice)
    {
        switch (choice.ToLowerInvariant())
        {
            case "1":
                AddExpense();
                break;
            case "2":
                ViewExpenses();
                break;
            case "3":
                SearchExpenses();
                break;
            case "4":
                FilterByCategory();
                break;
            case "5":
                ViewSummary();
                break;
            case "6":
                await SaveExpensesAsync();
                break;
            case "7":
                await LoadExpensesAsync();
                break;
            case "8":
                DeleteExpense();
                break;
            case "0" or "q" or "exit":
                return !await ConfirmExitAsync();
            case "":
                WriteWarning("Please type the number of a menu option.");
                break;
            default:
                WriteWarning($"'{choice}' is not a menu option. Please enter a number from 0 to 8.");
                break;
        }

        return true;
    }

    // =====================================================================
    // 1. Add expense
    // =====================================================================

    private static void AddExpense()
    {
        WriteHeader("ADD EXPENSE");

        // One variable per expense detail, each with a suitable data type.
        string title = ReadValidTitle();
        decimal amount = ReadValidAmount("Amount: ");
        Category category = ReadCategory(allowNew: true);
        DateTime date = ReadValidDate("Date (yyyy-MM-dd, Enter = today): ", DateTime.Today);
        string? notes = ReadOptionalNotes();
        bool isRecurring = ReadYesNo("Is this a recurring expense? (y/n): ");

        Expense expense = isRecurring
            ? new RecurringExpense(title, amount, category.Name, date, ReadFrequency(), notes)
            : new Expense(title, amount, category.Name, date, notes);

        Manager.AddExpense(expense);
        _hasUnsavedChanges = true;

        WriteSuccess($"Expense #{expense.Id} added.");
        DisplayExpenseDetails(expense);
        DisplayRunningTotals();
    }

    private static void DisplayExpenseDetails(Expense expense)
    {
        Console.WriteLine($"  Title    : {expense.Title}");
        Console.WriteLine($"  Amount   : {Money(expense.Amount)}");
        Console.WriteLine($"  Category : {expense.Category}");
        Console.WriteLine($"  Date     : {expense.Date:dddd, dd MMMM yyyy}");
        Console.WriteLine($"  Notes    : {expense.Notes ?? "-"}");

        // Pattern matching: only recurring expenses have a schedule to show.
        if (expense is RecurringExpense recurring)
        {
            Console.WriteLine($"  Repeats  : {recurring.Frequency}, next due {recurring.NextDueDate:yyyy-MM-dd}");
            Console.WriteLine($"  Per month: about {Money(recurring.MonthlyEquivalent)}");
        }
    }

    private static void DisplayRunningTotals()
    {
        // Totals, average and remaining budget with plain arithmetic operators.
        int count = Manager.Expenses.Count;
        decimal total = Manager.CalculateTotalExpenses();
        decimal average = count > 0 ? total / count : 0m;
        decimal spentThisMonth = Manager.GetSummary(_settings.MonthlyBudget).SpentThisMonth;
        decimal remaining = _settings.MonthlyBudget - spentThisMonth;

        Console.WriteLine();
        Console.WriteLine($"Running total: {Money(total)} across {count} expense(s), average {Money(average)}.");
        if (remaining >= 0)
        {
            Console.WriteLine($"Remaining budget this month: {Money(remaining)} of {Money(_settings.MonthlyBudget)}.");
        }
        else
        {
            WriteWarning($"You are {Money(-remaining)} over your monthly budget of {Money(_settings.MonthlyBudget)}.");
        }
    }

    // =====================================================================
    // 2. View expenses
    // =====================================================================

    private static void ViewExpenses()
    {
        WriteHeader("VIEW EXPENSES");
        if (!HasExpenses())
        {
            return;
        }

        SortOption sortOption = ReadSortOption();
        DisplayExpenseTable(Manager.Sort(sortOption), $"Sorted by {DescribeSort(sortOption)}");
    }

    private static SortOption ReadSortOption()
    {
        Console.WriteLine("Sort by:");
        Console.WriteLine("  1. Date (newest first)    2. Date (oldest first)    3. Amount (high to low)");
        Console.WriteLine("  4. Amount (low to high)   5. Title (A-Z)            6. Category");

        while (true)
        {
            SortOption? option = ReadInput("Choice (Enter = 1): ").Trim() switch
            {
                "" or "1" => SortOption.DateNewestFirst,
                "2" => SortOption.DateOldestFirst,
                "3" => SortOption.AmountHighestFirst,
                "4" => SortOption.AmountLowestFirst,
                "5" => SortOption.Title,
                "6" => SortOption.Category,
                _ => null,
            };

            if (option is SortOption chosen)
            {
                return chosen;
            }

            WriteError("Please enter a number from 1 to 6.");
        }
    }

    private static string DescribeSort(SortOption option) => option switch
    {
        SortOption.DateNewestFirst => "date, newest first",
        SortOption.DateOldestFirst => "date, oldest first",
        SortOption.AmountHighestFirst => "amount, highest first",
        SortOption.AmountLowestFirst => "amount, lowest first",
        SortOption.Title => "title",
        SortOption.Category => "category",
        _ => option.ToString(),
    };

    // =====================================================================
    // 3. Search expenses (overloaded methods)
    // =====================================================================

    private static void SearchExpenses()
    {
        WriteHeader("SEARCH EXPENSES");
        if (!HasExpenses())
        {
            return;
        }

        Console.WriteLine("  1. By keyword (title, category or notes)");
        Console.WriteLine("  2. By amount range");
        Console.WriteLine("  3. By date range");
        int searchType = ReadNumberInRange("Search type: ", 1, 3);

        switch (searchType)
        {
            case 1:
                SearchExpenses(ReadRequiredText("Keyword: "));
                break;

            case 2:
                decimal min = ReadValidAmount("Minimum amount (Enter = no minimum): ", 0m);
                decimal max = ReadValidAmount("Maximum amount (Enter = no maximum): ", Expense.MaxAmount);
                SearchExpenses(min, max);
                break;

            default:
                DateTime firstOfMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
                DateTime from = ReadValidDate($"From date (Enter = {firstOfMonth:yyyy-MM-dd}): ", firstOfMonth);
                DateTime to = ReadValidDate($"To date (Enter = {DateTime.Today:yyyy-MM-dd}): ", DateTime.Today, allowFuture: true);
                SearchExpenses(from, to);
                break;
        }
    }

    /// <summary>Overload 1: search by keyword.</summary>
    private static void SearchExpenses(string keyword) =>
        DisplaySearchResults(Manager.Search(keyword), $"matching \"{keyword}\"");

    /// <summary>Overload 2: search by amount range.</summary>
    private static void SearchExpenses(decimal minAmount, decimal maxAmount) =>
        DisplaySearchResults(
            Manager.Search(minAmount, maxAmount),
            $"between {Money(Math.Min(minAmount, maxAmount))} and {Money(Math.Max(minAmount, maxAmount))}");

    /// <summary>Overload 3: search by date range.</summary>
    private static void SearchExpenses(DateTime from, DateTime to)
    {
        DateTime start = from <= to ? from : to;
        DateTime end = from <= to ? to : from;
        DisplaySearchResults(Manager.Search(from, to), $"from {start:yyyy-MM-dd} to {end:yyyy-MM-dd}");
    }

    private static void DisplaySearchResults(IReadOnlyList<Expense> results, string description)
    {
        if (results.Count == 0)
        {
            WriteWarning($"No expenses found {description}.");
            return;
        }

        WriteSuccess($"Found {results.Count} expense(s) {description}.");
        DisplayExpenseTable(results);
    }

    // =====================================================================
    // 4. Filter by category
    // =====================================================================

    private static void FilterByCategory()
    {
        WriteHeader("FILTER BY CATEGORY");
        if (!HasExpenses())
        {
            return;
        }

        DisplayCategoryTotals();
        Category category = ReadCategory(allowNew: false);

        IReadOnlyList<Expense> filtered = Manager.FilterByCategory(category.Name);
        if (filtered.Count == 0)
        {
            WriteWarning($"There are no expenses in '{category.Name}' yet.");
            return;
        }

        DisplayExpenseTable(filtered, $"Category: {category.Name}");

        decimal categoryTotal = Manager.CalculateTotalExpenses(category.Name);
        decimal overallTotal = Manager.CalculateTotalExpenses();
        decimal share = overallTotal > 0 ? categoryTotal / overallTotal * 100 : 0;
        Console.WriteLine($"{category.Name} makes up {share:N1}% of all spending.");
    }

    private static void DisplayCategoryTotals()
    {
        Dictionary<string, CategorySummary> totals = Manager.GroupByCategory()
            .ToDictionary(s => s.Category, StringComparer.OrdinalIgnoreCase);

        Console.WriteLine($"  {"#",3}  {"Category",-16}{"Expenses",9}{"Total",14}");
        foreach (Category category in Manager.Categories.Values.OrderBy(c => c.Id))
        {
            CategorySummary? summary = totals.GetValueOrDefault(category.Name);
            Console.WriteLine(
                $"  {category.Id,3}  {category.Name,-16}{summary?.Count ?? 0,9}{Money(summary?.Total ?? 0m),14}");
        }

        Console.WriteLine();
    }

    // =====================================================================
    // 5. Summary report
    // =====================================================================

    private static void ViewSummary()
    {
        WriteHeader("SPENDING SUMMARY");
        if (!HasExpenses())
        {
            return;
        }

        ExpenseSummary summary = Manager.GetSummary(_settings.MonthlyBudget);

        WriteSection("Overview");
        WriteRow("Expenses recorded", summary.Count.ToString());
        WriteRow("Total spent", Money(summary.Total));
        WriteRow("Average expense", Money(summary.Average));
        WriteRow("Highest expense", DescribeExpense(summary.Highest));
        WriteRow("Lowest expense", DescribeExpense(summary.Lowest));

        WriteSection($"Budget for {DateTime.Today:MMMM yyyy}");
        WriteRow("Monthly budget", Money(summary.MonthlyBudget));
        WriteRow("Spent this month", Money(summary.SpentThisMonth));
        WriteRow("Remaining", Money(summary.RemainingBudget));
        WriteRow("Budget used", $"{summary.BudgetUsedPercent:N1}%  {Bar(summary.BudgetUsedPercent, 20)}");

        // Property patterns pick the status message.
        (string status, ConsoleColor color) = summary switch
        {
            { MonthlyBudget: <= 0m } => ("No budget set", ConsoleColor.Gray),
            { IsOverBudget: true } => ($"Over budget by {Money(-summary.RemainingBudget)}", ConsoleColor.Red),
            { BudgetUsedPercent: >= 80m } => ("Close to the limit", ConsoleColor.Yellow),
            _ => ("On track", ConsoleColor.Green),
        };
        WriteColored($"  {"Status",-20}: {status}", color);
        WriteRow("Recurring costs", $"about {Money(summary.RecurringPerMonth)} per month");

        WriteSection("Spending by category");
        Console.WriteLine($"  {"Category",-16}{"Count",6}{"Total",14}{"Average",12}{"Share",8}");
        foreach (CategorySummary category in Manager.GroupByCategory())
        {
            decimal share = category.ShareOf(summary.Total);
            Console.WriteLine(
                $"  {category.Category,-16}{category.Count,6}{Money(category.Total),14}{Money(category.Average),12}" +
                $"{share,7:N1}%  {Bar(share, 20)}");
        }

        WriteSection("Spending by month");
        Console.WriteLine($"  {"Month",-16}{"Count",6}{"Total",14}");
        foreach (MonthlyTotal month in Manager.GetMonthlyTotals())
        {
            Console.WriteLine($"  {month.Label,-16}{month.Count,6}{Money(month.Total),14}");
        }

        WriteSection("Top 3 expenses");
        foreach (Expense expense in Manager.Sort(SortOption.AmountHighestFirst).Take(3))
        {
            Console.WriteLine($"  {DescribeExpense(expense)}");
        }
    }

    private static string DescribeExpense(Expense? expense) =>
        expense is null
            ? "-"
            : $"{expense.Title} - {Money(expense.Amount)} on {expense.Date:yyyy-MM-dd} (#{expense.Id})";

    private static string Bar(decimal percent, int width)
    {
        int filled = (int)Math.Round(Math.Clamp(percent, 0m, 100m) / 100m * width);
        return "[" + new string('#', filled) + new string('.', width - filled) + "]";
    }

    // =====================================================================
    // 6 and 7. Save and load (async)
    // =====================================================================

    private static async Task SaveExpensesAsync()
    {
        WriteHeader("SAVE EXPENSES");

        if (Manager.Expenses.Count == 0 && _fileService.FileExists()
            && !ReadYesNo("There are no expenses in memory. Saving will empty the saved file. Continue? (y/n): "))
        {
            WriteInfo("Save cancelled.");
            return;
        }

        WriteInfo($"Saving {Manager.Expenses.Count} expense(s) to {_fileService.FullPath} ...");
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            await _fileService.SaveAsync(Manager.Expenses);
            _hasUnsavedChanges = false;
            WriteSuccess($"Saved {Manager.Expenses.Count} expense(s) successfully.");
        }
        catch (UnauthorizedAccessException ex)
        {
            WriteError($"Permission denied while saving: {ex.Message}");
        }
        catch (IOException ex)
        {
            WriteError($"The file could not be written: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            Console.WriteLine($"Save operation finished in {stopwatch.ElapsedMilliseconds} ms.");
        }
    }

    private static async Task LoadExpensesAsync()
    {
        WriteHeader("LOAD EXPENSES");

        if (!_fileService.FileExists())
        {
            WriteWarning($"No saved file was found at {_fileService.FullPath}.");
            WriteInfo("Add some expenses and choose 6 to save them first.");
            return;
        }

        if (_hasUnsavedChanges
            && !ReadYesNo($"Loading replaces the {Manager.Expenses.Count} expense(s) in memory, " +
                          "including unsaved changes. Continue? (y/n): "))
        {
            WriteInfo("Load cancelled.");
            return;
        }

        WriteInfo($"Loading expenses from {_fileService.FullPath} ...");
        Stopwatch stopwatch = Stopwatch.StartNew();
        try
        {
            IReadOnlyList<Expense> loaded = await _fileService.LoadAsync();
            Manager.ReplaceAll(loaded);
            _hasUnsavedChanges = false;

            int recurringCount = loaded.Count(e => e is RecurringExpense);
            WriteSuccess($"Loaded {loaded.Count} expense(s), {recurringCount} of them recurring. " +
                         $"Total: {Money(Manager.CalculateTotalExpenses())}.");
        }
        catch (FileNotFoundException ex)
        {
            WriteError(ex.Message); // the file was removed between the check and the read
        }
        catch (InvalidDataException ex)
        {
            WriteError($"Could not load expenses. {ex.Message}");
            WriteWarning("Your current expenses were left unchanged. Fix or delete the file, then try again.");
        }
        catch (UnauthorizedAccessException ex)
        {
            WriteError($"Permission denied while reading the file: {ex.Message}");
        }
        catch (IOException ex)
        {
            WriteError($"The file could not be read: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            Console.WriteLine($"Load operation finished in {stopwatch.ElapsedMilliseconds} ms.");
        }
    }

    // =====================================================================
    // 8. Delete expense
    // =====================================================================

    private static void DeleteExpense()
    {
        WriteHeader("DELETE EXPENSE");
        if (!HasExpenses())
        {
            return;
        }

        DisplayExpenseTable(Manager.Sort(SortOption.DateNewestFirst));
        int id = ReadNumberInRange("ID of the expense to delete (0 = cancel): ", 0, int.MaxValue);
        if (id == 0)
        {
            WriteInfo("Delete cancelled.");
            return;
        }

        Expense? expense = Manager.FindById(id);
        if (expense is null)
        {
            WriteWarning($"There is no expense with ID {id}.");
            return;
        }

        if (!ReadYesNo($"Delete \"{expense.Title}\" ({Money(expense.Amount)})? (y/n): "))
        {
            WriteInfo("Delete cancelled.");
            return;
        }

        decimal totalBefore = Manager.CalculateTotalExpenses();
        Manager.RemoveExpense(id);
        _hasUnsavedChanges = true;
        decimal totalAfter = Manager.CalculateTotalExpenses();

        WriteSuccess($"Expense #{id} deleted. Total spending changed from {Money(totalBefore)} to {Money(totalAfter)}.");
    }

    // =====================================================================
    // 0. Exit
    // =====================================================================

    /// <summary>Returns true if the application should close.</summary>
    private static async Task<bool> ConfirmExitAsync()
    {
        if (!_hasUnsavedChanges)
        {
            return true;
        }

        WriteWarning("You have unsaved changes.");
        while (true)
        {
            string answer = ReadInput("Save before exiting? (y = save and exit, n = exit without saving, c = cancel): ")
                .Trim()
                .ToLowerInvariant();

            switch (answer)
            {
                case "y" or "yes":
                    await SaveExpensesAsync();
                    return !_hasUnsavedChanges || ReadYesNo("Saving failed. Exit anyway? (y/n): ");
                case "n" or "no":
                    return true;
                case "c" or "cancel":
                    return false;
                default:
                    WriteError("Please answer y, n or c.");
                    break;
            }
        }
    }

    // =====================================================================
    // Input helpers: every one loops until the value is valid
    // =====================================================================

    private static string ReadInput(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? throw new InputClosedException();
    }

    private static string ReadValidTitle()
    {
        while (true)
        {
            string input = ReadInput("Title: ");
            try
            {
                return Expense.ValidateTitle(input); // throws InvalidExpenseException if empty or too long
            }
            catch (InvalidExpenseException ex)
            {
                WriteError(ex.Message);
            }
        }
    }

    /// <summary>
    /// Reads a money amount. Uses decimal.TryParse for the format and the Expense
    /// rules for the value. If a default is given, pressing Enter returns it.
    /// </summary>
    private static decimal ReadValidAmount(string prompt, decimal? defaultValue = null)
    {
        while (true)
        {
            string input = ReadInput(prompt).Trim();
            if (input.Length == 0 && defaultValue is decimal fallback)
            {
                return fallback;
            }

            string digits = input.Replace(_settings.CurrencySymbol, string.Empty).Trim();
            if (!decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amount))
            {
                WriteError(input.Length == 0
                    ? "Please enter an amount."
                    : $"'{input}' is not a valid amount. Use digits, for example 12.50.");
                continue;
            }

            try
            {
                return Expense.ValidateAmount(amount);
            }
            catch (InvalidExpenseException ex)
            {
                WriteError(ex.Message);
            }
        }
    }

    /// <summary>
    /// Reads a date in yyyy-MM-dd (or dd/MM/yyyy) format. Enter returns the default.
    /// Expense dates cannot be in the future unless <paramref name="allowFuture"/> is true.
    /// </summary>
    private static DateTime ReadValidDate(string prompt, DateTime defaultValue, bool allowFuture = false)
    {
        while (true)
        {
            string input = ReadInput(prompt).Trim();

            DateTime? parsed = input.ToLowerInvariant() switch
            {
                "" => defaultValue,
                "today" => DateTime.Today,
                "yesterday" => DateTime.Today.AddDays(-1),
                _ when DateTime.TryParseExact(
                    input, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime exact) => exact,
                _ => null,
            };

            if (parsed is not DateTime date)
            {
                WriteError($"'{input}' is not a valid date. Use yyyy-MM-dd, for example {DateTime.Today:yyyy-MM-dd}.");
                continue;
            }

            if (allowFuture)
            {
                return date.Date;
            }

            try
            {
                return Expense.ValidateDate(date);
            }
            catch (InvalidExpenseException ex)
            {
                WriteError(ex.Message);
            }
        }
    }

    private static string? ReadOptionalNotes()
    {
        while (true)
        {
            string input = ReadInput("Notes (optional, Enter to skip): ");
            try
            {
                return Expense.ValidateNotes(input);
            }
            catch (InvalidExpenseException ex)
            {
                WriteError(ex.Message);
            }
        }
    }

    /// <summary>Lets the user pick a category by number or name, and optionally create a new one.</summary>
    private static Category ReadCategory(bool allowNew)
    {
        if (allowNew)
        {
            Console.WriteLine("Categories:");
            DisplayCategoryList();
        }

        string prompt = allowNew
            ? "Category (number or name, a new name creates a category): "
            : "Category (number or name): ";

        while (true)
        {
            string input = ReadInput(prompt).Trim();

            if (int.TryParse(input, out int categoryId))
            {
                if (Manager.GetCategory(categoryId) is Category byNumber)
                {
                    return byNumber;
                }

                WriteError($"There is no category number {categoryId}.");
                continue;
            }

            if (Manager.FindCategory(input) is Category byName)
            {
                return byName;
            }

            if (!allowNew)
            {
                WriteError(input.Length == 0 ? "Please choose a category." : $"There is no category called '{input}'.");
                continue;
            }

            try
            {
                string newName = Category.ValidateName(input);
                if (!ReadYesNo($"'{newName}' is a new category. Create it? (y/n): "))
                {
                    continue;
                }

                Category created = Manager.AddCategory(newName);
                WriteSuccess($"Category '{created.Name}' created with number {created.Id}.");
                return created;
            }
            catch (InvalidExpenseException ex)
            {
                WriteError(ex.Message);
            }
        }
    }

    private static void DisplayCategoryList()
    {
        List<string> items = Manager.Categories.Values
            .OrderBy(c => c.Id)
            .Select(c => $"{c.Id,4}. {c.Name}")
            .ToList();

        for (int i = 0; i < items.Count; i += 2)
        {
            string right = i + 1 < items.Count ? items[i + 1] : string.Empty;
            Console.WriteLine($"{items[i],-24}{right}".TrimEnd());
        }
    }

    private static RecurrenceFrequency ReadFrequency()
    {
        while (true)
        {
            string input = ReadInput("How often? 1. Weekly  2. Monthly  3. Yearly (Enter = Monthly): ").Trim();
            RecurrenceFrequency? frequency = input.ToLowerInvariant() switch
            {
                "1" or "weekly" or "w" => RecurrenceFrequency.Weekly,
                "" or "2" or "monthly" or "m" => RecurrenceFrequency.Monthly,
                "3" or "yearly" or "y" => RecurrenceFrequency.Yearly,
                _ => null,
            };

            if (frequency.HasValue)
            {
                return frequency.Value;
            }

            WriteError("Please enter 1, 2 or 3.");
        }
    }

    private static bool ReadYesNo(string prompt)
    {
        while (true)
        {
            switch (ReadInput(prompt).Trim().ToLowerInvariant())
            {
                case "y" or "yes":
                    return true;
                case "n" or "no":
                    return false;
                default:
                    WriteError("Please answer y or n.");
                    break;
            }
        }
    }

    private static int ReadNumberInRange(string prompt, int min, int max)
    {
        while (true)
        {
            string input = ReadInput(prompt).Trim();
            if (int.TryParse(input, out int number) && number >= min && number <= max)
            {
                return number;
            }

            WriteError(max == int.MaxValue
                ? $"Please enter a whole number of {min} or more."
                : $"Please enter a whole number from {min} to {max}.");
        }
    }

    private static string ReadRequiredText(string prompt)
    {
        while (true)
        {
            string input = ReadInput(prompt).Trim();
            if (input.Length > 0)
            {
                return input;
            }

            WriteError("This value cannot be empty.");
        }
    }

    // =====================================================================
    // Output helpers
    // =====================================================================

    private static bool HasExpenses()
    {
        if (Manager.Expenses.Count > 0)
        {
            return true;
        }

        WriteInfo("There are no expenses yet. Choose 1 to add one, or 7 to load saved expenses.");
        return false;
    }

    private static void DisplayExpenseTable(IReadOnlyList<Expense> expenses, string? caption = null)
    {
        if (caption is not null)
        {
            WriteInfo(caption);
        }

        Console.WriteLine($"{"ID",-5}{"Date",-10}  {"Title",-26}  {"Category",-14}  {"Type",-9}  {"Amount",12}");
        Console.WriteLine(new string('-', TableWidth));
        foreach (Expense expense in expenses)
        {
            // Virtual call: RecurringExpense overrides GetDisplayText to add a schedule line.
            Console.WriteLine(expense.GetDisplayText(_settings.CurrencySymbol));
        }

        Console.WriteLine(new string('-', TableWidth));
        decimal total = expenses.Sum(e => e.Amount);
        string countText = $"{expenses.Count} expense(s)";
        string totalText = $"Total: {Money(total)}";
        Console.WriteLine($"{countText}{totalText.PadLeft(TableWidth - countText.Length)}");
    }

    private static string Money(decimal value) =>
        value < 0
            ? $"-{_settings.CurrencySymbol}{-value:N2}"
            : $"{_settings.CurrencySymbol}{value:N2}";

    private static void WriteHeader(string title)
    {
        Console.WriteLine();
        WriteColored($"--- {title} " + new string('-', Math.Max(3, 45 - title.Length)), ConsoleColor.Cyan);
    }

    private static void WriteSection(string title)
    {
        Console.WriteLine();
        WriteColored(title, ConsoleColor.White);
    }

    private static void WriteRow(string label, string value) => Console.WriteLine($"  {label,-20}: {value}");

    private static void WriteSuccess(string message) => WriteColored(message, ConsoleColor.Green);

    private static void WriteInfo(string message) => WriteColored(message, ConsoleColor.Cyan);

    private static void WriteWarning(string message) => WriteColored(message, ConsoleColor.Yellow);

    private static void WriteError(string message) => WriteColored($"Error: {message}", ConsoleColor.Red);

    private static void WriteColored(string message, ConsoleColor color)
    {
        ConsoleColor previous = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = previous; // always restore the colour, even if writing fails
        }
    }

    private static void DisplayWelcome()
    {
        string line = new('=', 60);
        WriteColored(line, ConsoleColor.Cyan);
        WriteColored($"   {_settings.AppName}  v{_settings.Version}", ConsoleColor.Cyan);
        Console.WriteLine("   Record, organise and understand your spending.");
        WriteColored(line, ConsoleColor.Cyan);
        Console.WriteLine($"Today is {DateTime.Today:dddd, dd MMMM yyyy}.");

        if (_fileService.FileExists())
        {
            WriteInfo($"Saved expenses found at {_fileService.FullPath}");
            WriteInfo("Choose 7 from the menu to load them.");
        }
        else
        {
            WriteInfo($"No saved expenses yet. They will be saved to {_fileService.FullPath}");
        }

        Console.WriteLine();
    }

    /// <summary>Raised when the console input stream ends (for example, piped input runs out).</summary>
    private sealed class InputClosedException() : Exception("Console input was closed.");
}
