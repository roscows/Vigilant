using MediatR;
using Vigilant.Application.Common.Graph;

namespace Vigilant.Application.Transactions.ProcessTransaction;

public sealed record TransactionProcessorCommand(
    string SenderIban,
    string ReceiverIban,
    decimal Amount,
    string Currency,
    string DeviceId,
    string IpAddress,
    string? IpCountryCode = null,
    string? BrowserFingerprint = null,
    ClientGraphSnapshot? SenderClient = null,
    ClientGraphSnapshot? ReceiverClient = null) : IRequest<TransactionProcessorResult>;

public sealed record TransactionProcessorResult(
    string TransactionId,
    string SenderIban,
    string ReceiverIban,
    decimal Amount,
    string Currency,
    DateTimeOffset ProcessedAtUtc);
