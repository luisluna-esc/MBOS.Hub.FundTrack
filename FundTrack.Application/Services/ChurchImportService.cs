using System.Net;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using FundTrack.Application.Dtos.Imports;
using FundTrack.Application.Interfaces.Imports;
using FundTrack.core.Constants;
using FundTrack.core.Entities.Organization;
using FundTrack.core.Entities.ResponseApi.Details;
using FundTrack.core.Entities.ResponseApi.DisplayFormat;
using FundTrack.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FundTrack.Application.Services;

/// <summary>
/// Carga inicial de la lista oficial de iglesias desde la plantilla Excel. Solo crea lo que no
/// existe: regiones y distritos por nombre, pastores por CI e iglesias por código. Lo que ya
/// existe no se modifica (se informa como aviso), porque esos cambios deben quedar en el
/// historial y se hacen desde sus pantallas.
/// </summary>
public partial class ChurchImportService : IChurchImportService
{
    // Orden de las columnas de la plantilla. Si cambia aquí, cambia la plantilla y la lectura.
    private static readonly string[] Headers =
    [
        "Región", "Distrito", "Distrito misionero (Sí/No)",
        "CI pastor", "Apellidos pastor", "Nombres pastor",
        "Código iglesia", "Nombre iglesia", "Tipo (Iglesia/Grupo/Filial)",
        "Dirección", "Teléfono", "Tesorero", "Teléfono tesorero", "Anciano", "Teléfono anciano"
    ];

    private static readonly string[] YesValues = ["si", "sí", "s", "x", "1", "true", "yes"];
    private static readonly string[] NoValues = ["", "no", "n", "0", "false"];

    private readonly IUnitOfWork _unitOfWork;

    public ChurchImportService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    private record ImportRow(
        int RowNumber,
        string Region,
        string District,
        bool IsMissionary,
        string? PastorCi,
        string? PastorLastNames,
        string? PastorFirstNames,
        string Code,
        string Name,
        string Type,
        string? Address,
        string? Phone,
        string? TreasurerName,
        string? TreasurerPhone,
        string? ElderName,
        string? ElderPhone)
    {
        public string DistrictKey => $"{Region}|{District}".ToUpperInvariant();
        public bool HasPastor => PastorLastNames is not null;
        /// <summary>Cómo se reconoce al pastor: por CI si lo tiene, si no por apellidos y nombres.</summary>
        public string PastorKey => PastorCi is not null ? PastorCiKey(PastorCi) : PastorNameKey(PastorLastNames ?? "", PastorFirstNames ?? "");
    }

    public byte[] GetTemplate()
    {
        using var workbook = new XLWorkbook();

        var sheet = workbook.AddWorksheet("Iglesias");
        for (var column = 0; column < Headers.Length; column++)
        {
            sheet.Cell(1, column + 1).Value = Headers[column];
        }

        var headerRow = sheet.Range(1, 1, 1, Headers.Length);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#1C274C");
        headerRow.Style.Font.FontColor = XLColor.White;
        sheet.SheetView.FreezeRows(1);

        // Listas desplegables para que no se escriban valores que el sistema no acepta.
        sheet.Range("C2:C2000").CreateDataValidation().List("\"Sí,No\"");
        sheet.Range("I2:I2000").CreateDataValidation().List($"\"{string.Join(",", ChurchTypes.All)}\"");
        sheet.Range("D2:D2000").Style.NumberFormat.Format = "@";
        sheet.Range("G2:G2000").Style.NumberFormat.Format = "@";
        sheet.Columns(1, Headers.Length).Width = 22;

        var instructions = workbook.AddWorksheet("Instrucciones");
        string[] lines =
        [
            "Cómo llenar la hoja \"Iglesias\"",
            "",
            "Una fila por iglesia. Las columnas obligatorias son: Región, Distrito, Código iglesia, Nombre iglesia y Tipo.",
            "Código iglesia: el mismo código del ACMS. No puede repetirse entre Iglesias y Grupos.",
            "Una Filial lleva el código de su Iglesia o Grupo madre (en el ACMS no tiene código propio) y debe estar en el mismo distrito.",
            "Tipo: Iglesia, Grupo o Filial.",
            "Distrito misionero: Sí si es un DM (por ejemplo Patihipi o Villa Remedios); si no, No o vacío.",
            "Pastor: si se llena, van apellidos y nombres; el CI es opcional. Repite el mismo pastor en todas las iglesias de su distrito.",
            "Un distrito tiene un solo pastor y un pastor un solo distrito.",
            "",
            "Qué hace el sistema al importar:",
            "Crea las regiones, distritos, pastores e iglesias que no existan. Lo que ya existe no se modifica.",
            "Asigna el pastor al distrito si el distrito todavía no tiene pastor.",
            "Primero conviene usar \"Solo revisar\": muestra los errores y lo que se crearía, sin guardar nada.",
            "Si hay un solo error en el archivo, no se guarda nada.",
            "",
            "Ejemplo de fila:",
            "Altiplano | Curahuara | No | 1234567 | CONDORI CANAVIRI | Alvaro Gabriel | 618 | Curahuara | Iglesia"
        ];

        for (var line = 0; line < lines.Length; line++)
        {
            instructions.Cell(line + 1, 1).Value = lines[line];
        }

        instructions.Cell(1, 1).Style.Font.Bold = true;
        instructions.Cell(11, 1).Style.Font.Bold = true;
        instructions.Column(1).Width = 120;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ResponseGetObject> Import(Stream content, bool dryRun, DateOnly pastorStartDate)
    {
        var result = new ImportResultDto { DryRun = dryRun };

        List<ImportRow> rows;
        try
        {
            rows = ReadRows(content, result);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            result.Errors.Add(new ImportIssueDto { Row = 0, Message = "El archivo no es un Excel (.xlsx) válido." });
            return Finish(result);
        }

        result.TotalRows = rows.Count;

        if (rows.Count == 0 && result.Errors.Count == 0)
        {
            result.Errors.Add(new ImportIssueDto { Row = 0, Message = "La hoja \"Iglesias\" no tiene filas con datos." });
        }

        ValidateFileConsistency(rows, result);

        if (result.Errors.Count > 0)
        {
            return Finish(result);
        }

        // Lo que ya existe en la base, cargado en lote (no una consulta por fila).
        var existingRegions = await _unitOfWork.Repository<Region>().Query().ToListAsync();
        var regionByName = existingRegions.ToDictionary(r => r.Name, StringComparer.OrdinalIgnoreCase);

        var existingDistricts = await _unitOfWork.Repository<District>().Query().ToListAsync();
        var regionNameById = existingRegions.ToDictionary(r => r.RegionId, r => r.Name);
        var districtByKey = existingDistricts.ToDictionary(
            d => $"{regionNameById[d.RegionId]}|{d.Name}".ToUpperInvariant());
        var districtKeyById = districtByKey.ToDictionary(kv => kv.Value.DistrictId, kv => kv.Key);

        var existingPastors = await _unitOfWork.Repository<Pastor>().Query().ToListAsync();
        var pastorByKey = new Dictionary<string, Pastor>(StringComparer.OrdinalIgnoreCase);
        foreach (var pastor in existingPastors)
        {
            pastorByKey.TryAdd(PastorNameKey(pastor.LastNames, pastor.FirstNames), pastor);
            if (pastor.Ci is not null)
                pastorByKey[PastorCiKey(pastor.Ci)] = pastor;
        }

        var fileCodes = rows.Select(r => r.Code).Distinct().ToList();
        var existingChurches = await _unitOfWork.Repository<Church>().Query()
            .Where(c => fileCodes.Contains(c.Code))
            .ToListAsync();

        // Madres (Iglesias y Grupos) por código; las Filiales se reconocen por código y nombre.
        var parentByCode = existingChurches
            .Where(c => c.Type != ChurchTypes.Branch)
            .ToDictionary(c => c.Code, StringComparer.OrdinalIgnoreCase);
        var existingFilialKeys = existingChurches
            .Where(c => c.Type == ChurchTypes.Branch)
            .Select(c => $"{c.Code}|{c.Name}".ToUpperInvariant())
            .ToHashSet();

        var openAssignments = await _unitOfWork.Repository<DistrictPastor>().Query()
            .Where(dp => dp.EndDate == null)
            .ToListAsync();

        // Qué hay que crear.
        var newRegions = rows.Select(r => r.Region)
            .Where(name => !regionByName.ContainsKey(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => new Region { Name = name, IsActive = true, CreatedAt = DateTime.UtcNow })
            .ToList();

        var newDistrictRows = rows.GroupBy(r => r.DistrictKey)
            .Where(g => !districtByKey.ContainsKey(g.Key))
            .Select(g => g.First())
            .ToList();

        foreach (var row in rows.GroupBy(r => r.DistrictKey).Where(g => districtByKey.ContainsKey(g.Key)).Select(g => g.First()))
        {
            if (districtByKey[row.DistrictKey].IsMissionary != row.IsMissionary)
            {
                result.Warnings.Add(new ImportIssueDto { Row = row.RowNumber, Message = $"El distrito {row.District} ya existe con otro valor de distrito misionero; no se cambió." });
            }
        }

        // Pastores: se reconocen por CI y, si no tienen CI, por apellidos y nombres.
        var newPastorRows = new List<ImportRow>();
        foreach (var row in rows.Where(r => r.HasPastor).GroupBy(r => r.PastorKey, StringComparer.OrdinalIgnoreCase).Select(g => g.First()))
        {
            var existing = FindPastor(pastorByKey, row);
            if (existing is null)
            {
                newPastorRows.Add(row);
            }
            else if (!SameText(existing.LastNames, row.PastorLastNames) || !SameText(existing.FirstNames, row.PastorFirstNames))
            {
                result.Warnings.Add(new ImportIssueDto { Row = row.RowNumber, Message = $"El pastor con CI {row.PastorCi} ya existe como \"{existing.LastNames}, {existing.FirstNames}\"; se usa ese registro sin cambiar el nombre." });
            }
        }

        var newParentRows = new List<ImportRow>();
        var newFilialRows = new List<ImportRow>();
        var fileParentByCode = rows.Where(r => r.Type != ChurchTypes.Branch)
            .ToDictionary(r => r.Code, StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (row.Type != ChurchTypes.Branch)
            {
                if (parentByCode.ContainsKey(row.Code))
                {
                    result.ChurchesSkipped++;
                    result.Warnings.Add(new ImportIssueDto { Row = row.RowNumber, Message = $"La iglesia con código {row.Code} ya existe; no se modificó." });
                }
                else
                {
                    newParentRows.Add(row);
                }

                continue;
            }

            // La madre de una Filial es la Iglesia o Grupo con su mismo código, del archivo o de la base.
            var parentDistrictKey = fileParentByCode.TryGetValue(row.Code, out var fileParent)
                ? fileParent.DistrictKey
                : parentByCode.TryGetValue(row.Code, out var dbParent)
                    ? districtKeyById.GetValueOrDefault(dbParent.DistrictId)
                    : null;

            if (parentDistrictKey is null)
            {
                result.Errors.Add(new ImportIssueDto { Row = row.RowNumber, Message = $"La Filial {row.Name} tiene el código {row.Code}, pero no hay ninguna Iglesia o Grupo con ese código." });
            }
            else if (parentDistrictKey != row.DistrictKey)
            {
                result.Errors.Add(new ImportIssueDto { Row = row.RowNumber, Message = $"La Filial {row.Name} está en el distrito {row.District}, pero su madre (código {row.Code}) está en otro distrito." });
            }
            else if (existingFilialKeys.Contains($"{row.Code}|{row.Name}".ToUpperInvariant()))
            {
                result.ChurchesSkipped++;
                result.Warnings.Add(new ImportIssueDto { Row = row.RowNumber, Message = $"La Filial {row.Name} ya existe; no se modificó." });
            }
            else
            {
                newFilialRows.Add(row);
            }
        }

        if (result.Errors.Count > 0)
        {
            return Finish(result);
        }

        // Asignación de pastor: solo si el distrito no tiene pastor y el pastor no tiene distrito.
        var assignmentRows = new List<ImportRow>();
        foreach (var row in rows.Where(r => r.HasPastor).GroupBy(r => r.DistrictKey).Select(g => g.First()))
        {
            districtByKey.TryGetValue(row.DistrictKey, out var existingDistrict);
            var existingPastor = FindPastor(pastorByKey, row);

            var districtAssignment = existingDistrict is null ? null : openAssignments.FirstOrDefault(a => a.DistrictId == existingDistrict.DistrictId);
            var pastorAssignment = existingPastor is null ? null : openAssignments.FirstOrDefault(a => a.PastorId == existingPastor.PastorId);

            if (districtAssignment is not null && existingPastor is not null && districtAssignment.PastorId == existingPastor.PastorId)
            {
                continue;
            }

            if (districtAssignment is not null)
            {
                result.Warnings.Add(new ImportIssueDto { Row = row.RowNumber, Message = $"El distrito {row.District} ya tiene un pastor asignado; no se cambió. Hazlo desde la pantalla de distritos." });
                continue;
            }

            if (pastorAssignment is not null)
            {
                result.Warnings.Add(new ImportIssueDto { Row = row.RowNumber, Message = $"El pastor {row.PastorLastNames}, {row.PastorFirstNames} ya está a cargo de otro distrito; no se asignó a {row.District}." });
                continue;
            }

            assignmentRows.Add(row);
        }

        result.RegionsCreated = newRegions.Count;
        result.DistrictsCreated = newDistrictRows.Count;
        result.PastorsCreated = newPastorRows.Count;
        result.ChurchesCreated = newParentRows.Count;
        result.FilialsCreated = newFilialRows.Count;
        result.PastorAssignmentsCreated = assignmentRows.Count;

        if (dryRun)
        {
            return Finish(result);
        }

        // Se guarda por pasos (cada paso necesita los ids del anterior) dentro de una sola
        // transacción: si algo falla a la mitad no queda una carga a medias.
        await _unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            foreach (var region in newRegions)
            {
                await _unitOfWork.Repository<Region>().AddAsync(region);
                regionByName[region.Name] = region;
            }
            await _unitOfWork.SaveChangesAsync();

            foreach (var row in newDistrictRows)
            {
                var district = new District
                {
                    RegionId = regionByName[row.Region].RegionId,
                    Name = row.District,
                    IsMissionary = row.IsMissionary,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Repository<District>().AddAsync(district);
                districtByKey[row.DistrictKey] = district;
            }
            await _unitOfWork.SaveChangesAsync();

            foreach (var row in newPastorRows)
            {
                var pastor = new Pastor
                {
                    Ci = row.PastorCi,
                    LastNames = row.PastorLastNames!,
                    FirstNames = row.PastorFirstNames!,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.Repository<Pastor>().AddAsync(pastor);
                pastorByKey[row.PastorKey] = pastor;
            }
            await _unitOfWork.SaveChangesAsync();

            foreach (var row in newParentRows)
            {
                var church = NewChurch(row, districtByKey[row.DistrictKey].DistrictId, null);
                await _unitOfWork.Repository<Church>().AddAsync(church);
                parentByCode[row.Code] = church;
            }
            await _unitOfWork.SaveChangesAsync();

            foreach (var row in newFilialRows)
            {
                var parent = parentByCode[row.Code];
                await _unitOfWork.Repository<Church>().AddAsync(NewChurch(row, parent.DistrictId, parent));
            }

            foreach (var row in assignmentRows)
            {
                await _unitOfWork.Repository<DistrictPastor>().AddAsync(new DistrictPastor
                {
                    DistrictId = districtByKey[row.DistrictKey].DistrictId,
                    PastorId = pastorByKey[row.PastorKey].PastorId,
                    StartDate = pastorStartDate
                });
            }
            await _unitOfWork.SaveChangesAsync();
        });

        result.Saved = true;
        return Finish(result);
    }

    private static Church NewChurch(ImportRow row, long districtId, Church? parent) => new()
    {
        Code = parent?.Code ?? row.Code,
        Name = row.Name,
        Type = row.Type,
        DistrictId = districtId,
        ParentChurchId = parent?.ChurchId,
        Address = row.Address,
        Phone = row.Phone,
        TreasurerName = row.TreasurerName,
        TreasurerPhone = row.TreasurerPhone,
        ElderName = row.ElderName,
        ElderPhone = row.ElderPhone,
        Status = ChurchStatuses.Active,
        CreatedAt = DateTime.UtcNow
    };

    private static Pastor? FindPastor(Dictionary<string, Pastor> pastorByKey, ImportRow row)
    {
        if (row.PastorCi is not null && pastorByKey.TryGetValue(PastorCiKey(row.PastorCi), out var byCi))
            return byCi;

        return pastorByKey.GetValueOrDefault(PastorNameKey(row.PastorLastNames!, row.PastorFirstNames!));
    }

    private static string PastorCiKey(string ci) => $"CI:{ci}".ToUpperInvariant();

    private static string PastorNameKey(string lastNames, string firstNames) => $"NAME:{lastNames}|{firstNames}".ToUpperInvariant();

    private static List<ImportRow> ReadRows(Stream content, ImportResultDto result)
    {
        using var workbook = new XLWorkbook(content);
        var sheet = workbook.Worksheets.FirstOrDefault(w => w.Name.Equals("Iglesias", StringComparison.OrdinalIgnoreCase))
                    ?? workbook.Worksheet(1);

        for (var column = 0; column < Headers.Length; column++)
        {
            if (!SameText(Clean(sheet.Cell(1, column + 1).GetFormattedString()), Headers[column]))
            {
                result.Errors.Add(new ImportIssueDto { Row = 1, Message = $"La columna {column + 1} debería ser \"{Headers[column]}\". Usa la plantilla descargada del sistema." });
                return [];
            }
        }

        var rows = new List<ImportRow>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var values = Enumerable.Range(1, Headers.Length)
                .Select(column => Clean(sheet.Cell(rowNumber, column).GetFormattedString()))
                .ToArray();

            if (values.All(string.IsNullOrEmpty))
                continue;

            var rowErrors = new List<string>();

            void Required(int index)
            {
                if (string.IsNullOrEmpty(values[index]))
                    rowErrors.Add($"Falta \"{Headers[index]}\".");
            }

            Required(0);
            Required(1);
            Required(6);
            Required(7);

            var missionaryText = values[2].ToLowerInvariant();
            var isMissionary = YesValues.Contains(missionaryText);
            if (!isMissionary && !NoValues.Contains(missionaryText))
                rowErrors.Add($"\"{Headers[2]}\" debe ser Sí o No.");

            var type = ChurchTypes.All.FirstOrDefault(t => SameText(t, values[8]));
            if (type is null)
                rowErrors.Add($"\"{Headers[8]}\" debe ser {string.Join(", ", ChurchTypes.All)}.");

            var hasPastor = new[] { values[3], values[4], values[5] }.Any(v => !string.IsNullOrEmpty(v));
            if (hasPastor && (string.IsNullOrEmpty(values[4]) || string.IsNullOrEmpty(values[5])))
                rowErrors.Add("Si se llena el pastor, van sus apellidos y nombres (el CI es opcional).");

            CheckLength(values, 0, 100, rowErrors);
            CheckLength(values, 1, 100, rowErrors);
            CheckLength(values, 3, 20, rowErrors);
            CheckLength(values, 4, 100, rowErrors);
            CheckLength(values, 5, 100, rowErrors);
            CheckLength(values, 6, 20, rowErrors);
            CheckLength(values, 7, 150, rowErrors);
            CheckLength(values, 9, 255, rowErrors);
            CheckLength(values, 10, 30, rowErrors);
            CheckLength(values, 11, 150, rowErrors);
            CheckLength(values, 12, 30, rowErrors);
            CheckLength(values, 13, 150, rowErrors);
            CheckLength(values, 14, 30, rowErrors);

            if (rowErrors.Count > 0)
            {
                result.Errors.AddRange(rowErrors.Select(message => new ImportIssueDto { Row = rowNumber, Message = message }));
                continue;
            }

            rows.Add(new ImportRow(
                rowNumber,
                values[0],
                values[1],
                isMissionary,
                hasPastor ? NullIfEmpty(values[3]) : null,
                hasPastor ? values[4] : null,
                hasPastor ? values[5] : null,
                values[6],
                values[7],
                type!,
                NullIfEmpty(values[9]),
                NullIfEmpty(values[10]),
                NullIfEmpty(values[11]),
                NullIfEmpty(values[12]),
                NullIfEmpty(values[13]),
                NullIfEmpty(values[14])));
        }

        return rows;
    }

    /// <summary>Reglas que dependen de varias filas a la vez.</summary>
    private static void ValidateFileConsistency(List<ImportRow> rows, ImportResultDto result)
    {
        // El código es único entre Iglesias y Grupos; las Filiales repiten el de su madre.
        foreach (var group in rows.Where(r => r.Type != ChurchTypes.Branch).GroupBy(r => r.Code, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
        {
            result.Errors.Add(new ImportIssueDto { Row = group.Skip(1).First().RowNumber, Message = $"El código {group.Key} está repetido en más de una Iglesia o Grupo (filas {string.Join(", ", group.Select(r => r.RowNumber))})." });
        }

        foreach (var group in rows.Where(r => r.Type == ChurchTypes.Branch).GroupBy(r => $"{r.Code}|{r.Name}".ToUpperInvariant()).Where(g => g.Count() > 1))
        {
            result.Errors.Add(new ImportIssueDto { Row = group.Skip(1).First().RowNumber, Message = $"La Filial {group.First().Name} está repetida (filas {string.Join(", ", group.Select(r => r.RowNumber))})." });
        }

        foreach (var group in rows.GroupBy(r => r.DistrictKey))
        {
            if (group.Select(r => r.IsMissionary).Distinct().Count() > 1)
            {
                result.Errors.Add(new ImportIssueDto { Row = group.First().RowNumber, Message = $"El distrito {group.First().District} está marcado como misionero en unas filas y en otras no." });
            }

            var pastors = group.Where(r => r.HasPastor).Select(r => $"{r.PastorLastNames}, {r.PastorFirstNames}").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (pastors.Count > 1)
            {
                result.Errors.Add(new ImportIssueDto { Row = group.First().RowNumber, Message = $"El distrito {group.First().District} tiene más de un pastor ({string.Join("; ", pastors)}). Un distrito tiene un solo pastor." });
            }
        }

        foreach (var group in rows.Where(r => r.HasPastor).GroupBy(r => r.PastorKey, StringComparer.OrdinalIgnoreCase))
        {
            var first = group.First();
            if (group.Select(r => r.DistrictKey).Distinct().Count() > 1)
            {
                result.Errors.Add(new ImportIssueDto { Row = first.RowNumber, Message = $"El pastor {first.PastorLastNames}, {first.PastorFirstNames} aparece en más de un distrito. Un pastor tiene un solo distrito." });
            }

            if (group.Select(r => $"{r.PastorLastNames}|{r.PastorFirstNames}".ToUpperInvariant()).Distinct().Count() > 1)
            {
                result.Errors.Add(new ImportIssueDto { Row = first.RowNumber, Message = $"El pastor con CI {first.PastorCi} está escrito con nombres distintos en el archivo." });
            }
        }
    }

    private static ResponseGetObject Finish(ImportResultDto result)
    {
        if (result.Errors.Count > 0)
        {
            return new ResponseGetObject
            {
                Data = result,
                Messages = [new Message { Type = MessageType.Error, Description = $"El archivo tiene {result.Errors.Count} error(es). No se guardó nada." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var description = result.DryRun
            ? $"Revisión correcta: se crearían {result.ChurchesCreated} Iglesia(s) o Grupo(s) y {result.FilialsCreated} Filial(es). No se guardó nada."
            : $"Importación terminada: {result.ChurchesCreated} Iglesia(s) o Grupo(s) y {result.FilialsCreated} Filial(es) creadas.";

        return new ResponseGetObject
        {
            Data = result,
            Messages = [new Message { Type = MessageType.Success, Description = description }],
            StatusCode = result.DryRun ? HttpStatusCode.OK : HttpStatusCode.Created
        };
    }

    private static void CheckLength(string[] values, int index, int maxLength, List<string> rowErrors)
    {
        if (values[index].Length > maxLength)
            rowErrors.Add($"\"{Headers[index]}\" no puede tener más de {maxLength} caracteres.");
    }

    // Quita espacios al inicio y al final y junta los espacios dobles: en el Excel original hay
    // nombres como "PINTO , Mario" o "Fabril " que de otro modo contarían como distintos.
    private static string Clean(string value) => MultipleSpaces().Replace(value.Trim(), " ");

    private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;

    private static bool SameText(string? a, string? b) => string.Equals(a?.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex MultipleSpaces();
}
