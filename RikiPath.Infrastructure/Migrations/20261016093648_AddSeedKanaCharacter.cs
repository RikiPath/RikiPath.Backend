using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace RikiPath.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSeedKanaCharacter : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_KanaCharacters_Users_ContentAuthorId",
                table: "KanaCharacters");

            migrationBuilder.DropIndex(
                name: "IX_KanaCharacters_ContentAuthorId",
                table: "KanaCharacters");

            migrationBuilder.DropColumn(
                name: "ContentAuthorId",
                table: "KanaCharacters");

            migrationBuilder.InsertData(
                table: "KanaCharacters",
                columns: new[] { "Id", "AudioUrl", "Character", "CreatedBy", "CreatedDate", "IsDeleted", "ModifiedBy", "ModifiedDate", "Romaji", "StrokeCount", "StrokeOrderImageUrl", "Type" },
                values: new object[,]
                {
                    { 1, "https://upload.wikimedia.org/wikipedia/commons/2/22/Ja-a.ogg", "あ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7153), false, null, null, "a", 3, null, "Hiragana" },
                    { 2, "https://upload.wikimedia.org/wikipedia/commons/2/28/Ja-i.ogg", "い", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7176), false, null, null, "i", 2, null, "Hiragana" },
                    { 3, "https://upload.wikimedia.org/wikipedia/commons/a/a2/Ja-u.ogg", "う", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7178), false, null, null, "u", 2, null, "Hiragana" },
                    { 4, "https://upload.wikimedia.org/wikipedia/commons/2/21/Ja-e.ogg", "え", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7180), false, null, null, "e", 2, null, "Hiragana" },
                    { 5, "https://upload.wikimedia.org/wikipedia/commons/d/df/Ja-o.ogg", "お", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7181), false, null, null, "o", 3, null, "Hiragana" },
                    { 6, "https://upload.wikimedia.org/wikipedia/commons/2/2e/Ja-ka.ogg", "か", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7185), false, null, null, "ka", 3, null, "Hiragana" },
                    { 7, "https://upload.wikimedia.org/wikipedia/commons/8/87/Ja-ki.ogg", "き", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7186), false, null, null, "ki", 4, null, "Hiragana" },
                    { 8, "https://upload.wikimedia.org/wikipedia/commons/a/a3/Ja-ku.ogg", "く", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7187), false, null, null, "ku", 1, null, "Hiragana" },
                    { 9, "https://upload.wikimedia.org/wikipedia/commons/b/b3/Ja-ke.ogg", "け", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7188), false, null, null, "ke", 3, null, "Hiragana" },
                    { 10, "https://upload.wikimedia.org/wikipedia/commons/1/18/Ja-ko.ogg", "こ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7190), false, null, null, "ko", 2, null, "Hiragana" },
                    { 11, "https://upload.wikimedia.org/wikipedia/commons/a/a9/Ja-sa.ogg", "さ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7192), false, null, null, "sa", 3, null, "Hiragana" },
                    { 12, "https://upload.wikimedia.org/wikipedia/commons/8/80/Ja-shi.ogg", "し", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7193), false, null, null, "shi", 1, null, "Hiragana" },
                    { 13, "https://upload.wikimedia.org/wikipedia/commons/a/a7/Ja-su.ogg", "す", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7194), false, null, null, "su", 2, null, "Hiragana" },
                    { 14, "https://upload.wikimedia.org/wikipedia/commons/a/a8/Ja-se.ogg", "せ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7195), false, null, null, "se", 3, null, "Hiragana" },
                    { 15, "https://upload.wikimedia.org/wikipedia/commons/0/00/Ja-so.ogg", "そ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7196), false, null, null, "so", 1, null, "Hiragana" },
                    { 16, "https://upload.wikimedia.org/wikipedia/commons/d/d2/Ja-ta.ogg", "た", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7198), false, null, null, "ta", 4, null, "Hiragana" },
                    { 17, "https://upload.wikimedia.org/wikipedia/commons/3/38/Ja-chi.ogg", "ち", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7199), false, null, null, "chi", 2, null, "Hiragana" },
                    { 18, "https://upload.wikimedia.org/wikipedia/commons/3/32/Ja-tsu.ogg", "つ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7202), false, null, null, "tsu", 1, null, "Hiragana" },
                    { 19, "https://upload.wikimedia.org/wikipedia/commons/1/10/Ja-te.ogg", "て", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7203), false, null, null, "te", 1, null, "Hiragana" },
                    { 20, "https://upload.wikimedia.org/wikipedia/commons/a/a5/Ja-to.ogg", "と", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7204), false, null, null, "to", 2, null, "Hiragana" },
                    { 21, "https://upload.wikimedia.org/wikipedia/commons/9/92/Ja-na.ogg", "な", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7205), false, null, null, "na", 4, null, "Hiragana" },
                    { 22, "https://upload.wikimedia.org/wikipedia/commons/b/b7/Ja-ni.ogg", "に", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7206), false, null, null, "ni", 3, null, "Hiragana" },
                    { 23, "https://upload.wikimedia.org/wikipedia/commons/b/b1/Ja-nu.ogg", "ぬ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7207), false, null, null, "nu", 2, null, "Hiragana" },
                    { 24, "https://upload.wikimedia.org/wikipedia/commons/8/86/Ja-ne.ogg", "ね", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7285), false, null, null, "ne", 2, null, "Hiragana" },
                    { 25, "https://upload.wikimedia.org/wikipedia/commons/5/54/Ja-no.ogg", "の", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7286), false, null, null, "no", 1, null, "Hiragana" },
                    { 26, "https://upload.wikimedia.org/wikipedia/commons/0/08/Ja-ha.ogg", "は", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7287), false, null, null, "ha", 3, null, "Hiragana" },
                    { 27, "https://upload.wikimedia.org/wikipedia/commons/9/99/Ja-hi.ogg", "ひ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7288), false, null, null, "hi", 1, null, "Hiragana" },
                    { 28, "https://upload.wikimedia.org/wikipedia/commons/7/70/Ja-fu.ogg", "ふ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7290), false, null, null, "fu", 4, null, "Hiragana" },
                    { 29, "https://upload.wikimedia.org/wikipedia/commons/2/2a/Ja-he.ogg", "へ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7291), false, null, null, "he", 1, null, "Hiragana" },
                    { 30, "https://upload.wikimedia.org/wikipedia/commons/a/a4/Ja-ho.ogg", "ほ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7293), false, null, null, "ho", 4, null, "Hiragana" },
                    { 31, "https://upload.wikimedia.org/wikipedia/commons/0/0d/Ja-ma.ogg", "ま", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7294), false, null, null, "ma", 3, null, "Hiragana" },
                    { 32, "https://upload.wikimedia.org/wikipedia/commons/1/16/Ja-mi.ogg", "み", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7295), false, null, null, "mi", 2, null, "Hiragana" },
                    { 33, "https://upload.wikimedia.org/wikipedia/commons/e/eb/Ja-mu.ogg", "む", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7296), false, null, null, "mu", 3, null, "Hiragana" },
                    { 34, "https://upload.wikimedia.org/wikipedia/commons/8/8b/Ja-me.ogg", "め", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7298), false, null, null, "me", 2, null, "Hiragana" },
                    { 35, "https://upload.wikimedia.org/wikipedia/commons/c/c1/Ja-mo.ogg", "も", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7299), false, null, null, "mo", 3, null, "Hiragana" },
                    { 36, "https://upload.wikimedia.org/wikipedia/commons/e/e8/Ja-ya.ogg", "や", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7300), false, null, null, "ya", 3, null, "Hiragana" },
                    { 37, "https://upload.wikimedia.org/wikipedia/commons/7/7b/Ja-yu.ogg", "ゆ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7301), false, null, null, "yu", 2, null, "Hiragana" },
                    { 38, "https://upload.wikimedia.org/wikipedia/commons/c/c9/Ja-yo.ogg", "よ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7302), false, null, null, "yo", 2, null, "Hiragana" },
                    { 39, "https://upload.wikimedia.org/wikipedia/commons/0/04/Ja-ra.ogg", "ら", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7303), false, null, null, "ra", 2, null, "Hiragana" },
                    { 40, "https://upload.wikimedia.org/wikipedia/commons/7/76/Ja-ri.ogg", "り", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7304), false, null, null, "ri", 2, null, "Hiragana" },
                    { 41, "https://upload.wikimedia.org/wikipedia/commons/5/56/Ja-ru.ogg", "る", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7306), false, null, null, "ru", 1, null, "Hiragana" },
                    { 42, "https://upload.wikimedia.org/wikipedia/commons/5/57/Ja-re.ogg", "れ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7307), false, null, null, "re", 2, null, "Hiragana" },
                    { 43, "https://upload.wikimedia.org/wikipedia/commons/e/ed/Ja-ro.ogg", "ろ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7308), false, null, null, "ro", 1, null, "Hiragana" },
                    { 44, "https://upload.wikimedia.org/wikipedia/commons/f/f2/Ja-wa.ogg", "わ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7309), false, null, null, "wa", 2, null, "Hiragana" },
                    { 45, "https://upload.wikimedia.org/wikipedia/commons/7/77/Ja-wo.ogg", "を", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7310), false, null, null, "wo", 3, null, "Hiragana" },
                    { 46, "https://upload.wikimedia.org/wikipedia/commons/0/0c/Ja-n.ogg", "ん", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7311), false, null, null, "n", 1, null, "Hiragana" },
                    { 47, "https://upload.wikimedia.org/wikipedia/commons/2/22/Ja-a.ogg", "ア", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7395), false, null, null, "a", 2, null, "Katakana" },
                    { 48, "https://upload.wikimedia.org/wikipedia/commons/2/28/Ja-i.ogg", "イ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7398), false, null, null, "i", 2, null, "Katakana" },
                    { 49, "https://upload.wikimedia.org/wikipedia/commons/a/a2/Ja-u.ogg", "ウ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7399), false, null, null, "u", 3, null, "Katakana" },
                    { 50, "https://upload.wikimedia.org/wikipedia/commons/2/21/Ja-e.ogg", "エ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7400), false, null, null, "e", 3, null, "Katakana" },
                    { 51, "https://upload.wikimedia.org/wikipedia/commons/d/df/Ja-o.ogg", "オ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7401), false, null, null, "o", 3, null, "Katakana" },
                    { 52, "https://upload.wikimedia.org/wikipedia/commons/2/2e/Ja-ka.ogg", "カ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7402), false, null, null, "ka", 2, null, "Katakana" },
                    { 53, "https://upload.wikimedia.org/wikipedia/commons/8/87/Ja-ki.ogg", "キ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7403), false, null, null, "ki", 3, null, "Katakana" },
                    { 54, "https://upload.wikimedia.org/wikipedia/commons/a/a3/Ja-ku.ogg", "ク", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7404), false, null, null, "ku", 2, null, "Katakana" },
                    { 55, "https://upload.wikimedia.org/wikipedia/commons/b/b3/Ja-ke.ogg", "ケ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7405), false, null, null, "ke", 3, null, "Katakana" },
                    { 56, "https://upload.wikimedia.org/wikipedia/commons/1/18/Ja-ko.ogg", "コ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7407), false, null, null, "ko", 2, null, "Katakana" },
                    { 57, "https://upload.wikimedia.org/wikipedia/commons/a/a9/Ja-sa.ogg", "サ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7408), false, null, null, "sa", 3, null, "Katakana" },
                    { 58, "https://upload.wikimedia.org/wikipedia/commons/8/80/Ja-shi.ogg", "シ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7409), false, null, null, "shi", 3, null, "Katakana" },
                    { 59, "https://upload.wikimedia.org/wikipedia/commons/a/a7/Ja-su.ogg", "ス", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7410), false, null, null, "su", 2, null, "Katakana" },
                    { 60, "https://upload.wikimedia.org/wikipedia/commons/a/a8/Ja-se.ogg", "セ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7411), false, null, null, "se", 2, null, "Katakana" },
                    { 61, "https://upload.wikimedia.org/wikipedia/commons/0/00/Ja-so.ogg", "ソ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7412), false, null, null, "so", 2, null, "Katakana" },
                    { 62, "https://upload.wikimedia.org/wikipedia/commons/d/d2/Ja-ta.ogg", "タ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7413), false, null, null, "ta", 3, null, "Katakana" },
                    { 63, "https://upload.wikimedia.org/wikipedia/commons/3/38/Ja-chi.ogg", "チ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7414), false, null, null, "chi", 3, null, "Katakana" },
                    { 64, "https://upload.wikimedia.org/wikipedia/commons/3/32/Ja-tsu.ogg", "ツ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7415), false, null, null, "tsu", 3, null, "Katakana" },
                    { 65, "https://upload.wikimedia.org/wikipedia/commons/1/10/Ja-te.ogg", "テ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7416), false, null, null, "te", 3, null, "Katakana" },
                    { 66, "https://upload.wikimedia.org/wikipedia/commons/a/a5/Ja-to.ogg", "ト", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7419), false, null, null, "to", 2, null, "Katakana" },
                    { 67, "https://upload.wikimedia.org/wikipedia/commons/9/92/Ja-na.ogg", "ナ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7420), false, null, null, "na", 2, null, "Katakana" },
                    { 68, "https://upload.wikimedia.org/wikipedia/commons/b/b7/Ja-ni.ogg", "ニ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7421), false, null, null, "ni", 2, null, "Katakana" },
                    { 69, "https://upload.wikimedia.org/wikipedia/commons/b/b1/Ja-nu.ogg", "ヌ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7463), false, null, null, "nu", 2, null, "Katakana" },
                    { 70, "https://upload.wikimedia.org/wikipedia/commons/8/86/Ja-ne.ogg", "ネ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7464), false, null, null, "ne", 4, null, "Katakana" },
                    { 71, "https://upload.wikimedia.org/wikipedia/commons/5/54/Ja-no.ogg", "ノ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7465), false, null, null, "no", 1, null, "Katakana" },
                    { 72, "https://upload.wikimedia.org/wikipedia/commons/0/08/Ja-ha.ogg", "ハ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7466), false, null, null, "ha", 2, null, "Katakana" },
                    { 73, "https://upload.wikimedia.org/wikipedia/commons/9/99/Ja-hi.ogg", "ヒ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7468), false, null, null, "hi", 2, null, "Katakana" },
                    { 74, "https://upload.wikimedia.org/wikipedia/commons/7/70/Ja-fu.ogg", "フ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7469), false, null, null, "fu", 1, null, "Katakana" },
                    { 75, "https://upload.wikimedia.org/wikipedia/commons/2/2a/Ja-he.ogg", "ヘ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7470), false, null, null, "he", 1, null, "Katakana" },
                    { 76, "https://upload.wikimedia.org/wikipedia/commons/a/a4/Ja-ho.ogg", "ホ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7471), false, null, null, "ho", 4, null, "Katakana" },
                    { 77, "https://upload.wikimedia.org/wikipedia/commons/0/0d/Ja-ma.ogg", "マ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7472), false, null, null, "ma", 2, null, "Katakana" },
                    { 78, "https://upload.wikimedia.org/wikipedia/commons/1/16/Ja-mi.ogg", "ミ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7473), false, null, null, "mi", 3, null, "Katakana" },
                    { 79, "https://upload.wikimedia.org/wikipedia/commons/e/eb/Ja-mu.ogg", "ム", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7474), false, null, null, "mu", 2, null, "Katakana" },
                    { 80, "https://upload.wikimedia.org/wikipedia/commons/8/8b/Ja-me.ogg", "メ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7475), false, null, null, "me", 2, null, "Katakana" },
                    { 81, "https://upload.wikimedia.org/wikipedia/commons/c/c1/Ja-mo.ogg", "モ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7477), false, null, null, "mo", 3, null, "Katakana" },
                    { 82, "https://upload.wikimedia.org/wikipedia/commons/e/e8/Ja-ya.ogg", "ヤ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7478), false, null, null, "ya", 2, null, "Katakana" },
                    { 83, "https://upload.wikimedia.org/wikipedia/commons/7/7b/Ja-yu.ogg", "ユ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7479), false, null, null, "yu", 2, null, "Katakana" },
                    { 84, "https://upload.wikimedia.org/wikipedia/commons/c/c9/Ja-yo.ogg", "ヨ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7481), false, null, null, "yo", 3, null, "Katakana" },
                    { 85, "https://upload.wikimedia.org/wikipedia/commons/0/04/Ja-ra.ogg", "ラ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7482), false, null, null, "ra", 2, null, "Katakana" },
                    { 86, "https://upload.wikimedia.org/wikipedia/commons/7/76/Ja-ri.ogg", "リ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7483), false, null, null, "ri", 2, null, "Katakana" },
                    { 87, "https://upload.wikimedia.org/wikipedia/commons/5/56/Ja-ru.ogg", "ル", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7484), false, null, null, "ru", 2, null, "Katakana" },
                    { 88, "https://upload.wikimedia.org/wikipedia/commons/5/57/Ja-re.ogg", "レ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7485), false, null, null, "re", 1, null, "Katakana" },
                    { 89, "https://upload.wikimedia.org/wikipedia/commons/e/ed/Ja-ro.ogg", "ロ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7486), false, null, null, "ro", 3, null, "Katakana" },
                    { 90, "https://upload.wikimedia.org/wikipedia/commons/f/f2/Ja-wa.ogg", "ワ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7487), false, null, null, "wa", 2, null, "Katakana" },
                    { 91, "https://upload.wikimedia.org/wikipedia/commons/7/77/Ja-wo.ogg", "ヲ", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7488), false, null, null, "wo", 3, null, "Katakana" },
                    { 92, "https://upload.wikimedia.org/wikipedia/commons/0/0c/Ja-n.ogg", "ン", null, new DateTime(2026, 10, 16, 9, 36, 46, 252, DateTimeKind.Utc).AddTicks(7489), false, null, null, "n", 2, null, "Katakana" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 23);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 24);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 25);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 26);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 27);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 28);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 29);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 30);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 31);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 32);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 34);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 35);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 36);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 37);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 38);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 39);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 40);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 41);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 42);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 43);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 44);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 45);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 46);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 47);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 48);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 49);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 50);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 51);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 52);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 53);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 54);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 55);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 56);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 57);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 58);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 59);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 60);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 61);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 62);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 63);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 64);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 65);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 66);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 67);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 68);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 69);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 70);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 71);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 72);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 73);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 74);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 75);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 76);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 77);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 78);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 79);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 80);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 81);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 82);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 83);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 84);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 85);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 86);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 87);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 88);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 89);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 90);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 91);

            migrationBuilder.DeleteData(
                table: "KanaCharacters",
                keyColumn: "Id",
                keyValue: 92);

            migrationBuilder.AddColumn<int>(
                name: "ContentAuthorId",
                table: "KanaCharacters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_KanaCharacters_ContentAuthorId",
                table: "KanaCharacters",
                column: "ContentAuthorId");

            migrationBuilder.AddForeignKey(
                name: "FK_KanaCharacters_Users_ContentAuthorId",
                table: "KanaCharacters",
                column: "ContentAuthorId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
