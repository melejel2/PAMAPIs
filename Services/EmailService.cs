using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.IO;

namespace PAMAPIs.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;
        private readonly SmtpClient _smtpClient;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
            _smtpClient = new SmtpClient(_configuration["EmailSettings:SMTPServer"])
            {
                Port = int.Parse(_configuration["EmailSettings:SMTPPort"]),
                Credentials = new NetworkCredential(_configuration["EmailSettings:Username"], _configuration["EmailSettings:Password"]),
                EnableSsl = true
            };
        }

        public async Task SendEmailAsync(string[] toEmail, string subject, string body, string[] ccEmails = null)
        {
            await SendEmailAsync(toEmail, subject, body, null, null, ccEmails);
        }

        public async Task SendEmailAsync(string[] toEmail, string subject, string body, byte[] attachment, string attachmentName, string[] ccEmails = null)
        {
            var mailMessage = new MailMessage
            {
                From = new MailAddress(_configuration["EmailSettings:SenderEmail"], _configuration["EmailSettings:SenderName"]),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            if (toEmail == null || toEmail.Length == 0)
            {
                throw new ArgumentException("No recipient specified");
            }
            if (toEmail != null)
            {
                foreach (var to in toEmail)
                {
                    if (!string.IsNullOrEmpty(to))
                    {
                        mailMessage.To.Add(to);
                    }
                }
            }
           
            // Adding CC recipients
            if (ccEmails != null)
            {
                foreach (var cc in ccEmails)
                {
                    if (!string.IsNullOrEmpty(cc))
                    {
                        mailMessage.CC.Add(cc);
                    }
                }
            }

            if (attachment != null)
            {
                var memoryStream = new MemoryStream(attachment);
                mailMessage.Attachments.Add(new Attachment(memoryStream, attachmentName));
            }

            try
            {
                await _smtpClient.SendMailAsync(mailMessage);
            }
            catch (Exception ex)
            {
                // Log the exception (you can use your logging framework of choice)
                throw new InvalidOperationException("Failed to send email", ex);
            }
        }
    }
}
