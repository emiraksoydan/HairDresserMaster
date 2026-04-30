using System.Text.Json;

namespace Business.Helpers
{
    /// <summary>Kayıtlı filtre JSON — düz veya { schemaVersion, criteria } sarmalayıcı.</summary>
    public static class SavedFilterCriteriaValidator
    {
        public static bool IsValidCriteriaJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return false;

            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                    return false;

                // v1: { "schemaVersion": 1, "criteria": { ... } }
                if (doc.RootElement.TryGetProperty("criteria", out var crit))
                    return crit.ValueKind == JsonValueKind.Object;

                // Eski düz kriter nesnesi — en az bir property beklenir (boş {} bile object sayılır)
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
