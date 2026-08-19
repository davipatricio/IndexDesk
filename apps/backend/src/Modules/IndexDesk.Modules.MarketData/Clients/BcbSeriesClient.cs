using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using IndexDesk.BuildingBlocks.Common.Results;
using Microsoft.Extensions.Logging;

namespace IndexDesk.Modules.MarketData.Clients;

public class BcbSeriesClient : IBcbSeriesClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BcbSeriesClient> _logger;

    public int CdiSeriesCode => 12;
    public int SelicSeriesCode => 11;
    public int IpcaSeriesCode => 433;
    public int IgpmSeriesCode => 189;

    public BcbSeriesClient(HttpClient httpClient, ILogger<BcbSeriesClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<Result<IReadOnlyList<BcbSeriesPoint>>> GetSeriesAsync(
        int seriesCode,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var all = new List<BcbSeriesPoint>();
            var errors = new List<string>();

            // BCB returns HTTP 406 when a single request exceeds its point limit (~2000 for daily
            // series). Split long ranges into ~400-day chunks and concatenate the results.
            const int chunkDays = 400;
            var chunkStart = startDate;
            while (chunkStart <= endDate)
            {
                var chunkEnd = chunkStart.AddDays(chunkDays - 1);
                if (chunkEnd > endDate)
                    chunkEnd = endDate;

                var chunk = await FetchChunkAsync(
                    seriesCode,
                    chunkStart,
                    chunkEnd,
                    cancellationToken
                );
                if (chunk.IsFailure)
                {
                    errors.Add(chunk.Error.Message);
                }
                else
                {
                    all.AddRange(chunk.Value);
                }

                if (chunkEnd == endDate)
                    break;
                chunkStart = chunkEnd.AddDays(1);
            }

            if (all.Count == 0 && errors.Count > 0)
            {
                return Result<IReadOnlyList<BcbSeriesPoint>>.Failure(
                    Error.Failure("Bcb.HttpError", string.Join("; ", errors))
                );
            }

            all.Sort((a, b) => a.Date.CompareTo(b.Date));
            return Result<IReadOnlyList<BcbSeriesPoint>>.Success(
                all.GroupBy(p => p.Date).Select(g => g.Last()).ToList()
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BCB] Error fetching SGS series {Code}.", seriesCode);
            return Result<IReadOnlyList<BcbSeriesPoint>>.Failure(
                Error.Failure("Bcb.Exception", ex.Message)
            );
        }
    }

    private async Task<Result<IReadOnlyList<BcbSeriesPoint>>> FetchChunkAsync(
        int seriesCode,
        DateOnly startDate,
        DateOnly endDate,
        CancellationToken cancellationToken
    )
    {
        var dataInicial = startDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var dataFinal = endDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var url =
            $"dados/serie/bcdata.sgs.{seriesCode}/dados?formato=json&dataInicial={dataInicial}&dataFinal={dataFinal}";

        var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return Result<IReadOnlyList<BcbSeriesPoint>>.Failure(
                Error.Failure(
                    "Bcb.HttpError",
                    $"BCB returned HTTP {response.StatusCode} for series {seriesCode}"
                )
            );
        }

        var content = await response.Content.ReadFromJsonAsync<List<BcbJsonPoint>>(
            cancellationToken: cancellationToken
        );
        if (content is null || content.Count == 0)
        {
            return Result<IReadOnlyList<BcbSeriesPoint>>.Success(Array.Empty<BcbSeriesPoint>());
        }

        var points = new List<BcbSeriesPoint>(content.Count);
        foreach (var item in content)
        {
            if (
                DateOnly.TryParseExact(
                    item.Data,
                    "dd/MM/yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var date
                )
                && decimal.TryParse(
                    item.Valor,
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var value
                )
            )
            {
                points.Add(new BcbSeriesPoint(date, value));
            }
        }

        return Result<IReadOnlyList<BcbSeriesPoint>>.Success(points);
    }

    private sealed class BcbJsonPoint
    {
        [JsonPropertyName("data")]
        public string Data { get; set; } = string.Empty;

        [JsonPropertyName("valor")]
        public string Valor { get; set; } = string.Empty;
    }
}
