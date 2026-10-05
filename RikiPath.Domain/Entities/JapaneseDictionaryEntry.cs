namespace RikiPath.Domain.Entities;

public class JapaneseDictionaryEntry : Base
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public UserAccount? UserAccount { get; set; }

    public string Surface { get; set; } = string.Empty;
    public string ReadingKana { get; set; } = string.Empty;
    public string ReadingRomaji { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public string? PartOfSpeech { get; set; }
    public bool IsKatakana { get; set; }
    public string Source { get; set; } = "JMdict";
}
