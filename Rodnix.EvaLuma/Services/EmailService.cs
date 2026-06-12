using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;

namespace Rodnix.EvaLuma.Services
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string bodyHtml);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration config, ILogger<EmailService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendEmailAsync(string to, string subject, string bodyHtml)
        {
            try
            {
                var host = _config["SmtpSettings:Server"];
                var portStr = _config["SmtpSettings:Port"];
                var user = _config["SmtpSettings:Username"];
                var pass = _config["SmtpSettings:Password"];

                if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass) || !int.TryParse(portStr, out int port))
                {
                    _logger.LogWarning("Configuración SMTP incompleta. No se enviará el correo a {To}.", to);
                    return;
                }

                using var client = new SmtpClient(host, port)
                {
                    Credentials = new NetworkCredential(user, pass),
                    EnableSsl = true
                };

                using var message = new MailMessage
                {
                    From = new MailAddress(user, "Portal EVALUMA"),
                    Subject = subject,
                    Body = bodyHtml,
                    IsBodyHtml = true
                };
                
                message.To.Add(to);

                await client.SendMailAsync(message);
                _logger.LogInformation("Correo enviado exitosamente a {To}", to);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo a {To}", to);
            }
        }
    }
}
