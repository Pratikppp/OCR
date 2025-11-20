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

        // ============================================================================
        // STEP 1: EXTRACT EASY-TO-FIND FIELDS WITH IMPROVED PATTERNS
        // ============================================================================
        
        ExtractBasicFields(textLines, result);
        
        // ============================================================================
        // STEP 2: SMART PERSON DATA EXTRACTION
        // ============================================================================
        
        ExtractPersonData(textLines, result);

        // ============================================================================
        // STEP 3: EXTRACT AGE AND GENDER FROM CPR
        // ============================================================================
        
        ExtractAgeAndGenderFromCpr(result);

        return result;
    }

    private static Dictionary<string, string> InitializeResultDictionary()
    {
        return new Dictionary<string, string>
        {
            ["holder_name"] = "",
            ["holder_address"] = "",
            ["holder_postal_city"] = "",
            ["cpr"] = "",
            ["doctor_name"] = "",
            ["doctor_address"] = "",
            ["doctor_phone"] = "",
            ["clinic_name"] = "", 
            ["clinic_address"] = "",
            ["municipality"] = "",
            ["region"] = "",
            ["valid_from"] = "",
            ["card_type"] = "",
            ["date_of_birth"] = "", // New field
            ["age"] = "", // New field
            ["gender"] = "" // New field
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
        
        // Municipality (look for "Kommune" in the text)
        result["municipality"] = textLines.FirstOrDefault(t => 
            t.Contains("Kommune", StringComparison.OrdinalIgnoreCase)) ?? "";
        
        // Valid from date
        result["valid_from"] = textLines.FirstOrDefault(t => 
            Regex.IsMatch(t, @"\d{2}\.\d{2}\.\d{4}")) ?? "";
        
        // Postal code and city (Danish format: 4 digits + space + city name)
        result["holder_postal_city"] = textLines.FirstOrDefault(t => 
            Regex.IsMatch(t, @"^\d{4} [A-ZÆØÅ]")) ?? "";

        // Card type
        result["card_type"] = textLines.FirstOrDefault(t => 
            t.Contains("Sundhedskort", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Rejsesygesikring", StringComparison.OrdinalIgnoreCase) ||
            t.Contains("Health Card", StringComparison.OrdinalIgnoreCase)) ?? "Danish Health Card";

        // Phone numbers - improved detection
        var phoneLines = textLines.Where(t => 
            t.Contains("Tlf.") || t.Contains("Tif.") || t.Contains("Tel.") ||
            t.Contains("Mobil:") || Regex.IsMatch(t, @"\d{2} \d{2} \d{2} \d{2}"));
        
        foreach (var phoneLine in phoneLines)
        {
            var cleanPhone = phoneLine
                .Replace("Tlf.", "").Replace("Tif.", "").Replace("Tel.", "")
                .Replace("Mobil:", "").Trim();
            
            if (Regex.IsMatch(cleanPhone, @"\d{2} \d{2} \d{2} \d{2}"))
            {
                if (string.IsNullOrEmpty(result["doctor_phone"]))
                    result["doctor_phone"] = cleanPhone;
            }
        }
    }

    private static void ExtractPersonData(List<string> textLines, Dictionary<string, string> result)
    {
        var cprIndex = textLines.FindIndex(t => Regex.IsMatch(t, @"\d{6}-\d{4}"));
        
        if (cprIndex == -1) return;

        // ============================================================================
        // STRATEGY: Analyze text around CPR number
        // ============================================================================
        
        // Look for HOLDER (person) below CPR
        ExtractHolderData(textLines, cprIndex, result);
        
        // Look for CLINIC/DOCTOR above CPR  
        ExtractClinicData(textLines, cprIndex, result);
        
        // Fallback: if holder not found, look for any person name
        if (string.IsNullOrEmpty(result["holder_name"]))
        {
            FindPersonByNamePattern(textLines, result);
        }
    }

    private static void ExtractHolderData(List<string> textLines, int cprIndex, Dictionary<string, string> result)
    {
        // Look for holder information BELOW CPR number
        for (int i = cprIndex + 1; i < textLines.Count; i++)
        {
            var line = textLines[i];
            
            // Skip header lines and dates
            if (IsHeaderLine(line) || IsDateLine(line)) continue;
            
            // Found a person name
            if (IsPersonName(line) && string.IsNullOrEmpty(result["holder_name"]))
            {
                result["holder_name"] = line;
                
                // Look for address in next lines
                if (i + 1 < textLines.Count && IsAddress(textLines[i + 1]))
                {
                    result["holder_address"] = textLines[i + 1];
                    
                    // Look for postal/city in next line
                    if (i + 2 < textLines.Count && IsPostalCity(textLines[i + 2]))
                    {
                        result["holder_postal_city"] = textLines[i + 2];
                    }
                }
                break;
            }
        }
    }

    private static void ExtractClinicData(List<string> textLines, int cprIndex, Dictionary<string, string> result)
    {
        // Look for clinic/doctor information ABOVE CPR number
        for (int i = cprIndex - 1; i >= 0; i--)
        {
            var line = textLines[i];
            
            if (IsHeaderLine(line)) continue;
            
            // Found clinic name (often contains "Laegehus", "Lægehus", "Clinic", etc.)
            if (IsClinicName(line) && string.IsNullOrEmpty(result["clinic_name"]))
            {
                result["clinic_name"] = line;
                result["doctor_name"] = line; // Use clinic name as doctor name for now
                
                // Look for clinic address
                if (i + 1 < textLines.Count && IsAddress(textLines[i + 1]))
                {
                    result["clinic_address"] = textLines[i + 1];
                    result["doctor_address"] = textLines[i + 1];
                }
                break;
            }
            
            // Found a person name (could be actual doctor name)
            if (IsPersonName(line) && string.IsNullOrEmpty(result["doctor_name"]))
            {
                result["doctor_name"] = line;
                
                // Look for doctor address
                if (i + 1 < textLines.Count && IsAddress(textLines[i + 1]))
                {
                    result["doctor_address"] = textLines[i + 1];
                }
                break;
            }
        }
    }

    private static void FindPersonByNamePattern(List<string> textLines, Dictionary<string, string> result)
    {
        // Fallback: find any person name that looks like "Firstname Lastname"
        var potentialNames = textLines
            .Where(IsPersonName)
            .Where(name => !IsClinicName(name)) // Exclude clinic names
            .ToList();

        if (potentialNames.Count > 0)
        {
            result["holder_name"] = potentialNames[0];
            
            // Try to find associated address
            var nameIndex = textLines.IndexOf(potentialNames[0]);
            if (nameIndex != -1 && nameIndex + 1 < textLines.Count && IsAddress(textLines[nameIndex + 1]))
            {
                result["holder_address"] = textLines[nameIndex + 1];
            }
        }
    }

    // ============================================================================
    // CPR PARSING FOR AGE AND GENDER
    // ============================================================================
    
    private static void ExtractAgeAndGenderFromCpr(Dictionary<string, string> result)
{
    var cpr = result["cpr"];
    
    if (string.IsNullOrEmpty(cpr) || cpr.Length <= 10)
        return;

    try
    {
        // Clean the CPR - remove any non-digit characters except hyphen
        var cleanCpr = Regex.Replace(cpr, @"[^\d-]", "");
        
        // Extract the 10-digit CPR number
        var cprDigits = cleanCpr.Replace("-", "");
        if (cprDigits.Length != 10)
            return;

        // Extract date parts from CPR (DDMMYYXXXX)
        var day = int.Parse(cprDigits.Substring(0, 2));
        var month = int.Parse(cprDigits.Substring(2, 2));
        var year = int.Parse(cprDigits.Substring(4, 2));
        
        // CORRECTED: Get the LAST digit (10th digit) for gender determination
        var lastDigit = int.Parse(cprDigits.Substring(9, 1)); // This is the gender digit

        Console.WriteLine($"CPR: {cprDigits}, Last digit: {lastDigit}"); // Debug log

        // Determine century and create full birth date
        var birthDate = DetermineBirthDate(day, month, year);
        result["date_of_birth"] = birthDate.ToString("dd.MM.yyyy");

        // Calculate age
        var today = DateTime.Today;
        var age = today.Year - birthDate.Year;
        if (birthDate.Date > today.AddYears(-age)) age--;
        result["age"] = age.ToString();

        // CORRECTED: Determine gender (odd = male, even = female)
        result["gender"] = (lastDigit % 2 == 1) ? "Male" : "Female";
        
        Console.WriteLine($"Gender determined: {result["gender"]} (last digit: {lastDigit})"); // Debug log
    }
    catch (Exception ex)
    {
        // If CPR parsing fails, log but don't crash
        Console.WriteLine($"Error parsing CPR {cpr}: {ex.Message}");
    }
}
   private static DateTime DetermineBirthDate(int day, int month, int year)
{
    // Danish CPR century determination rules:
    // 00-36: 2000-2036
    // 37-99: 1937-1999
    
    int fullYear;
    
    if (year >= 0 && year <= 36)
    {
        fullYear = 2000 + year;
    }
    else if (year >= 37 && year <= 99)
    {
        fullYear = 1900 + year;
    }
    else
    {
        throw new ArgumentException($"Invalid year in CPR: {year}");
    }

    // Validate and create date
    if (month < 1 || month > 12)
        throw new ArgumentException($"Invalid month in CPR: {month}");
        
    if (day < 1 || day > DateTime.DaysInMonth(fullYear, month))
        throw new ArgumentException($"Invalid day in CPR: {day}");

    return new DateTime(fullYear, month, day);
}

    // ============================================================================
    // IMPROVED PATTERN RECOGNITION METHODS
    // ============================================================================
    
    private static bool IsPersonName(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return false;
        if (line.Length < 3) return false;
        if (Regex.IsMatch(line, @"\d")) return false; // No numbers in names
        
        // Person names typically have space between first and last name
        bool hasSpace = line.Contains(' ');
        bool isMostlyUpperCase = line == line.ToUpper();
        
        // Exclude obvious non-names
        bool isNotName = IsHeaderLine(line) || IsClinicName(line) || IsAddress(line);
        
        return hasSpace && !isNotName && (isMostlyUpperCase || IsMixedCaseName(line));
    }

    private static bool IsMixedCaseName(string line)
    {
        // Handle names like "Birka Synnove Abildgaard" (mixed case)
        if (!line.Contains(' ')) return false;
        
        var words = line.Split(' ');
        return words.All(word => word.Length >= 2 && char.IsUpper(word[0])) &&
               !Regex.IsMatch(line, @"\d");
    }

    private static bool IsClinicName(string line)
    {
        var clinicIndicators = new[]
        {
            "LAEGEHUS", "LÆGEHUS", "LEGEHUS", "CLINIC", "MEDICAL", "CENTER",
            "HOSPITAL", "PRAKSIS", "DOCTOR", "LEGEN", "LÆGE"
        };

        return clinicIndicators.Any(indicator => 
            line.Contains(indicator, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAddress(string line)
    {
        return !string.IsNullOrEmpty(line) && 
               (line.Contains("gade", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("vej", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("allé", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("plads", StringComparison.OrdinalIgnoreCase) ||
                Regex.IsMatch(line, @",\s*\d")) && // Contains comma followed by number
               !IsHeaderLine(line) &&
               !IsDateLine(line) &&
               !Regex.IsMatch(line, @"\d{6}-\d{4}"); // Not CPR
    }

    private static bool IsPostalCity(string line)
    {
        return Regex.IsMatch(line, @"^\d{4} [A-ZÆØÅ]");
    }

    private static bool IsHeaderLine(string line)
    {
        var headers = new[]
        {
            "REGION", "KOMMUNE", "SUNDHEDSKORT", "REJSESYGESIKRINGSKORT",
            "CERTIFICADO", "TOURIST", "HEALTH", "INSURANCE", "CARD",
            "GYLDIG FRA", "VALID FROM", "TELEFON", "TELEFAX", "INTERNET"
        };

        return headers.Any(header => line.Contains(header, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDateLine(string line)
    {
        return Regex.IsMatch(line, @"\d{2}\.\d{2}\.\d{4}") ||
               line.Contains("Gyldig fra", StringComparison.OrdinalIgnoreCase) ||
               line.Contains("Valid from", StringComparison.OrdinalIgnoreCase);
    }
}