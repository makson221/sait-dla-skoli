using ArkhivRobit.Data;
using ArkhivRobit.Models;
using ArkhivRobit.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ArkhivRobit.Tests;

/// <summary>Перевіряє пошук на справжній базі SQLite в пам'яті.</summary>
public sealed class WorkFilterTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public WorkFilterTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();

        Add(WorkType.Coursework, "Веб-застосунок бібліотеки", "Іваненко Петро", "КН-41", 2025, "Шевченко Т. Г.");
        Add(WorkType.Diploma, "Архів дипломних робіт", "Сидоренко Марія", "КН-41", 2025, "Франко І. Я.");
        Add(WorkType.Coursework, "Мобільний розклад", "Коваль Андрій", "ІПЗ-31", 2026, null);
        _db.SaveChanges();
    }

    private void Add(WorkType type, string title, string student, string group, int year, string? supervisor) =>
        _db.Works.Add(new Work
        {
            Type = type, Title = title, StudentName = student, GroupName = group, Year = year, Supervisor = supervisor,
            SearchText = TextNormalizer.SearchText(title, student, supervisor, group),
            FileKey = Guid.NewGuid().ToString(), FileName = "a.pdf", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
        });

    private List<string> Find(WorkFilter filter) =>
        filter.Apply(_db.Works).OrderBy(w => w.Id).Select(w => w.StudentName).ToList();

    [Fact]
    public void EmptyFilter_ReturnsEverything() => Assert.Equal(3, Find(new WorkFilter()).Count);

    [Fact]
    public void ByGroupAndYear() =>
        Assert.Equal(new[] { "Іваненко Петро", "Сидоренко Марія" }, Find(new WorkFilter { Group = "кн 41", Year = 2025 }));

    [Fact]
    public void ByYearOnly() => Assert.Equal(new[] { "Коваль Андрій" }, Find(new WorkFilter { Year = 2026 }));

    [Fact]
    public void ByType() =>
        Assert.Equal(new[] { "Сидоренко Марія" }, Find(new WorkFilter { Type = WorkType.Diploma }));

    [Theory]
    [InlineData("сидоренко")]
    [InlineData("АРХІВ")]
    [InlineData("франко")]
    [InlineData("архів марія")]
    public void TextSearch_IsCaseInsensitiveAndCoversAllFields(string query) =>
        Assert.Equal(new[] { "Сидоренко Марія" }, Find(new WorkFilter { Query = query }));

    [Fact]
    public void TextSearch_RequiresAllWords() =>
        Assert.Empty(Find(new WorkFilter { Query = "архів коваль" }));

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }
}
