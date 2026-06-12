using Amazon.SQS;
using Amazon.SQS.Model;

namespace EntryService.Infrastructure.Messaging;

public interface ISqsPublisher
{
    Task PublishAsync(string messageBody, CancellationToken ct = default);
}

public sealed class SqsPublisher(IAmazonSQS sqsClient, IConfiguration config, ILogger<SqsPublisher> logger)
    : ISqsPublisher
{
    private readonly string _queueUrl = config["Sqs:QueueUrl"]
        ?? throw new InvalidOperationException("Sqs:QueueUrl não configurado.");

    public async Task PublishAsync(string messageBody, CancellationToken ct = default)
    {
        var request = new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = messageBody
        };

        var response = await sqsClient.SendMessageAsync(request, ct);
        logger.LogDebug("Mensagem publicada no SQS. MessageId={MessageId}", response.MessageId);
    }
}
