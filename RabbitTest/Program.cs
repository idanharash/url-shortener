using RabbitMQ.Client;
using System.Text;

var factory = new ConnectionFactory()
{
    HostName = "localhost",
    UserName = "guest",
    Password = "guest"
};

using var connection = factory.CreateConnection();
using var channel = connection.CreateModel();

channel.QueueDeclare(queue: "test-queue", durable: false, exclusive: false, autoDelete: false);

var body = Encoding.UTF8.GetBytes("Hello!");
channel.BasicPublish(exchange: "", routingKey: "test-queue", body: body);

Console.WriteLine("Message sent.");
