using System.Text;
using ClosedXML.Excel;
using FluentAssertions;
using IndexDesk.Modules.MarketData.Ingestion.Holdings;
using Xunit;

namespace IndexDesk.UnitTests.MarketData;

/// <summary>
/// Parser tests against LOCAL fixtures only (never live sites) — iShares CSV,
/// SPDR XLSX built in-memory with the same shape as the real file, and the
/// It Now / Investo composition HTML pages.
/// </summary>
public class HoldingsParsersTests
{
    private static string FixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Holdings", fileName);

    // ---- iShares ---------------------------------------------------------------

    [Theory]
    [InlineData(
        """<a href="https://www.blackrock.com/br/intermediarios/produtos/310490/ishares-iboovespa-fundo-de-indice-ajax.jsp?fileType=csv&fileName=BOVA11_acoes&dataType=fund">CSV</a>"""
    )]
    [InlineData(
        """<div data-download="https://www.ishares.com/us/products/239726/ishares-core-sp-500-etf_advisor.ajax?fileType=csv&amp;fileName=IVV_holdings&amp;dataType=fund"></div>"""
    )]
    public void ExtractAjaxCsvUrl_FindsRotatingLink_InProductPageHtml(string html)
    {
        var url = ISharesHoldingsParser.ExtractAjaxCsvUrl(html);

        url.Should().NotBeNull();
        url.Should().Contain("fileType=csv");
    }

    [Fact]
    public void ExtractAjaxCsvUrl_DecodesHtmlEntities()
    {
        var html =
            """<a href="https://x/y.ajax?fileType=csv&amp;fileName=f.csv&amp;dataType=fund">x</a>""";

        var url = ISharesHoldingsParser.ExtractAjaxCsvUrl(html);

        url.Should().Be("https://x/y.ajax?fileType=csv&fileName=f.csv&dataType=fund");
    }

    [Fact]
    public void ExtractAjaxCsvUrl_RelativeHref_LiveShape_ResolvesAgainstProductPageUrl()
    {
        // Live BOVA11 product page (2026-08-23): the ajax CSV link is a quoted
        // root-relative href; the hash rotates and must never be hardcoded.
        const string html =
            """<a href="/br/products/251816/ishares-ibovespa-fundo-de-ndice-fund/1506433276998.ajax?fileType=csv&fileName=BOVA11_holdings&dataType=fund">CSV</a>""";
        const string pageUrl =
            "https://www.blackrock.com/br/products/251816/ishares-ibovespa-fundo-de-ndice-fund";

        var url = ISharesHoldingsParser.ExtractAjaxCsvUrl(html, baseUrl: pageUrl);

        url.Should().NotBeNull();
        url.Should()
            .StartWith("https://www.blackrock.com/br/products/251816/")
            .And.Contain("1506433276998.ajax?fileType=csv")
            .And.NotContain("&amp;");
    }

    [Fact]
    public void ExtractAjaxCsvUrl_RelativeHref_WithoutBaseUrl_ReturnsRelativePath()
    {
        const string html =
            """<a href="/br/products/1/x.ajax?fileType=csv&amp;dataType=fund">CSV</a>""";

        var url = ISharesHoldingsParser.ExtractAjaxCsvUrl(html);

        url.Should().NotBeNull();
        url.Should().StartWith("/br/products/1/x.ajax?fileType=csv").And.NotContain("&amp;");
    }

    [Fact]
    public void ExtractAjaxCsvUrl_NoLink_ReturnsNull()
    {
        ISharesHoldingsParser
            .ExtractAjaxCsvUrl("<html><body>mudou</body></html>")
            .Should()
            .BeNull();
    }

    [Fact]
    public void ParseCsv_ToleratesPreamble_QuotedCommaDecimals_AndFooterRows()
    {
        var csv = File.ReadAllText(FixturePath("ishares_bova11_sample.csv"));

        var holdings = ISharesHoldingsParser.ParseCsv(csv);

        holdings.Should().HaveCount(5);
        var petr = holdings.First(h => h.Ticker == "PETR4");
        petr.WeightPercentage.Should().Be(8.51m); // "8,51" quoted comma decimal
        // pt-BR weights below 1.5 must NOT be read as fractions (live-feed regression:
        // RENT3 "1,32" is 1.32%, never 132%).
        holdings.First(h => h.Ticker == "RENT3").WeightPercentage.Should().Be(1.32m);
        holdings.First(h => h.Ticker == "WEGE3").WeightPercentage.Should().Be(13.45m);
        holdings.First(h => h.Ticker == "ITSA4").WeightPercentage.Should().Be(2.10m);
        holdings.Should().NotContain(h => h.Name == "Total"); // footer skipped
    }

    [Fact]
    public void ParseCsv_ChangedLayout_ThrowsLayoutException()
    {
        const string broken = "ColunaA,ColunaB\n1,2\n";

        var act = () => ISharesHoldingsParser.ParseCsv(broken);

        act.Should().Throw<HoldingsLayoutException>();
    }

    [Theory]
    [InlineData("8,51", 8.51)]
    [InlineData("8.51%", 8.51)]
    [InlineData("0.0851", 8.51)] // legacy dot-decimal fraction
    [InlineData("100", 100)]
    [InlineData("1,32", 1.32)] // pt-BR comma decimal: never a fraction
    [InlineData("0,98", 0.98)]
    [InlineData("1.32", 1.32)] // dot-only ≥... without leading "0." stays percent points
    public void TryParseWeight_HandlesFormats(string raw, decimal expected)
    {
        ISharesHoldingsParser.TryParseWeight(raw, out var weight).Should().BeTrue();
        weight.Should().Be(expected);
    }

    [Fact]
    public void TryParseWeight_Empty_ReturnsFalse()
    {
        ISharesHoldingsParser.TryParseWeight("", out _).Should().BeFalse();
        ISharesHoldingsParser.TryParseWeight(null, out _).Should().BeFalse();
        ISharesHoldingsParser.TryParseWeight("n/a", out _).Should().BeFalse();
    }

    // ---- SPDR ------------------------------------------------------------------

    private static MemoryStream BuildSpdrXlsx()
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Fund");

        // Metadata preamble like the real file.
        sheet.Cell(1, 1).Value = "SPDR Portfolio S&P 500 ETF";
        sheet.Cell(2, 1).Value = "Holdings Daily";
        sheet.Cell(3, 1).Value = "As of 08/21/2026";

        sheet.Cell(5, 1).Value = "Ticker";
        sheet.Cell(5, 2).Value = "Name";
        sheet.Cell(5, 3).Value = "Sector";
        sheet.Cell(5, 4).Value = "Weight";

        sheet.Cell(6, 1).Value = "AAPL";
        sheet.Cell(6, 2).Value = "Apple Inc.";
        sheet.Cell(6, 3).Value = "Information Technology";
        sheet.Cell(6, 4).Value = 7.12;

        sheet.Cell(7, 1).Value = "MSFT";
        sheet.Cell(7, 2).Value = "Microsoft Corp";
        sheet.Cell(7, 3).Value = "Information Technology";
        sheet.Cell(7, 4).Value = 5.9;

        sheet.Cell(8, 4).Value = "100"; // footer

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    [Fact]
    public void ParseXlsx_LocatesHeaderBelowPreamble_AndReadsRows()
    {
        using var stream = BuildSpdrXlsx();

        var holdings = SpdrHoldingsParser.ParseXlsx(stream);

        holdings.Should().HaveCount(2);
        holdings[0].Ticker.Should().Be("AAPL");
        holdings[0].Name.Should().Be("Apple Inc.");
        holdings[0].Sector.Should().Be("Information Technology");
        holdings[0].WeightPercentage.Should().Be(7.12m);
        holdings[1].WeightPercentage.Should().Be(5.9m);
    }

    [Fact]
    public void ParseXlsx_EmptyWorkbook_ThrowsLayoutException()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Empty");
        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        var act = () => SpdrHoldingsParser.ParseXlsx(stream);

        act.Should().Throw<HoldingsLayoutException>();
    }

    // ---- It Now / Investo HTML -------------------------------------------------

    [Fact]
    public void HtmlParse_ItNowFixture_ReadsTickersAndWeights()
    {
        var html = File.ReadAllText(FixturePath("itnow_composicao_sample.html"));

        var holdings = HtmlCompositionParser.Parse(html);

        holdings.Should().HaveCount(4); // Total row has no ticker cell -> skipped
        holdings.First(h => h.Ticker == "PETR4").WeightPercentage.Should().Be(8.51m);
        holdings.First(h => h.Ticker == "BBAS3").WeightPercentage.Should().Be(4.88m);
        holdings.Select(h => h.Country).Should().OnlyContain(c => c == "BRA");
    }

    [Fact]
    public void HtmlParse_InvestoFixture_MatchesRealLayout_NamesOnly_NoCountryTable()
    {
        // Live layout (checked 2026-08-23): "Ativo | Peso" with company names and no
        // ticker column, plus a separate "País" exposure table that must be skipped.
        var html = File.ReadAllText(FixturePath("investo_etf_sample.html"));

        var holdings = HtmlCompositionParser.Parse(html);

        holdings.Should().HaveCount(3); // country rows excluded
        holdings.First(h => h.Name == "Apple Inc.").WeightPercentage.Should().Be(3.82m);
        holdings.Should().OnlyContain(h => h.Ticker == null); // name-only source
    }

    [Fact]
    public void HtmlParse_TickerRows_StillResolveTickers()
    {
        const string html = """
            <table>
              <tr><td>PETR4</td><td>12.500</td><td>8,51 %</td></tr>
            </table>
            """;

        var holdings = HtmlCompositionParser.Parse(html);

        holdings.Should().HaveCount(1);
        holdings[0].Ticker.Should().Be("PETR4");
    }

    [Fact]
    public void HtmlParse_ZeroRows_MeansLayoutChanged()
    {
        var act = () => HtmlCompositionParser.Parse("<html><body>Sem tabela hoje</body></html>");

        act.Should().Throw<HoldingsLayoutException>();
    }

    // ---- It Now structured JSON API ---------------------------------------------

    [Fact]
    public void TryExtractFundCode_LivePageSnippet_IgnoresDistractorFundIsins()
    {
        // Live page (2026-08-23): the ticker selector lists other funds' ISINs as
        // codProduct comparisons; only the page's own API calls carry 'fundo=CODE'.
        var html = File.ReadAllText(FixturePath("itnow_composicao_live_sample.html"));

        var fundCode = ItNowJsonParser.TryExtractFundCode(html);

        fundCode.Should().Be("BRBOVVCTF009");
    }

    [Fact]
    public void TryExtractFundCode_NoCode_ReturnsNull()
    {
        ItNowJsonParser
            .TryExtractFundCode("<html><body>layout mudou</body></html>")
            .Should()
            .BeNull();
        ItNowJsonParser.TryExtractFundCode("").Should().BeNull();
    }

    [Fact]
    public void ParseJson_LivePayload_ReadsTickersWeightsAndNames()
    {
        var json = File.ReadAllText(FixturePath("itnow_bovv11_api.json"));

        var holdings = ItNowJsonParser.ParseJson(json);

        holdings.Should().HaveCount(78);
        var vale = holdings.First(h => h.Ticker == "VALE3");
        vale.WeightPercentage.Should().Be(11.2377m);
        vale.Name.Should().Be("VALE");
        vale.Country.Should().Be("BRA");
        holdings.Select(h => h.Ticker).Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t));
    }

    [Fact]
    public void ParseJson_LivePayload_ExposesAsOfDate()
    {
        var json = File.ReadAllText(FixturePath("itnow_bovv11_api.json"));

        ItNowJsonParser.TryGetAsOfDate(json).Should().Be(new DateOnly(2026, 8, 21));
    }

    [Fact]
    public void ParseJson_ChangedContract_ThrowsLayoutException()
    {
        const string broken = """{"sucesso":true,"mensagem_erro":null}""";
        var notJson = "<html>Access Denied</html>";

        var actBroken = () => ItNowJsonParser.ParseJson(broken);
        var actNotJson = () => ItNowJsonParser.ParseJson(notJson);

        actBroken.Should().Throw<HoldingsLayoutException>();
        actNotJson.Should().Throw<HoldingsLayoutException>();
    }

    [Fact]
    public void ParseJson_SkipsRowsWithoutTickerOrWeight()
    {
        const string json = """
            {"sucesso":true,"dados":[
              {"nome_ticker_fundo":"PETR4","descricao_ticker_fundo":"PETROBRAS","porcentagem_participacao_fundo":7.5,"data_hora_posicao_carteira_fundo":"2026-08-21T00:00:00"},
              {"nome_ticker_fundo":null,"descricao_ticker_fundo":"RESERVA","porcentagem_participacao_fundo":0.5},
              {"nome_ticker_fundo":"BBDC4","descricao_ticker_fundo":null,"porcentagem_participacao_fundo":0}
            ]}
            """;

        var holdings = ItNowJsonParser.ParseJson(json);

        holdings.Should().ContainSingle(h => h.Ticker == "PETR4");
        holdings.Single().Name.Should().Be("PETROBRAS");
        ItNowJsonParser.TryGetAsOfDate(json).Should().Be(new DateOnly(2026, 8, 21));
    }
}
