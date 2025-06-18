using RabbitMQ.Client;
using System;

public static class RabbitMqHealthCheck
{
    public static bool IsRabbitAlive(string host, int port, string username, string password)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = host,
                Port = port,
                UserName = username,
                Password = password,
                RequestedConnectionTimeout = TimeSpan.FromMilliseconds(500)
            };

            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            return true; // אם לא נזרקה שגיאה – RabbitMQ חי
        }
        catch
        {
            return false;
        }
    }
}
