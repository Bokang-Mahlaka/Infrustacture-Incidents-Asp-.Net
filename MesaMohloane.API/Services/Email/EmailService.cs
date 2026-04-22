using MailKit.Net.Smtp;
using MimeKit;

namespace MesaMohloane.API.Services.Email
{
    public interface IEmailService
    {
        Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody);
        Task SendAssignmentNotificationAsync(string contractorEmail, string contractorName, string incidentTitle);
        Task SendCompletionNotificationAsync(string citizenEmail, string citizenName, string incidentTitle);
        Task SendPaymentNotificationAsync(string contractorEmail, string contractorName, string incidentTitle, decimal amount);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SendEmailAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            try
            {
                var emailSettings = _configuration.GetSection("EmailSettings");

                var message = new MimeMessage();
                message.From.Add(new MailboxAddress(
                    emailSettings["SenderName"] ?? "Mesa-Mohloane System",
                    emailSettings["SenderEmail"] ?? "noreply@mesa-mohloane.co.ls"));
                message.To.Add(new MailboxAddress(toName, toEmail));
                message.Subject = subject;

                var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
                message.Body = bodyBuilder.ToMessageBody();

                using var client = new SmtpClient();
                var useSsl = bool.Parse(emailSettings["UseSsl"] ?? "false");
                var port = int.Parse(emailSettings["SmtpPort"] ?? "25");

                await client.ConnectAsync(
                    emailSettings["SmtpServer"] ?? "localhost",
                    port,
                    useSsl ? MailKit.Security.SecureSocketOptions.SslOnConnect : MailKit.Security.SecureSocketOptions.None);

                var username = emailSettings["Username"];
                if (!string.IsNullOrEmpty(username))
                {
                    await client.AuthenticateAsync(username, emailSettings["Password"]);
                }

                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                _logger.LogInformation("Email sent to {Email} — Subject: {Subject}", toEmail, subject);
            }
            catch (Exception ex)
            {
                // Log but don't throw — email failure should not break the workflow
                _logger.LogWarning(ex, "Failed to send email to {Email}. Subject: {Subject}", toEmail, subject);
            }
        }

        public async Task SendAssignmentNotificationAsync(string contractorEmail, string contractorName, string incidentTitle)
        {
            var subject = $"Mesa-Mohloane: You have been assigned to \"{incidentTitle}\"";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #2563eb;'>🔧 Job Assignment Notification</h2>
                    <p>Dear <strong>{contractorName}</strong>,</p>
                    <p>Congratulations! Your proposal has been accepted and you have been assigned to the following incident:</p>
                    <div style='background: #f0f9ff; border-left: 4px solid #2563eb; padding: 16px; margin: 16px 0;'>
                        <strong>{incidentTitle}</strong>
                    </div>
                    <p>Please begin work as outlined in your proposal. You can update your progress through the Mesa-Mohloane system.</p>
                    <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 24px 0;' />
                    <p style='color: #6b7280; font-size: 12px;'>Mesa-Mohloane Infrastructure Management System — Kingdom of Lesotho</p>
                </div>";

            await SendEmailAsync(contractorEmail, contractorName, subject, body);
        }

        public async Task SendCompletionNotificationAsync(string citizenEmail, string citizenName, string incidentTitle)
        {
            var subject = $"Mesa-Mohloane: Work completed on \"{incidentTitle}\"";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #16a34a;'>✅ Work Completion Notification</h2>
                    <p>Dear <strong>{citizenName}</strong>,</p>
                    <p>The contractor has marked the following incident as completed:</p>
                    <div style='background: #f0fdf4; border-left: 4px solid #16a34a; padding: 16px; margin: 16px 0;'>
                        <strong>{incidentTitle}</strong>
                    </div>
                    <p>Please log into the system to review and acknowledge the completed work. You will also be able to rate the contractor's performance.</p>
                    <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 24px 0;' />
                    <p style='color: #6b7280; font-size: 12px;'>Mesa-Mohloane Infrastructure Management System — Kingdom of Lesotho</p>
                </div>";

            await SendEmailAsync(citizenEmail, citizenName, subject, body);
        }

        public async Task SendPaymentNotificationAsync(string contractorEmail, string contractorName, string incidentTitle, decimal amount)
        {
            var subject = $"Mesa-Mohloane: Payment of M{amount:N2} has been disbursed";
            var body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #9333ea;'>💰 Payment Disbursement Notification</h2>
                    <p>Dear <strong>{contractorName}</strong>,</p>
                    <p>Payment has been disbursed for the following incident:</p>
                    <div style='background: #faf5ff; border-left: 4px solid #9333ea; padding: 16px; margin: 16px 0;'>
                        <strong>{incidentTitle}</strong><br/>
                        <span style='font-size: 20px; font-weight: bold; color: #9333ea;'>M{amount:N2}</span>
                    </div>
                    <p>Thank you for your service to the people of Lesotho.</p>
                    <hr style='border: none; border-top: 1px solid #e5e7eb; margin: 24px 0;' />
                    <p style='color: #6b7280; font-size: 12px;'>Mesa-Mohloane Infrastructure Management System — Kingdom of Lesotho</p>
                </div>";

            await SendEmailAsync(contractorEmail, contractorName, subject, body);
        }
    }
}
