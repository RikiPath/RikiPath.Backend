namespace RikiPath.Application.Responses.JapaneseDictionary;

public class JapaneseDictionaryEntryResponse
{
    public int Id { get; set; }
    public string Surface { get; set; } = string.Empty;
    public string ReadingKana { get; set; } = string.Empty;
    public string ReadingRomaji { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string? PartOfSpeech { get; set; }
    public bool IsKatakana { get; set; }
    public bool IsPersonal { get; set; }
}

public class CreatePersonalDictionaryEntryRequest
{
    public string Surface { get; set; } = string.Empty;
    public string ReadingKana { get; set; } = string.Empty;
    public string ReadingRomaji { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string? PartOfSpeech { get; set; }
    public bool IsKatakana { get; set; }
}
