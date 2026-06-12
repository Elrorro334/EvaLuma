using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Rodnix.EvaLuma.Data;
using Rodnix.EvaLuma.Services;

namespace Rodnix.EvaLuma.Workers
{
    public class EmailTriggerWorker : BackgroundService
    {
        private readonly ILogger<EmailTriggerWorker> _logger;
        private readonly IServiceScopeFactory _scopeFactory;

        public EmailTriggerWorker(ILogger<EmailTriggerWorker> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Iniciando EmailTriggerWorker para notificaciones automáticas.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var context = scope.ServiceProvider.GetRequiredService<EvalumaDbContext>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    // Buscar asignaciones pendientes que estén a 24 horas de vencer y no se les haya notificado
                    var limiteProximo = DateTime.UtcNow.AddHours(24);
                    
                    var asignacionesPorVencer = await context.AsignacionesProgreso
                        .Include(a => a.Empleado)
                        .Include(a => a.Simulacion).ThenInclude(s => s!.Campana)
                        .Where(a => a.Estado == "Pendiente" || a.Estado == "En Progreso")
                        .Where(a => a.Simulacion!.Campana!.FechaLimite <= limiteProximo && a.Simulacion.Campana.FechaLimite > DateTime.UtcNow)
                        .ToListAsync(stoppingToken);

                    foreach (var asignacion in asignacionesPorVencer)
                    {
                        if (asignacion.Empleado != null && !string.IsNullOrEmpty(asignacion.Empleado.EmailCorporativo))
                        {
                            var htmlTemplate = $@"
                                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #C3E0E6; border-radius: 12px; background-color: #F3F7F9;'>
                                    <h2 style='color: #194B64; text-align: center;'>¡Alerta de Vencimiento de Evaluación!</h2>
                                    <p style='color: #5D7E88; font-size: 16px;'>Hola <strong>{asignacion.Empleado.NombreCompleto}</strong>,</p>
                                    <p style='color: #5D7E88; font-size: 16px;'>Te recordamos que tu asignación para la evaluación <strong>{asignacion.Simulacion!.Titulo}</strong> de la campaña <strong>{asignacion.Simulacion!.Campana!.NombreCampana}</strong> vencerá pronto.</p>
                                    
                                    <div style='background-color: #fff; padding: 15px; border-radius: 8px; border-left: 4px solid #rose-500; margin: 20px 0;'>
                                        <p style='color: #e11d48; margin: 0; font-weight: bold;'>Fecha Límite: {asignacion.Simulacion!.Campana!.FechaLimite.ToString("dd/MM/yyyy HH:mm")}</p>
                                    </div>

                                    <p style='color: #5D7E88; font-size: 16px;'>Por favor, completa la evaluación antes de la fecha límite para evitar penalizaciones por incumplimiento normativo (Compliance).</p>
                                    
                                    <div style='text-align: center; margin-top: 30px;'>
                                        <a href='http://localhost:3000/login' style='background-color: #13B4CE; color: white; padding: 12px 25px; text-decoration: none; border-radius: 8px; font-weight: bold; display: inline-block;'>Ir al Portal EVALUMA</a>
                                    </div>
                                    
                                    <p style='color: #8EACB4; font-size: 12px; text-align: center; margin-top: 30px;'>
                                        EVALUMA - Evaluación Corporativa Inmutable.<br/>
                                        Este es un mensaje automático generado por el motor de triggers.
                                    </p>
                                </div>
                            ";

                            await emailService.SendEmailAsync(
                                asignacion.Empleado.EmailCorporativo, 
                                $"[URGENTE] Evaluación por vencer: {asignacion.Simulacion!.Titulo}", 
                                htmlTemplate);
                            
                            // En un sistema real, aquí actualizaríamos un campo "Notificado24h = true" en BD.
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en EmailTriggerWorker.");
                }

                // Evaluar una vez al día o cada x horas. Para demostración, cada minuto.
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
        }
    }
}
