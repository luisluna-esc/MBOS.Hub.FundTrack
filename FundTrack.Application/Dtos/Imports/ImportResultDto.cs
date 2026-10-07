namespace FundTrack.Application.Dtos.Imports;

public class ImportResultDto
{
    /// <summary>True si solo se revisó el archivo sin guardar nada.</summary>
    public bool DryRun { get; set; }
    public bool Saved { get; set; }
    public int TotalRows { get; set; }
    public int RegionsCreated { get; set; }
    public int DistrictsCreated { get; set; }
    public int PastorsCreated { get; set; }
    /// <summary>Iglesias y Grupos creados.</summary>
    public int ChurchesCreated { get; set; }
    public int FilialsCreated { get; set; }
    public int PastorAssignmentsCreated { get; set; }
    /// <summary>Iglesias y Filiales del archivo que ya existían: no se modifican.</summary>
    public int ChurchesSkipped { get; set; }
    /// <summary>Problemas que impiden guardar. Si hay alguno no se guarda nada.</summary>
    public List<ImportIssueDto> Errors { get; set; } = [];
    /// <summary>Avisos que no impiden guardar (por ejemplo, datos que ya existían y no se cambiaron).</summary>
    public List<ImportIssueDto> Warnings { get; set; } = [];
}

public class ImportIssueDto
{
    /// <summary>Número de fila del Excel (la primera fila de datos es la 2).</summary>
    public int Row { get; set; }
    public string Message { get; set; } = string.Empty;
}
