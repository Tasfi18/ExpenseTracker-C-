using System.Text.Json.Serialization;
using ExpenseTracker.Exceptions;

namespace ExpenseTracker.Models;

/// <summary>
/// A single expense. Every property validates its value in the setter (encapsulation),
/// so an Expense object can never hold an empty title, a negative amount or a future date.
/// </summary>
/// <remarks>
/// The JsonPolymorphic attributes let a List&lt;Expense&gt; that also contains
/// RecurringExpense objects be saved and loaded without losing the derived type.
/// </remarks>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Expense), "one-time")]
[JsonDerivedType(typeof(RecurringExpense), "recurring")]
public class Expense
{
    public const int MaxTitleLength = 60;
    public const int MaxNotesLength = 200;
    public const decimal MaxAmount = 1_000_000m;
    public static readonly DateTime MinDate = new(2000, 1, 1);

    // Private backing fields: the only way to change them is through the validated properties.
    private string _title = string.Empty;
    private decimal _amount;
    private string _category = "Other";
    private DateTime _date = DateTime.Today;
    private string? _notes;

    /// <summary>Parameterless constructor used by the JSON serializer when loading a file.</summary>
    public Expense()
    {
    }

    public Expense(string title, decimal amount, string category, DateTime date, string? notes = null)
    {
        Title = title;
        Amount = amount;
        Category = category;
        Date = date;
        Notes = notes;
    }

    /// <summary>Unique number assigned by the ExpenseManager when the expense is added.</summary>
    [JsonInclude]
    public int Id { get; internal set; }

    public string Title
    {
        get => _title;
        set => _title = ValidateTitle(value);
    }

    public decimal Amount
    {
        get => _amount;
        set => _amount = ValidateAmount(value);
    }

    public string Category
    {
        get => _category;
        set => _category = Models.Category.ValidateName(value);
    }

    public DateTime Date
    {
        get => _date;
        set => _date = ValidateDate(value);
    }

    public string? Notes
    {
        get => _notes;
        set => _notes = ValidateNotes(value);
    }

    /// <summary>Short label shown in tables. Overridden by RecurringExpense.</summary>
    [JsonIgnore]
    public virtual string ExpenseType => "One-time";

    // ----------------------------------------------------------------------
    // Validation rules. They are public and static so the console UI can check
    // a single value as soon as the user types it, using the same rules.
    // ----------------------------------------------------------------------

    public static string ValidateTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new InvalidExpenseException("Title cannot be empty.", nameof(Title));
        }

        string trimmed = title.Trim();
        if (trimmed.Length > MaxTitleLength)
        {
            throw new InvalidExpenseException(
                $"Title cannot be longer than {MaxTitleLength} characters (you entered {trimmed.Length}).", nameof(Title));
        }

        return trimmed;
    }

    public static decimal ValidateAmount(decimal amount) => amount switch
    {
        <= 0m => throw new InvalidExpenseException("Amount must be greater than zero.", nameof(Amount)),
        > MaxAmount => throw new InvalidExpenseException($"Amount cannot be more than {MaxAmount:N0}.", nameof(Amount)),
        _ when decimal.Round(amount, 2) != amount =>
            throw new InvalidExpenseException("Amount can have at most two decimal places.", nameof(Amount)),
        _ => amount,
    };

    public static DateTime ValidateDate(DateTime date)
    {
        DateTime day = date.Date;
        if (day > DateTime.Today)
        {
            throw new InvalidExpenseException("Date cannot be in the future.", nameof(Date));
        }

        if (day < MinDate)
        {
            throw new InvalidExpenseException($"Date cannot be earlier than {MinDate:yyyy-MM-dd}.", nameof(Date));
        }

        return day;
    }

    public static string? ValidateNotes(string? notes)
    {
        if (string.IsNullOrWhiteSpace(notes))
        {
            return null;
        }

        string trimmed = notes.Trim();
        return trimmed.Length <= MaxNotesLength
            ? trimmed
            : throw new InvalidExpenseException($"Notes cannot be longer than {MaxNotesLength} characters.", nameof(Notes));
    }

    /// <summary>
    /// Re-checks every field. Used after loading from JSON, where a missing
    /// property would otherwise leave a field at its default value.
    /// </summary>
    public virtual void Validate()
    {
        ValidateTitle(_title);
        ValidateAmount(_amount);
        Models.Category.ValidateName(_category);
        ValidateDate(_date);
        ValidateNotes(_notes);
    }

    /// <summary>One table row: id, date, title, category, type and amount.</summary>
    public virtual string GetDisplayText(string currencySymbol = "$")
    {
        string amountText = currencySymbol + Amount.ToString("N2");
        return $"{Id,-5}{Date:yyyy-MM-dd}  {Fit(Title, 26),-26}  {Fit(Category, 14),-14}  {Fit(ExpenseType, 9),-9}  {amountText,12}";
    }

    public override string ToString() => $"#{Id} {Title} - {Amount:N2} ({Category}, {Date:yyyy-MM-dd})";

    /// <summary>Shortens long text so table columns stay aligned.</summary>
    protected static string Fit(string text, int width) =>
        text.Length <= width ? text : text[..(width - 3)] + "...";
}
