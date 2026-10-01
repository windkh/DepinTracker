namespace DepinTracker.Tests.Domain;

using DepinTracker.Domain.Enums;
using DepinTracker.Domain.ValueObjects;
using FluentAssertions;

public class WalletAddressFormatTests
{
    private const string Evm = "0xAbCdEf0123456789abcdef0123456789ABCDEF01";
    private const string Solana = "CWv4V2RcmRemTVxdSd9oyTKBnXgRPzU9hLujw5a8B6H2";

    [Theory]
    [InlineData(ChainType.Evm, Evm)]
    [InlineData(ChainType.Evm, "  " + Evm + "  ")]
    [InlineData(ChainType.Solana, Solana)]
    [InlineData(ChainType.Cosmos, "cosmos1anything")]
    public void Matching_address_passes(ChainType chainType, string address) =>
        WalletAddressFormat.Validate(chainType, address).Should().BeNull();

    [Fact]
    public void Solana_address_on_evm_chain_names_the_mixup() =>
        WalletAddressFormat.Validate(ChainType.Evm, Solana).Should().Contain("looks like a Solana address");

    [Fact]
    public void Evm_address_on_solana_chain_names_the_mixup() =>
        WalletAddressFormat.Validate(ChainType.Solana, Evm).Should().Contain("looks like an EVM address");

    [Theory]
    [InlineData("0x123")]
    [InlineData("0xZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ")]
    [InlineData("not an address")]
    public void Malformed_evm_address_is_rejected(string address) =>
        WalletAddressFormat.Validate(ChainType.Evm, address).Should().Contain("not a valid EVM address");
}
