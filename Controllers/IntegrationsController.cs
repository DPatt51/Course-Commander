using CourseCommander.Data;
using CourseCommander.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;

namespace CourseCommander.Controllers;

[ApiController]
[Route("api/integrations")]
public class IntegrationsController : ControllerBase
{
    private const double DefaultLatitude = 32.940583969923814;
    private const double DefaultLongitude = -84.96102247116396;
    private const int ForecastApiPastDays = 7;

    private readonly AppDbContext _context;
    private readonly IHttpClientFactory _httpClientFactory;

    public IntegrationsController(AppDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet("sources")]
    public async Task<ActionResult<IEnumerable<SourceSystem>>> GetSources()
    {
        return await _context.SourceSystems
            .OrderBy(source => source.Name)
            .ToListAsync();
    }

    [HttpPost("sources")]
    public async Task<ActionResult<SourceSystem>> CreateSource(SourceSystem sourceSystem)
    {
        if (string.IsNullOrWhiteSpace(sourceSystem.Name) || string.IsNullOrWhiteSpace(sourceSystem.Type))
        {
            return BadRequest("Name and Type are required.");
        }

        sourceSystem.CreatedAt = DateTime.UtcNow;
        _context.SourceSystems.Add(sourceSystem);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetSources), new { id = sourceSystem.Id }, sourceSystem);
    }

    [HttpGet("sync-runs")]
    public async Task<ActionResult<IEnumerable<SyncRun>>> GetSyncRuns()
    {
        return await _context.SyncRuns
            .Include(run => run.SourceSystem)
            .OrderByDescending(run => run.StartedAt)
            .ToListAsync();
    }

    [HttpPost("asb/mock-sync")]
    public async Task<ActionResult<SyncRun>> MockAsbTaskSync()
    {
        var sourceSystem = await GetOrCreateSourceSystemAsync("ASB Task Tracker", "Task");
        var syncRun = await StartSyncRunAsync(sourceSystem, "ASB Task Tracker sync started.");
        var syncedAt = DateTime.UtcNow;
        var recordsProcessed = 0;

        var asbTasks = new List<AsbTaskRecord>
        {
            new("ASB-1001", "Repair irrigation leak near hole 4", "Valve leak reported by morning crew.", "Irrigation", "High", "assigned", "Jordan"),
            new("ASB-1002", "Inspect bunker washout on hole 12", "Rain created washout along the front-right bunker face.", "Fairways", "Critical", "new", "Alex"),
            new("ASB-1003", "Replace range picker tire", "Rear tire is losing pressure during collection runs.", "Equipment", "Medium", "blocked", "Sam"),
            new("ASB-1004", "Finish clubhouse entry cleanup", "Cleanup completed after weekend event.", "Clubhouse", "Low", "completed", "Morgan")
        };

        foreach (var asbTask in asbTasks)
        {
            var task = await _context.MaintenanceTasks
                .FirstOrDefaultAsync(task => task.ExternalTaskId == asbTask.ExternalTaskId);
            var mappedStatus = MapAsbStatus(asbTask.ExternalStatus);

            if (task is null)
            {
                task = new MaintenanceTask
                {
                    CreatedAt = syncedAt,
                    ExternalSourceName = sourceSystem.Name,
                    ExternalTaskId = asbTask.ExternalTaskId,
                    IsExternal = true
                };

                _context.MaintenanceTasks.Add(task);
            }

            task.Title = asbTask.Title;
            task.Description = asbTask.Description;
            task.Category = asbTask.Category;
            task.Priority = asbTask.Priority;
            task.AssignedTo = asbTask.AssignedTo;
            task.Status = mappedStatus;
            task.ExternalStatus = asbTask.ExternalStatus;
            task.ExternalSourceName = sourceSystem.Name;
            task.ExternalTaskId = asbTask.ExternalTaskId;
            task.IsExternal = true;
            task.LastSyncedAt = syncedAt;
            task.UpdatedAt = syncedAt;

            if (mappedStatus is MaintenanceTaskStatus.InProgress or MaintenanceTaskStatus.Blocked or MaintenanceTaskStatus.Completed)
            {
                task.StartedAt ??= syncedAt;
            }

            task.CompletedAt = mappedStatus == MaintenanceTaskStatus.Completed
                ? task.CompletedAt ?? syncedAt
                : null;

            recordsProcessed++;
        }

        await CompleteSyncRunAsync(syncRun, $"ASB Task Tracker sync completed. {recordsProcessed} task(s) processed.", recordsProcessed);

        return Ok(syncRun);
    }

    [HttpPost("weather/mock-sync")]
    public async Task<ActionResult<SyncRun>> MockWeatherSync([FromQuery] DateOnly? date)
    {
        var syncDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var sourceSystem = await GetOrCreateSourceSystemAsync("Course Weather Sample", "Weather");
        var syncRun = await StartSyncRunAsync(sourceSystem, "Weather sync started.");

        var metric = await _context.DailyWeatherMetrics
            .FirstOrDefaultAsync(metric => metric.Date == syncDate && metric.SourceSystemName == sourceSystem.Name);

        if (metric is null)
        {
            metric = new DailyWeatherMetric
            {
                Date = syncDate,
                SourceSystemName = sourceSystem.Name
            };
            _context.DailyWeatherMetrics.Add(metric);
        }

        metric.HighTemp = 78;
        metric.LowTemp = 61;
        metric.RainfallInches = 0.12m;
        metric.WeatherSummary = "Partly cloudy with light afternoon rain.";
        metric.SyncedAt = DateTime.UtcNow;

        await CompleteSyncRunAsync(syncRun, "Weather sync completed.", 1);

        return Ok(syncRun);
    }

    [HttpPost("weather/sync")]
    public async Task<ActionResult<SyncRun>> SyncWeather([FromQuery] DateOnly? date)
    {
        if (date is null)
        {
            return BadRequest("Date is required. Example: /api/integrations/weather/sync?date=2026-04-28");
        }

        var syncDate = date.Value;
        var sourceSystem = await GetOrCreateSourceSystemAsync("Open-Meteo", "Weather");
        var syncRun = await StartSyncRunAsync(sourceSystem, "Open-Meteo weather sync started.");

        try
        {
            var weatherData = await GetOpenMeteoWeatherAsync(syncDate);
            await SaveWeatherMetricAsync(sourceSystem.Name, weatherData);
            await _context.SaveChangesAsync();

            await CompleteSyncRunAsync(syncRun, "Open-Meteo weather sync completed.", 1);

            return Ok(syncRun);
        }
        catch (Exception ex)
        {
            await FailSyncRunAsync(syncRun, $"Open-Meteo weather sync failed: {ex.Message}");
            return StatusCode(502, syncRun);
        }
    }

    [HttpPost("weather/sync-range")]
    public async Task<ActionResult<SyncRun>> SyncWeatherRange([FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate)
    {
        if (startDate is null || endDate is null)
        {
            return BadRequest("startDate and endDate are required. Example: /api/integrations/weather/sync-range?startDate=2026-04-01&endDate=2026-04-07");
        }

        if (startDate > endDate)
        {
            return BadRequest("startDate must be before or equal to endDate.");
        }

        var sourceSystem = await GetOrCreateSourceSystemAsync("Open-Meteo", "Weather");
        var syncRun = await StartSyncRunAsync(sourceSystem, "Open-Meteo weather range sync started.");

        try
        {
            var recordsProcessed = await SyncWeatherRangeAsync(startDate.Value, endDate.Value, sourceSystem.Name);
            await CompleteSyncRunAsync(syncRun, $"Open-Meteo weather range sync completed. {recordsProcessed} day(s) processed.", recordsProcessed);

            return Ok(syncRun);
        }
        catch (Exception ex)
        {
            await FailSyncRunAsync(syncRun, $"Open-Meteo weather range sync failed: {ex.Message}");
            return StatusCode(502, syncRun);
        }
    }

    [HttpPost("weather/sync-missing")]
    public async Task<ActionResult<SyncRun>> SyncMissingWeather()
    {
        var sourceSystem = await GetOrCreateSourceSystemAsync("Open-Meteo", "Weather");
        var syncRun = await StartSyncRunAsync(sourceSystem, "Open-Meteo missing weather sync started.");

        try
        {
            var playDates = await _context.DailyPlayMetrics
                .Select(metric => metric.Date)
                .ToListAsync();

            var salesDates = await _context.DailySalesMetrics
                .Select(metric => metric.Date)
                .ToListAsync();

            var weatherDates = await _context.DailyWeatherMetrics
                .Select(metric => metric.Date)
                .ToListAsync();

            var missingDates = playDates
                .Concat(salesDates)
                .Distinct()
                .Except(weatherDates)
                .OrderBy(date => date)
                .ToList();

            if (missingDates.Count == 0)
            {
                await CompleteSyncRunAsync(syncRun, "No missing weather dates found.", 0);
                return Ok(syncRun);
            }

            var recordsProcessed = await SyncWeatherDatesAsync(missingDates, sourceSystem.Name);

            await CompleteSyncRunAsync(syncRun, $"Open-Meteo missing weather sync completed. {recordsProcessed} day(s) processed.", recordsProcessed);

            return Ok(syncRun);
        }
        catch (Exception ex)
        {
            await FailSyncRunAsync(syncRun, $"Open-Meteo missing weather sync failed: {ex.Message}");
            return StatusCode(502, syncRun);
        }
    }

    [HttpPost("sales/mock-sync")]
    public async Task<ActionResult<SyncRun>> MockSalesSync([FromQuery] DateOnly? date)
    {
        var syncDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var sourceSystem = await GetOrCreateSourceSystemAsync("Course POS Sample", "Sales");
        var syncRun = await StartSyncRunAsync(sourceSystem, "Sales sync started.");

        var metric = await _context.DailySalesMetrics
            .FirstOrDefaultAsync(metric => metric.Date == syncDate && metric.SourceSystemName == sourceSystem.Name);

        if (metric is null)
        {
            metric = new DailySalesMetric
            {
                Date = syncDate,
                SourceSystemName = sourceSystem.Name
            };
            _context.DailySalesMetrics.Add(metric);
        }

        metric.TotalRevenue = 18450.75m;
        metric.FoodAndBeverageRevenue = 4320.50m;
        metric.ProShopRevenue = 2875.25m;
        metric.AlcoholRevenue = 1620.25m;
        metric.RangeBallRevenue = 875.00m;
        metric.TransactionCount = 236;
        metric.SyncedAt = DateTime.UtcNow;

        await CompleteSyncRunAsync(syncRun, "Sales sync completed.", 1);

        return Ok(syncRun);
    }

    [HttpPost("sales/import-csv")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<SyncRun>> ImportSalesCsv(IFormFile file)
    {
        var sourceSystem = await GetOrCreateSourceSystemAsync("CSV Import", "Sales");
        var syncRun = await StartSyncRunAsync(sourceSystem, "Sales CSV import started.");

        try
        {
            var rows = await ReadCsvRowsAsync(file, new[]
            {
                "Date",
                "TotalRevenue",
                "ProShopRevenue",
                "FoodAndBeverageRevenue",
                "TransactionCount"
            });

            var salesRows = rows.Select(row => new SalesCsvRow
            {
                Date = ParseDate(row[0], "Date"),
                TotalRevenue = ParseDecimal(row[1], "TotalRevenue"),
                ProShopRevenue = ParseDecimal(row[2], "ProShopRevenue"),
                FoodAndBeverageRevenue = ParseDecimal(row[3], "FoodAndBeverageRevenue"),
                TransactionCount = ParseInt(row[4], "TransactionCount")
            }).ToList();

            foreach (var row in salesRows)
            {
                var metric = await _context.DailySalesMetrics
                    .FirstOrDefaultAsync(metric => metric.Date == row.Date && metric.SourceSystemName == sourceSystem.Name);

                if (metric is null)
                {
                    metric = new DailySalesMetric
                    {
                        Date = row.Date,
                        SourceSystemName = sourceSystem.Name
                    };
                    _context.DailySalesMetrics.Add(metric);
                }

                metric.TotalRevenue = row.TotalRevenue;
                metric.ProShopRevenue = row.ProShopRevenue;
                metric.FoodAndBeverageRevenue = row.FoodAndBeverageRevenue;
                metric.AlcoholRevenue = row.AlcoholRevenue;
                metric.RangeBallRevenue = row.RangeBallRevenue;
                metric.TransactionCount = row.TransactionCount;
                metric.SyncedAt = DateTime.UtcNow;
            }

            await CompleteSyncRunAsync(syncRun, $"Sales CSV import completed. {salesRows.Count} record(s) processed.", salesRows.Count);

            return Ok(syncRun);
        }
        catch (Exception ex)
        {
            await FailSyncRunAsync(syncRun, $"Sales CSV import failed: {ex.Message}");
            return BadRequest(syncRun);
        }
    }

    [HttpPost("play/mock-sync")]
    public async Task<ActionResult<SyncRun>> MockPlaySync([FromQuery] DateOnly? date)
    {
        var syncDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var sourceSystem = await GetOrCreateSourceSystemAsync("Course Tee Sheet Sample", "Play");
        var syncRun = await StartSyncRunAsync(sourceSystem, "Play sync started.");

        var metric = await _context.DailyPlayMetrics
            .FirstOrDefaultAsync(metric => metric.Date == syncDate && metric.SourceSystemName == sourceSystem.Name);

        if (metric is null)
        {
            metric = new DailyPlayMetric
            {
                Date = syncDate,
                SourceSystemName = sourceSystem.Name
            };
            _context.DailyPlayMetrics.Add(metric);
        }

        metric.RoundsPlayed = 142;
        metric.CartRentals = 96;
        metric.NineHoleRounds = 22;
        metric.EighteenHoleRounds = 120;
        metric.SyncedAt = DateTime.UtcNow;

        await CompleteSyncRunAsync(syncRun, "Play sync completed.", 1);

        return Ok(syncRun);
    }

    [HttpPost("play/import-csv")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<SyncRun>> ImportPlayCsv(IFormFile file)
    {
        var sourceSystem = await GetOrCreateSourceSystemAsync("CSV Import", "Play");
        var syncRun = await StartSyncRunAsync(sourceSystem, "Play CSV import started.");

        try
        {
            var rows = await ReadCsvRowsAsync(file, new[]
            {
                "Date",
                "RoundsPlayed",
                "CartRentals",
                "NineHoleRounds",
                "EighteenHoleRounds"
            });

            var playRows = rows.Select(row => new PlayCsvRow
            {
                Date = ParseDate(row[0], "Date"),
                RoundsPlayed = ParseInt(row[1], "RoundsPlayed"),
                CartRentals = ParseInt(row[2], "CartRentals"),
                NineHoleRounds = ParseInt(row[3], "NineHoleRounds"),
                EighteenHoleRounds = ParseInt(row[4], "EighteenHoleRounds")
            }).ToList();

            foreach (var row in playRows)
            {
                var metric = await _context.DailyPlayMetrics
                    .FirstOrDefaultAsync(metric => metric.Date == row.Date && metric.SourceSystemName == sourceSystem.Name);

                if (metric is null)
                {
                    metric = new DailyPlayMetric
                    {
                        Date = row.Date,
                        SourceSystemName = sourceSystem.Name
                    };
                    _context.DailyPlayMetrics.Add(metric);
                }

                metric.RoundsPlayed = row.RoundsPlayed;
                metric.CartRentals = row.CartRentals;
                metric.NineHoleRounds = row.NineHoleRounds;
                metric.EighteenHoleRounds = row.EighteenHoleRounds;
                metric.SyncedAt = DateTime.UtcNow;
            }

            await CompleteSyncRunAsync(syncRun, $"Play CSV import completed. {playRows.Count} record(s) processed.", playRows.Count);

            return Ok(syncRun);
        }
        catch (Exception ex)
        {
            await FailSyncRunAsync(syncRun, $"Play CSV import failed: {ex.Message}");
            return BadRequest(syncRun);
        }
    }

    private async Task<SourceSystem> GetOrCreateSourceSystemAsync(string name, string type)
    {
        var sourceSystem = await _context.SourceSystems
            .FirstOrDefaultAsync(source => source.Name == name && source.Type == type);

        if (sourceSystem is not null)
        {
            return sourceSystem;
        }

        sourceSystem = new SourceSystem
        {
            Name = name,
            Type = type,
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.SourceSystems.Add(sourceSystem);
        await _context.SaveChangesAsync();

        return sourceSystem;
    }

    private async Task<SyncRun> StartSyncRunAsync(SourceSystem sourceSystem, string message)
    {
        var syncRun = new SyncRun
        {
            SourceSystemId = sourceSystem.Id,
            SourceSystem = sourceSystem,
            StartedAt = DateTime.UtcNow,
            Status = "InProgress",
            Message = message
        };

        _context.SyncRuns.Add(syncRun);
        await _context.SaveChangesAsync();

        return syncRun;
    }

    private async Task CompleteSyncRunAsync(SyncRun syncRun, string message, int recordsProcessed)
    {
        syncRun.CompletedAt = DateTime.UtcNow;
        syncRun.Status = "Success";
        syncRun.Message = message;
        syncRun.RecordsProcessed = recordsProcessed;

        await _context.SaveChangesAsync();
    }

    private async Task FailSyncRunAsync(SyncRun syncRun, string message)
    {
        syncRun.CompletedAt = DateTime.UtcNow;
        syncRun.Status = "Failed";
        syncRun.Message = message;
        syncRun.RecordsProcessed = 0;

        await _context.SaveChangesAsync();
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

    private static DateOnly ParseDate(string value, string columnName)
    {
        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        throw new InvalidOperationException($"{columnName} must be a valid date.");
    }

    private static int ParseInt(string value, string columnName)
    {
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        throw new InvalidOperationException($"{columnName} must be a whole number.");
    }

    private static decimal ParseDecimal(string value, string columnName)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return number;
        }

        throw new InvalidOperationException($"{columnName} must be a valid number.");
    }

    private async Task<int> SyncWeatherRangeAsync(
        DateOnly startDate,
        DateOnly endDate,
        string sourceSystemName,
        HashSet<DateOnly>? onlyDates = null)
    {
        var weatherRows = await GetOpenMeteoWeatherRangeAsync(startDate, endDate);
        var recordsProcessed = 0;

        foreach (var weatherData in weatherRows)
        {
            if (onlyDates is not null && !onlyDates.Contains(weatherData.Date))
            {
                continue;
            }

            await SaveWeatherMetricAsync(sourceSystemName, weatherData);
            recordsProcessed++;
        }

        await _context.SaveChangesAsync();

        return recordsProcessed;
    }

    private async Task<int> SyncWeatherDatesAsync(List<DateOnly> dates, string sourceSystemName)
    {
        var recordsProcessed = 0;

        foreach (var range in GetConsecutiveDateRanges(dates))
        {
            recordsProcessed += await SyncWeatherRangeAsync(
                range.StartDate,
                range.EndDate,
                sourceSystemName,
                range.Dates.ToHashSet());
        }

        return recordsProcessed;
    }

    private async Task SaveWeatherMetricAsync(string sourceSystemName, OpenMeteoWeatherData weatherData)
    {
        var metric = await _context.DailyWeatherMetrics
            .FirstOrDefaultAsync(metric => metric.Date == weatherData.Date && metric.SourceSystemName == sourceSystemName);

        if (metric is null)
        {
            metric = new DailyWeatherMetric
            {
                Date = weatherData.Date,
                SourceSystemName = sourceSystemName
            };
            _context.DailyWeatherMetrics.Add(metric);
        }

        metric.HighTemp = weatherData.HighTemp;
        metric.LowTemp = weatherData.LowTemp;
        metric.RainfallInches = weatherData.RainfallInches;
        metric.WeatherSummary = weatherData.WeatherSummary;
        metric.SyncedAt = DateTime.UtcNow;
    }

    private async Task<OpenMeteoWeatherData> GetOpenMeteoWeatherAsync(DateOnly date)
    {
        var weatherRows = await GetOpenMeteoWeatherRangeAsync(date, date);

        if (weatherRows.Count == 0)
        {
            throw new InvalidOperationException("Open-Meteo returned no weather rows for the requested date.");
        }

        return weatherRows[0];
    }

    private async Task<List<OpenMeteoWeatherData>> GetOpenMeteoWeatherRangeAsync(DateOnly startDate, DateOnly endDate)
    {
        var forecastStartDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-ForecastApiPastDays);
        var weatherRows = new List<OpenMeteoWeatherData>();

        if (startDate < forecastStartDate)
        {
            var archiveEndDate = endDate < forecastStartDate ? endDate : forecastStartDate.AddDays(-1);
            weatherRows.AddRange(await FetchOpenMeteoWeatherRangeAsync(
                "https://archive-api.open-meteo.com/v1/archive",
                startDate,
                archiveEndDate));
        }

        if (endDate >= forecastStartDate)
        {
            var forecastStart = startDate > forecastStartDate ? startDate : forecastStartDate;
            weatherRows.AddRange(await FetchOpenMeteoWeatherRangeAsync(
                "https://api.open-meteo.com/v1/forecast",
                forecastStart,
                endDate));
        }

        return weatherRows
            .OrderBy(row => row.Date)
            .ToList();
    }

    private async Task<List<OpenMeteoWeatherData>> FetchOpenMeteoWeatherRangeAsync(
        string apiUrl,
        DateOnly startDate,
        DateOnly endDate)
    {
        var client = _httpClientFactory.CreateClient();
        var formattedStartDate = startDate.ToString("yyyy-MM-dd");
        var formattedEndDate = endDate.ToString("yyyy-MM-dd");
        var url =
            apiUrl +
            $"?latitude={DefaultLatitude}" +
            $"&longitude={DefaultLongitude}" +
            "&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,weather_code" +
            "&temperature_unit=fahrenheit" +
            "&precipitation_unit=inch" +
            "&timezone=auto" +
            $"&start_date={formattedStartDate}" +
            $"&end_date={formattedEndDate}";

        using var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Open-Meteo returned {(int)response.StatusCode}: {errorBody}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);

        if (!document.RootElement.TryGetProperty("daily", out var daily))
        {
            throw new InvalidOperationException("Open-Meteo response did not include daily weather data.");
        }

        if (!daily.TryGetProperty("time", out var times))
        {
            throw new InvalidOperationException("Open-Meteo response did not include daily dates.");
        }

        var weatherRows = new List<OpenMeteoWeatherData>();

        for (var index = 0; index < times.GetArrayLength(); index++)
        {
            var date = DateOnly.Parse(times[index].GetString() ?? string.Empty);
            var weatherCode = GetDecimalAt(daily, "weather_code", index);

            weatherRows.Add(new OpenMeteoWeatherData
            {
                Date = date,
                HighTemp = GetDecimalAt(daily, "temperature_2m_max", index),
                LowTemp = GetDecimalAt(daily, "temperature_2m_min", index),
                RainfallInches = GetDecimalAt(daily, "precipitation_sum", index),
                WeatherSummary = GetWeatherSummary((int)weatherCode)
            });
        }

        return weatherRows;
    }

    private static List<WeatherDateRange> GetConsecutiveDateRanges(List<DateOnly> dates)
    {
        var sortedDates = dates
            .Distinct()
            .OrderBy(date => date)
            .ToList();

        var ranges = new List<WeatherDateRange>();

        if (sortedDates.Count == 0)
        {
            return ranges;
        }

        var currentRangeDates = new List<DateOnly> { sortedDates[0] };

        for (var index = 1; index < sortedDates.Count; index++)
        {
            if (sortedDates[index] == sortedDates[index - 1].AddDays(1))
            {
                currentRangeDates.Add(sortedDates[index]);
            }
            else
            {
                ranges.Add(new WeatherDateRange(currentRangeDates));
                currentRangeDates = new List<DateOnly> { sortedDates[index] };
            }
        }

        ranges.Add(new WeatherDateRange(currentRangeDates));

        return ranges;
    }

    private static decimal GetDecimalAt(JsonElement daily, string propertyName, int index)
    {
        if (!daily.TryGetProperty(propertyName, out var values) || values.GetArrayLength() <= index)
        {
            throw new InvalidOperationException($"Open-Meteo response did not include {propertyName}.");
        }

        if (values[index].ValueKind == JsonValueKind.Null)
        {
            return 0;
        }

        return values[index].GetDecimal();
    }

    private static string GetWeatherSummary(int weatherCode)
    {
        return weatherCode switch
        {
            0 => "Clear sky",
            1 or 2 or 3 => "Mainly clear, partly cloudy, or overcast",
            45 or 48 => "Fog",
            51 or 53 or 55 => "Drizzle",
            56 or 57 => "Freezing drizzle",
            61 or 63 or 65 => "Rain",
            66 or 67 => "Freezing rain",
            71 or 73 or 75 => "Snow",
            77 => "Snow grains",
            80 or 81 or 82 => "Rain showers",
            85 or 86 => "Snow showers",
            95 => "Thunderstorm",
            96 or 99 => "Thunderstorm with hail",
            _ => "Weather conditions unavailable"
        };
    }

    private static MaintenanceTaskStatus MapAsbStatus(string externalStatus)
    {
        return externalStatus.ToLowerInvariant() switch
        {
            "new" => MaintenanceTaskStatus.Open,
            "assigned" => MaintenanceTaskStatus.InProgress,
            "completed" => MaintenanceTaskStatus.Completed,
            "blocked" => MaintenanceTaskStatus.Blocked,
            _ => MaintenanceTaskStatus.Open
        };
    }

    private record AsbTaskRecord(
        string ExternalTaskId,
        string Title,
        string Description,
        string Category,
        string Priority,
        string ExternalStatus,
        string AssignedTo);

    private class OpenMeteoWeatherData
    {
        public DateOnly Date { get; set; }
        public decimal HighTemp { get; set; }
        public decimal LowTemp { get; set; }
        public decimal RainfallInches { get; set; }
        public string WeatherSummary { get; set; } = string.Empty;
    }

    private class WeatherDateRange
    {
        public WeatherDateRange(List<DateOnly> dates)
        {
            Dates = dates;
            StartDate = dates.First();
            EndDate = dates.Last();
        }

        public DateOnly StartDate { get; }
        public DateOnly EndDate { get; }
        public List<DateOnly> Dates { get; }
    }

    private class PlayCsvRow
    {
        public DateOnly Date { get; set; }
        public int RoundsPlayed { get; set; }
        public int CartRentals { get; set; }
        public int NineHoleRounds { get; set; }
        public int EighteenHoleRounds { get; set; }
    }

    private class SalesCsvRow
    {
        public DateOnly Date { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal ProShopRevenue { get; set; }
        public decimal FoodAndBeverageRevenue { get; set; }
        public decimal AlcoholRevenue { get; set; }
        public decimal RangeBallRevenue { get; set; }
        public int TransactionCount { get; set; }
    }
}
