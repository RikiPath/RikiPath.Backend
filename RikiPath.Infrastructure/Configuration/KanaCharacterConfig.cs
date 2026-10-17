using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RikiPath.Domain.Entities;
using RikiPath.Domain.Enums;

namespace RikiPath.Infrastructure.Configuration
{
    public class KanaCharacterConfig : IEntityTypeConfiguration<KanaCharacter>
    {
        public void Configure(EntityTypeBuilder<KanaCharacter> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Character).IsRequired().HasMaxLength(10);
            builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            builder.Property(x => x.Romaji).IsRequired().HasMaxLength(20);
            builder.HasIndex(x => new { x.Character, x.Type }).IsUnique();

            // Seed Data cho 46 chữ Hiragana và 46 chữ Katakana
            builder.HasData(GetSeedKanaCharacters());
        }

        private static List<KanaCharacter> GetSeedKanaCharacters()
        {
            const string baseAudioUrl = "https://upload.wikimedia.org/wikipedia/commons";
            var result = new List<KanaCharacter>();
            int currentId = 1;

            // 1. DANH SÁCH HIRAGANA (46 Chữ cơ bản - Id từ 1 đến 46)
            var hiraganaData = new (string Character, string Romaji, int Strokes, string AudioPath)[]
            {
                // Hàng A
                ("あ", "a", 3, "/2/22/Ja-a.ogg"),
                ("い", "i", 2, "/2/28/Ja-i.ogg"),
                ("う", "u", 2, "/a/a2/Ja-u.ogg"),
                ("え", "e", 2, "/2/21/Ja-e.ogg"),
                ("お", "o", 3, "/d/df/Ja-o.ogg"),

                // Hàng Ka
                ("か", "ka", 3, "/2/2e/Ja-ka.ogg"),
                ("き", "ki", 4, "/8/87/Ja-ki.ogg"),
                ("く", "ku", 1, "/a/a3/Ja-ku.ogg"),
                ("け", "ke", 3, "/b/b3/Ja-ke.ogg"),
                ("こ", "ko", 2, "/1/18/Ja-ko.ogg"),

                // Hàng Sa
                ("さ", "sa", 3, "/a/a9/Ja-sa.ogg"),
                ("し", "shi", 1, "/8/80/Ja-shi.ogg"),
                ("す", "su", 2, "/a/a7/Ja-su.ogg"),
                ("せ", "se", 3, "/a/a8/Ja-se.ogg"),
                ("そ", "so", 1, "/0/00/Ja-so.ogg"),

                // Hàng Ta
                ("た", "ta", 4, "/d/d2/Ja-ta.ogg"),
                ("ち", "chi", 2, "/3/38/Ja-chi.ogg"),
                ("つ", "tsu", 1, "/3/32/Ja-tsu.ogg"),
                ("て", "te", 1, "/1/10/Ja-te.ogg"),
                ("と", "to", 2, "/a/a5/Ja-to.ogg"),

                // Hàng Na
                ("な", "na", 4, "/9/92/Ja-na.ogg"),
                ("に", "ni", 3, "/b/b7/Ja-ni.ogg"),
                ("ぬ", "nu", 2, "/b/b1/Ja-nu.ogg"),
                ("ね", "ne", 2, "/8/86/Ja-ne.ogg"),
                ("の", "no", 1, "/5/54/Ja-no.ogg"),

                // Hàng Ha
                ("は", "ha", 3, "/0/08/Ja-ha.ogg"),
                ("ひ", "hi", 1, "/9/99/Ja-hi.ogg"),
                ("ふ", "fu", 4, "/7/70/Ja-fu.ogg"),
                ("へ", "he", 1, "/2/2a/Ja-he.ogg"),
                ("ほ", "ho", 4, "/a/a4/Ja-ho.ogg"),

                // Hàng Ma
                ("ま", "ma", 3, "/0/0d/Ja-ma.ogg"),
                ("み", "mi", 2, "/1/16/Ja-mi.ogg"),
                ("む", "mu", 3, "/e/eb/Ja-mu.ogg"),
                ("め", "me", 2, "/8/8b/Ja-me.ogg"),
                ("も", "mo", 3, "/c/c1/Ja-mo.ogg"),

                // Hàng Ya
                ("や", "ya", 3, "/e/e8/Ja-ya.ogg"),
                ("ゆ", "yu", 2, "/7/7b/Ja-yu.ogg"),
                ("よ", "yo", 2, "/c/c9/Ja-yo.ogg"),

                // Hàng Ra
                ("ら", "ra", 2, "/0/04/Ja-ra.ogg"),
                ("り", "ri", 2, "/7/76/Ja-ri.ogg"),
                ("る", "ru", 1, "/5/56/Ja-ru.ogg"),
                ("れ", "re", 2, "/5/57/Ja-re.ogg"),
                ("ろ", "ro", 1, "/e/ed/Ja-ro.ogg"),

                // Hàng Wa & N
                ("わ", "wa", 2, "/f/f2/Ja-wa.ogg"),
                ("を", "wo", 3, "/7/77/Ja-wo.ogg"),
                ("ん", "n", 1, "/0/0c/Ja-n.ogg")
            };

            foreach (var item in hiraganaData)
            {
                result.Add(new KanaCharacter
                {
                    Id = currentId++,
                    Character = item.Character,
                    Romaji = item.Romaji,
                    Type = KanaCharacterType.Hiragana,
                    StrokeCount = item.Strokes,
                    AudioUrl = $"{baseAudioUrl}{item.AudioPath}"
                });
            }

            // 2. DANH SÁCH KATAKANA (46 Chữ cơ bản - Id từ 47 đến 92)
            var katakanaData = new (string Character, string Romaji, int Strokes, string AudioPath)[]
            {
                // Hàng A
                ("ア", "a", 2, "/2/22/Ja-a.ogg"),
                ("イ", "i", 2, "/2/28/Ja-i.ogg"),
                ("ウ", "u", 3, "/a/a2/Ja-u.ogg"),
                ("エ", "e", 3, "/2/21/Ja-e.ogg"),
                ("オ", "o", 3, "/d/df/Ja-o.ogg"),

                // Hàng Ka
                ("カ", "ka", 2, "/2/2e/Ja-ka.ogg"),
                ("キ", "ki", 3, "/8/87/Ja-ki.ogg"),
                ("ク", "ku", 2, "/a/a3/Ja-ku.ogg"),
                ("ケ", "ke", 3, "/b/b3/Ja-ke.ogg"),
                ("コ", "ko", 2, "/1/18/Ja-ko.ogg"),

                // Hàng Sa
                ("サ", "sa", 3, "/a/a9/Ja-sa.ogg"),
                ("シ", "shi", 3, "/8/80/Ja-shi.ogg"),
                ("ス", "su", 2, "/a/a7/Ja-su.ogg"),
                ("セ", "se", 2, "/a/a8/Ja-se.ogg"),
                ("ソ", "so", 2, "/0/00/Ja-so.ogg"),

                // Hàng Ta
                ("タ", "ta", 3, "/d/d2/Ja-ta.ogg"),
                ("チ", "chi", 3, "/3/38/Ja-chi.ogg"),
                ("ツ", "tsu", 3, "/3/32/Ja-tsu.ogg"),
                ("テ", "te", 3, "/1/10/Ja-te.ogg"),
                ("ト", "to", 2, "/a/a5/Ja-to.ogg"),

                // Hàng Na
                ("ナ", "na", 2, "/9/92/Ja-na.ogg"),
                ("ニ", "ni", 2, "/b/b7/Ja-ni.ogg"),
                ("ヌ", "nu", 2, "/b/b1/Ja-nu.ogg"),
                ("ネ", "ne", 4, "/8/86/Ja-ne.ogg"),
                ("ノ", "no", 1, "/5/54/Ja-no.ogg"),

                // Hàng Ha
                ("ハ", "ha", 2, "/0/08/Ja-ha.ogg"),
                ("ヒ", "hi", 2, "/9/99/Ja-hi.ogg"),
                ("フ", "fu", 1, "/7/70/Ja-fu.ogg"),
                ("ヘ", "he", 1, "/2/2a/Ja-he.ogg"),
                ("ホ", "ho", 4, "/a/a4/Ja-ho.ogg"),

                // Hàng Ma
                ("マ", "ma", 2, "/0/0d/Ja-ma.ogg"),
                ("ミ", "mi", 3, "/1/16/Ja-mi.ogg"),
                ("ム", "mu", 2, "/e/eb/Ja-mu.ogg"),
                ("メ", "me", 2, "/8/8b/Ja-me.ogg"),
                ("モ", "mo", 3, "/c/c1/Ja-mo.ogg"),

                // Hàng Ya
                ("ヤ", "ya", 2, "/e/e8/Ja-ya.ogg"),
                ("ユ", "yu", 2, "/7/7b/Ja-yu.ogg"),
                ("ヨ", "yo", 3, "/c/c9/Ja-yo.ogg"),

                // Hàng Ra
                ("ラ", "ra", 2, "/0/04/Ja-ra.ogg"),
                ("リ", "ri", 2, "/7/76/Ja-ri.ogg"),
                ("ル", "ru", 2, "/5/56/Ja-ru.ogg"),
                ("レ", "re", 1, "/5/57/Ja-re.ogg"),
                ("ロ", "ro", 3, "/e/ed/Ja-ro.ogg"),

                // Hàng Wa & N
                ("ワ", "wa", 2, "/f/f2/Ja-wa.ogg"),
                ("ヲ", "wo", 3, "/7/77/Ja-wo.ogg"),
                ("ン", "n", 2, "/0/0c/Ja-n.ogg")
            };

            foreach (var item in katakanaData)
            {
                result.Add(new KanaCharacter
                {
                    Id = currentId++,
                    Character = item.Character,
                    Romaji = item.Romaji,
                    Type = KanaCharacterType.Katakana,
                    StrokeCount = item.Strokes,
                    AudioUrl = $"{baseAudioUrl}{item.AudioPath}"
                });
            }

            return result;
        }
    }
}