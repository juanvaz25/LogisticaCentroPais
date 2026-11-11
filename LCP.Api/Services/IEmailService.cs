namespace LCP.Api.Services;

public interface IEmailService
{
    Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
    Task<bool> SendCredencialesAutoGeneradasAsync(string toEmail, string nombreDestinatario, string passwordGenerada, string rol);
    Task<bool> SendNotificacionAltaEmpresaAsync(string toEmail, string razonSocial);
    Task<bool> SendConfirmacionEmailRegistroAsync(string toEmail, string razonSocial, string confirmUrl);
    Task<bool> SendConfirmacionEmailChoferAsync(string toEmail, string nombreCompleto, string confirmUrl);
    Task<bool> SendConfirmacionPersonalAsync(string toEmail, string nombreCompleto, string puesto, string rol, string passwordProvisoria, string confirmUrl);
    Task<bool> SendAprobacionEmpresaAsync(string toEmail, string razonSocial);
}
