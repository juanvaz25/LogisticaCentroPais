using System.Net;
using System.Net.Mail;

namespace LCP.Api.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
    {
        try
        {
            var server = _config["SmtpSettings:Server"] ?? "smtp.gmail.com";
            var port = int.TryParse(_config["SmtpSettings:Port"], out var p) ? p : 587;
            var senderEmail = _config["SmtpSettings:SenderEmail"] ?? "jjvazquezin@gmail.com";
            var senderName = _config["SmtpSettings:SenderName"] ?? "LCP · Logística Centro País";
            var rawPass = _config["SmtpSettings:Password"] ?? "iltzlbqsukyvtnrd";
            var password = rawPass.Replace(" ", ""); // Quitar espacios de contraseña de aplicación Google
            var enableSsl = bool.TryParse(_config["SmtpSettings:EnableSsl"], out var ssl) ? ssl : true;

            using var client = new SmtpClient(server, port)
            {
                Credentials = new NetworkCredential(senderEmail, password),
                EnableSsl = enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Timeout = 15000
            };

            using var mail = new MailMessage
            {
                From = new MailAddress(senderEmail, senderName),
                Subject = subject,
                Body = htmlBody,
                IsBodyHtml = true
            };

            mail.To.Add(toEmail);

            await client.SendMailAsync(mail);
            _logger.LogInformation("Correo enviado exitosamente a {ToEmail} con asunto: {Subject}", toEmail, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al enviar correo SMTP a {ToEmail} con asunto {Subject}", toEmail, subject);
            return false;
        }
    }

    public async Task<bool> SendCredencialesAutoGeneradasAsync(string toEmail, string nombreDestinatario, string passwordGenerada, string rol)
    {
        var webUrl = _config["SmtpSettings:WebBaseUrl"] ?? "http://localhost:5147";
        var loginUrl = $"{webUrl.TrimEnd('/')}/Auth/Login";
        var html = GenerarTemplateHtmlCredenciales(nombreDestinatario, toEmail, passwordGenerada, rol, loginUrl);
        var subject = "LCP · Credenciales de Acceso a la Plataforma (Santa Rosa, La Pampa)";

        return await SendEmailAsync(toEmail, subject, html);
    }

    public async Task<bool> SendNotificacionAltaEmpresaAsync(string toEmail, string razonSocial)
    {
        var webUrl = _config["SmtpSettings:WebBaseUrl"] ?? "http://localhost:5147";
        var html = GenerarTemplateHtmlNotificacionGenerica(
            razonSocial,
            "Solicitud de Alta Recibida",
            $"Hemos recibido correctamente su solicitud de alta de cuenta para la empresa <strong>{razonSocial}</strong>. Sus datos han sido registrados en nuestro sistema.",
            "Deberá esperar a que un administrador valide y habilite su cuenta. Una vez aprobada, recibirá un nuevo correo electrónico con su usuario y una contraseña generada automáticamente para su primer ingreso, momento en el cual se le requerirá establecer su contraseña definitiva.",
            webUrl,
            "Ir a la Plataforma LCP"
        );

        return await SendEmailAsync(toEmail, "LCP · Solicitud de Alta de Empresa Registrada", html);
    }

    public async Task<bool> SendConfirmacionEmailRegistroAsync(string toEmail, string razonSocial, string confirmUrl)
    {
        var html = GenerarTemplateHtmlConfirmacionEmail(razonSocial, toEmail, confirmUrl);
        var subject = "LCP · Verificación requerida: Confirme su Correo Electrónico";

        return await SendEmailAsync(toEmail, subject, html);
    }

    public async Task<bool> SendConfirmacionEmailChoferAsync(string toEmail, string nombreCompleto, string confirmUrl)
    {
        var html = GenerarTemplateHtmlConfirmacionEmailChofer(nombreCompleto, toEmail, confirmUrl);
        var subject = "LCP · Verificación de Correo: Postulación de Conductor";

        return await SendEmailAsync(toEmail, subject, html);
    }

    public async Task<bool> SendConfirmacionPersonalAsync(string toEmail, string nombreCompleto, string puesto, string rol, string passwordProvisoria, string confirmUrl)
    {
        var html = GenerarTemplateHtmlConfirmacionPersonal(nombreCompleto, toEmail, puesto, rol, passwordProvisoria, confirmUrl);
        var subject = "LCP · Verificación de Correo y Credenciales: Alta de Personal Interno";

        return await SendEmailAsync(toEmail, subject, html);
    }

    public async Task<bool> SendAprobacionEmpresaAsync(string toEmail, string razonSocial)
    {
        var webUrl = _config["SmtpSettings:WebBaseUrl"] ?? "http://localhost:5147";
        var loginUrl = $"{webUrl.TrimEnd('/')}/Auth/Login";
        var html = GenerarTemplateHtmlNotificacionGenerica(
            razonSocial,
            "¡Empresa Aprobada y Habilitada!",
            $"Nos complace informarle que la empresa <strong>{razonSocial}</strong> ha sido verificada y aprobada por la administración de Logística Centro País.",
            "Ya puede ingresar a la plataforma con su correo electrónico y la contraseña elegida en el registro para comenzar a operar dársenas, gestionar inventario y coordinar arribos.",
            loginUrl,
            "Ingresar a la Plataforma ➔"
        );

        return await SendEmailAsync(toEmail, "LCP · Cuenta de Empresa Habilitada", html);
    }

    private string GenerarTemplateHtmlCredenciales(string nombre, string email, string password, string rol, string loginUrl)
    {
        return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Credenciales de Acceso - Logística Centro País</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #07090C; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #FFFFFF; -webkit-font-smoothing: antialiased;"">
    <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #07090C; padding: 36px 16px;"">
        <tr>
            <td align=""center"">
                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""max-width: 600px; background-color: #0E1217; border: 1px solid #1E2732; border-radius: 8px; overflow: hidden; box-shadow: 0 12px 36px rgba(0,0,0,0.7);"">
                    
                    <!-- Top Technical Brand Bar -->
                    <tr>
                        <td style=""padding: 24px 30px; background-color: #07090C; border-bottom: 1px solid #1E2732;"">
                            <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""left"">
                                        <span style=""background: #FFFFFF; color: #07090C; font-weight: 900; font-size: 13px; padding: 4px 10px; border-radius: 3px; letter-spacing: 1.5px; display: inline-block;"">LCP</span>
                                        <span style=""font-weight: 800; font-size: 15px; letter-spacing: -0.3px; color: #FFFFFF; margin-left: 10px; vertical-align: middle;"">Logística Centro País</span>
                                    </td>
                                    <td align=""right"" style=""font-family: 'Courier New', Courier, monospace; font-size: 11px; color: #64748B; letter-spacing: 0.5px;"">
                                        36°37′S 64°17′W · HUB CENTRAL
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Body Content -->
                    <tr>
                        <td style=""padding: 36px 32px 28px;"">
                            <div style=""font-family: 'Courier New', Courier, monospace; font-size: 11px; color: #94A3B8; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 8px;"">
                                ACCESO A LA INFRAESTRUCTURA TELEMÁTICA
                            </div>
                            <h1 style=""margin: 0 0 16px; font-size: 24px; font-weight: 800; color: #FFFFFF; letter-spacing: -0.5px; line-height: 1.25;"">
                                Credenciales de Acceso a la Plataforma
                            </h1>
                            <p style=""margin: 0 0 24px; font-size: 14.5px; line-height: 1.6; color: #CBD5E1;"">
                                Estimado/a <strong>{WebUtility.HtmlEncode(nombre)}</strong>, se han generado sus credenciales de acceso operativo para la gestión logística en el nodo de <strong>Santa Rosa, La Pampa</strong>.
                            </p>

                            <!-- Credentials Card -->
                            <div style=""background-color: #141A22; border: 1px solid #283544; border-radius: 6px; padding: 22px 24px; margin-bottom: 24px;"">
                                <div style=""font-family: 'Courier New', Courier, monospace; font-size: 11px; color: #94A3B8; letter-spacing: 0.5px; text-transform: uppercase; margin-bottom: 14px; border-bottom: 1px solid #1E2732; padding-bottom: 8px;"">
                                    DATOS DE INICIO DE SESIÓN
                                </div>
                                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                                    <tr>
                                        <td style=""padding-bottom: 12px; font-size: 13px; color: #94A3B8; width: 140px;"">Correo / Usuario:</td>
                                        <td style=""padding-bottom: 12px; font-size: 14px; color: #FFFFFF; font-weight: 700; font-family: 'Courier New', Courier, monospace;"">{WebUtility.HtmlEncode(email)}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding-bottom: 12px; font-size: 13px; color: #94A3B8;"">Rol Asignado:</td>
                                        <td style=""padding-bottom: 12px; font-size: 13px; color: #93C5FD; font-weight: 700;"">{WebUtility.HtmlEncode(rol)}</td>
                                    </tr>
                                    <tr>
                                        <td style=""font-size: 13px; color: #94A3B8; vertical-align: middle;"">Contraseña Provisoria:</td>
                                        <td style=""vertical-align: middle;"">
                                            <span style=""display: inline-block; background-color: #07090C; border: 1px solid #FFFFFF; color: #FFFFFF; font-family: 'Courier New', Courier, monospace; font-size: 16px; font-weight: 800; padding: 6px 14px; border-radius: 4px; letter-spacing: 1.5px;"">
                                                {WebUtility.HtmlEncode(password)}
                                            </span>
                                        </td>
                                    </tr>
                                </table>
                            </div>

                            <!-- Security Warning Box -->
                            <div style=""background-color: #1A1813; border-left: 4px solid #F59E0B; border-radius: 0 6px 6px 0; padding: 16px 20px; margin-bottom: 28px;"">
                                <div style=""color: #FBBF24; font-weight: 800; font-size: 13px; margin-bottom: 5px; letter-spacing: 0.3px;"">
                                    ⚠️ CAMBIO OBLIGATORIO DE CONTRASEÑA EN PRIMER INGRESO
                                </div>
                                <div style=""color: #D1D5DB; font-size: 13px; line-height: 1.5;"">
                                    Por políticas de seguridad operacional del predio, esta contraseña es estrictamente provisoria. Al iniciar sesión por primera vez, el sistema <strong>le exigirá establecer una contraseña definitiva y privada</strong> antes de habilitar el acceso a los módulos operativos.
                                </div>
                            </div>

                            <!-- Action Button -->
                            <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""center"" style=""padding-bottom: 10px;"">
                                        <a href=""{loginUrl}"" target=""_blank"" style=""display: inline-block; background-color: #FFFFFF; color: #07090C; font-weight: 800; font-size: 14.5px; padding: 13px 34px; border-radius: 4px; text-decoration: none; letter-spacing: -0.2px; box-shadow: 0 4px 14px rgba(255,255,255,0.25);"">
                                            Ingresar al Sistema ➔
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Minimalist Technical Footer -->
                    <tr>
                        <td style=""padding: 22px 28px; background-color: #07090C; border-top: 1px solid #1E2732; text-align: center;"">
                            <div style=""font-size: 12px; color: #64748B; font-weight: 600; margin-bottom: 4px;"">
                                LCP · Logística Centro País — Santa Rosa, La Pampa
                            </div>
                            <div style=""font-size: 11px; color: #475569; font-family: 'Courier New', Courier, monospace;"">
                                RN 5 & RN 35 · Parque Industrial · Argentina · Transmisión Telemática Segura
                            </div>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string GenerarTemplateHtmlNotificacionGenerica(string destinatario, string titulo, string parrafo1, string parrafo2, string urlAccion, string textoBoton = "Ver Detalles en la Plataforma ➔")
    {
        return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{titulo} - Logística Centro País</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #07090C; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #FFFFFF; -webkit-font-smoothing: antialiased;"">
    <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #07090C; padding: 36px 16px;"">
        <tr>
            <td align=""center"">
                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""max-width: 600px; background-color: #0E1217; border: 1px solid #1E2732; border-radius: 8px; overflow: hidden; box-shadow: 0 12px 36px rgba(0,0,0,0.7);"">
                    
                    <tr>
                        <td style=""padding: 24px 30px; background-color: #07090C; border-bottom: 1px solid #1E2732;"">
                            <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""left"">
                                        <span style=""background: #FFFFFF; color: #07090C; font-weight: 900; font-size: 13px; padding: 4px 10px; border-radius: 3px; letter-spacing: 1.5px; display: inline-block;"">LCP</span>
                                        <span style=""font-weight: 800; font-size: 15px; letter-spacing: -0.3px; color: #FFFFFF; margin-left: 10px; vertical-align: middle;"">Logística Centro País</span>
                                    </td>
                                    <td align=""right"" style=""font-family: 'Courier New', Courier, monospace; font-size: 11px; color: #64748B; letter-spacing: 0.5px;"">
                                        36°37′S 64°17′W · HUB CENTRAL
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <tr>
                        <td style=""padding: 36px 32px 28px;"">
                            <div style=""font-family: 'Courier New', Courier, monospace; font-size: 11px; color: #94A3B8; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 8px;"">
                                NOTIFICACIÓN DE PLATAFORMA
                            </div>
                            <h1 style=""margin: 0 0 16px; font-size: 24px; font-weight: 800; color: #FFFFFF; letter-spacing: -0.5px; line-height: 1.25;"">
                                {titulo}
                            </h1>
                            <p style=""margin: 0 0 16px; font-size: 14.5px; line-height: 1.6; color: #CBD5E1;"">
                                Estimado/a <strong>{WebUtility.HtmlEncode(destinatario)}</strong>,
                            </p>
                            <p style=""margin: 0 0 16px; font-size: 14px; line-height: 1.6; color: #CBD5E1;"">
                                {parrafo1}
                            </p>
                            <p style=""margin: 0 0 28px; font-size: 14px; line-height: 1.6; color: #CBD5E1;"">
                                {parrafo2}
                            </p>

                            <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""center"" style=""padding-bottom: 10px;"">
                                        <a href=""{urlAccion}"" target=""_blank"" style=""display: inline-block; background-color: #FFFFFF; color: #07090C; font-weight: 800; font-size: 14px; padding: 12px 30px; border-radius: 4px; text-decoration: none; letter-spacing: -0.2px; box-shadow: 0 4px 14px rgba(255,255,255,0.2);"">
                                            {textoBoton}
                                        </a>
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <tr>
                        <td style=""padding: 22px 28px; background-color: #07090C; border-top: 1px solid #1E2732; text-align: center;"">
                            <div style=""font-size: 12px; color: #64748B; font-weight: 600; margin-bottom: 4px;"">
                                LCP · Logística Centro País — Santa Rosa, La Pampa
                            </div>
                            <div style=""font-size: 11px; color: #475569; font-family: 'Courier New', Courier, monospace;"">
                                RN 5 & RN 35 · Parque Industrial · Argentina · Transmisión Telemática Segura
                            </div>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string GenerarTemplateHtmlConfirmacionEmail(string razonSocial, string email, string confirmUrl)
    {
        return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Confirmación de Correo - Logística Centro País</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #07090C; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #FFFFFF; -webkit-font-smoothing: antialiased;"">
    <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #07090C; padding: 36px 16px;"">
        <tr>
            <td align=""center"">
                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""max-width: 600px; background-color: #0E1217; border: 1px solid #1E2732; border-radius: 8px; overflow: hidden; box-shadow: 0 12px 36px rgba(0,0,0,0.7);"">
                    
                    <!-- Top Technical Brand Bar -->
                    <tr>
                        <td style=""padding: 24px 30px; background-color: #07090C; border-bottom: 1px solid #1E2732;"">
                            <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""left"">
                                        <span style=""background: #FFFFFF; color: #07090C; font-weight: 900; font-size: 13px; padding: 4px 10px; border-radius: 3px; letter-spacing: 1.5px; display: inline-block;"">LCP</span>
                                        <span style=""font-weight: 800; font-size: 15px; letter-spacing: -0.3px; color: #FFFFFF; margin-left: 10px; vertical-align: middle;"">Logística Centro País</span>
                                    </td>
                                    <td align=""right"" style=""font-family: 'Courier New', Courier, monospace; font-size: 11px; color: #64748B; letter-spacing: 0.5px;"">
                                        36°37′S 64°17′W · HUB CENTRAL
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Body Content -->
                    <tr>
                        <td style=""padding: 36px 32px 28px;"">
                            <div style=""display: inline-block; background-color: #1E293B; border: 1px solid #334155; color: #38BDF8; font-family: 'Courier New', Courier, monospace; font-size: 11px; font-weight: 700; padding: 4px 10px; border-radius: 4px; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 12px;"">
                                VERIFICACIÓN DE SEGURIDAD · IDENTIDAD TELEMÁTICA
                            </div>
                            <h1 style=""margin: 0 0 16px; font-size: 24px; font-weight: 800; color: #FFFFFF; letter-spacing: -0.5px; line-height: 1.25;"">
                                Confirmá tu Dirección de Correo Electrónico
                            </h1>
                            <p style=""margin: 0 0 16px; font-size: 14.5px; line-height: 1.6; color: #CBD5E1;"">
                                Estimado/a representante de <strong>{WebUtility.HtmlEncode(razonSocial)}</strong>,
                            </p>
                            <p style=""margin: 0 0 16px; font-size: 14px; line-height: 1.6; color: #CBD5E1;"">
                                Hemos recibido la solicitud de alta de cuenta para su empresa asociada a la casilla <strong style=""color: #FFFFFF;"">{WebUtility.HtmlEncode(email)}</strong> en el Centro Logístico Intermodal de Santa Rosa, La Pampa.
                            </p>
                            <p style=""margin: 0 0 24px; font-size: 14px; line-height: 1.6; color: #CBD5E1;"">
                                Para comprobar que este correo le pertenece y evitar el registro indebido de cuentas no autorizadas, <strong>es obligatorio presionar el siguiente botón</strong> para confirmar su dirección de email:
                            </p>

                            <!-- Button Area -->
                            <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""margin-bottom: 26px;"">
                                <tr>
                                    <td align=""center"">
                                        <a href=""{confirmUrl}"" target=""_blank"" style=""display: inline-block; background-color: #2563EB; color: #FFFFFF; font-weight: 800; font-size: 15px; padding: 14px 34px; border-radius: 6px; text-decoration: none; letter-spacing: -0.2px; box-shadow: 0 4px 16px rgba(37,99,235,0.45);"">
                                            ✓ Confirmar mi Correo Electrónico ➔
                                        </a>
                                    </td>
                                </tr>
                            </table>

                            <!-- Important Notice Callout -->
                            <div style=""background-color: #141A22; border-left: 4px solid #38BDF8; border-radius: 0 6px 6px 0; padding: 16px 20px; margin-bottom: 24px;"">
                                <div style=""color: #38BDF8; font-weight: 800; font-size: 12.5px; margin-bottom: 4px; letter-spacing: 0.3px;"">
                                    ⚖️ PASO SIGUIENTE TRAS LA CONFIRMACIÓN
                                </div>
                                <div style=""color: #CBD5E1; font-size: 13px; line-height: 1.55;"">
                                    Una vez presionado el botón, su solicitud quedará enviada automáticamente a la <strong>Bandeja de Aprobaciones del Administrador de LCP</strong>. El Administrador verificará los datos de la empresa, aprobará la apertura de cuenta y le enviará sus credenciales provisorias para su primer ingreso.
                                </div>
                            </div>

                            <!-- Alternate link fallback -->
                            <div style=""border-top: 1px solid #1E2732; padding-top: 16px; margin-bottom: 12px;"">
                                <p style=""font-size: 12px; color: #94A3B8; margin: 0 0 6px;"">
                                    Si el botón superior no funciona, copiá y pegá el siguiente enlace directo en tu navegador web:
                                </p>
                                <p style=""font-family: 'Courier New', Courier, monospace; font-size: 11.5px; color: #60A5FA; word-break: break-all; margin: 0;"">
                                    {WebUtility.HtmlEncode(confirmUrl)}
                                </p>
                            </div>

                            <p style=""font-size: 11.5px; color: #64748B; margin: 18px 0 0; line-height: 1.5;"">
                                🔒 <em>Si usted no inició este registro en Logística Centro País, por favor ignore este mensaje. La solicitud no será procesada ni visible para la administración sin esta confirmación.</em>
                            </p>

                        </td>
                    </tr>

                    <!-- Minimalist Technical Footer -->
                    <tr>
                        <td style=""padding: 22px 28px; background-color: #07090C; border-top: 1px solid #1E2732; text-align: center;"">
                            <div style=""font-size: 12px; color: #64748B; font-weight: 600; margin-bottom: 4px;"">
                                LCP · Logística Centro País — Santa Rosa, La Pampa
                            </div>
                            <div style=""font-size: 11px; color: #475569; font-family: 'Courier New', Courier, monospace;"">
                                RN 5 & RN 35 · Parque Industrial · Argentina · Transmisión Telemática Segura
                            </div>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string GenerarTemplateHtmlConfirmacionEmailChofer(string nombreCompleto, string email, string confirmUrl)
    {
        return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Confirmación de Correo - Logística Centro País</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #07090C; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #FFFFFF; -webkit-font-smoothing: antialiased;"">
    <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #07090C; padding: 36px 16px;"">
        <tr>
            <td align=""center"">
                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""max-width: 600px; background-color: #0E1217; border: 1px solid #1E2732; border-radius: 8px; overflow: hidden; box-shadow: 0 12px 36px rgba(0,0,0,0.7);"">
                    
                    <tr>
                        <td style=""padding: 24px 30px; background-color: #07090C; border-bottom: 1px solid #1E2732;"">
                            <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""left"">
                                        <span style=""background: #FFFFFF; color: #07090C; font-weight: 900; font-size: 13px; padding: 4px 10px; border-radius: 3px; letter-spacing: 1.5px; display: inline-block;"">LCP</span>
                                        <span style=""font-weight: 800; font-size: 15px; letter-spacing: -0.3px; color: #FFFFFF; margin-left: 10px; vertical-align: middle;"">Logística Centro País</span>
                                    </td>
                                    <td align=""right"" style=""font-family: 'Courier New', Courier, monospace; font-size: 11px; color: #64748B; letter-spacing: 0.5px;"">
                                        36°37′S 64°17′W · HUB CENTRAL
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <tr>
                        <td style=""padding: 36px 32px 28px;"">
                            <div style=""display: inline-block; background-color: #1E293B; border: 1px solid #334155; color: #38BDF8; font-family: 'Courier New', Courier, monospace; font-size: 11px; font-weight: 700; padding: 4px 10px; border-radius: 4px; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 12px;"">
                                POSTULACIÓN DE CONDUCTOR · VALIDACIÓN TELEMÁTICA
                            </div>
                            <h1 style=""margin: 0 0 16px; font-size: 24px; font-weight: 800; color: #FFFFFF; letter-spacing: -0.5px; line-height: 1.25;"">
                                Confirmá tu Dirección de Correo Electrónico
                            </h1>
                            <p style=""margin: 0 0 16px; font-size: 14.5px; line-height: 1.6; color: #CBD5E1;"">
                                Hola <strong>{WebUtility.HtmlEncode(nombreCompleto)}</strong>,
                            </p>
                            <p style=""margin: 0 0 16px; font-size: 14px; line-height: 1.6; color: #CBD5E1;"">
                                Hemos recibido tu postulación para integrarte como conductor a la red logística de Logística Centro País asociada al correo <strong style=""color: #FFFFFF;"">{WebUtility.HtmlEncode(email)}</strong>.
                            </p>
                            <p style=""margin: 0 0 24px; font-size: 14px; line-height: 1.6; color: #CBD5E1;"">
                                Para confirmar que este correo te pertenece y habilitar tu expediente para revisión, <strong>hacé clic en el siguiente botón</strong>:
                            </p>

                            <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""margin-bottom: 26px;"">
                                <tr>
                                    <td align=""center"">
                                        <a href=""{confirmUrl}"" target=""_blank"" style=""display: inline-block; background-color: #2563EB; color: #FFFFFF; font-weight: 800; font-size: 15px; padding: 14px 34px; border-radius: 6px; text-decoration: none; letter-spacing: -0.2px; box-shadow: 0 4px 16px rgba(37,99,235,0.45);"">
                                            ✓ Confirmar mi Correo Electrónico ➔
                                        </a>
                                    </td>
                                </tr>
                            </table>

                            <div style=""background-color: #141A22; border-left: 4px solid #38BDF8; border-radius: 0 6px 6px 0; padding: 16px 20px; margin-bottom: 24px;"">
                                <div style=""color: #38BDF8; font-weight: 800; font-size: 12.5px; margin-bottom: 4px; letter-spacing: 0.3px;"">
                                    🚛 PRÓXIMO PASO
                                </div>
                                <div style=""color: #CBD5E1; font-size: 13px; line-height: 1.55;"">
                                    Al confirmar tu email, tu postulación y tus documentos adjuntos ingresarán a la <strong>Bandeja de Aprobaciones del Administrador de LCP</strong>. Una vez aprobada tu postulación, el sistema te enviará automáticamente tus credenciales para ingresar a la plataforma.
                                </div>
                            </div>

                            <div style=""border-top: 1px solid #1E2732; padding-top: 16px; margin-bottom: 12px;"">
                                <p style=""font-size: 12px; color: #94A3B8; margin: 0 0 6px;"">
                                    Si el botón no funciona, copiá y pegá este enlace en tu navegador:
                                </p>
                                <p style=""font-family: 'Courier New', Courier, monospace; font-size: 11.5px; color: #60A5FA; word-break: break-all; margin: 0;"">
                                    {WebUtility.HtmlEncode(confirmUrl)}
                                </p>
                            </div>

                            <p style=""font-size: 11.5px; color: #64748B; margin: 18px 0 0; line-height: 1.5;"">
                                🔒 <em>Si no realizaste esta postulación, por favor desestimá este mensaje. Sin la confirmación, tus datos no serán procesados.</em>
                            </p>
                        </td>
                    </tr>

                    <tr>
                        <td style=""padding: 22px 28px; background-color: #07090C; border-top: 1px solid #1E2732; text-align: center;"">
                            <div style=""font-size: 12px; color: #64748B; font-weight: 600; margin-bottom: 4px;"">
                                LCP · Logística Centro País — Santa Rosa, La Pampa
                            </div>
                            <div style=""font-size: 11px; color: #475569; font-family: 'Courier New', Courier, monospace;"">
                                RN 5 & RN 35 · Parque Industrial · Argentina · Transmisión Telemática Segura
                            </div>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }

    private string GenerarTemplateHtmlConfirmacionPersonal(string nombre, string email, string puesto, string rol, string password, string confirmUrl)
    {
        return $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>Alta de Personal y Confirmación de Correo - Logística Centro País</title>
</head>
<body style=""margin: 0; padding: 0; background-color: #07090C; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; color: #FFFFFF; -webkit-font-smoothing: antialiased;"">
    <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""background-color: #07090C; padding: 36px 16px;"">
        <tr>
            <td align=""center"">
                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""max-width: 600px; background-color: #0E1217; border: 1px solid #1E2732; border-radius: 8px; overflow: hidden; box-shadow: 0 12px 36px rgba(0,0,0,0.7);"">
                    
                    <!-- Top Technical Brand Bar -->
                    <tr>
                        <td style=""padding: 24px 30px; background-color: #07090C; border-bottom: 1px solid #1E2732;"">
                            <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                                <tr>
                                    <td align=""left"">
                                        <span style=""background: #FFFFFF; color: #07090C; font-weight: 900; font-size: 13px; padding: 4px 10px; border-radius: 3px; letter-spacing: 1.5px; display: inline-block;"">LCP</span>
                                        <span style=""font-weight: 800; font-size: 15px; letter-spacing: -0.3px; color: #FFFFFF; margin-left: 10px; vertical-align: middle;"">Logística Centro País</span>
                                    </td>
                                    <td align=""right"" style=""font-family: 'Courier New', Courier, monospace; font-size: 11px; color: #64748B; letter-spacing: 0.5px;"">
                                        36°37′S 64°17′W · HUB CENTRAL
                                    </td>
                                </tr>
                            </table>
                        </td>
                    </tr>

                    <!-- Body Content -->
                    <tr>
                        <td style=""padding: 36px 32px 28px;"">
                            <div style=""display: inline-block; background-color: #1E293B; border: 1px solid #334155; color: #38BDF8; font-family: 'Courier New', Courier, monospace; font-size: 11px; font-weight: 700; padding: 4px 10px; border-radius: 4px; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 12px;"">
                                ALTA DE PERSONAL · CONFIRMACIÓN TELEMÁTICA DE CASILLA
                            </div>
                            <h1 style=""margin: 0 0 16px; font-size: 24px; font-weight: 800; color: #FFFFFF; letter-spacing: -0.5px; line-height: 1.25;"">
                                Bienvenido/a al Personal de Logística Centro País
                            </h1>
                            <p style=""margin: 0 0 16px; font-size: 14.5px; line-height: 1.6; color: #CBD5E1;"">
                                Estimado/a <strong>{WebUtility.HtmlEncode(nombre)}</strong>, la administración de LCP ha registrado su alta laboral formal en el nodo operativo de <strong>Santa Rosa, La Pampa</strong>.
                            </p>

                            <!-- Credentials Box -->
                            <div style=""background-color: #141A22; border: 1px solid #283544; border-radius: 6px; padding: 20px 22px; margin-bottom: 22px;"">
                                <div style=""font-family: 'Courier New', Courier, monospace; font-size: 11px; color: #94A3B8; letter-spacing: 0.5px; text-transform: uppercase; margin-bottom: 12px; border-bottom: 1px solid #1E2732; padding-bottom: 8px;"">
                                    DATOS DE LEGAJO Y ACCESO ASIGNADOS
                                </div>
                                <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"">
                                    <tr>
                                        <td style=""padding-bottom: 10px; font-size: 13px; color: #94A3B8; width: 140px;"">Usuario / Casilla:</td>
                                        <td style=""padding-bottom: 10px; font-size: 13.5px; color: #FFFFFF; font-weight: 700; font-family: 'Courier New', Courier, monospace;"">{WebUtility.HtmlEncode(email)}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding-bottom: 10px; font-size: 13px; color: #94A3B8;"">Puesto / Cargo:</td>
                                        <td style=""padding-bottom: 10px; font-size: 13px; color: #F1F5F9; font-weight: 700;"">{WebUtility.HtmlEncode(puesto)}</td>
                                    </tr>
                                    <tr>
                                        <td style=""padding-bottom: 10px; font-size: 13px; color: #94A3B8;"">Rol Operativo:</td>
                                        <td style=""padding-bottom: 10px; font-size: 13px; color: #38BDF8; font-weight: 700;"">{WebUtility.HtmlEncode(rol)}</td>
                                    </tr>
                                    <tr>
                                        <td style=""font-size: 13px; color: #94A3B8; vertical-align: middle;"">Contraseña Provisoria:</td>
                                        <td style=""vertical-align: middle;"">
                                            <span style=""display: inline-block; background-color: #07090C; border: 1px solid #FFFFFF; color: #FFFFFF; font-family: 'Courier New', Courier, monospace; font-size: 15px; font-weight: 800; padding: 5px 12px; border-radius: 4px; letter-spacing: 1.5px;"">
                                                {WebUtility.HtmlEncode(password)}
                                            </span>
                                        </td>
                                    </tr>
                                </table>
                            </div>

                            <!-- Verification Callout -->
                            <div style=""background-color: #14231E; border-left: 4px solid #10B981; border-radius: 0 6px 6px 0; padding: 16px 20px; margin-bottom: 24px;"">
                                <div style=""color: #34D399; font-weight: 800; font-size: 13px; margin-bottom: 4px;"">
                                    ✓ VALIDACIÓN DE EMAIL VERÍDICO Y ACTIVACIÓN DE CUENTA
                                </div>
                                <div style=""color: #D1FAE5; font-size: 13px; line-height: 1.5;"">
                                    Para asegurar que su correo sea genuino y único en la infraestructura de LCP, debe hacer clic en el botón de confirmación. Al pulsar, <strong>su cuenta quedará formalmente activada</strong> para iniciar sesión.
                                </div>
                            </div>

                            <!-- Action Button -->
                            <table width=""100%"" border=""0"" cellpadding=""0"" cellspacing=""0"" style=""margin-bottom: 24px;"">
                                <tr>
                                    <td align=""center"">
                                        <a href=""{confirmUrl}"" target=""_blank"" style=""display: inline-block; background-color: #10B981; color: #07090C; font-weight: 900; font-size: 15px; padding: 15px 36px; border-radius: 4px; text-decoration: none; letter-spacing: -0.2px; box-shadow: 0 4px 18px rgba(16, 185, 129, 0.35);"">
                                            ✓ Confirmar Correo y Activar Cuenta
                                        </a>
                                    </td>
                                </tr>
                            </table>

                            <!-- Password change note -->
                            <div style=""background-color: #1C1917; border: 1px solid #292524; border-radius: 6px; padding: 14px 18px; margin-bottom: 20px;"">
                                <div style=""font-size: 12px; color: #F59E0B; font-weight: 700; margin-bottom: 4px;"">
                                    ⚠️ Primer Inicio de Sesión
                                </div>
                                <div style=""font-size: 12px; color: #A8A29E; line-height: 1.5;"">
                                    Una vez confirmado su correo, acceda con su usuario y contraseña provisoria. La plataforma le requerirá inmediatamente definir su contraseña definitiva y personal.
                                </div>
                            </div>

                            <!-- Raw link fallback -->
                            <div style=""border-top: 1px solid #1E2732; padding-top: 16px; margin-bottom: 12px;"">
                                <p style=""font-size: 12px; color: #94A3B8; margin: 0 0 6px;"">
                                    Si el botón no funciona, copiá y pegá este enlace en tu navegador:
                                </p>
                                <p style=""font-family: 'Courier New', Courier, monospace; font-size: 11.5px; color: #60A5FA; word-break: break-all; margin: 0;"">
                                    {WebUtility.HtmlEncode(confirmUrl)}
                                </p>
                            </div>
                        </td>
                    </tr>

                    <tr>
                        <td style=""padding: 22px 28px; background-color: #07090C; border-top: 1px solid #1E2732; text-align: center;"">
                            <div style=""font-size: 12px; color: #64748B; font-weight: 600; margin-bottom: 4px;"">
                                LCP · Logística Centro País — Santa Rosa, La Pampa
                            </div>
                            <div style=""font-size: 11px; color: #475569; font-family: 'Courier New', Courier, monospace;"">
                                RN 5 & RN 35 · Parque Industrial · Argentina · Transmisión Telemática Segura
                            </div>
                        </td>
                    </tr>

                </table>
            </td>
        </tr>
    </table>
</body>
</html>";
    }
}
