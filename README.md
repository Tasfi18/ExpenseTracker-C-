# FinTrack Expense Tracker

A console application for recording expenses, organising them into categories, searching and filtering them, producing spending summaries, and saving everything to a JSON file between sessions.

Built with C# 12 on .NET 8 as the course project for FinTrack Solutions. It replaces the old hard-coded prototype with validated input, object-oriented models, LINQ reports, async file storage and xUnit tests.

---

## Features

- Add one-time and recurring (weekly, monthly, yearly) expenses, with input checked as you type
- View all expenses, sorted by date, amount, title or category
- Search by keyword, by amount range or by date range
- Filter by category, with totals for each category
- Summary report with totals, average, highest and lowest expense, monthly budget status, and spending by category and by month
- Delete expenses; totals update straight away
- Save to and load from `Data/expenses.json` asynchronously, with clear messages for missing or corrupted files
- Asks whether to save unsaved changes before you exit

## Requirements

- [.NET 8 SDK](https://dotnet.microsoft.com/download) or later
- Visual Studio Code with the **C# Dev Kit** extension (for debugging)
- Git

## Getting started

```bash
git clone https://github.com/Tasfi18/ExpenseTracker-C-.git
cd ExpenseTracker-C-
dotnet run
```

The app asks for your monthly budget (press Enter to accept $2,000.00) and then shows the main menu.

`Data/expenses.json` already holds 13 sample expenses. Choose **7** to load them and try the reports straight away.

To use a different data file, pass its path after `--`:

```bash
dotnet run -- my-expenses.json
```

## Using the app

| Option | What it does |
|---|---|
| 1. Add Expense | Asks for title, amount, category, date, notes, and whether it repeats |
| 2. View Expenses | Lists every expense in the sort order you choose, with a total |
| 3. Search Expenses | Keyword (title, category or notes), amount range, or date range |
| 4. Filter by Category | Shows totals per category, then the expenses in the one you pick |
| 5. View Summary | Overview, budget status, by-category and by-month breakdowns, top 3 |
| 6. Save Expenses | Writes all expenses to the JSON file |
| 7. Load Expenses | Replaces the expenses in memory with the ones in the file |
| 8. Delete Expense | Removes an expense by its ID after asking you to confirm |
| 0. Exit | Closes the app (also accepts `q` or `exit`) |

Input rules:

- **Amounts**: positive numbers with up to two decimal places, such as `12.50` or `1,250.00`
- **Dates**: `yyyy-MM-dd` (for example `2026-09-30`). `today`, `yesterday` and `dd/MM/yyyy` also work. Press Enter for today. Expense dates cannot be in the future.
- **Categories**: type the number or the name. A new name creates a new category after you confirm it.

If you type something invalid, the app explains what is wrong and asks again. It never closes because of bad input.

Part of the summary report for the sample data, with a $1,500 budget:

```
Budget for October 2026
  Monthly budget      : $1,500.00
  Spent this month    : $22.50
  Remaining           : $1,477.50
  Budget used         : 1.5%  [....................]
  Status              : On track
  Recurring costs     : about $892.99 per month

Spending by category
  Category         Count         Total     Average   Share
  Housing              1       $850.00     $850.00   66.3%  [#############.......]
  Food                 4       $143.45      $35.86   11.2%  [##..................]
  Utilities            2        $92.74      $46.37    7.2%  [#...................]
  Shopping             1        $89.99      $89.99    7.0%  [#...................]
```

## Project structure

```
ExpenseTracker/
├── Program.cs                       Entry point, menu loop, input and output
├── ExpenseTracker.csproj            App project (net8.0, nullable enabled)
├── ExpenseTracker.sln               Solution containing the app and the tests
├── Models/
│   ├── Expense.cs                   Expense model with validated properties
│   ├── RecurringExpense.cs          Derived class + RecurrenceFrequency enum
│   ├── Category.cs                  Category model and default categories
│   ├── ExpenseSummary.cs            Records: ExpenseSummary, CategorySummary, MonthlyTotal
│   ├── AppSettings.cs               Record for immutable configuration
│   └── SortOption.cs                Sort orders for the expense list
├── Interfaces/
│   └── IExpenseManager.cs           Contract for all expense operations
├── Services/
│   ├── ExpenseManager.cs            Business logic: List, Dictionary, LINQ
│   └── FileService.cs               Async JSON save and load
├── Exceptions/
│   └── InvalidExpenseException.cs   Custom exception for invalid expense data
├── Data/
│   └── expenses.json                Saved expense records (sample data included)
├── Tests/
│   └── ExpenseTracker.Tests/        xUnit test project
├── .vscode/                         Debug (F5) and build/test task settings
└── README.md
```

`Program.cs` only deals with the console. All rules live in `ExpenseManager` (used through `IExpenseManager`) and all file work in `FileService`, which is why both can be unit tested without a console.

## Where each course concept is used

| Task | Concept | Where to find it |
|---|---|---|
| 1 | `string`, `decimal`, `DateTime`, `bool`, `int` variables | `Program.AddExpense()` declares `title`, `amount`, `category`, `date`, `notes`, `isRecurring` |
| 1 | `Console.ReadLine()` and `TryParse` | `Program.ReadInput()`, `ReadValidAmount()` (`decimal.TryParse`), `ReadValidDate()` (`DateTime.TryParseExact`), `ReadNumberInRange()` (`int.TryParse`) |
| 1 | String interpolation, arithmetic operators | `DisplayExpenseDetails()`, `DisplayRunningTotals()` (total, `total / count`, `budget - spent`) |
| 2 | Menu loop, `switch`, `if`, pattern matching | `RunMenuLoopAsync()` (`while`), `HandleMenuChoiceAsync()` (`case "0" or "q" or "exit"`), invalid choices go to `default` |
| 3 | Single-purpose methods with parameters and return values | `DisplayMenu`, `AddExpense`, `ViewExpenses`, `SearchExpenses`, `ReadValidAmount`, `ReadValidDate`, `ExpenseManager.CalculateTotalExpenses` |
| 3 | Method overloading | `SearchExpenses(string)`, `SearchExpenses(decimal, decimal)`, `SearchExpenses(DateTime, DateTime)` in `Program.cs`, and matching `Search` overloads in `IExpenseManager`. Also `AddExpense` and `CalculateTotalExpenses` |
| 4 | Classes, constructors, encapsulation | `Expense` (private fields, validating setters, two constructors), `Category` |
| 4 | Interface | `IExpenseManager`, implemented by `ExpenseManager` |
| 4 | Inheritance, overriding | `RecurringExpense : Expense` overrides `ExpenseType`, `GetDisplayText()`, `ToString()`, `Validate()` |
| 5 | `List<Expense>`, `Dictionary<int, Category>` | Fields in `ExpenseManager` |
| 5 | Lambdas and LINQ | `Where`, `OrderBy`, `Sum`, `Average`, `MaxBy`, `MinBy`, `GroupBy`, `OfType` across `ExpenseManager`. `GroupByCategory()` uses query syntax |
| 6 | Records and `with` | `AppSettings`, `ExpenseSummary`, `CategorySummary`, `MonthlyTotal`. `Program.Main` uses `_settings with { MonthlyBudget = budget }` |
| 6 | Pattern matching | Switch expressions with relational patterns (`Expense.ValidateAmount`), property patterns (`ViewSummary` status), type patterns (`expense is RecurringExpense recurring`) |
| 6 | Nullable reference types, `??`, `?.` | `<Nullable>enable</Nullable>` in the `.csproj`, `string? Notes`, `expense.Notes ?? "-"`, `asOf ?? DateTime.Today`, `FindCategory(name) ?? AddCategory(name)` |
| 6 | `try` / `catch` / `finally`, custom exception | `InvalidExpenseException` is thrown by the validators and caught in the input loops. `SaveExpensesAsync` and `LoadExpensesAsync` use `finally` to report timing. `WriteColored` uses `finally` to restore the console colour |
| 7 | JSON file I/O, existence check | `FileService.SaveAsync` / `LoadAsync`, `File.Exists` before reading |
| 7 | `async` / `await` | `Main`, `SaveExpensesAsync`, `LoadExpensesAsync`, `JsonSerializer.SerializeAsync` / `DeserializeAsync` |
| 7 | Missing or corrupted files | `FileNotFoundException` and `InvalidDataException` with readable messages. Your current data stays untouched |
| 8 | xUnit tests | `Tests/ExpenseTracker.Tests` |
| 8 | Git | See the commit history below |

## Data file format

Expenses are stored as an indented JSON array. The `type` field tells one-time and recurring expenses apart, so recurring ones are restored as `RecurringExpense` objects.

```json
[
  {
    "type": "recurring",
    "frequency": "Monthly",
    "id": 1,
    "title": "Apartment rent",
    "amount": 850.00,
    "category": "Housing",
    "date": "2026-09-01T00:00:00",
    "notes": "Paid by bank transfer"
  },
  {
    "type": "one-time",
    "id": 2,
    "title": "Monthly bus pass",
    "amount": 30.00,
    "category": "Transport",
    "date": "2026-09-02T00:00:00",
    "notes": null
  }
]
```

The file is written to a temporary file first and then swapped in, so a failed save never leaves a half-written file behind. When loading, every record is validated. If anything is wrong, nothing is loaded and you are told which record is the problem.

## Running the tests

```bash
dotnet test ExpenseTracker.sln
```

The first run downloads xUnit from NuGet. The test project has 60 test cases in three classes:

| Test class | Covers |
|---|---|
| `ExpenseTests` | Constructors, validation rules, `InvalidExpenseException`, recurring expense calculations and overrides, record equality |
| `ExpenseManagerTests` | Adding expenses, totals and averages, totals after removal, the three search overloads, filtering, sorting, grouping, budget summary, error cases |
| `FileServiceTests` | Async save and load round trip, restoring after a "restart", missing, empty and corrupted files |

File tests use a temporary folder, so they never change `Data/expenses.json`. GitHub Actions builds the solution and runs the tests on every push and pull request.

In VS Code you can also run the tests from the **Testing** panel (beaker icon), or with **Terminal → Run Task → test**.

## Debugging in VS Code

`.vscode/launch.json` is already set up. It runs the app in the integrated terminal so you can type input while debugging.

A short exercise that uses every debugger tool:

1. Open the project folder in VS Code.
2. **Breakpoints**: click in the left margin to set a red dot on these lines:
   - `Program.cs`, inside `ReadValidAmount`: the line `if (!decimal.TryParse(digits, ...`
   - `Services/ExpenseManager.cs`, inside `AddExpense(Expense expense)`: the line `expense.Validate();`
3. Press **F5** and choose **Debug Expense Tracker**. Press Enter to accept the default budget, choose `1`, and type a title.
4. At the amount prompt, type `abc`. The debugger stops at the `TryParse` line.
   - **Locals** shows `prompt`, `input` and `digits`.
   - Press **F10 (Step Over)**. The `if` is true, so execution moves into the error branch and `amount` stays `0`.
5. Press **F5 (Continue)**, then enter `12.50` and finish the remaining prompts. The debugger stops in `ExpenseManager.AddExpense`.
   - Press **F11 (Step Into)** on `expense.Validate();` to step into `Expense.Validate()` and watch each rule run. Press **Shift+F11** to step back out.
6. **Watch**: in the Watch panel, add `_expenses.Count`, `_nextExpenseId` and `expense.Amount`. Step over `_expenses.Add(expense);` and see the count go up by one.
7. **Call Stack** shows how execution got here: `RunMenuLoopAsync` → `HandleMenuChoiceAsync` → `AddExpense` → `ExpenseManager.AddExpense`.
8. Optional: right-click a breakpoint, choose **Edit Breakpoint**, and add the condition `expense.Amount > 100` so it only stops for large expenses.

## Git and GitHub

The repository was built up in milestones:

```
Scaffold console app, xUnit test project and folder structure
Add expense models, category model and InvalidExpenseException
Add IExpenseManager interface and LINQ-based ExpenseManager
Add async JSON FileService and sample expense data
Build interactive menu, validated input and reports in Program.cs
Add xUnit tests for expenses, manager logic and file persistence
Add README and VS Code debug configuration
```

See the full history with `git log --oneline`.

To push to GitHub, first create an **empty** repository on github.com (no README, licence or .gitignore). Then run:

```bash
git remote add origin https://github.com/<your-username>/ExpenseTracker.git
git push -u origin main
```

## Design notes

- **Validation lives in the model.** `Expense` checks every value in its setters, so invalid data cannot get in through the console, a test or the JSON file. The console reuses the same `Expense.ValidateX` methods to check each field as it is typed.
- **The UI depends on an interface.** `Program` uses `IExpenseManager`, so the storage could later change (for example to a database) without changing the menu code.
- **Same formats on every computer.** The app uses the invariant culture, so `12.50` and `2026-09-30` mean the same thing on any machine.
- **Safe saving.** Writing to a temporary file and moving it into place protects the existing data if a save fails.
