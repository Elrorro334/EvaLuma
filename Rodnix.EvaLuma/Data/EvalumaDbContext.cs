using Microsoft.EntityFrameworkCore;
using Rodnix.EvaLuma.Models;

namespace Rodnix.EvaLuma.Data
{
    public class EvalumaDbContext : DbContext
    {
        public EvalumaDbContext(DbContextOptions<EvalumaDbContext> options) : base(options) { }
        public DbSet<Usuario> Usuarios { get; set; }
    }
}
