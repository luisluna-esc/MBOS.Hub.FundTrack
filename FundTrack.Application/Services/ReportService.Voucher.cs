using FundTrack.Application.Dtos.Reports;
using FundTrack.Application.Dtos.StoredProcedures;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Funds;
using FundTrack.core.Entities.Organization;
using FundTrack.core.QueryFilters.Reports;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FundTrack.Application.Services;

// "Arqueo del fondo local": la misma hoja que llena el tesorero en papel (saldo ACMS, extracto y
// depósitos, efectivo, dólares, documentos a rendir, total en custodia, diferencia, observaciones y
// las firmas del tesorero, el pastor y el anciano), armada con lo registrado en el sistema. Sale de
// un arqueo (botón Imprimir del registro) o de los de un mes, una hoja por iglesia.
public partial class ReportService
{
    private const string VoucherReportName = "report_local_fund_count";
    private const string VoucherLineColor = "#9CA3AF";

    private sealed record VoucherData(
        LocalFundCountGetAllRow Count,
        IReadOnlyList<LocalFundCountDocument> Documents,
        IReadOnlyList<string> Filials);

    public async Task<ReportResult> ExportLocalFundCountVoucher(LocalFundCountVoucherQueryFilter filter)
    {
        if (filter.LocalFundCountId is null && filter.PeriodId is null)
            return new ReportResult(null, BadRequest("Elige el mes del arqueo."));

        var restrictedDistrictId = await _dataScopeService.GetRestrictedDistrictId();

        // Un mes completo de toda la Misión serían cientos de hojas: se pide al menos el distrito.
        if (filter.LocalFundCountId is null && filter.DistrictId is null && filter.ChurchId is null && restrictedDistrictId is null)
            return new ReportResult(null, BadRequest("Elige un distrito o una iglesia para imprimir sus arqueos."));

        var counts = await _unitOfWork.SqlQueryAsync<LocalFundCountGetAllRow>(
            LocalFundCountService.GetLocalFundCountsSql,
            (object?)filter.LocalFundCountId ?? DBNull.Value,
            (object?)filter.PeriodId ?? DBNull.Value,
            DBNull.Value,
            (object?)filter.ChurchId ?? DBNull.Value,
            (object?)filter.DistrictId ?? DBNull.Value,
            DBNull.Value,
            DBNull.Value,
            DBNull.Value,
            DBNull.Value,
            DBNull.Value,
            (object?)restrictedDistrictId ?? DBNull.Value,
            1,
            1000);

        if (counts.Count == 0)
            return new ReportResult(null, BadRequest("No hay arqueos registrados para los filtros elegidos."));

        var countIds = counts.Select(c => c.LocalFundCountId).ToList();
        var churchIds = counts.Select(c => c.ChurchId).Distinct().ToList();

        var documents = await _unitOfWork.Repository<LocalFundCountDocument>().Query()
            .Where(d => countIds.Contains(d.LocalFundCountId))
            .OrderBy(d => d.DocumentDate).ThenBy(d => d.LocalFundCountDocumentId)
            .ToListAsync();

        var filials = await _unitOfWork.Repository<Church>().Query()
            .Where(c => c.ParentChurchId != null && churchIds.Contains(c.ParentChurchId.Value) && c.Status == ChurchStatuses.Active)
            .OrderBy(c => c.Name)
            .Select(c => new { ParentId = c.ParentChurchId!.Value, c.Name })
            .ToListAsync();

        var vouchers = counts
            .OrderBy(c => c.DistrictName, StringComparer.Create(ReportCulture, false))
            .ThenBy(c => c.ChurchName, StringComparer.Create(ReportCulture, false))
            .Select(c => new VoucherData(
                c,
                documents.Where(d => d.LocalFundCountId == c.LocalFundCountId).ToList(),
                filials.Where(f => f.ParentId == c.ChurchId).Select(f => f.Name).ToList()))
            .ToList();

        var username = await CurrentUserFullNameAsync();
        var first = vouchers[0].Count;
        var fileName = vouchers.Count == 1
            ? $"arqueo-{first.ChurchCode}-{first.PeriodYear}-{first.PeriodMonth:00}.pdf"
            : $"arqueos-{first.PeriodYear}-{first.PeriodMonth:00}.pdf";

        var file = new ReportFile(BuildVoucherPdf(vouchers, username), PdfContentType, fileName);

        var description = vouchers.Count == 1
            ? $"Arqueo {first.ChurchName} {MonthNames[first.PeriodMonth - 1]} {first.PeriodYear}"
            : $"{vouchers.Count} arqueos de {MonthNames[first.PeriodMonth - 1]} {first.PeriodYear}";
        await _auditLogService.LogReportAccess(VoucherReportName, "EXPORT", description);

        return new ReportResult(file, null);
    }

    private byte[] BuildVoucherPdf(IReadOnlyList<VoucherData> vouchers, string username)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            foreach (var voucher in vouchers)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.Letter);
                    page.Margin(36);
                    page.DefaultTextStyle(x => x.FontSize(9));
                    page.Header().Element(c => ComposeHeader(c, "ARQUEO DEL FONDO LOCAL", []));
                    page.Content().PaddingTop(10).Element(c => ComposeVoucher(c, voucher));
                    page.Footer().Element(c => ComposeFooter(c, username));
                });
            }
        });

        return document.GeneratePdf();
    }

    private static void ComposeVoucher(IContainer container, VoucherData voucher)
    {
        var count = voucher.Count;
        var submitted = count.SubmissionStatus == SubmissionStatuses.Submitted;
        var fundsTotal = count.TotalAmount - count.DocumentsTotal;

        container.Column(column =>
        {
            column.Spacing(8);

            // Iglesia, distrito y mes.
            column.Item().Border(1).BorderColor(VoucherLineColor).Padding(8).Column(info =>
            {
                info.Spacing(3);
                info.Item().Row(row =>
                {
                    row.RelativeItem(3).Text(t =>
                    {
                        t.Span("Iglesia / Grupo: ").SemiBold();
                        t.Span($"{count.ChurchCode} {count.ChurchName} ({count.ChurchType})");
                    });
                    row.RelativeItem(2).AlignRight().Text(t =>
                    {
                        t.Span("Al mes de: ").SemiBold();
                        t.Span($"{MonthNames[count.PeriodMonth - 1]} {count.PeriodYear}");
                    });
                });
                info.Item().Row(row =>
                {
                    row.RelativeItem(3).Text(t =>
                    {
                        t.Span("Distrito: ").SemiBold();
                        t.Span($"{count.DistrictName}  ·  {count.RegionName}");
                    });
                    row.RelativeItem(2).AlignRight().Text(t =>
                    {
                        t.Span("Fecha del arqueo: ").SemiBold();
                        t.Span(count.CountDate.HasValue ? $"{count.CountDate:dd/MM/yyyy}" : "");
                    });
                });
                if (voucher.Filials.Count > 0)
                {
                    info.Item().Text(t =>
                    {
                        t.Span("Incluye las filiales: ").SemiBold();
                        t.Span(string.Join(", ", voucher.Filials));
                    });
                }
            });

            column.Item().Element(c => AmountLine(c,
                $"SALDO SEGÚN BALANCE POR DEPARTAMENTO (ACMS){(count.CountDate.HasValue ? $" al {count.CountDate:dd/MM/yyyy}" : "")}",
                count.AcmsBalance, bold: true, fill: true));

            if (!submitted)
            {
                column.Item().PaddingVertical(20).AlignCenter()
                    .Text("La iglesia no presentó su arqueo este mes.").Italic().FontSize(11);
            }
            else
            {
                // Extracto, depósitos y efectivo.
                column.Item().Element(c => SectionTitle(c, "EXTRACTO, DEPÓSITOS BANCARIOS Y EFECTIVO", fundsTotal));
                column.Item().PaddingLeft(14).Column(lines =>
                {
                    lines.Spacing(2);
                    lines.Item().Element(c => AmountLine(c, "Saldo según extracto bancario", count.BankStatementBalance));
                    lines.Item().Element(c => AmountLine(c, "Depósito bancario", count.BankDeposit));
                    lines.Item().PaddingTop(4).Text("EFECTIVO").SemiBold().FontSize(8);
                    lines.Item().Element(c => AmountLine(c, "Billetes y monedas en moneda nacional (fondo local)", count.LocalCash));
                    lines.Item().Element(c => AmountLine(c,
                        count.UsdAmount > 0
                            ? $"Dólares ($us {Money(count.UsdAmount)} × tipo de cambio {count.ExchangeRate.ToString("N2", ReportCulture)})"
                            : "Dólares ($us)",
                        count.UsdAmountBs));
                });

                // Documentos a rendir.
                column.Item().Element(c => SectionTitle(c, "DOCUMENTOS (FACTURAS, RECIBOS Y OTROS A RENDIR)", count.DocumentsTotal));
                column.Item().PaddingLeft(14).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(62);
                        columns.ConstantColumn(80);
                        columns.RelativeColumn();
                        columns.ConstantColumn(80);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Fecha");
                        header.Cell().Element(HeaderCell).Text("Factura / Recibo");
                        header.Cell().Element(HeaderCell).Text("Detalle");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Monto Bs");
                    });
                    if (voucher.Documents.Count == 0)
                    {
                        table.Cell().ColumnSpan(4).Element(BodyCell).AlignCenter().Text("Sin documentos a rendir.").Italic();
                    }
                    foreach (var doc in voucher.Documents)
                    {
                        table.Cell().Element(BodyCell).Text($"{doc.DocumentDate:dd/MM/yyyy}");
                        table.Cell().Element(BodyCell).Text(doc.DocumentNumber ?? "");
                        table.Cell().Element(BodyCell).Text(doc.Detail);
                        table.Cell().Element(BodyCell).AlignRight().Text(Money(doc.Amount));
                    }
                });

                // Totales.
                column.Item().PaddingTop(4).AlignRight().Width(300).Column(totals =>
                {
                    totals.Spacing(2);
                    totals.Item().Element(c => AmountLine(c, "TOTAL FONDOS EN CUSTODIA DEL TESORERO", count.TotalAmount, bold: true, fill: true));
                    totals.Item().Element(c => AmountLine(c,
                        count.Difference == 0 ? "Diferencia" : count.Difference > 0 ? "Diferencia de más" : "Diferencia de menos",
                        count.Difference, bold: true, color: DifferenceColor(count.Difference)));
                    if (count.Difference != 0)
                    {
                        totals.Item().AlignRight().Text($"Estado: {DifferenceStatusLabel(count.DifferenceStatus)}").FontSize(8).Italic();
                    }
                });
            }

            // Observaciones.
            column.Item().PaddingTop(4).Text("OBSERVACIONES:").SemiBold();
            column.Item().Border(1).BorderColor(VoucherLineColor).MinHeight(48).Padding(6)
                .Text(count.Observation ?? "");

            // Las tres firmas.
            column.Item().PaddingTop(46).Row(row =>
            {
                row.Spacing(24);
                row.RelativeItem().Element(c => SignatureBox(c, "Tesorero(a)", count.TreasurerName, null));
                row.RelativeItem().Element(c => SignatureBox(c, "Pastor del distrito", count.PastorName, count.SignedByPastor));
                row.RelativeItem().Element(c => SignatureBox(c, "Anciano(a)", count.ElderName, count.SignedByElder));
            });
        });
    }

    private static void SectionTitle(IContainer container, string title, decimal amount)
    {
        container.PaddingTop(4).BorderBottom(1).BorderColor(VoucherLineColor).PaddingBottom(2).Row(row =>
        {
            row.RelativeItem().Text(title).SemiBold();
            row.ConstantItem(110).AlignRight().Text($"Bs {Money(amount)}").SemiBold();
        });
    }

    private static void AmountLine(IContainer container, string label, decimal amount, bool bold = false, bool fill = false, string? color = null)
    {
        var box = fill ? container.Background(Colors.Grey.Lighten3).Padding(4) : container.PaddingVertical(1);
        box.Row(row =>
        {
            var labelText = row.RelativeItem().Text(label);
            if (bold) labelText.SemiBold();

            var amountText = row.ConstantItem(110).AlignRight().Text($"Bs {Money(amount)}");
            if (bold) amountText.Bold();
            if (color is not null) amountText.FontColor(color);
        });
    }

    /// <summary>Línea para firmar con el nombre y el cargo debajo; si en el registro se marcó la firma, lo indica.</summary>
    private static void SignatureBox(IContainer container, string role, string? name, bool? signed)
    {
        container.Column(column =>
        {
            column.Item().BorderTop(1).BorderColor(Colors.Grey.Darken2).PaddingTop(3).AlignCenter()
                .Text(string.IsNullOrWhiteSpace(name) ? " " : name).SemiBold();
            column.Item().AlignCenter().Text(role).FontSize(8);
            if (signed == true)
            {
                column.Item().AlignCenter().Text("Firma registrada")
                    .FontSize(7).Italic().FontColor(Colors.Grey.Darken1);
            }
        });
    }
}
