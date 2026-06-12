using Microsoft.EntityFrameworkCore;
using Rodnix.EvaLuma.Models;
using Rodnix.EvaLuma.Utils;

namespace Rodnix.EvaLuma.Data
{
    public class EvalumaDbContext : DbContext
    {
        public EvalumaDbContext(DbContextOptions<EvalumaDbContext> options) : base(options) { }

        // Mantenido exactamente igual para preservar el funcionamiento del AuthController y el aprovisionamiento
        public DbSet<Usuario> Usuarios { get; set; }

        // Nuevas entidades requeridas para el flujo del motor asíncrono y auditoría
        public DbSet<Campana> Campanas { get; set; }
        public DbSet<Simulacion> Simulaciones { get; set; }
        public DbSet<AsignacionProgreso> AsignacionesProgreso { get; set; }
        public DbSet<BitacoraAuditoria> BitacorasAuditoria { get; set; }
        public DbSet<Pregunta> Preguntas { get; set; }
        public DbSet<OpcionRespuesta> OpcionesRespuesta { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuración estricta para la Bitácora de Auditoría Inmutable
            // Garantiza a nivel de motor de base de datos que no existan hashes duplicados en la cadena
            modelBuilder.Entity<BitacoraAuditoria>(entity =>
            {
                entity.HasIndex(b => b.HashCriptografico)
                      .IsUnique();

                entity.Property(b => b.AccionRealizada)
                      .HasConversion(
                          v => AesEncryptionHelper.Encrypt(v),
                          v => AesEncryptionHelper.Decrypt(v)
                      );
            });
        }
    }
}