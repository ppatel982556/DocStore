using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;
using Repositories.Models;
using Repositories.Services.EmailService;

namespace Services.EmailService;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;

    public EmailService(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public async Task SendResetPasswordEmail(string email, string resetLink)
    {
        var message = new MimeMessage();

        message.From.Add(
            new MailboxAddress(
                _settings.DisplayName,
                _settings.Email));

        message.To.Add(
            MailboxAddress.Parse(email));

        message.Subject = "Reset Your DocStore Password";

        message.Body = new BodyBuilder
        {
            HtmlBody = $@"
                <div style='font-family:Segoe UI,sans-serif;font-size:15px;color:#333;'>

                    <h2>Reset Password</h2>

                    <p>Hello,</p>

                    <p>
                        We received a request to reset your DocStore password.
                    </p>

                    <p>
                        Click the button below to reset your password.
                    </p>

                    <p style='margin:30px 0'>
                        <a href='{resetLink}'
                           style='
                               background:#4f46e5;
                               color:white;
                               padding:12px 24px;
                               text-decoration:none;
                               border-radius:8px;
                               display:inline-block;'>
                            Reset Password
                        </a>
                    </p>

                    <p>
                        This link will expire in <b>30 minutes</b>.
                    </p>

                    <p>
                        If you didn't request this, you can safely ignore this email.
                    </p>

                    <br/>

                    <p>
                        Regards,<br/>
                        <b>DocStore Team</b>
                    </p>

                </div>"
        }.ToMessageBody();

        using var smtp = new SmtpClient();

        await smtp.ConnectAsync(
            _settings.Host,
            _settings.Port,
            SecureSocketOptions.StartTls);

        await smtp.AuthenticateAsync(
            _settings.Email,
            _settings.Password);

        await smtp.SendAsync(message);

        await smtp.DisconnectAsync(true);
    }
}   