using System;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Company.Function
{
    public class ServiceBusQueueTrigger1
    {
        private readonly ILogger<ServiceBusQueueTrigger1> _logger;
        private readonly ServiceBusClient _serviceBusClient;

        public ServiceBusQueueTrigger1(ILogger<ServiceBusQueueTrigger1> logger, ServiceBusClient serviceBusClient)
        {
            _logger = logger;
            _serviceBusClient = serviceBusClient;
        }

        [Function(nameof(ServiceBusQueueTrigger1))]
        public async Task Run(
            [ServiceBusTrigger("my-test", Connection = "redccansbnamespace_SERVICEBUS")]
            ServiceBusReceivedMessage message,
            ServiceBusMessageActions messageActions)
        {
            try 
            {
                string messageBody = message.Body.ToString();
                _logger.LogError("***Error***Message Body: {MessageBody}", messageBody);
                _logger.LogWarning("***Warning***Message Body: {MessageBody}", messageBody);
                _logger.LogInformation("***Info***Message Body: {MessageBody}", messageBody);

                _logger.LogInformation("Received message: {MessageId},\n Body: {MessageBody}, \n Content-Type: {ContentType}", message.MessageId, messageBody, message.ContentType);

                // Process the message (your logic here)
                
                // Calculate the duration
                var enqueuedTime = message.EnqueuedTime.UtcDateTime;
                var completedTime = DateTime.UtcNow;
                var duration = completedTime - enqueuedTime;

                // Log the duration
                _logger.LogInformation("Message {MessageId} processed. Enqueued Time: {EnqueuedTime}, Completed Time: {CompletedTime}, Duration: {Duration}",
                    message.MessageId, enqueuedTime.ToString("o"), completedTime.ToString("o"), duration);

                // Complete the message
                await messageActions.CompleteMessageAsync(message);
            }
            catch (Exception ex)
            {
                // Log the exception
                _logger.LogError(ex, "Error processing message {MessageId}", message.MessageId);
                await messageActions.AbandonMessageAsync(message); // Puts message back in queue for retry
            }
        }
    }
}
