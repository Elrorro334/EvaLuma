using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CsvHelper;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Rodnix.EvaLuma.Models;

namespace Rodnix.EvaLuma.Services
{
    public interface IReportGeneratorService
    {
        byte[] GeneratePdfReport(string empleado, string campana, decimal calificacion, List<BitacoraAuditoria> historial);
        byte[] GenerateCsvReport(List<BitacoraAuditoria> historial);
    }

    public class ReportGeneratorService : IReportGeneratorService
    {
        public ReportGeneratorService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GeneratePdfReport(string empleado, string campana, decimal calificacion, List<BitacoraAuditoria> historial)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily(Fonts.Arial));

                    page.Header().Element(c => ComposeHeader(c, empleado, campana, calificacion));
                    page.Content().Element(c => ComposeContent(c, historial));
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        private void ComposeHeader(IContainer container, string empleado, string campana, decimal calificacion)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("Reporte de Auditoría Inmutable").FontSize(20).SemiBold().FontColor(Colors.Blue.Darken2);
                    column.Item().PaddingTop(5).Text($"Empleado: {empleado}");
                    column.Item().Text($"Campaña: {campana}");
                    column.Item().Text($"Calificación Temporal: {calificacion:F2}%");
                });
            });
        }

        private void ComposeContent(IContainer container, List<BitacoraAuditoria> historial)
        {
            container.PaddingVertical(1, Unit.Centimetre).Column(column =>
            {
                column.Item().PaddingBottom(10).Text("Historial de Eventos de la Sesión").FontSize(14).SemiBold();
                
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(1);
                        columns.RelativeColumn(4);
                    });

                    table.Header(header =>
                    {
                        header.Cell().PaddingBottom(5).Text("Fecha/Hora").SemiBold();
                        header.Cell().PaddingBottom(5).Text("Acción").SemiBold();
                        header.Cell().PaddingBottom(5).Text("Tiempo (ms)").SemiBold();
                        header.Cell().PaddingBottom(5).Text("Hash Criptográfico (SHA-256)").SemiBold();
                    });

                    foreach (var item in historial)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(item.MarcaTiempo.ToString("yyyy-MM-dd HH:mm:ss")).FontSize(9);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(item.AccionRealizada).FontSize(9);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(item.TiempoRespuestaMs.ToString()).FontSize(9);
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(3).Text(item.HashCriptografico).FontSize(7);
                    }
                });
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(x =>
            {
                x.Span("Página ");
                x.CurrentPageNumber();
                x.Span(" de ");
                x.TotalPages();
            });
        }

        public byte[] GenerateCsvReport(List<BitacoraAuditoria> historial)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new StreamWriter(memoryStream);
            using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

            var exportData = historial.Select(h => new
            {
                h.IdEvento,
                h.MarcaTiempo,
                h.AccionRealizada,
                h.TiempoRespuestaMs,
                h.HashPrevio,
                h.HashCriptografico
            }).ToList();

            csv.WriteRecords(exportData);
            writer.Flush();
            return memoryStream.ToArray();
        }
    }
}
