using Amazon.Textract;
using Amazon.Textract.Model;
using System.Text.RegularExpressions;

namespace HealthCardApi.Mappers;

public static class HealthCardMapper
{
    public static Dictionary<string, string> Map(List<Block> blocks)
    {
        var textLines = blocks
            .Where(b => b.BlockType == BlockType.LINE)
            .Select(b => b.Text)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        var result = InitializeResultDictionary();

        // Extract basic fields
        ExtractBasicFields(textLines, result);
        
        // Extract person data
        ExtractPersonData(textLines, result);

        // Extract age and gender from CPR
        ExtractAgeAndGenderFromCpr(result);

        // Split name into first name and surname
        SplitNameFields(result);

        // Split postal code and city
        SplitPostalCodeAndCity(result);

        return result;
    }

    private static Dictionary<string, string> InitializeResultDictionary()
    {
        return new Dictionary<string, string>
        {
            ["holder_first_name"] = "",
            ["holder_surname"] = "",
            ["holder_name"] = "",
            ["holder_address"] = "",
            ["postal_code"] = "",
            ["city"] = "",
            ["holder_postal_city"] = "",
            ["cpr"] = "",
            ["doctor_name"] = "",
            ["doctor_address"] = "",
            ["doctor_phone"] = "",
            ["municipality"] = "",
            ["region"] = "",
            ["valid_from"] = "",
            ["date_of_birth"] = "",
            ["age"] = "",
            ["gender"] = ""
        };
    }

    private static void ExtractBasicFields(List<string> textLines, Dictionary<string, string> result)
    {
        // CPR number (Danish format: DDMMYY-XXXX)
        var cprMatch = textLines.FirstOrDefault(t => Regex.IsMatch(t, @"\d{6}-\d{4}"));
        if (cprMatch != null)
        {
            result["cpr"] = cprMatch.Split('*')[0].Trim();
        }
        
        // Region
        result["region"] = textLines.FirstOrDefault(t => 
            t.Contains("Region", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Hovedstaden", StringComparison.OrdinalIgnoreCase)) ?? "";
        
        // Municipality
        result["municipality"] = textLines.FirstOrDefault(t => 
            t.Contains("Kommune", StringComparison.OrdinalIgnoreCase)) ?? "";
        
        // Valid from date
        result["valid_from"] = textLines.FirstOrDefault(t => 
            Regex.IsMatch(t, @"\d{2}\.\d{2}\.\d{4}")) ?? "";
        
        // Postal code and city (keep original for splitting later)
        result["holder_postal_city"] = textLines.FirstOrDefault(t => 
            Regex.IsMatch(t, @"^\d{4} [A-ZÆØÅ]")) ?? "";

        // Phone numbers
        var phoneLines = textLines.Where(t => 
            t.Contains("Tlf.") || t.Contains("Tif.") || t.Contains("Tel.") ||
            (Regex.IsMatch(t, @"\b\d{2} \d{2} \d{2} \d{2}\b") && !t.Contains("TH.")));

        foreach (var phoneLine in phoneLines)
        {
            var cleanPhone = phoneLine
                .Replace("Tlf.", "").Replace("Tif.", "").Replace("Tel.", "")
                .Replace("Mobil:", "").Trim();
            
            var phoneMatch = Regex.Match(cleanPhone, @"\b\d{2} \d{2} \d{2} \d{2}\b");
            if (phoneMatch.Success)
            {
                if (string.IsNullOrEmpty(result["doctor_phone"]))
                    result["doctor_phone"] = phoneMatch.Value;
            }
        }
    }

    private static void ExtractPersonData(List<string> textLines, Dictionary<string, string> result)
    {
        var cprIndex = textLines.FindIndex(t => Regex.IsMatch(t, @"\d{6}-\d{4}"));
        
        if (cprIndex == -1) return;

        // Look for HOLDER below CPR
        for (int i = cprIndex + 1; i < textLines.Count; i++)
        {
            var line = textLines[i];
            
            if (IsHeaderLine(line) || IsDateLine(line)) continue;
            
            if (IsPersonName(line) && string.IsNullOrEmpty(result["holder_name"]))
            {
                result["holder_name"] = line;
                
                if (i + 1 < textLines.Count && IsAddress(textLines[i + 1]))
                {
                    result["holder_address"] = textLines[i + 1];
                    
                    if (i + 2 < textLines.Count && IsPostalCity(textLines[i + 2]))
                    {
                        result["holder_postal_city"] = textLines[i + 2];
                    }
                }
                break;
            }
        }

        // Look for CLINIC/DOCTOR above CPR
        for (int i = cprIndex - 1; i >= 0; i--)
        {
            var line = textLines[i];
            
            if (IsHeaderLine(line)) continue;
            
            if (IsClinicName(line) && string.IsNullOrEmpty(result["doctor_name"]))
            {
                result["doctor_name"] = line;
                
                if (i + 1 < textLines.Count && IsAddress(textLines[i + 1]))
                {
                    result["doctor_address"] = textLines[i + 1];
                }
                break;
            }
            
            if (IsPersonName(line) && string.IsNullOrEmpty(result["doctor_name"]))
            {
                result["doctor_name"] = line;
                
                if (i + 1 < textLines.Count && IsAddress(textLines[i + 1]))
                {
                    result["doctor_address"] = textLines[i + 1];
                }
                break;
            }
        }
    }

    private static void SplitNameFields(Dictionary<string, string> result)
    {
        var fullName = result["holder_name"];
        if (string.IsNullOrEmpty(fullName)) return;

        // Split name by spaces
        var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (nameParts.Length >= 2)
        {
            // First name is all parts except the last one
            result["holder_first_name"] = string.Join(" ", nameParts.Take(nameParts.Length - 1));
            // Surname is the last part
            result["holder_surname"] = nameParts.Last();
        }
        else if (nameParts.Length == 1)
        {
            // If only one name part, use it as first name
            result["holder_first_name"] = nameParts[0];
        }
    }

    private static void SplitPostalCodeAndCity(Dictionary<string, string> result)
    {
        var postalCity = result["holder_postal_city"];
        if (string.IsNullOrEmpty(postalCity)) return;

        // Danish format: "2300 København S"
        var match = Regex.Match(postalCity, @"^(\d{4})\s+(.+)$");
        if (match.Success)
        {
            result["postal_code"] = match.Groups[1].Value;
            result["city"] = match.Groups[2].Value.Trim();
        }
    }

    private static void ExtractAgeAndGenderFromCpr(Dictionary<string, string> result)
    {
        var cpr = result["cpr"];
        
        if (string.IsNullOrEmpty(cpr) || cpr.Length < 10)
            return;

        try
        {
            // Extract digits only
            var digitsOnly = new string(cpr.Where(char.IsDigit).ToArray());
            
            if (digitsOnly.Length != 10) return;

            // Parse date parts
            var day = int.Parse(digitsOnly.Substring(0, 2));
            var month = int.Parse(digitsOnly.Substring(2, 2));
            var year = int.Parse(digitsOnly.Substring(4, 2));
            
            // Get gender digit
            var genderDigit = int.Parse(digitsOnly.Substring(9, 1));

            // Determine century
            int fullYear = (year <= 36) ? 2000 + year : 1900 + year;
            
            var birthDate = new DateTime(fullYear, month, day);
            result["date_of_birth"] = birthDate.ToString("dd.MM.yyyy");

            // Calculate age
            var today = DateTime.Today;
            var age = today.Year - birthDate.Year;
            if (birthDate.Date > today.AddYears(-age)) age--;
            result["age"] = age.ToString();

            // Gender determination
            result["gender"] = (genderDigit % 2 == 1) ? "Male" : "Female";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CPR parsing error for '{cpr}': {ex.Message}");
        }
    }

    // Helper methods (keep your existing ones)
    private static bool IsPersonName(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        if (line.Length < 3) return false;
        if (Regex.IsMatch(line, @"\d")) return false;
        
        bool hasSpace = line.Contains(' ');
        bool isMostlyUpperCase = line == line.ToUpper();
        
        bool isNotName = IsHeaderLine(line) || IsClinicName(line) || IsAddress(line);
        
        return hasSpace && !isNotName && (isMostlyUpperCase || IsMixedCaseName(line));
    }

    private static bool IsMixedCaseName(string line)
    {
        if (!line.Contains(' ')) return false;
        var words = line.Split(' ');
        return words.All(word => word.Length >= 2 && char.IsUpper(word[0])) &&
               !Regex.IsMatch(line, @"\d");
    }

    private static bool IsClinicName(string line)
    {
        var clinicIndicators = new[] { "LAEGEHUS", "LÆGEHUS", "LEGEHUS", "CLINIC", "MEDICAL", "CENTER", "HOSPITAL" };
        return clinicIndicators.Any(indicator => line.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAddress(string line)
    {
        return !string.IsNullOrEmpty(line) && 
               (line.Contains("gade", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("vej", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("allé", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(line, @",\s*\d")) &&
               !IsHeaderLine(line) &&
               !IsDateLine(line) &&
               !Regex.IsMatch(line, @"\d{6}-\d{4}");
    }

    private static bool IsPostalCity(string line)
    {
        return Regex.IsMatch(line, @"^\d{4} [A-ZÆØÅ]");
    }

    private static bool IsHeaderLine(string line)
    {
        var headers = new[] { "REGION", "KOMMUNE", "SUNDHEDSKORT", "REJSESYGESIKRINGSKORT", "CERTIFICADO", "TOURIST", "HEALTH", "INSURANCE", "CARD", "GYLDIG FRA", "VALID FROM" };
        return headers.Any(header => line.Contains(header, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDateLine(string line)
    {
        return Regex.IsMatch(line, @"\d{2}\.\d{2}\.\d{4}") ||
               line.Contains("Gyldig fra", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("Valid from", StringComparison.OrdinalIgnoreCase);
    }
}