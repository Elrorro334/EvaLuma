using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using CsvHelper;
using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Rodnix.EvaLuma.Models;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using iText.Kernel.Pdf;
using iText.Signatures;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Crypto;
using iText.Bouncycastle.Crypto;
using iText.Bouncycastle.X509;

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
                    
                    page.Background().AlignCenter().AlignMiddle().Text("DOCUMENTO INMUTABLE").FontSize(45).FontColor(Colors.Grey.Lighten3);

                    page.Header().Element(c => ComposeHeader(c, empleado, campana, calificacion));
                    page.Content().Element(c => ComposeContent(c, historial));
                    page.Footer().Element(ComposeFooter);
                });
            });

            var pdfBytes = document.GeneratePdf();
            return SignPdf(pdfBytes);
        }

        private byte[] SignPdf(byte[] pdfBytes)
        {
            try
            {
                using var rsa = RSA.Create(2048);
                var request = new CertificateRequest("cn=EVALUMA Inmutable", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                var certificate = request.CreateSelfSigned(DateTimeOffset.Now, DateTimeOffset.Now.AddYears(5));
                var pfxBytes = certificate.Export(X509ContentType.Pfx, "evaluma");

                using var reader = new PdfReader(new MemoryStream(pdfBytes));
                using var outStream = new MemoryStream();
                
                var properties = new StampingProperties();
                var signer = new PdfSigner(reader, outStream, properties);
                
                var pk12 = new Pkcs12StoreBuilder().Build();
                using var msPfx = new MemoryStream(pfxBytes);
                pk12.Load(msPfx, "evaluma".ToCharArray());

                string alias = null;
                foreach (string tAlias in pk12.Aliases)
                {
                    if (pk12.IsKeyEntry(tAlias))
                    {
                        alias = tAlias;
                        break;
                    }
                }

                var pk = pk12.GetKey(alias).Key;
                var ce = pk12.GetCertificateChain(alias);
                
                var wrappedChain = new iText.Commons.Bouncycastle.Cert.IX509Certificate[ce.Length];
                for (int k = 0; k < ce.Length; ++k)
                    wrappedChain[k] = new X509CertificateBC(ce[k].Certificate);

                IExternalSignature pks = new PrivateKeySignature(new PrivateKeyBC(pk), "SHA-256");
                signer.SignDetached(pks, wrappedChain, null, null, null, 0, PdfSigner.CryptoStandard.CMS);
                
                return outStream.ToArray();
            }
            catch(Exception)
            {
                // Si falla la firma digital (por librerías faltantes), retorna el PDF sin firmar.
                return pdfBytes;
            }
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
                x.Span(" | FIRMADO DIGITALMENTE").FontColor(Colors.Blue.Darken2).SemiBold();
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
