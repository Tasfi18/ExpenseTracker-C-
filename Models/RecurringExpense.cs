using System.Text.Json.Serialization;
using ExpenseTracker.Exceptions;

namespace ExpenseTracker.Models;

/// <summary>How often a recurring expense repeats.</summary>
public enum RecurrenceFrequency
{
    Weekly,
    Monthly,
    Yearly,
}

/// <summary>
/// An expense that repeats on a schedule, such as rent or a streaming subscription.
/// Inherits everything from Expense and adds a frequency. The Date is the date of the
/// payment that was recorded; the next due date is calculated from it.
/// </summary>
public class RecurringExpense : Expense
{
    private RecurrenceFrequency _frequency = RecurrenceFrequency.Monthly;

    /// <summary>Parameterless constructor used by the JSON serializer.</summary>
    public RecurringExpense()
    {
    }

    public RecurringExpense(
        string title,
        decimal amount,
        string category,
        DateTime date,
        RecurrenceFrequency frequency,
        string? notes = null)
        : base(title, amount, category, date, notes)
    {
        Frequency = frequency;
    }

    public RecurrenceFrequency Frequency
    {
        get => _frequency;
        set => _frequency = Enum.IsDefined(value)
            ? value
            : throw new InvalidExpenseException($"'{value}' is not a valid frequency.", nameof(Frequency));
    }

    /// <summary>Overrides the base label, e.g. "Monthly" instead of "One-time".</summary>
    [JsonIgnore]
    public override string ExpenseType => Frequency.ToString();

    /// <summary>What this expense costs per month on average, used for budget planning.</summary>
    [JsonIgnore]
    public decimal MonthlyEquivalent => Frequency switch
    {
        RecurrenceFrequency.Weekly => decimal.Round(Amount * 52m / 12m, 2),
        RecurrenceFrequency.Monthly => Amount,
        RecurrenceFrequency.Yearly => decimal.Round(Amount / 12m, 2),
        _ => Amount,
    };

    /// <summary>The next payment date after today.</summary>
    [JsonIgnore]
    public DateTime NextDueDate => GetNextDueDate(DateTime.Today);

    /// <summary>
    /// Returns the first occurrence strictly after <paramref name="fromDate"/>.
    /// Each occurrence is calculated from the original date (not from the previous
    /// occurrence) so a payment on the 31st does not drift to the 28th after February.
    /// </summary>
    public DateTime GetNextDueDate(DateTime fromDate)
    {
        DateTime limit = fromDate.Date;
        for (int occurrence = 0; ; occurrence++)
        {
            DateTime candidate = Frequency switch
            {
                RecurrenceFrequency.Weekly => Date.AddDays(7 * occurrence),
                RecurrenceFrequency.Monthly => Date.AddMonths(occurrence),
                _ => Date.AddYears(occurrence),
            };

            if (candidate > limit)
            {
                return candidate;
            }
        }
    }

    public override void Validate()
    {
        base.Validate();
        Frequency = _frequency;
    }

    /// <summary>Adds a second line with the schedule under the normal table row.</summary>
    public override string GetDisplayText(string currencySymbol = "$")
    {
        string schedule =
            $"       -> repeats {Frequency.ToString().ToLowerInvariant()}, next due {NextDueDate:yyyy-MM-dd}, " +
            $"about {currencySymbol}{MonthlyEquivalent:N2} per month";

        return base.GetDisplayText(currencySymbol) + Environment.NewLine + schedule;
    }

    public override string ToString() => $"{base.ToString()} [repeats {Frequency}]";
}
