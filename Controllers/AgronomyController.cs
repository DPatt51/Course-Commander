using CourseCommander.Data;
using CourseCommander.Entities;
using CourseCommander.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/agronomy")]
public class AgronomyController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AgronomyService _agronomyService;

    public AgronomyController(AppDbContext context, AgronomyService agronomyService)
    {
        _context = context;
        _agronomyService = agronomyService;
    }

    [HttpPost]
    public async Task<ActionResult<AgronomyReading>> CreateAgronomyReading(AgronomyReading reading)
    {
        if (string.IsNullOrWhiteSpace(reading.MeasurementType))
        {
            return BadRequest("MeasurementType is required.");
        }

        if (string.IsNullOrWhiteSpace(reading.Location))
        {
            return BadRequest("Location is required.");
        }

        reading.SourceSystemName = string.IsNullOrWhiteSpace(reading.SourceSystemName)
            ? "Manual Entry"
            : reading.SourceSystemName;
        reading.CreatedAt = DateTime.UtcNow;

        _context.AgronomyReadings.Add(reading);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetAgronomyReadings), new { date = DateOnly.FromDateTime(reading.DateTime).ToString("yyyy-MM-dd") }, reading);
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AgronomyReading>>> GetAgronomyReadings([FromQuery] DateOnly? date)
    {
        if (date is null)
        {
            return BadRequest("Date is required. Example: /api/agronomy?date=2026-04-28");
        }

        return await _agronomyService.GetReadingsForDateAsync(date.Value);
    }

    [HttpGet("location")]
    public async Task<ActionResult<IEnumerable<AgronomyReading>>> GetAgronomyReadingsByLocation([FromQuery] string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest("Location name is required. Example: /api/agronomy/location?name=Green%204");
        }

        return await _context.AgronomyReadings
            .Where(reading => reading.Location == name)
            .OrderByDescending(reading => reading.DateTime)
            .ToListAsync();
    }

    [HttpPost("import-csv")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportAgronomyCsv(IFormFile file)
    {
        try
        {
            var rows = await ReadCsvRowsAsync(file, new[]
            {
                "DateTime",
                "MeasurementType",
                "Location",
                "Zone",
                "Value",
                "Unit",
                "Notes"
            });
            var now = DateTime.UtcNow;
            var readings = rows.Select(row => new AgronomyReading
            {
                DateTime = ParseDateTime(row[0], "DateTime"),
                MeasurementType = row[1],
                Location = row[2],
                Zone = row[3],
                Value = ParseDecimal(row[4], "Value"),
                Unit = row[5],
                Notes = row[6],
                SourceSystemName = "CSV Import",
                CreatedAt = now,
                SyncedAt = now
            }).ToList();

            _context.AgronomyReadings.AddRange(readings);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Agronomy CSV import completed.",
                recordsProcessed = readings.Count
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new
            {
                message = $"Agronomy CSV import failed: {ex.Message}",
                recordsProcessed = 0
            });
        }
    }

    private static async Task<List<string[]>> ReadCsvRowsAsync(IFormFile file, string[] expectedHeaders)
    {
        if (file is null || file.Length == 0)
        {
            throw new InvalidOperationException("Please upload a CSV file.");
        }

        var rows = new List<string[]>();

        using var reader = new StreamReader(file.OpenReadStream());
        var headerLine = await reader.ReadLineAsync();

        if (string.IsNullOrWhiteSpace(headerLine))
        {
            throw new InvalidOperationException("CSV file is missing a header row.");
        }

        var headers = ParseCsvLine(headerLine);

        if (!HeadersMatch(headers, expectedHeaders))
        {
            throw new InvalidOperationException($"CSV headers must be: {string.Join(",", expectedHeaders)}");
        }

        var lineNumber = 1;

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            lineNumber++;

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var values = ParseCsvLine(line);

            if (values.Length != expectedHeaders.Length)
            {
                throw new InvalidOperationException($"Line {lineNumber} has {values.Length} column(s), but {expectedHeaders.Length} were expected.");
            }

            rows.Add(values);
        }

        return rows;
    }

    private static string[] ParseCsvLine(string line)
    {
        return line
            .Split(',')
            .Select(value => value.Trim().Trim('"'))
            .ToArray();
    }

    private static bool HeadersMatch(string[] actualHeaders, string[] expectedHeaders)
    {
        if (actualHeaders.Length != expectedHeaders.Length)
        {
            return false;
        }

        for (var index = 0; index < expectedHeaders.Length; index++)
        {
            if (!string.Equals(actualHeaders[index], expectedHeaders[index], StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static DateTime ParseDateTime(string value, string columnName)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTime))
        {
            return dateTime;
        }

        throw new InvalidOperationException($"{columnName} must be a valid date and time.");
    }

    private static decimal ParseDecimal(string value, string columnName)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        throw new InvalidOperationException($"{columnName} must be a valid number.");
    }
}
