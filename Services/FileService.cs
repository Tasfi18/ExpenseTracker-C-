using System.Text.Json;
using System.Text.Json.Serialization;
using ExpenseTracker.Exceptions;
using ExpenseTracker.Models;

namespace ExpenseTracker.Services;

/// <summary>
/// Saves expenses to a JSON file and loads them back, asynchronously.
/// Problems are reported as exceptions with clear messages:
/// <list type="bullet">
///   <item>FileNotFoundException - there is no saved file yet</item>
///   <item>InvalidDataException - the file is corrupted or holds an invalid expense</item>
///   <item>IOException / UnauthorizedAccessException - the disk or permissions failed</item>
/// </list>
/// </summary>
public class FileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public FileService(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("A file path is required.", nameof(filePath));
        }

        FilePath = filePath;
    }

    /// <summary>The path as configured (may be relative to the current folder).</summary>
    public string FilePath { get; }

    /// <summary>The absolute path, useful in messages so the user can find the file.</summary>
    public string FullPath => Path.GetFullPath(FilePath);

    public bool FileExists() => File.Exists(FullPath);

    /// <summary>Writes all expenses to the JSON file, replacing its previous contents.</summary>
    public async Task SaveAsync(IEnumerable<Expense> expenses, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expenses);

        string? directory = Path.GetDirectoryName(FullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory); // does nothing if the folder already exists
        }

        // Write to a temporary file first and then swap it in. If something fails
        // half way, the previous data file is still intact.
        string tempPath = FullPath + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                tempPath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, expenses.ToList(), JsonOptions, cancellationToken);
            }

            File.Move(tempPath, FullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>Reads the expenses back from the JSON file.</summary>
    public async Task<IReadOnlyList<Expense>> LoadAsync(CancellationToken cancellationToken = default)
    {
        // Check that the file exists before trying to read it.
        if (!File.Exists(FullPath))
        {
            throw new FileNotFoundException($"No saved data was found at '{FullPath}'.", FullPath);
        }

        if (new FileInfo(FullPath).Length == 0)
        {
            return []; // an empty file simply means "no expenses yet"
        }

        List<Expense?>? loaded;
        try
        {
            await using var stream = new FileStream(
                FullPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
            loaded = await JsonSerializer.DeserializeAsync<List<Expense?>>(stream, JsonOptions, cancellationToken);
        }
        catch (JsonException ex)
        {
            long line = (ex.LineNumber ?? 0) + 1;
            throw new InvalidDataException(
                $"The data file is corrupted or is not valid JSON (problem near line {line}).", ex);
        }
        catch (NotSupportedException ex)
        {
            throw new InvalidDataException($"The data file has an unsupported format: {ex.Message}", ex);
        }
        catch (InvalidExpenseException ex)
        {
            // Thrown by a property setter while reading, e.g. a negative amount.
            throw new InvalidDataException($"The data file contains an invalid expense: {ex.Message}", ex);
        }

        // "null" in the file, or null entries in the list, are skipped.
        List<Expense> expenses = (loaded ?? []).OfType<Expense>().ToList();

        // Fields missing from the JSON keep their defaults (e.g. an empty title), so check each record.
        for (int i = 0; i < expenses.Count; i++)
        {
            try
            {
                expenses[i].Validate();
            }
            catch (InvalidExpenseException ex)
            {
                throw new InvalidDataException($"Record {i + 1} in the data file is invalid: {ex.Message}", ex);
            }
        }

        return expenses;
    }
}
