using Google.Cloud.PubSub.V1;
using Grpc.Core;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace ThumbnailService.Services
{
    public class PubSubService
    {
        private readonly PublisherServiceApiClient _publisherClient;
        private readonly string _topicName;

        public PubSubService(string topicName)
        {
            _publisherClient = PublisherServiceApiClient.Create();
            _topicName = topicName;
        }

        public async Task PublishAsync<T>(T message)
        {
            var topic = TopicName.Parse(_topicName, allowUnparsed: true);
            string json = JsonSerializer.Serialize(message);
            var pubsubMessage = new PubsubMessage { Data = Google.Protobuf.ByteString.CopyFromUtf8(json) };
            try
            {
                await _publisherClient.PublishAsync(topic, new[] { pubsubMessage });
            }
            catch (RpcException ex)
            {
                // Log or handle error
                throw new Exception($"Failed to publish Pub/Sub message: {ex.Message}", ex);
            }
        }
    }

    public class ThumbnailJob
    {
        public Guid ImageId { get; set; }
        public Guid UserId { get; set; }
        public string GcsPath { get; set; } = string.Empty;
    }
}
