namespace FundTrack.core.Constants;

public static class RoleNames
{
    /// <summary>Superusuario: el único rol que administra usuarios, roles y menús.</summary>
    public const string Admin = "M-BOS";
    public const string Analyst = "Analista";
    public const string Administrator = "Administrador";
    public const string Treasurer = "Tesorero";
    public const string Auditor = "Auditor";
    /// <summary>Solo ve los datos del distrito que tiene a cargo.</summary>
    public const string Pastor = "Pastor";

    /// <summary>Roles que registran y modifican datos; el resto solo visualiza.</summary>
    public const string Writers = Admin + "," + Analyst;

    /// <summary>Roles que pueden consultar la auditoría.</summary>
    public const string AuditViewers = Admin + "," + Analyst + "," + Administrator;
}
