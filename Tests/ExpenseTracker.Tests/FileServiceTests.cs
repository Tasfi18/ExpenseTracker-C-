using ExpenseTracker.Models;
using ExpenseTracker.Services;

namespace ExpenseTracker.Tests;

/// <summary>
/// Tests for saving and loading. Each test works in its own temporary folder,
/// which Dispose() deletes afterwards, so the real Data/expenses.json is never touched.
/// </summary>
public sealed class FileServiceTests : IDisposable
{
    private readonly string _folder;
    private readonly string _filePath;

    public FileServiceTests()
    {
        _folder = Path.Combine(Path.GetTempPath(), "ExpenseTrackerTests", Guid.NewGuid().ToString("N"));
        _filePath = Path.Combine(_folder, "Data", "expenses.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RestoresAllExpenses()
    {
        var manager = new ExpenseManager();
        manager.AddExpense("Groceries", 54.30m, "Food", new DateTime(2026, 9, 3), "Weekly shop");
        manager.AddExpense(new RecurringExpense(
            "Netflix", 15.49m, "Entertainment", new DateTime(2026, 9, 5), RecurrenceFrequency.Monthly));
        var service = new FileService(_filePath);

        await service.SaveAsync(manager.Expenses);
        IReadOnlyList<Expense> loaded = await service.LoadAsync();

        Assert.Equal(2, loaded.Count);

        Expense groceries = loaded[0];
        Assert.Equal(1, groceries.Id);
        Assert.Equal("Groceries", groceries.Title);
        Assert.Equal(54.30m, groceries.Amount);
        Assert.Equal("Food", groceries.Category);
        Assert.Equal(new DateTime(2026, 9, 3), groceries.Date);
        Assert.Equal("Weekly shop", groceries.Notes);

        // The derived type survives the round trip.
        RecurringExpense netflix = Assert.IsType<RecurringExpense>(loaded[1]);
        Assert.Equal(RecurrenceFrequency.Monthly, netflix.Frequency);
    }

    [Fact]
    public async Task SaveAsync_CreatesMissingFolder()
    {
        var service = new FileService(_filePath);

        await service.SaveAsync(new List<Expense>());

        Assert.True(File.Exists(_filePath));
    }

    [Fact]
    public async Task LoadAsync_RestoredExpenses_WorkWithNewManager()
    {
        var first = new ExpenseManager();
        first.AddExpense("Coffee", 3.80m, "Food", new DateTime(2026, 9, 24));
        first.AddExpense("Taxi", 22.50m, "Transport", new DateTime(2026, 9, 25));
        var service = new FileService(_filePath);
        await service.SaveAsync(first.Expenses);

        // Simulates restarting the application.
        var afterRestart = new ExpenseManager();
        afterRestart.ReplaceAll(await service.LoadAsync());

        Assert.Equal(first.CalculateTotalExpenses(), afterRestart.CalculateTotalExpenses());
        Assert.Equal(3, afterRestart.AddExpense("Tea", 2m, "Food", new DateTime(2026, 9, 26)).Id);
    }

    [Fact]
    public async Task LoadAsync_MissingFile_ThrowsFileNotFoundException()
    {
        var service = new FileService(Path.Combine(_folder, "does-not-exist.json"));

        Assert.False(service.FileExists());
        await Assert.ThrowsAsync<FileNotFoundException>(() => service.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_CorruptedFile_ThrowsInvalidDataException()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllTextAsync(_filePath, "{ this is not valid json ");
        var service = new FileService(_filePath);

        await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadAsync());
    }

    [Fact]
    public async Task LoadAsync_FileWithInvalidExpense_ThrowsInvalidDataException()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllTextAsync(
            _filePath,
            """[{ "type": "one-time", "id": 1, "title": "Bad", "amount": -5, "category": "Food", "date": "2026-09-01" }]""");
        var service = new FileService(_filePath);

        InvalidDataException ex = await Assert.ThrowsAsync<InvalidDataException>(() => service.LoadAsync());
        Assert.Contains("greater than zero", ex.Message);
    }

    [Fact]
    public async Task LoadAsync_EmptyFile_ReturnsNoExpenses()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        await File.WriteAllTextAsync(_filePath, string.Empty);
        var service = new FileService(_filePath);

        Assert.Empty(await service.LoadAsync());
    }
}
