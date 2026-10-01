namespace DepinTracker.Tests.Infrastructure;

using System.Net;
using System.Text;
using DepinTracker.Application.Abstractions.Persistence;
using DepinTracker.Infrastructure.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

public class EtherscanV2EvmExplorerTests
{
    private const string EvmAddress = "0x1111111111111111111111111111111111111111";

    [Fact]
    public async Task Api_error_fails_the_fetch_with_the_result_detail()
    {
        var (explorer, handler) = Create("""{"status":"0","message":"NOTOK","result":"Error! Invalid API Key"}""");

        var act = () => explorer.FetchRewardsAsync("polygon", EvmAddress, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Invalid API Key*");
        handler.Requests.Should().Be(1);
    }

    [Fact]
    public async Task No_transactions_found_returns_an_empty_result()
    {
        var (explorer, _) = Create("""{"status":"0","message":"No transactions found","result":[]}""");

        var result = await explorer.FetchRewardsAsync("polygon", EvmAddress, null, CancellationToken.None);

        result.Rewards.Should().BeEmpty();
    }

    [Fact]
    public async Task Http_error_fails_the_fetch()
    {
        var (explorer, _) = Create("oops", HttpStatusCode.BadGateway);

        var act = () => explorer.FetchRewardsAsync("polygon", EvmAddress, null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*HTTP 502*");
    }

    [Fact]
    public async Task Solana_address_is_rejected_before_any_request()
    {
        var (explorer, handler) = Create("{}");

        var act = () => explorer.FetchRewardsAsync(
            "polygon", "CWv4V2RcmRemTVxdSd9oyTKBnXgRPzU9hLujw5a8B6H2", null, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*looks like a Solana address*");
        handler.Requests.Should().Be(0);
    }

    private static (EtherscanV2EvmExplorer Explorer, StubHandler Handler) Create(
        string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler(body, status);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(() => new HttpClient(handler, disposeHandler: false));
        var settings = new Mock<IUserSettingsStore>();
        settings.Setup(s => s.GetAsync(EtherscanV2EvmExplorer.ApiKeySettingName, It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-key");

        var explorer = new EtherscanV2EvmExplorer(
            factory.Object, Options.Create(new ProvidersOptions()), settings.Object,
            NullLogger<EtherscanV2EvmExplorer>.Instance);
        return (explorer, handler);
    }

    private sealed class StubHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        public int Requests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }
}
