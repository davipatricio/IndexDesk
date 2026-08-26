using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using IndexDesk.IntegrationTests.Auth;
using FluentAssertions;
using Xunit;

namespace IndexDesk.IntegrationTests.Portfolio;

/// <summary>
/// Integration tests for /api/v1/portfolios: CRUD limit (3), ownership isolation,
/// transaction flow with average price, and logical amendment.
/// Runs against the dedicated indexdesk_test database (schema via EnsureCreated).
/// </summary>
public class PortfolioEndpointsTests : IClassFixture<AuthWebApplicationFactory>
{
    private readonly AuthWebApplicationFactory _factory;

    public PortfolioEndpointsTests(AuthWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> NewUserTokenAsync()
    {
        var client = _factory.CreateClient();
        var payload = new
        {
            email = $"pf_{Guid.NewGuid():N}@indexdesk.com.br",
            password = "SecurePassword123!",
            fullName = "Portfolio Tester",
        };
        var response = await client.PostAsJsonAsync("/api/v1/auth/signup", payload);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>();
        return body!.AccessToken;
    }

    private HttpClient NewClientWith(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            token
        );
        return client;
    }

    [Fact]
    public async Task Create_List_And_LimitOfThree()
    {
        var token = await NewUserTokenAsync();
        var client = NewClientWith(token);

        for (var i = 1; i <= 3; i++)
        {
            var response = await client.PostAsJsonAsync(
                "/api/v1/portfolios",
                new { title = $"Carteira {i}", riskProfile = "moderado" }
            );
            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var fourth = await client.PostAsJsonAsync(
            "/api/v1/portfolios",
            new { title = "Carteira 4" }
        );
        fourth.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var list = await client.GetFromJsonAsync<List<PortfolioItem>>("/api/v1/portfolios");
        list.Should().HaveCount(3);
    }

    [Fact]
    public async Task Cross_User_Access_Returns404()
    {
        var owner = await NewUserTokenAsync();
        var ownerClient = NewClientWith(owner);
        var created = await ownerClient.PostAsJsonAsync(
            "/api/v1/portfolios",
            new { title = "Privada" }
        );
        created.EnsureSuccessStatusCode();
        var portfolio = await created.Content.ReadFromJsonAsync<PortfolioItem>();

        var stranger = await NewUserTokenAsync();
        var strangerClient = NewClientWith(stranger);

        var get = await strangerClient.GetAsync($"/api/v1/portfolios/{portfolio!.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var tx = await strangerClient.PostAsJsonAsync(
            $"/api/v1/portfolios/{portfolio.Id}/transactions",
            new
            {
                type = "BUY",
                syntheticIndexCode = "CDI",
                broker = "XP",
                grossAmount = 100,
                quantity = 100,
                unitPrice = 1,
            }
        );
        tx.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Transaction_Flow_Buy_Income_Summary_WithFeesInAverage()
    {
        var token = await NewUserTokenAsync();
        var client = NewClientWith(token);

        var created = await client.PostAsJsonAsync(
            "/api/v1/portfolios",
            new { title = "RF Smoke" }
        );
        var portfolio = await created.Content.ReadFromJsonAsync<PortfolioItem>();

        var buy = await client.PostAsJsonAsync(
            $"/api/v1/portfolios/{portfolio!.Id}/transactions",
            new
            {
                type = "BUY",
                syntheticIndexCode = "CDI",
                broker = "Nu",
                quantity = 1000,
                unitPrice = 1,
                grossAmount = 1000,
                fees = 2,
                tradeDate = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            }
        );
        buy.StatusCode.Should().Be(HttpStatusCode.Created);

        var income = await client.PostAsJsonAsync(
            $"/api/v1/portfolios/{portfolio.Id}/transactions",
            new
            {
                type = "INCOME",
                syntheticIndexCode = "CDI",
                broker = "Nu",
                grossAmount = 50,
            }
        );
        income.StatusCode.Should().Be(HttpStatusCode.Created);

        var summary = await client.GetFromJsonAsync<SummaryResponse>(
            $"/api/v1/portfolios/{portfolio.Id}"
        );

        summary!.Positions.Should().HaveCount(1);
        var position = summary.Positions[0];
        position.Ticker.Should().Be("CDI");
        position.Quantity.Should().Be(1000);
        position.AveragePrice.Should().Be(1.002m); // fees entram no custo
        position.CurrentValue.Should().Be(1002m); // sem accrual ainda: valor = custo
        summary.IncomeReceived.Should().Be(50m);
    }

    [Fact]
    public async Task Amend_Transaction_Supersedes_Old_Version()
    {
        var token = await NewUserTokenAsync();
        var client = NewClientWith(token);

        var portfolio = await (
            await client.PostAsJsonAsync("/api/v1/portfolios", new { title = "Edição" })
        )
            .Content.ReadFromJsonAsync<PortfolioItem>();

        var buy = await (
            await client.PostAsJsonAsync(
                $"/api/v1/portfolios/{portfolio!.Id}/transactions",
                new
                {
                    type = "BUY",
                    syntheticIndexCode = "SELIC",
                    broker = "XP",
                    quantity = 100,
                    unitPrice = 1,
                    grossAmount = 100,
                }
            )
        )
            .Content.ReadFromJsonAsync<TransactionItem>();

        var amended = await client.PutAsJsonAsync(
            $"/api/v1/portfolios/{portfolio.Id}/transactions/{buy!.Id}",
            new
            {
                type = "BUY",
                syntheticIndexCode = "SELIC",
                broker = "XP",
                quantity = 200,
                unitPrice = 1,
                grossAmount = 200,
            }
        );
        amended.StatusCode.Should().Be(HttpStatusCode.OK);
        var amendedBody = await amended.Content.ReadFromJsonAsync<TransactionItem>();
        amendedBody!.IsAmendment.Should().BeTrue();

        // Listagem mostra só a versão vigente? Não — log completo é auditável;
        // a PROJEÇÃO (resumo) deve usar apenas a versão nova.
        var summary = await client.GetFromJsonAsync<SummaryResponse>(
            $"/api/v1/portfolios/{portfolio.Id}"
        );
        summary!.TotalInvested.Should().Be(200m);
    }

    // ---------- minimal DTOs ----------

    private sealed record AuthResponse(string AccessToken, int ExpiresIn);

    private sealed record PortfolioItem(Guid Id, string Title);

    private sealed record TransactionItem(Guid Id, bool IsAmendment);

    private sealed record SummaryPosition(
        decimal Quantity,
        decimal AveragePrice,
        decimal CurrentValue,
        string Ticker
    );

    private sealed record SummaryResponse(
        decimal TotalValue,
        decimal TotalInvested,
        decimal IncomeReceived,
        IReadOnlyList<SummaryPosition> Positions
    );
}
