using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace NotificationService.Services;

public sealed class EmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendOrderConfirmationAsync(string to, string orderId, string productName, int quantity, decimal totalPrice)
    {
        var subject = $"Order #{orderId[..8]} Confirmed - StockFlow";
        var body = $"""
            Hi,

            Your order has been confirmed!

            Order ID: {orderId}
            Product: {productName}
            Quantity: {quantity}
            Total: {totalPrice:F2} USD

            Thank you for shopping with StockFlow!
            """;

        await SendEmailAsync(to, subject, body);
    }

    private async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            var smtpHost = _config["Email:SmtpHost"] ?? "localhost";
            var smtpPort = int.Parse(_config["Email:SmtpPort"] ?? "1025");
            var from = _config["Email:From"] ?? "noreply@stockflow.com";

            using var message = new MimeMessage();
            message.From.Add(new MailboxAddress("StockFlow", from));
            message.To.Add(new MailboxAddress("", to));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();
            await client.ConnectAsync(smtpHost, smtpPort, SecureSocketOptions.None);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            _logger.LogInformation("Email sent to {To}: {Subject}", to, subject);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send email to {To}. Mailhog may not be running.", to);
        }
    }
}
