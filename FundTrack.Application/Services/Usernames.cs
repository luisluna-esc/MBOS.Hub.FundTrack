using System.Globalization;
using System.Text;

namespace FundTrack.Application.Services;

/// <summary>
/// El usuario con que se entra al sistema: nombre.apellido, en minúsculas, sin tildes ni eñes
/// (ej. "Juan Carlos Mamani Quispe" da juan.mamani). Si ya existe, se le agrega un número.
/// </summary>
public static class Usernames
{
    public const string Pattern = "^[a-z]+\\.[a-z]+[0-9]*$";

    public static string Normalize(string username) => username.Trim().ToLowerInvariant();

    /// <summary>
    /// Nombre y primer apellido: con 4 palabras o más se toma la tercera (dos nombres y dos
    /// apellidos); con 2 o 3, la segunda.
    /// </summary>
    public static string FromFullName(string fullName)
    {
        var words = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(Clean)
            .Where(w => w.Length > 0)
            .ToList();

        if (words.Count == 0)
            return "usuario.nuevo";
        if (words.Count == 1)
            return $"{words[0]}.usuario";

        var lastName = words.Count >= 4 ? words[2] : words[1];
        return $"{words[0]}.{lastName}";
    }

    private static string Clean(string word)
    {
        var builder = new StringBuilder();
        foreach (var c in word.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
                continue;
            var lower = char.ToLowerInvariant(c);
            if (lower is >= 'a' and <= 'z')
                builder.Append(lower);
        }
        return builder.ToString();
    }
}
